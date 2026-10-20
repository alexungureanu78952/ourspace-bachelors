using OurSpace.Profiles.Application;

namespace OurSpace.Profiles.Api.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal &&
        Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) && id != Guid.Empty
            ? id
            : null;
}
