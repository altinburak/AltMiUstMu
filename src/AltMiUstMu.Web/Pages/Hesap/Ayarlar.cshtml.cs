using System.ComponentModel.DataAnnotations;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Web.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Hesap;

[EnableRateLimiting("auth")]
public class AyarlarModel(UserManager<AppUser> users, SignInManager<AppUser> signIn, AppDbContext db, LanguageService languages) : PageModel
{
    [BindProperty]
    public NameInput Name { get; set; } = new();

    [BindProperty]
    public PasswordInput Password { get; set; } = new();

    [BindProperty]
    public string Language { get; set; } = Lang.Turkish;

    public string Email { get; private set; } = "";

    public sealed class NameInput
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunlu.")]
        [Display(Name = "Kullanıcı adı")]
        public string DisplayName { get; set; } = "";
    }

    public sealed class PasswordInput
    {
        [Required(ErrorMessage = "Mevcut şifre zorunlu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mevcut şifre")]
        public string Current { get; set; } = "";

        [Required(ErrorMessage = "Yeni şifre zorunlu.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı.")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifre")]
        public string New { get; set; } = "";

        [DataType(DataType.Password)]
        [Compare(nameof(New), ErrorMessage = "Şifreler eşleşmiyor.")]
        [Display(Name = "Yeni şifre (tekrar)")]
        public string Confirm { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        Email = user.Email ?? "";
        Name.DisplayName = user.DisplayName;
        Language = user.Language;
        return Page();
    }

    public async Task<IActionResult> OnPostNameAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        Email = user.Email ?? "";
        Language = user.Language;
        ClearOtherForm(nameof(Password));
        var displayName = TextNormalizer.CleanDisplay(Name.DisplayName);
        foreach (var error in NameRules.ValidateDisplayName(displayName))
        {
            ModelState.AddModelError("Name.DisplayName", error.ToString());
        }

        var key = TextNormalizer.Fold(displayName);
        if (ModelState.IsValid && await db.Users.AnyAsync(u => u.DisplayNameKey == key && u.Id != user.Id))
        {
            ModelState.AddModelError("Name.DisplayName", Lang.T("Bu kullanıcı adı alınmış.", "This username is taken."));
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        user.DisplayName = displayName;
        user.DisplayNameKey = key;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("Name.DisplayName", Lang.T("Kullanıcı adı güncellenemedi.", "Could not update your username."));
            return Page();
        }

        await signIn.RefreshSignInAsync(user);
        TempData["Toast"] = Lang.T("Kullanıcı adın güncellendi.", "Your username was updated.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        Email = user.Email ?? "";
        Name.DisplayName = user.DisplayName;
        Language = user.Language;
        ClearOtherForm(nameof(Name));
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await users.ChangePasswordAsync(user, Password.Current, Password.New);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code == "PasswordMismatch" ? "Password.Current" : "Password.New", error.Description);
            }

            return Page();
        }

        await signIn.RefreshSignInAsync(user);
        TempData["Toast"] = Lang.T("Şifren değiştirildi.", "Your password was changed.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLanguageAsync()
    {
        await languages.SetAsync(HttpContext, Language);

        // This request still runs in the old language; word the toast in the new one.
        TempData["Toast"] = Lang.Normalize(Language) == Lang.English ? "Language updated: English." : "Dil güncellendi: Türkçe.";
        return RedirectToPage();
    }

    private void ClearOtherForm(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith(prefix + ".", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }
    }
}
