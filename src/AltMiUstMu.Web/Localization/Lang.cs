using System.Globalization;
using AltMiUstMu.Core.Text;

namespace AltMiUstMu.Web.Localization;

/// <summary>
/// The two UI languages. Turkish is the default; English is opt-in per user (saved on the account) or per browser
/// (cookie). Text lives next to its usage as a pair: <c>T("Giriş yap", "Sign in")</c>.
/// </summary>
public static class Lang
{
    public const string Turkish = "tr";
    public const string English = "en";
    public const string CookieName = ".altmiustmu.lang";

    public static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    public static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    public static bool IsEnglish => LocalizedText.IsEnglish;

    /// <summary>"tr" or "en" for the current request.</summary>
    public static string Current => IsEnglish ? English : Turkish;

    public static string T(string tr, string en) => IsEnglish ? en : tr;

    /// <summary>Anything that is not "en" falls back to Turkish.</summary>
    public static string Normalize(string? code) =>
        string.Equals(code?.Trim(), English, StringComparison.OrdinalIgnoreCase) ? English : Turkish;

    public static CultureInfo Culture(string? code) => Normalize(code) == English ? EnglishCulture : TurkishCulture;
}
