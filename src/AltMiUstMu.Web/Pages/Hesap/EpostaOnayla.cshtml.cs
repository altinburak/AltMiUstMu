using System.Text;
using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AltMiUstMu.Web.Pages.Hesap;

public class EpostaOnaylaModel(UserManager<AppUser> users, SignInManager<AppUser> signIn) : PageModel
{
    public bool Success { get; private set; }

    public string Next { get; private set; } = "/tahminler";

    public async Task<IActionResult> OnGetAsync(string? userId, string? code, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            Next = returnUrl;
        }

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
        {
            return Page();
        }

        var user = await users.FindByIdAsync(userId);
        if (user is null || user.IsPundit)
        {
            return Page();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return Page();
        }

        var result = user.EmailConfirmed ? IdentityResult.Success : await users.ConfirmEmailAsync(user, token);
        Success = result.Succeeded;
        if (Success && !user.IsDisabled)
        {
            await signIn.SignInAsync(user, isPersistent: true);
        }

        return Page();
    }
}
