using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Web.Localization;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AltMiUstMu.Web.Infrastructure;

public static class PublicPageCache
{
    public const string PolicyName = "public";
    public const string Tag = "public-pages";

    /// <summary>
    /// Output cache for anonymous visitors of public pages. Entries are evicted in-process after a sync and are
    /// also keyed by a "data version" (last successful sync / admin change), so a sync run by the separate cron
    /// process is picked up within <see cref="DataVersionProvider.RefreshInterval"/>.
    /// </summary>
    public static void AddPublicPageCache(this IServiceCollection services)
    {
        services.AddSingleton<DataVersionProvider>();
        services.AddSingleton<ICacheInvalidator, OutputCacheInvalidator>();
        services.AddOutputCache(o =>
        {
            o.AddPolicy(PolicyName, b => b
                .Expire(TimeSpan.FromMinutes(10))
                .Tag(Tag)
                .SetVaryByQuery("*")
                .SetVaryByHeader("HX-Request")
                .VaryByValue(async (ctx, ct) =>
                {
                    var version = await ctx.RequestServices.GetRequiredService<DataVersionProvider>().GetAsync(ct);
                    return new KeyValuePair<string, string>("dv", version);
                })
                // Anonymous visitors pick a language with a cookie; never serve one language's page to the other.
                .VaryByValue(_ => new KeyValuePair<string, string>("lang", Lang.Current)));
        });
    }
}

public sealed class OutputCacheInvalidator(IOutputCacheStore store, DataVersionProvider version) : ICacheInvalidator
{
    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        version.Reset();
        await store.EvictByTagAsync(PublicPageCache.Tag, ct);
    }
}

public sealed class DataVersionProvider(IServiceScopeFactory scopes, IMemoryCache cache)
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
    private const string Key = "altmiustmu:data-version";

    public async ValueTask<string> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(Key, out string? cached) && cached is not null)
        {
            return cached;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lastSync = await db.SyncRuns.Where(r => r.Status == SyncRunStatus.Succeeded).MaxAsync(r => (DateTime?)r.FinishedAt, ct);
        var lastAdmin = await db.AdminAuditLogs.MaxAsync(a => (DateTime?)a.CreatedAt, ct);
        var value = $"{lastSync?.Ticks ?? 0}-{lastAdmin?.Ticks ?? 0}";
        cache.Set(Key, value, RefreshInterval);
        return value;
    }

    public void Reset() => cache.Remove(Key);
}
