using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Infrastructure;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace AltMiUstMu.Web.Pages;

[OutputCache(PolicyName = PublicPageCache.PolicyName)]
public class IndexModel(SeasonService seasons, GameQueries queries) : PageModel
{
    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public List<PlayerRow> Top { get; private set; } = [];
    public List<PunditView> Pundits { get; private set; } = [];
    public int CompletePlayers { get; private set; }
    public int RankedCount { get; private set; }
    public int TeamCount { get; private set; } = PickRules.TeamCount;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return;
        }

        Locked = seasons.IsLocked(Season);
        Pundits = await queries.GetPunditsAsync(Season.Id, ct);
        if (Locked)
        {
            Top = await queries.TopAsync(Season.Id, 5, includePundits: true, ct);
            RankedCount = await queries.RankedCountAsync(Season.Id, ct);
        }
        else
        {
            CompletePlayers = await queries.CompletePlayersAsync(Season.Id, TeamCount, ct);
        }
    }
}
