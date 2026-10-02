using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Infrastructure;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace AltMiUstMu.Web.Pages;

[OutputCache(PolicyName = PublicPageCache.PolicyName)]
public class TakimlarModel(SeasonService seasons, GameQueries queries) : PageModel
{
    public Season? Season { get; private set; }
    public List<TeamOverview> Teams { get; private set; } = [];
    public DateTime? LastUpdate { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return;
        }

        Teams = await queries.GetTeamsAsync(Season, ct);
        LastUpdate = Teams.Max(t => t.RecordUpdatedAt);
    }
}
