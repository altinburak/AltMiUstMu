using System.Globalization;
using System.Text;

namespace AltMiUstMu.Core.Text;

public static class TextNormalizer
{
    /// <summary>
    /// Culture-independent key for comparisons: upper case, Turkish letters folded to ASCII, whitespace collapsed.
    /// "İnan Özdemir", "inan ozdemir" and "INAN  OZDEMIR" all map to "INAN OZDEMIR".
    /// </summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var sb = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var raw in value.Trim())
        {
            var c = raw switch
            {
                'ı' or 'İ' or 'i' => 'I',
                'ş' or 'Ş' => 'S',
                'ğ' or 'Ğ' => 'G',
                'ü' or 'Ü' => 'U',
                'ö' or 'Ö' => 'O',
                'ç' or 'Ç' => 'C',
                _ => char.ToUpperInvariant(raw),
            };

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            lastWasSpace = false;
            sb.Append(c);
        }

        // Strip any remaining diacritics (é -> E etc.).
        var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(c);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>URL slug: lower-case ASCII letters, digits and dashes.</summary>
    public static string Slugify(string value, int maxLength = 40)
    {
        var folded = Fold(value).ToLowerInvariant();
        var sb = new StringBuilder(folded.Length);
        var lastDash = true;
        foreach (var c in folded)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(c);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug.Length == 0 ? "grup" : slug;
    }

    /// <summary>Trims and collapses inner whitespace without changing case.</summary>
    public static string CleanDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
