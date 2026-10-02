using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Infrastructure;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace AltMiUstMu.Web.Pages;

[OutputCache(PolicyName = PublicPageCache.PolicyName)]
public class TakimModel(SeasonService seasons, GameQueries queries) : PageModel
{
    public Season Season { get; private set; } = null!;
    public bool Locked { get; private set; }
    public TeamOverview Team { get; private set; } = null!;
    public List<PunditView> Pundits { get; private set; } = [];
    public PickSide? MyPick { get; private set; }

    public async Task<IActionResult> OnGetAsync(string abbr, CancellationToken ct)
    {
        var season = await seasons.GetCurrentAsync(ct);
        if (season is null)
        {
            return NotFound();
        }

        Season = season;
        var teams = await queries.GetTeamsAsync(season, ct);
        var team = teams.FirstOrDefault(t => string.Equals(t.Abbreviation, abbr, StringComparison.OrdinalIgnoreCase));
        if (team is null)
        {
            return NotFound();
        }

        Team = team;
        Locked = seasons.IsLocked(season);
        Pundits = await queries.GetPunditsAsync(season.Id, ct);
        if (User.UserId() is { } userId)
        {
            var picks = await queries.GetPicksAsync(season.Id, userId, ct);
            MyPick = picks.TryGetValue(team.Id, out var side) ? side : null;
        }

        return Page();
    }
}
