using System.Globalization;
using System.Security.Claims;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Time;
using static AltMiUstMu.Web.Localization.Lang;

namespace AltMiUstMu.Web.Helpers;

public sealed record StatusView(string Label, string Css, string Icon);

/// <summary>
/// Formatting and display mapping used across pages, in the request language (numbers: "47,5" in Turkish, "47.5" in
/// English). Class names live here so Tailwind can see them.
/// </summary>
public static class Display
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    public static string SideLabel(PickSide side) => side == PickSide.Over ? T("ÜST", "OVER") : T("ALT", "UNDER");

    public static string Over => T("ÜST", "OVER");

    public static string Under => T("ALT", "UNDER");

    /// <summary>Label for Istanbul time next to dates.</summary>
    public static string TimeZone => T("TSİ", "Istanbul time");

    public static string SideChipCss(PickSide side) => side == PickSide.Over
        ? "bg-brand-500/15 text-brand-700 dark:text-brand-300 ring-1 ring-brand-500/30"
        : "bg-sky-500/15 text-sky-700 dark:text-sky-300 ring-1 ring-sky-500/30";

    public static StatusView Status(PickStatus? status) => status switch
    {
        PickStatus.ClinchedWin => new(T("Kesin ✓", "Clinched ✓"), "bg-emerald-500 text-white", "check"),
        PickStatus.TrendingWin => new(T("Gidiyor", "On track"), "bg-emerald-500/15 text-emerald-700 ring-1 ring-emerald-500/30 dark:text-emerald-300", "up"),
        PickStatus.TrendingLoss => new(T("Riskte", "At risk"), "bg-amber-500/15 text-amber-700 ring-1 ring-amber-500/30 dark:text-amber-300", "warn"),
        PickStatus.ClinchedLoss => new(T("Kaybetti", "Lost"), "bg-red-500/15 text-red-700 ring-1 ring-red-500/30 dark:text-red-300", "x"),
        PickStatus.Push => new("Push", "bg-zinc-500/15 text-zinc-700 ring-1 ring-zinc-500/30 dark:text-zinc-300", "eq"),
        PickStatus.Pending => new(T("Bekliyor", "Pending"), "bg-zinc-500/10 text-zinc-600 ring-1 ring-zinc-500/20 dark:text-zinc-400", "clock"),
        _ => new(T("Seçilmedi", "No pick"), "bg-zinc-500/10 text-zinc-500 ring-1 ring-dashed ring-zinc-500/20", "none"),
    };

    public static string SeasonStatusLabel(SeasonStatus status) => status switch
    {
        SeasonStatus.Upcoming => T("Başlamadı", "Not started"),
        SeasonStatus.Active => T("Devam ediyor", "In progress"),
        SeasonStatus.Finished => T("Bitti", "Finished"),
        _ => status.ToString(),
    };

    public static string ConferenceLabel(Conference conference) => conference == Conference.East
        ? T("Doğu Konferansı", "Eastern Conference")
        : T("Batı Konferansı", "Western Conference");

    /// <summary>Division names are stored in Turkish (see TeamCatalog).</summary>
    public static string Division(string division) => !IsEnglish ? division : division switch
    {
        "Atlantik" => "Atlantic",
        "Merkez" => "Central",
        "Güneydoğu" => "Southeast",
        "Kuzeybatı" => "Northwest",
        "Pasifik" => "Pacific",
        "Güneybatı" => "Southwest",
        _ => division,
    };

    /// <summary>Line like 47.5 -> "47,5" in Turkish (decimal comma), "47.5" in English.</summary>
    public static string Line(decimal line) => line.ToString("0.0", Culture);

    public static string Number(decimal? value, string format = "0.0") => value?.ToString(format, Culture) ?? "–";

    /// <summary>"%64" in Turkish, "64%" in English.</summary>
    public static string Percent(double value)
    {
        var number = Math.Round(value * 100).ToString("0", Culture);
        return IsEnglish ? number + "%" : "%" + number;
    }

    /// <summary>Invariant number for CSS/JS (never "45,5").</summary>
    public static string Css(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    public static string Css(decimal value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    public static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string DateTime(DateTime utc) => Istanbul.FromUtc(utc).ToString("d MMMM yyyy HH:mm", Culture);

    public static string ShortDateTime(DateTime utc) =>
        Istanbul.FromUtc(utc).ToString(IsEnglish ? "d MMM yyyy HH:mm" : "dd.MM.yyyy HH:mm", Culture);

    public static string Date(DateOnly date) => date.ToString("d MMM", Culture);

    public static string Iso(DateTime utc) => System.DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    public static string Movement(int? rank, int? previous, out string css)
    {
        if (rank is null || previous is null || rank == previous)
        {
            css = "text-zinc-400";
            return "–";
        }

        var delta = previous.Value - rank.Value;
        css = delta > 0 ? "text-emerald-500" : "text-red-500";
        return delta > 0 ? $"▲{delta}" : $"▼{-delta}";
    }

    /// <summary>Text colour (black or white) with the best contrast on a team colour.</summary>
    public static string TextOn(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            return "#FFFFFF";
        }

        var r = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var luminance = (0.2126 * Lin(r)) + (0.7152 * Lin(g)) + (0.0722 * Lin(b));
        return luminance > 0.36 ? "#0B0B0B" : "#FFFFFF";
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : name.Length >= 2 ? name[..2] : name;
        return initials.ToUpper(Tr);
    }
}

public static class HttpExtensions
{
    public static bool IsHtmx(this HttpRequest request) =>
        request.Headers.ContainsKey("HX-Request") && !request.Headers.ContainsKey("HX-History-Restore-Request");

    public static string? UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}
