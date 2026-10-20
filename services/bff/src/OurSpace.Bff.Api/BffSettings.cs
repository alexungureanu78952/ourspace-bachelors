namespace OurSpace.Bff.Api;

public sealed class BffSettings
{
    public string Authority { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ProfilesOrigin { get; set; } = string.Empty;
}
