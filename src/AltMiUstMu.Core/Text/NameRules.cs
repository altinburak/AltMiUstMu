namespace AltMiUstMu.Core.Text;

/// <summary>Validation for user-chosen public names (display names, group names). Messages are Turkish.</summary>
public static class NameRules
{
    public const int DisplayNameMin = 3;
    public const int DisplayNameMax = 20;
    public const int GroupNameMin = 3;
    public const int GroupNameMax = 40;

    // Folded (see TextNormalizer.Fold). This is a speed bump, not a moderation system.
    // Matched anywhere inside the name (spaces/punctuation removed).
    private static readonly string[] BlockedAnywhere =
    [
        "AMINA", "AMCIK", "ANASINI", "ORUSPU", "OROSPU", "SIKER", "SIKIS", "SIKIM", "SIKTIR", "YARRAK",
        "PEZEVENK", "GAVAT", "KAHPE", "SEREFSIZ",
        "FUCK", "SHIT", "BITCH", "CUNT", "PUSSY", "NIGGER", "NIGGA", "FAGGOT", "WHORE", "ASSHOLE",
    ];

    // Short or ambiguous words only match as whole words (so "Göktuğ" does not hit "GOT").
    private static readonly string[] BlockedWords = ["AMK", "AMQ", "ANANI", "MK", "OC", "GOT", "PIC", "IBNE", "YARAK", "DICK", "SLUT"];

    private static readonly string[] Reserved =
    [
        "ADMIN", "ADMINISTRATOR", "YONETICI", "MODERATOR", "MOD", "DESTEK", "SUPPORT", "SISTEM", "SYSTEM",
        "KAAN KURAL", "INAN OZDEMIR", "AMERIKAN MUTFAK",
    ];

    public static IReadOnlyList<string> ValidateDisplayName(string? raw, bool allowReserved = false)
    {
        var errors = new List<string>();
        var name = TextNormalizer.CleanDisplay(raw);

        if (name.Length < DisplayNameMin || name.Length > DisplayNameMax)
        {
            errors.Add($"Kullanıcı adı {DisplayNameMin}-{DisplayNameMax} karakter olmalı.");
            return errors;
        }

        if (!name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '.' or '-'))
        {
            errors.Add("Kullanıcı adında yalnızca harf, rakam, boşluk, nokta, tire ve alt çizgi kullanılabilir.");
        }

        if (!name.Any(char.IsLetter))
        {
            errors.Add("Kullanıcı adı en az bir harf içermeli.");
        }

        if (ContainsProfanity(name))
        {
            errors.Add("Bu kullanıcı adı uygun değil, lütfen başka bir ad seç.");
        }
        else if (!allowReserved && IsReserved(name))
        {
            errors.Add("Bu kullanıcı adı ayrılmış, lütfen başka bir ad seç.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateGroupName(string? raw)
    {
        var errors = new List<string>();
        var name = TextNormalizer.CleanDisplay(raw);
        if (name.Length < GroupNameMin || name.Length > GroupNameMax)
        {
            errors.Add($"Grup adı {GroupNameMin}-{GroupNameMax} karakter olmalı.");
        }
        else if (ContainsProfanity(name))
        {
            errors.Add("Bu grup adı uygun değil, lütfen başka bir ad seç.");
        }

        return errors;
    }

    public static bool ContainsProfanity(string value)
    {
        var folded = Deleet(TextNormalizer.Fold(value));
        var words = folded.Split([' ', '_', '.', '-'], StringSplitOptions.RemoveEmptyEntries);
        var compact = string.Concat(words);

        if (BlockedAnywhere.Any(bad => compact.Contains(bad, StringComparison.Ordinal)))
        {
            return true;
        }

        if (words.Any(w => BlockedWords.Contains(w, StringComparer.Ordinal)))
        {
            return true;
        }

        return false;
    }

    public static bool IsReserved(string value)
    {
        var compact = TextNormalizer.Fold(value).Replace(" ", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return Reserved.Any(r => compact == r.Replace(" ", "", StringComparison.Ordinal));
    }

    private static string Deleet(string value) =>
        value.Replace('0', 'O').Replace('1', 'I').Replace('3', 'E').Replace('4', 'A').Replace('5', 'S').Replace('@', 'A').Replace('$', 'S');
}
