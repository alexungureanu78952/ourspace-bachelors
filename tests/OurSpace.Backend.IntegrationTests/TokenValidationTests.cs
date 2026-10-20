using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace OurSpace.Backend.IntegrationTests;

public sealed class TokenValidationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task RealJwtHandlerRequiresTrustedSignatureAudienceAndLifetime()
    {
        using var signingKey = RSA.Create(2048);
        using var untrustedKey = RSA.Create(2048);
        var key = new RsaSecurityKey(signingKey) { KeyId = "test-only-key" };
        var configuration = new OpenIdConnectConfiguration { Issuer = "https://identity.example.test" };
        configuration.SigningKeys.Add(key);
        await using var factory = new ProfileApiFactory(postgres.ProfilesConnection, fakeAuthentication: false);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration))));
        using var client = host.CreateClient();
        var subject = Guid.NewGuid().ToString();
        var valid = Token(key, "ourspace.profiles", subject, DateTime.UtcNow.AddMinutes(5));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", valid);
        using var accepted = await client.PostAsJsonAsync("/api/v1/profiles/", new { displayName = "Real validator" });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);

        foreach (var rejected in new[]
        {
            Token(key, "other-service", subject, DateTime.UtcNow.AddMinutes(5)),
            Token(key, "ourspace.profiles", subject, DateTime.UtcNow.AddMinutes(-10)),
            Token(new RsaSecurityKey(untrustedKey) { KeyId = key.KeyId }, "ourspace.profiles", subject, DateTime.UtcNow.AddMinutes(5)),
        })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rejected);
            using var response = await client.GetAsync(accepted.Headers.Location);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public void InsecureIssuerConfigurationFailsStartup()
    {
        using var factory = new ProfileApiFactory(postgres.ProfilesConnection);
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Authority"] = "http://insecure.example.test",
                })));
        Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }

    private static string Token(SecurityKey key, string audience, string subject, DateTime expires) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "https://identity.example.test",
            audience: audience,
            claims: [new Claim("sub", subject)],
            notBefore: DateTime.UtcNow.AddHours(-1),
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
}
