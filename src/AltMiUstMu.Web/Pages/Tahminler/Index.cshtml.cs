using System.Text.Json;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Identity;
using AltMiUstMu.Web.Infrastructure;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AltMiUstMu.Web.Pages.Tahminler;

public sealed record PickCardVm(TeamOverview Team, PickSide? Mine, bool Locked, IReadOnlyList<PunditView> Pundits, string PostUrl);

public sealed record PickProgressVm(int Count, int Total, bool Locked, DateTime LockAt, bool Oob);

public sealed record PickCardResponseVm(PickCardVm Card, PickProgressVm Progress);

[EnableRateLimiting("writes")]
public class IndexModel(SeasonService seasons, GameQueries queries, PickService picks, AccountEmails links) : PageModel
{
    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public List<TeamOverview> Teams { get; private set; } = [];
    public Dictionary<int, PickSide> MyPicks { get; private set; } = [];
    public List<PunditView> Pundits { get; private set; } = [];

    public string PostUrl => Url.Page("/Tahminler/Index", "Pick")!;

    public string ShareUrl => Url.Page("/Tahminler/Index", "Share")!;

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostPickAsync(int teamId, PickSide side, CancellationToken ct)
    {
        var userId = User.UserId()!;
        var result = await picks.SetPickAsync(userId, teamId, side, ct);
        await LoadAsync(ct);

        var message = result switch
        {
            PickResult.Locked => Lang.T("Tahminler kilitlendi; sezon başladı.", "Picks are locked; the season has started."),
            PickResult.InvalidTeam => Lang.T("Takım bulunamadı.", "Team not found."),
            PickResult.NoSeason => Lang.T("Aktif sezon yok.", "There is no active season."),
            PickResult.NotAllowed => Lang.T("Bu hesap tahmin yapamaz.", "This account can't make picks."),
            _ => null,
        };

        if (!Request.IsHtmx())
        {
            if (message is not null)
            {
                TempData["ToastError"] = message;
            }

            return RedirectToPage();
        }

        if (message is not null)
        {
            Response.Headers["HX-Trigger"] = JsonSerializer.Serialize(new { toast = new { message, type = "error" } });
        }

        var team = Teams.FirstOrDefault(t => t.Id == teamId);
        if (team is null)
        {
            return new NoContentResult();
        }

        return Partial("_PickCardResponse", new PickCardResponseVm(Card(team), Progress(oob: true)));
    }

    /// <summary>
    /// Everything the share sheet needs to draw the picks card (client-side canvas) and build the network links.
    /// The site address is part of both the text and the image, so a shared post always leads back here.
    /// </summary>
    public async Task<IActionResult> OnGetShareAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        if (Season is null)
        {
            return NotFound();
        }

        var over = MyPicks.Count(p => p.Value == PickSide.Over);
        var under = MyPicks.Count - over;
        var url = links.Absolute("/");
        var conferences = Teams
            .GroupBy(t => t.Conference)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                label = Display.ConferenceLabel(g.Key),
                teams = g.OrderBy(t => t.Division).ThenBy(t => t.City).Select(t => new
                {
                    abbr = t.Abbreviation,
                    primary = t.PrimaryColor,
                    secondary = t.SecondaryColor,
                    fg = Display.TextOn(t.PrimaryColor),
                    line = Display.Line(t.Line),
                    side = MyPicks.TryGetValue(t.Id, out var s) ? Display.SideLabel(s) : null,
                    over = MyPicks.TryGetValue(t.Id, out var o) ? o == PickSide.Over : (bool?)null,
                }),
            });

        return new JsonResult(new
        {
            url,
            host = new Uri(url).Host,
            text = Lang.T(
                $"{Season.Label} NBA sezonu için galibiyet tahminlerimi yaptım: {over} ÜST, {under} ALT 🏀 Kaan ve İnan'ı geçebilecek misin? Sen de tahminini yap:",
                $"I made my {Season.Label} NBA win-total picks: {over} OVER, {under} UNDER 🏀 Can you beat Kaan and İnan? Make your picks:"),
            heading = Lang.T($"{Season.Label} sezonu tahminlerim", $"My {Season.Label} season picks"),
            name = User.DisplayName(),
            over,
            under,
            overLabel = Display.Over,
            underLabel = Display.Under,
            tagline = Lang.T("Kaan ve İnan'ı geçebilecek misin?", "Can you beat Kaan and İnan?"),
            cta = Lang.T("Sen de tahminini yap 👉", "Make your picks 👉"),
            fileName = Lang.T("altmiustmu-tahminlerim.png", "altmiustmu-my-picks.png"),
            conferences,
        });
    }

    public PickCardVm Card(TeamOverview team) =>
        new(team, MyPicks.TryGetValue(team.Id, out var side) ? side : null, Locked, Pundits, PostUrl);

    public PickProgressVm Progress(bool oob = false) =>
        new(MyPicks.Count, Teams.Count == 0 ? PickRules.TeamCount : Teams.Count, Locked, Season?.LockAt ?? DateTime.UtcNow, oob);

    private async Task LoadAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return;
        }

        Locked = seasons.IsLocked(Season);
        Teams = await queries.GetTeamsAsync(Season, ct);
        MyPicks = await queries.GetPicksAsync(Season.Id, User.UserId()!, ct);
        Pundits = await queries.GetPunditsAsync(Season.Id, ct);
    }
}
