namespace OurSpace.Identity.Api;

public sealed class IdentityProtocolSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string BffOrigin { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public const string ClientId = "ourspace-bff";
    public const string ProfilesScope = "ourspace.profiles";
}
