using System.Text.Encodings.Web;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using Microsoft.AspNetCore.Html;
using static AltMiUstMu.Web.Localization.Lang;

namespace AltMiUstMu.Web.Helpers;

/// <summary>
/// Team "logos" are abbreviation badges in team colours (no official marks). Primary colour fills the badge,
/// secondary colour is a bottom stripe.
/// </summary>
public static class Badges
{
    public static IHtmlContent Team(string abbreviation, string primary, string secondary, string size = "md", string? title = null)
    {
        var (box, text) = size switch
        {
            "xs" => ("size-7 rounded-lg", "text-[9px]"),
            "sm" => ("size-9", "text-[11px]"),
            "lg" => ("size-16 sm:size-20 rounded-2xl", "text-lg sm:text-xl"),
            _ => ("size-12", "text-sm"),
        };
        var fg = Display.TextOn(primary);
        var enc = HtmlEncoder.Default;
        var label = title is null ? "" : $" title=\"{enc.Encode(title)}\"";
        return new HtmlString(
            $"<span class=\"team-badge {box} {text}\" style=\"background:{enc.Encode(primary)};color:{fg};box-shadow:inset 0 -4px 0 {enc.Encode(secondary)}\" role=\"img\" aria-label=\"{enc.Encode(title ?? abbreviation)}\"{label}>{enc.Encode(abbreviation)}</span>");
    }

    public static IHtmlContent Team(Services.TeamOverview t, string size = "md") => Team(t.Abbreviation, t.PrimaryColor, t.SecondaryColor, size, t.FullName);

    public static IHtmlContent Status(PickStatus? status, string extra = "")
    {
        var view = Display.Status(status);
        return new HtmlString($"<span class=\"chip {view.Css} {extra}\">{HtmlEncoder.Default.Encode(view.Label)}</span>");
    }

    public static IHtmlContent Side(PickSide? side, string extra = "")
    {
        if (side is null)
        {
            return new HtmlString($"<span class=\"chip bg-zinc-500/10 text-zinc-500 {extra}\">—</span>");
        }

        return new HtmlString($"<span class=\"chip {Display.SideChipCss(side.Value)} {extra}\">{Display.SideLabel(side.Value)}</span>");
    }

    public static IHtmlContent Pundit(string extra = "") =>
        new HtmlString($"<span class=\"chip bg-gradient-to-r from-brand-500 to-amber-400 text-white shadow-sm {extra}\" title=\"Amerikan Mutfak\">{T("🎙️ Mutfak", "🎙️ Pundit")}</span>");
}
