using AltMiUstMu.Core.Catalog;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AltMiUstMu.Infrastructure.Services;

/// <summary>Idempotent base data: teams, the 2026-27 season, placeholder lines, pundits, admin.</summary>
public class SeedService(
    AppDbContext db,
    UserManager<AppUser> users,
    RoleManager<IdentityRole> roles,
    IConfiguration config,
    TimeProvider time,
    ILogger<SeedService> logger)
{
    public const string SeasonLabel = "2026-27";

    /// <summary>Placeholder: opening night 2026-27 is expected around Oct 20, 2026 (US evening = 23:00 UTC).</summary>
    public static readonly DateTime PlaceholderLockAt = new(2026, 10, 20, 23, 0, 0, DateTimeKind.Utc);

    public static readonly (string DisplayName, string Email)[] Pundits =
    [
        ("Kaan Kural", "kaan.kural@pundit.altmiustmu.invalid"),
        ("İnan Özdemir", "inan.ozdemir@pundit.altmiustmu.invalid"),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await SeedTeamsAsync(ct);

        var season = await db.Seasons.SingleOrDefaultAsync(s => s.Label == SeasonLabel, ct);
        if (season is null)
        {
            season = new Season { Label = SeasonLabel, LockAt = PlaceholderLockAt, Status = SeasonStatus.Upcoming, GamesPerTeam = 82, CreatedAt = now };
            db.Seasons.Add(season);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created season {Label} (lock {LockAt:u})", season.Label, season.LockAt);
        }

        var teams = await db.Teams.ToDictionaryAsync(t => t.Abbreviation, ct);
        var lines = await db.TeamLines.Where(l => l.SeasonId == season.Id).Select(l => l.TeamId).ToListAsync(ct);
        var records = await db.TeamRecords.Where(r => r.SeasonId == season.Id).Select(r => r.TeamId).ToListAsync(ct);
        foreach (var seed in TeamCatalog.Teams)
        {
            var team = teams[seed.Abbreviation];
            if (!lines.Contains(team.Id))
            {
                db.TeamLines.Add(new TeamLine { SeasonId = season.Id, TeamId = team.Id, Line = seed.PlaceholderLine, UpdatedAt = now });
            }

            if (!records.Contains(team.Id))
            {
                db.TeamRecords.Add(new TeamRecord { SeasonId = season.Id, TeamId = team.Id, Wins = 0, Losses = 0, Source = RecordSource.Api, UpdatedAt = now });
            }
        }

        await db.SaveChangesAsync(ct);

        if (!await roles.RoleExistsAsync(Roles.Admin))
        {
            await roles.CreateAsync(new IdentityRole(Roles.Admin));
        }

        foreach (var (name, email) in Pundits)
        {
            await EnsurePunditAsync(name, email, now);
        }

        await EnsureAdminAsync(now);
        logger.LogInformation("Seed complete");
    }

    public async Task<AppUser> EnsurePunditAsync(string displayName, string email, DateTime now)
    {
        var key = TextNormalizer.Fold(displayName);
        var existing = await db.Users.SingleOrDefaultAsync(u => u.DisplayNameKey == key);
        if (existing is not null)
        {
            if (!existing.IsPundit)
            {
                existing.IsPundit = true;
                await users.UpdateAsync(existing);
            }

            return existing;
        }

        var pundit = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            DisplayNameKey = key,
            IsPundit = true,
            CreatedAt = now,
            EmailConfirmed = false,
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.MaxValue,
        };
        Check(await users.CreateAsync(pundit), $"pundit {displayName}");
        logger.LogInformation("Created pundit {Name}", displayName);
        return pundit;
    }

    private async Task SeedTeamsAsync(CancellationToken ct)
    {
        var existing = await db.Teams.ToDictionaryAsync(t => t.Abbreviation, ct);
        foreach (var seed in TeamCatalog.Teams)
        {
            if (!existing.TryGetValue(seed.Abbreviation, out var team))
            {
                team = new Team { Abbreviation = seed.Abbreviation };
                db.Teams.Add(team);
            }

            team.City = seed.City;
            team.Name = seed.Name;
            team.Conference = seed.Conference;
            team.Division = seed.Division;
            team.PrimaryColor = seed.PrimaryColor;
            team.SecondaryColor = seed.SecondaryColor;
            team.EspnId = seed.EspnId;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAdminAsync(DateTime now)
    {
        var email = config["ADMIN_EMAIL"];
        var password = config["ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("ADMIN_EMAIL not set; no admin user created");
            return;
        }

        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("ADMIN_PASSWORD not set; cannot create admin {Email}", email);
                return;
            }

            var displayName = "Admin";
            var suffix = 1;
            while (await db.Users.AnyAsync(u => u.DisplayNameKey == TextNormalizer.Fold(displayName)))
            {
                displayName = $"Admin{++suffix}";
            }

            admin = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                DisplayNameKey = TextNormalizer.Fold(displayName),
                CreatedAt = now,
                LockoutEnabled = true,
            };
            Check(await users.CreateAsync(admin, password), "admin");
            logger.LogInformation("Created admin user {Email}", email);
        }
        else if (!admin.EmailConfirmed)
        {
            admin.EmailConfirmed = true;
            await users.UpdateAsync(admin);
        }

        if (!await users.IsInRoleAsync(admin, Roles.Admin))
        {
            await users.AddToRoleAsync(admin, Roles.Admin);
        }
    }

    private static void Check(IdentityResult result, string what)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not create {what}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
