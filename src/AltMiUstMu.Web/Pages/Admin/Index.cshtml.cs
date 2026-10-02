using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Admin;

public class IndexModel(AppDbContext db, SeasonService seasons, SyncService sync) : AdminPageModel
{
    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public int Users { get; private set; }
    public int CompletePlayers { get; private set; }
    public int Groups { get; private set; }
    public int ManualRecords { get; private set; }
    public List<SyncRun> Runs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        Users = await db.Users.CountAsync(u => !u.IsPundit, ct);
        Groups = await db.Groups.CountAsync(ct);
        Runs = await db.SyncRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(30).ToListAsync(ct);
        if (Season is not null)
        {
            Locked = seasons.IsLocked(Season);
            CompletePlayers = await db.Picks.Where(p => p.SeasonId == Season.Id).GroupBy(p => p.UserId).CountAsync(g => g.Count() >= 30, ct);
            ManualRecords = await db.TeamRecords.CountAsync(r => r.SeasonId == Season.Id && r.Source == RecordSource.Manual, ct);
        }
    }

    public async Task<IActionResult> OnPostSyncAsync(CancellationToken ct)
    {
        var outcome = await sync.RunAsync(SyncTrigger.Admin, ct);
        if (outcome.Success)
        {
            Flash("Senkronizasyon tamam: " + outcome.Message);
        }
        else
        {
            FlashError("Senkronizasyon başarısız: " + outcome.Message);
        }

        return RedirectToPage();
    }
}
