using System.Text.RegularExpressions;

namespace NemesisBakuApi.Helpers;

public static class ShowcaseValidation
{
    // Keep reserved first segments aligned with AppRoutes and infrastructure paths.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "superadmin", "api", "assets", "search", "favorites", "basket", "profile",
        "login", "products", "orders", "checkout", "order-success", "order-failed",
        "infoaddress", "delivery", "return-policy", "about", "career", "stores", "register",
        "forgot-password", "promo", "404", "swagger", "health", "robots", "sitemap"
    };

    public static string? NormalizeSlug(string? value)
    {
        var slug = value?.Trim().Trim('/').ToLowerInvariant();
        return !string.IsNullOrEmpty(slug) && slug.Length <= 100 &&
            Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$") && !Reserved.Contains(slug)
            ? slug : null;
    }

    public static bool IsExternalUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
        !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo) &&
        !value!.Any(char.IsControl) && !value.Contains('\\');
}
