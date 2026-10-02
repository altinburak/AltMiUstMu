using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace AltMiUstMu.Web.Localization;

/// <summary>Changes the UI language: always the browser cookie, plus the account (and its auth cookie) when signed in.</summary>
public sealed class LanguageService(UserManager<AppUser> users, SignInManager<AppUser> signIn)
{
    public async Task SetAsync(HttpContext ctx, string? code)
    {
        var language = Lang.Normalize(code);
        UserLanguageCultureProvider.SetCookie(ctx.Response, language);

        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = await users.GetUserAsync(ctx.User);
        if (user is null || user.Language == language)
        {
            return;
        }

        user.Language = language;
        var result = await users.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Re-issues the auth cookie so the language claim changes right away.
            await signIn.RefreshSignInAsync(user);
        }
    }

    /// <summary>After sign-in: the browser keeps the account's language, also after signing out.</summary>
    public static void RememberInBrowser(HttpContext ctx, AppUser user) =>
        UserLanguageCultureProvider.SetCookie(ctx.Response, user.Language);
}
