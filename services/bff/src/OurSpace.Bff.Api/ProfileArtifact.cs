using System.Text.Json;

namespace OurSpace.Bff.Api;

public sealed class ProfileArtifact
{
    private const string DataStart = "<script id=\"profile-data\" type=\"application/json\">";
    private const string DataEnd = "</script>";

    private readonly string _htmlPrefix;
    private readonly string _htmlSuffix;
    private readonly string _scriptHash;
    private readonly string _styleHash;
    private readonly string _treeJson;

    public ProfileArtifact(string htmlContent, string headersJson)
    {
        var startIndex = htmlContent.IndexOf(DataStart, StringComparison.Ordinal);
        if (startIndex < 0) throw new InvalidOperationException("Profile artifact is missing the profile-data script tag.");
        var payloadStart = startIndex + DataStart.Length;
        var endIndex = htmlContent.IndexOf(DataEnd, payloadStart, StringComparison.Ordinal);
        if (endIndex < 0) throw new InvalidOperationException("Profile artifact has an unclosed profile-data script tag.");

        _htmlPrefix = htmlContent[..payloadStart];
        _htmlSuffix = htmlContent[endIndex..];

        var payload = JsonDocument.Parse(htmlContent[payloadStart..endIndex]);
        _treeJson = payload.RootElement.GetProperty("tree").GetRawText();

        using var headers = JsonDocument.Parse(headersJson);
        var csp = headers.RootElement.GetProperty("Content-Security-Policy").GetString() ?? string.Empty;
        _scriptHash = ExtractHash(csp, "script-src");
        _styleHash = ExtractHash(csp, "style-src");
    }

    public (string Html, string ContentSecurityPolicy) Build(string displayName, string bio, string frameOrigin)
    {
        var data = JsonSerializer.Serialize(new
        {
            schemaVersion = "profile-data-v1",
            bio = bio,
            displayName = displayName,
            sections = Array.Empty<object>(),
        });
        // Escape '<' per the existing artifact convention to prevent script injection.
        var escapedData = data.Replace("<", "\\u003c", StringComparison.Ordinal);
        var payload = "{\"data\":" + escapedData + ",\"tree\":" + _treeJson + "}";

        var html = _htmlPrefix + payload + _htmlSuffix;
        var csp = "default-src 'none'; script-src 'sha256-" + _scriptHash + "'; style-src 'sha256-" + _styleHash +
            "'; connect-src 'none'; img-src 'none'; font-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors " +
            frameOrigin + "; sandbox allow-scripts";
        return (html, csp);
    }

    private static readonly Action<ILogger, string, Exception?> LogArtifactMissing =
        LoggerMessage.Define<string>(LogLevel.Warning, default, "Profile runtime artifacts not found at {Dir}. Profile view endpoint will be unavailable.");

    internal static ProfileArtifact? TryLoad(IWebHostEnvironment env, ILogger<ProfileArtifactService> logger)
    {
        var dir = Path.Combine(env.ContentRootPath, "profile-runtime-artifacts");
        var htmlPath = Path.Combine(dir, "profile.html");
        var headersPath = Path.Combine(dir, "headers.json");
        if (!File.Exists(htmlPath) || !File.Exists(headersPath))
        {
            LogArtifactMissing(logger, dir, null);
            return null;
        }

        return new ProfileArtifact(File.ReadAllText(htmlPath), File.ReadAllText(headersPath));
    }

    private static string ExtractHash(string csp, string directive)
    {
        var marker = directive + " 'sha256-";
        var start = csp.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += marker.Length;
        var end = csp.IndexOf('\'', start);
        return end < 0 ? string.Empty : csp[start..end];
    }
}

public sealed class ProfileArtifactService(IWebHostEnvironment env, ILogger<ProfileArtifactService> logger)
{
    public ProfileArtifact? Artifact { get; } = ProfileArtifact.TryLoad(env, logger);
}
