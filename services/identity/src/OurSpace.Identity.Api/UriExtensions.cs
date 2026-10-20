namespace OurSpace.Identity.Api;

internal static class UriExtensions
{
    internal static string Origin(this Uri uri) => uri.GetLeftPart(UriPartial.Authority);
}
