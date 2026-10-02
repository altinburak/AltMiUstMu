using System.Globalization;
using System.Security.Claims;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Time;

namespace AltMiUstMu.Web.Helpers;

public sealed record StatusView(string Label, string Css, string Icon);

/// <summary>Formatting and display mapping used across pages. Class names live here so Tailwind can see them.</summary>
public static class Display
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string SideLabel(PickSide side) => side == PickSide.Over ? "ÜST" : "ALT";

    public static string SideChipCss(PickSide side) => side == PickSide.Over
        ? "bg-brand-500/15 text-brand-700 dark:text-brand-300 ring-1 ring-brand-500/30"
        : "bg-sky-500/15 text-sky-700 dark:text-sky-300 ring-1 ring-sky-500/30";

    public static StatusView Status(PickStatus? status) => status switch
    {
        PickStatus.ClinchedWin => new("Kesin ✓", "bg-emerald-500 text-white", "check"),
        PickStatus.TrendingWin => new("Gidiyor", "bg-emerald-500/15 text-emerald-700 ring-1 ring-emerald-500/30 dark:text-emerald-300", "up"),
        PickStatus.TrendingLoss => new("Riskte", "bg-amber-500/15 text-amber-700 ring-1 ring-amber-500/30 dark:text-amber-300", "warn"),
        PickStatus.ClinchedLoss => new("Kaybetti", "bg-red-500/15 text-red-700 ring-1 ring-red-500/30 dark:text-red-300", "x"),
        PickStatus.Push => new("Push", "bg-zinc-500/15 text-zinc-700 ring-1 ring-zinc-500/30 dark:text-zinc-300", "eq"),
        PickStatus.Pending => new("Bekliyor", "bg-zinc-500/10 text-zinc-600 ring-1 ring-zinc-500/20 dark:text-zinc-400", "clock"),
        _ => new("Seçilmedi", "bg-zinc-500/10 text-zinc-500 ring-1 ring-dashed ring-zinc-500/20", "none"),
    };

    public static string SeasonStatusLabel(SeasonStatus status) => status switch
    {
        SeasonStatus.Upcoming => "Başlamadı",
        SeasonStatus.Active => "Devam ediyor",
        SeasonStatus.Finished => "Bitti",
        _ => status.ToString(),
    };

    public static string ConferenceLabel(Conference conference) => conference == Conference.East ? "Doğu Konferansı" : "Batı Konferansı";

    /// <summary>Line like 47.5 -> "47,5" (Turkish decimal comma).</summary>
    public static string Line(decimal line) => line.ToString("0.0", Tr);

    public static string Number(decimal? value, string format = "0.0") => value?.ToString(format, Tr) ?? "–";

    public static string Percent(double value) => "%" + Math.Round(value * 100).ToString("0", Tr);

    /// <summary>Invariant number for CSS/JS (never "45,5").</summary>
    public static string Css(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    public static string Css(decimal value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    public static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string DateTime(DateTime utc) => Istanbul.FromUtc(utc).ToString("d MMMM yyyy HH:mm", Tr);

    public static string ShortDateTime(DateTime utc) => Istanbul.FromUtc(utc).ToString("dd.MM.yyyy HH:mm", Tr);

    public static string Date(DateOnly date) => date.ToString("d MMM", Tr);

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
