using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OurSpace.Hosting;

public static class Deployment
{
    public static void AddDeploymentConfiguration(this WebApplicationBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("OURSPACE_CONFIGURATION_FILE") is { Length: > 0 } path)
            builder.Configuration.AddJsonFile(path, optional: false, reloadOnChange: false).AddEnvironmentVariables();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var address in builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(address));
            foreach (var host in builder.Configuration.GetSection("Proxy:AllowedHosts").Get<string[]>() ?? [])
                options.AllowedHosts.Add(host);
        });
    }

    public static void AddPersistentKeys(this WebApplicationBuilder builder, string applicationName)
    {
        var protection = builder.Services.AddDataProtection().SetApplicationName(applicationName);
        var path = builder.Configuration["DataProtection:KeyPath"];
        var certificatePath = builder.Configuration["DataProtection:Certificate"];
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(certificatePath))
        {
            if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
                throw new InvalidOperationException("Persistent, certificate-encrypted Data Protection keys must be configured.");
            return;
        }
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, builder.Configuration["DataProtection:CertificatePassword"]);
        if (!certificate.HasPrivateKey) throw new InvalidOperationException("A private key is required for Data Protection.");
        protection.PersistKeysToFileSystem(new DirectoryInfo(path)).ProtectKeysWithCertificate(certificate);
        var previous = builder.Configuration.GetSection("DataProtection:PreviousCertificates").Get<string[]>() ?? [];
        protection.UnprotectKeysWithAnyCertificate([certificate, .. previous.Select(file =>
            X509CertificateLoader.LoadPkcs12FromFile(file, builder.Configuration["DataProtection:CertificatePassword"]))]);
    }

    public static async Task<bool> TryHealthCheckAsync(string[] args)
    {
        if (!args.Contains("--health-check", StringComparer.Ordinal)) return false;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync("http://localhost:8080/health/ready");
            Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException) { Environment.ExitCode = 1; }
        catch (TaskCanceledException) { Environment.ExitCode = 1; }
        return true;
    }
}
