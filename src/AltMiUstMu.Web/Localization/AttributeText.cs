using System.Collections.Frozen;
using Microsoft.Extensions.Localization;

namespace AltMiUstMu.Web.Localization;

/// <summary>
/// English for the Turkish strings used in DataAnnotations attributes (<c>ErrorMessage</c>, <c>[Display(Name)]</c>),
/// which must be compile-time constants and therefore cannot use <see cref="Lang.T"/>. The Turkish text is the key.
/// A test checks that every attribute string on the public (non-admin) pages has an entry; admin pages stay Turkish.
/// </summary>
public static class AttributeText
{
    public static readonly FrozenDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Beni hatırla"] = "Remember me",
        ["E-posta"] = "Email",
        ["Kullanıcı adı"] = "Username",
        ["Mevcut şifre"] = "Current password",
        ["Yeni şifre"] = "New password",
        ["Yeni şifre (tekrar)"] = "New password (again)",
        ["Şifre"] = "Password",
        ["Şifre (tekrar)"] = "Password (again)",
        ["Dil"] = "Language",
        ["Devam etmek için kuralları kabul etmelisin."] = "You need to accept the rules to continue.",
        ["E-posta zorunlu."] = "Email is required.",
        ["Geçerli bir e-posta adresi gir."] = "Enter a valid email address.",
        ["Kullanıcı adı zorunlu."] = "Username is required.",
        ["Mevcut şifre zorunlu."] = "Current password is required.",
        ["Yeni şifre zorunlu."] = "New password is required.",
        ["Şifre en az 8 karakter olmalı."] = "Password must be at least 8 characters.",
        ["Şifre zorunlu."] = "Password is required.",
        ["Şifreler eşleşmiyor."] = "Passwords don't match.",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static string Translate(string turkish) =>
        Lang.IsEnglish && English.TryGetValue(turkish, out var english) ? english : turkish;
}

/// <summary>DataAnnotations localizer backed by <see cref="AttributeText"/>; unknown keys are returned unchanged.</summary>
public sealed class AttributeTextLocalizer : IStringLocalizer
{
    public LocalizedString this[string name] => new(name, AttributeText.Translate(name));

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(System.Globalization.CultureInfo.CurrentCulture, AttributeText.Translate(name), arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        AttributeText.English.Select(p => new LocalizedString(p.Key, AttributeText.Translate(p.Key)));
}
