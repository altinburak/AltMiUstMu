using System.Text.Json;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AltMiUstMu.Web.Pages.Tahminler;

public sealed record PickCardVm(TeamOverview Team, PickSide? Mine, bool Locked, IReadOnlyList<PunditView> Pundits, string PostUrl);

public sealed record PickProgressVm(int Count, int Total, bool Locked, DateTime LockAt, bool Oob);

public sealed record PickCardResponseVm(PickCardVm Card, PickProgressVm Progress);

[EnableRateLimiting("writes")]
public class IndexModel(SeasonService seasons, GameQueries queries, PickService picks) : PageModel
{
    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public List<TeamOverview> Teams { get; private set; } = [];
    public Dictionary<int, PickSide> MyPicks { get; private set; } = [];
    public List<PunditView> Pundits { get; private set; } = [];

    public string PostUrl => Url.Page("/Tahminler/Index", "Pick")!;

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostPickAsync(int teamId, PickSide side, CancellationToken ct)
    {
        var userId = User.UserId()!;
        var result = await picks.SetPickAsync(userId, teamId, side, ct);
        await LoadAsync(ct);

        var message = result switch
        {
            PickResult.Locked => "Tahminler kilitlendi; sezon başladı.",
            PickResult.InvalidTeam => "Takım bulunamadı.",
            PickResult.NoSeason => "Aktif sezon yok.",
            PickResult.NotAllowed => "Bu hesap tahmin yapamaz.",
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
