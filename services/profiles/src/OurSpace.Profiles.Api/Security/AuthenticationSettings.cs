namespace OurSpace.Profiles.Api.Security;

public sealed class AuthenticationSettings
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
