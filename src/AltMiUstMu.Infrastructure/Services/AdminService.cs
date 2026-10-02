using System.Globalization;
using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Infrastructure.Services;

/// <summary>
/// Admin mutations. Each one writes AdminAuditLog rows in the same SaveChanges, recomputes the stored scores
/// and invalidates public page caches.
/// </summary>
public class AdminService(
    AppDbContext db,
    AuditService audit,
    ScoreService scores,
    ICacheInvalidator cache,
    UserManager<AppUser> users,
    SeedService seed,
    TimeProvider time)
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<string?> UpdateSeasonAsync(AuditActor actor, int seasonId, string label, DateTime lockAtUtc, int gamesPerTeam, SeasonStatus status, CancellationToken ct = default)
    {
        label = label.Trim();
        if (label.Length is 0 or > 20)
        {
            return "Sezon adı 1-20 karakter olmalı.";
        }

        if (gamesPerTeam is < 1 or > 100)
        {
            return "Takım başına maç sayısı 1 ile 100 arasında olmalı.";
        }

        var season = await db.Seasons.SingleAsync(s => s.Id == seasonId, ct);
        if (await db.Seasons.AnyAsync(s => s.Label == label && s.Id != seasonId, ct))
        {
            return "Bu sezon adı zaten kullanılıyor.";
        }

        var maxPlayed = await db.TeamRecords.Where(r => r.SeasonId == seasonId).Select(r => (int?)(r.Wins + r.Losses)).MaxAsync(ct) ?? 0;
        if (gamesPerTeam < maxPlayed)
        {
            return $"Bazı takımlar zaten {maxPlayed} maç oynadı; maç sayısı bundan az olamaz.";
        }

        var target = $"Sezon {season.Label}";
        if (season.Label != label)
        {
            audit.Log(actor, "season", target, $"Label={season.Label}", $"Label={label}");
            season.Label = label;
        }

        lockAtUtc = DateTime.SpecifyKind(lockAtUtc, DateTimeKind.Utc);
        if (season.LockAt != lockAtUtc)
        {
            audit.Log(actor, "season", target, $"LockAt={season.LockAt.ToString("u", Inv)}", $"LockAt={lockAtUtc.ToString("u", Inv)}");
            season.LockAt = lockAtUtc;
        }

        if (season.GamesPerTeam != gamesPerTeam)
        {
            audit.Log(actor, "season", target, $"GamesPerTeam={season.GamesPerTeam}", $"GamesPerTeam={gamesPerTeam}");
            season.GamesPerTeam = gamesPerTeam;
        }

        if (season.Status != status)
        {
            audit.Log(actor, "season", target, $"Status={season.Status}", $"Status={status}");
            season.Status = status;
        }

        await SaveAndRefreshAsync(seasonId, ct);
        return null;
    }

    /// <summary>Returns per-team error messages (empty when everything was saved).</summary>
    public async Task<List<string>> UpdateLinesAsync(AuditActor actor, int seasonId, IReadOnlyDictionary<int, decimal> newLines, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().SingleAsync(s => s.Id == seasonId, ct);
        var lines = await db.TeamLines.Include(l => l.Team).Where(l => l.SeasonId == seasonId).ToDictionaryAsync(l => l.TeamId, ct);
        var errors = new List<string>();
        var now = time.GetUtcNow().UtcDateTime;

        foreach (var (teamId, value) in newLines)
        {
            if (!lines.TryGetValue(teamId, out var line))
            {
                continue;
            }

            if (value <= 0 || value >= season.GamesPerTeam || decimal.Round(value, 1) != value)
            {
                errors.Add($"{line.Team!.Abbreviation}: barem 0 ile {season.GamesPerTeam} arasında, en fazla bir ondalıklı olmalı.");
                continue;
            }

            if (line.Line != value)
            {
                audit.Log(actor, "line", $"{line.Team!.Abbreviation} barem", line.Line.ToString(Inv), value.ToString(Inv));
                line.Line = value;
                line.UpdatedAt = now;
            }
        }

        if (errors.Count == 0)
        {
            await SaveAndRefreshAsync(seasonId, ct);
        }
        else
        {
            db.ChangeTracker.Clear();
        }

        return errors;
    }

    public async Task<string?> SetManualRecordAsync(AuditActor actor, int seasonId, int teamId, int wins, int losses, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().SingleAsync(s => s.Id == seasonId, ct);
        if (wins < 0 || losses < 0 || wins + losses > season.GamesPerTeam)
        {
            return $"Galibiyet + mağlubiyet 0 ile {season.GamesPerTeam} arasında olmalı.";
        }

        var team = await db.Teams.AsNoTracking().SingleAsync(t => t.Id == teamId, ct);
        var record = await db.TeamRecords.SingleOrDefaultAsync(r => r.SeasonId == seasonId && r.TeamId == teamId, ct);
        if (record is null)
        {
            record = new TeamRecord { SeasonId = seasonId, TeamId = teamId };
            db.TeamRecords.Add(record);
        }

        audit.Log(actor, "manual-record", team.Abbreviation, $"{record.Wins}-{record.Losses} ({record.Source})", $"{wins}-{losses} (Manual)");
        record.Wins = wins;
        record.Losses = losses;
        record.Source = RecordSource.Manual;
        record.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await SaveAndRefreshAsync(seasonId, ct);
        return null;
    }

    public async Task ClearManualRecordAsync(AuditActor actor, int seasonId, int teamId, CancellationToken ct = default)
    {
        var record = await db.TeamRecords.Include(r => r.Team).SingleOrDefaultAsync(r => r.SeasonId == seasonId && r.TeamId == teamId, ct);
        if (record is null || record.Source != RecordSource.Manual)
        {
            return;
        }

        audit.Log(actor, "manual-record", record.Team!.Abbreviation, "Manual", "Api (override kaldırıldı)");
        record.Source = RecordSource.Api;
        await SaveAndRefreshAsync(seasonId, ct);
    }

    /// <summary>
    /// Sets a random temporary password (used while email is off, so "forgot password" goes through the admin).
    /// Returns the new password, or an error message.
    /// </summary>
    public async Task<(string? Password, string? Error)> ResetPasswordAsync(AuditActor actor, string userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.IsPundit)
        {
            return (null, "Kullanıcı bulunamadı.");
        }

        // Lowercase letters + digits only: easy to dictate, satisfies the password policy.
        const string alphabet = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        var chars = new char[10];
        for (var i = 0; i < chars.Length; i++)
        {
            var set = i % 3 == 2 ? digits : alphabet;
            chars[i] = set[System.Security.Cryptography.RandomNumberGenerator.GetInt32(set.Length)];
        }

        var password = new string(chars);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
        {
            return (null, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        audit.Log(actor, "user", user.DisplayName, null, "geçici şifre verildi");
        await db.SaveChangesAsync(ct);
        return (password, null);
    }

    public async Task<string?> SetDisabledAsync(AuditActor actor, string userId, bool disabled, CancellationToken ct = default)
    {
        if (userId == actor.UserId)
        {
            return "Kendi hesabını devre dışı bırakamazsın.";
        }

        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return "Kullanıcı bulunamadı.";
        }

        if (user.IsDisabled == disabled)
        {
            return null;
        }

        user.IsDisabled = disabled;
        audit.Log(actor, "user", user.DisplayName, disabled ? "aktif" : "devre dışı", disabled ? "devre dışı" : "aktif");
        await users.UpdateAsync(user); // saves the audit row too
        await users.UpdateSecurityStampAsync(user); // signs the user out everywhere within the validation interval

        var season = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (season is not null)
        {
            await scores.RecomputeAsync(season.Id, writeSnapshot: false, ct);
        }

        await cache.InvalidateAsync(ct);
        return null;
    }

    public async Task<string?> CreatePunditAsync(AuditActor actor, string rawName, CancellationToken ct = default)
    {
        var name = TextNormalizer.CleanDisplay(rawName);
        var errors = NameRules.ValidateDisplayName(name, allowReserved: true);
        if (errors.Count > 0)
        {
            return errors[0];
        }

        var key = TextNormalizer.Fold(name);
        if (await db.Users.AnyAsync(u => u.DisplayNameKey == key, ct))
        {
            return "Bu ad zaten kullanılıyor.";
        }

        var email = $"{TextNormalizer.Slugify(name)}-{GroupService.RandomCode(4).ToLowerInvariant()}@pundit.altmiustmu.invalid";
        await seed.EnsurePunditAsync(name, email, time.GetUtcNow().UtcDateTime);
        audit.Log(actor, "pundit", name, null, "oluşturuldu");
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(ct);
        return null;
    }

    private async Task SaveAndRefreshAsync(int seasonId, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await scores.RecomputeAsync(seasonId, writeSnapshot: false, ct);
        await cache.InvalidateAsync(ct);
    }
}
