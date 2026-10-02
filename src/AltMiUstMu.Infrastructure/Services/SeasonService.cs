using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Infrastructure.Services;

public class SeasonService(AppDbContext db, TimeProvider time)
{
    private Season? _current;
    private bool _loaded;

    /// <summary>The season the app is about: the most recently created one.</summary>
    public async Task<Season?> GetCurrentAsync(CancellationToken ct = default)
    {
        if (!_loaded)
        {
            _current = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
            _loaded = true;
        }

        return _current;
    }

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public bool IsLocked(Season season) => PickRules.IsLocked(season, UtcNow);
}
