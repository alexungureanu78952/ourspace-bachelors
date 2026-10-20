using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OurSpace.Backend.IntegrationTests;

// Test-only HTTPS transport for real browser cookies over an ASP.NET Core TestServer.
internal sealed class HttpsTestBridge : IAsyncDisposable
{
    private readonly WebApplication application;
    private HttpClient? target;
    internal string Origin { get; }
    internal Func<HttpContext, Task<bool>>? TestEndpoint { get; set; }

    private HttpsTestBridge(WebApplication application, string origin)
    {
        this.application = application;
        Origin = origin;
    }

    internal static async Task<HttpsTestBridge> StartAsync(X509Certificate2 certificate)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(certificate)));
        var app = builder.Build();
        HttpsTestBridge? bridge = null;
        app.Run(context => bridge!.ForwardAsync(context));
        await app.StartAsync();
        bridge = new HttpsTestBridge(app, app.Urls.Single());
        return bridge;
    }

    internal void Connect(HttpMessageHandler handler) => target = new HttpClient(handler);

    private async Task ForwardAsync(HttpContext context)
    {
        if (TestEndpoint is { } endpoint && await endpoint(context)) return;
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), Origin + context.Request.Path + context.Request.QueryString);
        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            request.Content = new StreamContent(context.Request.Body);
        foreach (var header in context.Request.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
        using var response = await (target ?? throw new InvalidOperationException("Bridge is not connected.")).SendAsync(request);
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers) context.Response.Headers[header.Key] = header.Value.ToArray();
        foreach (var header in response.Content.Headers) context.Response.Headers[header.Key] = header.Value.ToArray();
        context.Response.Headers.Remove("Transfer-Encoding");
        await response.Content.CopyToAsync(context.Response.Body);
    }

    public async ValueTask DisposeAsync()
    {
        target?.Dispose();
        await application.StopAsync();
        await application.DisposeAsync();
    }
}
