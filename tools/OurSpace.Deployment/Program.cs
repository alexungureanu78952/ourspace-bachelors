using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OurSpace.Bff.Infrastructure;
using OurSpace.Identity.Infrastructure;
using OurSpace.Profiles.Infrastructure;

if (args.Length == 0) throw new ArgumentException("Use initialize or migrate.");
if (args[0] == "initialize")
{
    var directory = Path.GetFullPath(".local/deployment");
    Directory.CreateDirectory(directory);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    var marker = Path.Combine(directory, "initialized");
    if (File.Exists(marker)) { Console.WriteLine("Local deployment secrets already initialized."); return; }
    if (Directory.EnumerateFiles(directory).Any()) throw new InvalidOperationException("Incomplete initialization: inspect .local/deployment before retrying.");
    var password = Secret();
    using var caKey = RSA.Create(3072);
    var caRequest = new CertificateRequest("CN=OurSpace local deployment CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
    caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
    using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
    File.WriteAllText(Path.Combine(directory, "ca.crt"), ca.ExportCertificatePem());
    File.WriteAllBytes(Path.Combine(directory, "ca.pfx"), ca.Export(X509ContentType.Pfx, password));
    using var tlsKey = RSA.Create(3072);
    var tlsRequest = new CertificateRequest("CN=app.localhost", tlsKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var names = new SubjectAlternativeNameBuilder();
    foreach (var name in new[] { "app.localhost", "identity.localhost", "profiles.localhost", "mailpit", "localhost" }) names.AddDnsName(name);
    tlsRequest.CertificateExtensions.Add(names.Build());
    tlsRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    tlsRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
    tlsRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
    using var issued = tlsRequest.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1), RandomNumberGenerator.GetBytes(16));
    File.WriteAllText(Path.Combine(directory, "tls.crt"), issued.ExportCertificatePem() + "\n" + ca.ExportCertificatePem());
    File.WriteAllText(Path.Combine(directory, "tls.key"), tlsKey.ExportPkcs8PrivateKeyPem());
    foreach (var name in new[] { "signing", "encryption", "identity-protection", "bff-protection" })
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest("CN=OurSpace local " + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        File.WriteAllBytes(Path.Combine(directory, name + ".pfx"), certificate.Export(X509ContentType.Pfx, password));
    }
    var clientSecret = Secret();
    var migrations = new Dictionary<string, object>(StringComparer.Ordinal);
    foreach (var name in new[] { "profiles", "identity", "bff" })
    {
        var ownerPassword = Secret();
        var runtimePassword = Secret();
        var owner = "ourspace_" + name;
        var runtime = owner + "_app";
        File.WriteAllText(Path.Combine(directory, name + "-db-password"), ownerPassword);
        migrations[name] = new { OwnerConnection = Connection(name, owner, ownerPassword), RuntimeRole = runtime, RuntimePassword = runtimePassword };
        var settings = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["ConnectionStrings"] = new Dictionary<string, string>(StringComparer.Ordinal) { [name == "bff" ? "Bff" : char.ToUpperInvariant(name[0]) + name[1..]] = Connection(name, runtime, runtimePassword) },
            ["AllowedHosts"] = name + ".localhost;localhost",
            ["Logging"] = new { LogLevel = new Dictionary<string, string>(StringComparer.Ordinal) { ["Default"] = "Warning", ["Microsoft.Hosting.Lifetime"] = "Information" } },
            ["Proxy"] = new { KnownProxies = new[] { "172.29.249.10" }, AllowedHosts = new[] { name + ".localhost" } },
        };
        if (name != "profiles")
            settings["DataProtection"] = new { KeyPath = "/var/ourspace/keys", Certificate = "/run/secrets/" + name + "-protection.pfx", CertificatePassword = password };
        if (name == "identity")
        {
            settings["Identity"] = new
            {
                Issuer = "https://identity.localhost:7443",
                BffOrigin = "https://app.localhost:7443",
                ClientSecret = clientSecret,
                SigningCertificate = "/run/secrets/signing.pfx",
                SigningCertificatePassword = password,
                EncryptionCertificate = "/run/secrets/encryption.pfx",
                EncryptionCertificatePassword = password
            };
            settings["Email"] = new { Host = "mailpit", Port = 1025, From = "accounts@ourspace.test", UseTls = true, CheckCertificateRevocation = false };
        }
        else if (name == "bff")
        {
            settings["AllowedHosts"] = "app.localhost;localhost";
            settings["Proxy"] = new { KnownProxies = new[] { "172.29.249.10" }, AllowedHosts = new[] { "app.localhost" } };
            settings["Bff"] = new { Authority = "https://identity.localhost:7443", ClientSecret = clientSecret, ProfilesOrigin = "https://profiles.localhost:7443" };
        }
        else settings["Authentication"] = new { Authority = "https://identity.localhost:7443", Audience = "ourspace.profiles" };
        WriteJson(Path.Combine(directory, name + ".json"), settings);
    }
    WriteJson(Path.Combine(directory, "migrations.json"), migrations);
    File.WriteAllText(Path.Combine(directory, "initialized"), "Local-only credentials. Do not commit or distribute.\n");
    Console.WriteLine("Local deployment credentials and certificates initialized under ignored .local/deployment.");
}
else if (args[0] == "migrate")
{
    using var settings = JsonDocument.Parse(File.ReadAllText("/run/secrets/migrations.json"));
    foreach (var item in settings.RootElement.EnumerateObject())
    {
        var connection = item.Value.GetProperty("OwnerConnection").GetString()!;
        if (item.Name == "identity")
        {
            await using var database = new IdentityStoreContext(new DbContextOptionsBuilder<IdentityStoreContext>().UseNpgsql(connection).Options);
            await database.Database.MigrateAsync();
        }
        else if (item.Name == "bff")
        {
            await using var database = new SessionDbContext(new DbContextOptionsBuilder<SessionDbContext>().UseNpgsql(connection).Options);
            await database.Database.MigrateAsync();
        }
        else
        {
            await using var database = new ProfilesDbContext(new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql(connection).Options);
            await database.Database.MigrateAsync();
        }
        var role = item.Value.GetProperty("RuntimeRole").GetString()!;
        var password = item.Value.GetProperty("RuntimePassword").GetString()!;
        if (!System.Text.RegularExpressions.Regex.IsMatch(role, "^ourspace_(identity|profiles|bff)_app$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(password, "^[A-F0-9]{64}$")) throw new InvalidOperationException("Unexpected runtime-role credentials.");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{role}') THEN CREATE ROLE "{role}" LOGIN; END IF;
            END $$;
            ALTER ROLE "{role}" PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
            GRANT CONNECT ON DATABASE "ourspace_{item.Name}" TO "{role}";
            GRANT USAGE ON SCHEMA public TO "{role}";
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO "{role}";
            REVOKE INSERT, UPDATE, DELETE ON TABLE "__EFMigrationsHistory" FROM "{role}";
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO "{role}";
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "{role}";
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO "{role}";
            """, admin);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine(item.Name + " migrations and runtime-role grants complete.");
    }
}
else throw new ArgumentException("Unknown deployment command.");

static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
static string Connection(string service, string username, string password) =>
    new NpgsqlConnectionStringBuilder { Host = service + "-db", Database = "ourspace_" + service, Username = username, Password = password }.ConnectionString;
static void WriteJson(string file, object value) => File.WriteAllText(file, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n");
