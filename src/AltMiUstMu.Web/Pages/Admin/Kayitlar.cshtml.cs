using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AltMiUstMu.Web.Pages.Admin;

public class KayitlarModel(SeasonService seasons, GameQueries queries, AdminService admin) : AdminPageModel
{
    public Season? Season { get; private set; }
    public List<TeamOverview> Teams { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LoadAsync(ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostSetAsync(int teamId, int wins, int losses, CancellationToken ct)
    {
        if (!await LoadAsync(ct))
        {
            return NotFound();
        }

        var error = await admin.SetManualRecordAsync(Actor, Season!.Id, teamId, wins, losses, ct);
        if (error is null)
        {
            Flash("Manuel derece kaydedildi; bu takım senkronizasyonda atlanacak.");
        }
        else
        {
            FlashError(error);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearAsync(int teamId, CancellationToken ct)
    {
        if (!await LoadAsync(ct))
        {
            return NotFound();
        }

        await admin.ClearManualRecordAsync(Actor, Season!.Id, teamId, ct);
        Flash("Override kaldırıldı; bir sonraki senkronizasyon dereceyi güncelleyecek.");
        return RedirectToPage();
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return false;
        }

        Teams = (await queries.GetTeamsAsync(Season, ct)).OrderBy(t => t.Conference).ThenBy(t => t.City).ToList();
        return true;
    }
}
