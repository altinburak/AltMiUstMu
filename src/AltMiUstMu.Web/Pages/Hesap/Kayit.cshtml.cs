using System.ComponentModel.DataAnnotations;
using System.Text;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Hesap;

[EnableRateLimiting("auth")]
public class KayitModel(UserManager<AppUser> users, AppDbContext db, AccountEmails emails, TimeProvider time, ILogger<KayitModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunlu.")]
        [Display(Name = "Kullanıcı adı")]
        public string DisplayName { get; set; } = "";

        [Required(ErrorMessage = "E-posta zorunlu.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi gir.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Şifre zorunlu.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
        [Display(Name = "Şifre (tekrar)")]
        public string ConfirmPassword { get; set; } = "";

        [Range(typeof(bool), "true", "true", ErrorMessage = "Devam etmek için kuralları kabul etmelisin.")]
        public bool AcceptRules { get; set; }
    }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Redirect("/tahminler") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var displayName = TextNormalizer.CleanDisplay(Input.DisplayName);
        foreach (var error in NameRules.ValidateDisplayName(displayName))
        {
            ModelState.AddModelError("Input.DisplayName", error);
        }

        var key = TextNormalizer.Fold(displayName);
        if (ModelState.IsValid && await db.Users.AnyAsync(u => u.DisplayNameKey == key))
        {
            ModelState.AddModelError("Input.DisplayName", "Bu kullanıcı adı alınmış.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            DisplayNameKey = key,
            CreatedAt = time.GetUtcNow().UtcDateTime,
            LockoutEnabled = true,
        };

        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, Input.Password);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Input.DisplayName", "Bu kullanıcı adı alınmış.");
            return Page();
        }

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var field = error.Code.StartsWith("Password", StringComparison.Ordinal) ? "Input.Password"
                    : error.Code.Contains("Email", StringComparison.Ordinal) || error.Code.Contains("UserName", StringComparison.Ordinal) ? "Input.Email"
                    : string.Empty;
                ModelState.AddModelError(field, error.Description);
            }

            return Page();
        }

        logger.LogInformation("New user registered: {DisplayName}", displayName);
        await SendConfirmationAsync(users, emails, user, logger, Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null);
        return RedirectToPage("/Hesap/KayitTamam", new { email });
    }

    internal static async Task SendConfirmationAsync(UserManager<AppUser> users, AccountEmails emails, AppUser user, ILogger logger, string? returnUrl = null)
    {
        try
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = $"/hesap/eposta-onayla?userId={Uri.EscapeDataString(user.Id)}&code={code}";
            if (!string.IsNullOrEmpty(returnUrl))
            {
                link += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            }
            await emails.SendConfirmationAsync(user.Email!, user.DisplayName, link);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send confirmation email to user {UserId}", user.Id);
        }
    }
}
