using System.Globalization;

namespace AltMiUstMu.Core.Text;

/// <summary>
/// A user-facing message in both supported languages. <see cref="ToString"/> picks the one matching the current UI
/// culture, so services can return messages without knowing who reads them.
/// </summary>
public sealed record LocalizedText(string Tr, string En)
{
    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    public override string ToString() => IsEnglish ? En : Tr;
}
