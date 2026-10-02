using AltMiUstMu.Web.Identity;
using Microsoft.AspNetCore.Localization;

namespace AltMiUstMu.Web.Localization;

/// <summary>
/// Picks the request language: the signed-in user's saved language (a claim in the auth cookie, so no DB lookup),
/// then the language cookie for anonymous visitors. Returns null otherwise, so the default (Turkish) applies.
/// The browser's Accept-Language is deliberately ignored: Turkish is the default for everyone.
/// </summary>
public sealed class UserLanguageCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var code = httpContext.User.FindFirst(AppClaimsFactory.LanguageClaim)?.Value
            ?? httpContext.Request.Cookies[Lang.CookieName];
        if (string.IsNullOrEmpty(code))
        {
            return NullProviderCultureResult;
        }

        var culture = Lang.Culture(code).Name;
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }

    public static void SetCookie(HttpResponse response, string code) =>
        response.Cookies.Append(Lang.CookieName, Lang.Normalize(code), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = response.HttpContext.Request.IsHttps,
        });
}
