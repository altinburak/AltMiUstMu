using System.ComponentModel.DataAnnotations;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AltMiUstMu.Web.Pages.Hesap;

[EnableRateLimiting("auth")]
public class OnayGonderModel(UserManager<AppUser> users, AccountEmails emails, ILogger<OnayGonderModel> logger) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "E-posta zorunlu.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi gir.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    public bool Sent { get; private set; }

    public void OnGet(string? email) => Email = email ?? "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Same answer whether or not the account exists (no account enumeration).
        var user = await users.FindByEmailAsync(Email.Trim());
        if (user is { EmailConfirmed: false, IsPundit: false, IsDisabled: false })
        {
            await KayitModel.SendConfirmationAsync(users, emails, user, logger);
        }

        Sent = true;
        return Page();
    }
}
