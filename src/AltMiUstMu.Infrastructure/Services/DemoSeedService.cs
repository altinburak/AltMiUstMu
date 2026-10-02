using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Core.Time;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AltMiUstMu.Infrastructure.Services;

/// <summary>
/// Development-only fake data: ~50 players, groups, a mid-season set of records and 30 days of snapshot history.
/// Moves the season lock into the past so all "in season" views can be checked.
/// </summary>
public class DemoSeedService(
    AppDbContext db,
    UserManager<AppUser> users,
    SeedService seed,
    ScoreService scores,
    TimeProvider time,
    ILogger<DemoSeedService> logger)
{
    public const string DemoPassword = "Demo1234";
    public const string DemoEmailDomain = "demo.altmiustmu.test";
    private const int DemoUserCount = 50;
    private const int SeasonDays = 75;
    private const int HistoryDays = 30;

    private static readonly string[] FirstParts =
    [
        "Pota", "Smaç", "Ribaund", "Üçlük", "Asist", "Blok", "Fastbreak", "Pivot", "Forvet", "Guard",
        "Turnike", "Çember", "Fileci", "Parke", "Mola", "Faul", "Serbest", "Alley", "Kanca", "Pick",
    ];

    private static readonly string[] SecondParts =
    [
        "Kralı", "Ustası", "Canavarı", "Sever", "Bey", "Hoca", "Avcısı", "Tutkunu", "Baba", "Kaptan",
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await seed.SeedAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        var rng = new Random(42);
        var season = await db.Seasons.OrderByDescending(s => s.Id).FirstAsync(ct);

        season.LockAt = now.AddDays(-SeasonDays);
        season.Status = SeasonStatus.Active;
        await db.SaveChangesAsync(ct);

        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);
        var lines = await db.TeamLines.AsNoTracking().Where(l => l.SeasonId == season.Id).ToDictionaryAsync(l => l.TeamId, l => l.Line, ct);

        // Mid-season records: ~41 games, win% around the line with noise.
        var finalStates = new Dictionary<int, (int Wins, int Losses, double Pct)>();
        var records = await db.TeamRecords.Where(r => r.SeasonId == season.Id).ToDictionaryAsync(r => r.TeamId, ct);
        foreach (var team in teams)
        {
            var played = rng.Next(38, 45);
            var pct = Math.Clamp((double)lines[team.Id] / season.GamesPerTeam + (rng.NextDouble() - 0.5) * 0.24, 0.12, 0.88);
            var wins = (int)Math.Round(played * pct);
            finalStates[team.Id] = (wins, played - wins, pct);

            if (!records.TryGetValue(team.Id, out var record))
            {
                record = new TeamRecord { SeasonId = season.Id, TeamId = team.Id };
                db.TeamRecords.Add(record);
            }

            record.Wins = wins;
            record.Losses = played - wins;
            record.Source = RecordSource.Api;
            record.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        if (await db.Users.AnyAsync(u => u.Email!.EndsWith("@" + DemoEmailDomain), ct))
        {
            logger.LogInformation("Demo users already exist; refreshed records only");
            await scores.RecomputeAsync(season.Id, writeSnapshot: true, ct);
            return;
        }

        var playerIds = new List<string>();
        var usedNames = new HashSet<string>();
        for (var i = 1; i <= DemoUserCount; i++)
        {
            string name;
            do
            {
                name = $"{FirstParts[rng.Next(FirstParts.Length)]}{SecondParts[rng.Next(SecondParts.Length)]}{(rng.Next(3) == 0 ? rng.Next(1, 99).ToString(System.Globalization.CultureInfo.InvariantCulture) : "")}";
            }
            while (!usedNames.Add(TextNormalizer.Fold(name)) || name.Length > NameRules.DisplayNameMax
                   || await db.Users.AnyAsync(u => u.DisplayNameKey == TextNormalizer.Fold(name), ct));

            var email = $"demo{i:00}@{DemoEmailDomain}";
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = name,
                DisplayNameKey = TextNormalizer.Fold(name),
                CreatedAt = season.LockAt.AddDays(-rng.Next(5, 40)),
                LockoutEnabled = true,
            };
            var result = await users.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            playerIds.Add(user.Id);
        }

        // Picks: 90% complete, the rest partial. Pundits get picks too if they have none yet.
        var pundits = await db.Users.Where(u => u.IsPundit).Select(u => u.Id).ToListAsync(ct);
        foreach (var userId in playerIds.Concat(pundits))
        {
            if (await db.Picks.AnyAsync(p => p.UserId == userId && p.SeasonId == season.Id, ct))
            {
                continue;
            }

            var count = pundits.Contains(userId) || rng.NextDouble() < 0.9 ? teams.Count : rng.Next(10, teams.Count);
            var start = season.LockAt.AddDays(-rng.Next(2, 20)).AddMinutes(-rng.Next(0, 600));
            var chosen = teams.OrderBy(_ => rng.Next()).Take(count).ToList();
            for (var i = 0; i < chosen.Count; i++)
            {
                var team = chosen[i];
                // Smart-ish pickers: lean toward the side the season actually went.
                var leanOver = finalStates[team.Id].Pct * season.GamesPerTeam > (double)lines[team.Id];
                var side = rng.NextDouble() < 0.62 ? (leanOver ? PickSide.Over : PickSide.Under) : (leanOver ? PickSide.Under : PickSide.Over);
                var at = start.AddSeconds(i * rng.Next(5, 40));
                db.Picks.Add(new Pick { UserId = userId, SeasonId = season.Id, TeamId = team.Id, Side = side, CreatedAt = at, UpdatedAt = at });
            }
        }

        await db.SaveChangesAsync(ct);

        // Groups.
        string[] groupNames = ["Mutfak Ekibi", "Ofis Ligi", "Kadıköy Potası", "Üniversite Arkadaşları"];
        foreach (var groupName in groupNames)
        {
            var members = playerIds.OrderBy(_ => rng.Next()).Take(rng.Next(6, 16)).ToList();
            var group = new Group
            {
                Name = groupName,
                Slug = $"{TextNormalizer.Slugify(groupName)}-{GroupService.RandomCode(4).ToLowerInvariant()}",
                InviteCode = GroupService.RandomCode(8),
                OwnerId = members[0],
                SeasonId = season.Id,
                CreatedAt = season.LockAt.AddDays(-rng.Next(1, 10)),
            };
            foreach (var m in members)
            {
                group.Members.Add(new GroupMember { UserId = m, JoinedAt = group.CreatedAt.AddHours(rng.Next(0, 48)) });
            }

            db.Groups.Add(group);
        }

        await db.SaveChangesAsync(ct);

        await WriteHistoryAsync(season, teams, lines, finalStates, now, ct);
        await scores.RecomputeAsync(season.Id, writeSnapshot: true, ct);
        logger.LogInformation("Demo data created: {Users} players (password '{Password}'), {Groups} groups", playerIds.Count, DemoPassword, groupNames.Length);
    }

    /// <summary>Synthesizes the past 30 days of snapshots by scaling the current records back in time.</summary>
    private async Task WriteHistoryAsync(
        Season season,
        List<Team> teams,
        Dictionary<int, decimal> lines,
        Dictionary<int, (int Wins, int Losses, double Pct)> finalStates,
        DateTime now,
        CancellationToken ct)
    {
        var picks = await db.Picks.AsNoTracking().Where(p => p.SeasonId == season.Id)
            .Select(p => new { p.UserId, p.TeamId, p.Side, p.CreatedAt }).ToListAsync(ct);
        var byUser = picks.GroupBy(p => p.UserId).Where(g => g.Count() >= teams.Count).ToList();
        var today = Istanbul.Today(now);
        var historyRng = new Random(7);

        for (var daysAgo = HistoryDays; daysAgo >= 1; daysAgo--)
        {
            var fraction = (double)(SeasonDays - daysAgo) / SeasonDays;
            var states = teams.ToDictionary(t => t.Id, t =>
            {
                var (w, l, pct) = finalStates[t.Id];
                var played = (int)Math.Round((w + l) * fraction);
                var noisyPct = Math.Clamp(pct + (historyRng.NextDouble() - 0.5) * 0.2 * (daysAgo / (double)HistoryDays), 0.05, 0.95);
                var wins = Math.Min(w, (int)Math.Round(played * noisyPct));
                return new TeamState(lines[t.Id], wins, Math.Max(0, played - wins), season.GamesPerTeam);
            });

            var scored = byUser.Select(g => new
            {
                UserId = g.Key,
                Score = ScoringEngine.Score(g.Select(p => new PickInput(p.TeamId, p.Side)), states),
                CompletedAt = g.Max(p => p.CreatedAt),
            }).ToList();

            var ranks = ScoringEngine.Rank(scored.Select(s => new RankInput(s.UserId, s.Score.ProjectedCorrect, s.Score.ClinchedCorrect, s.CompletedAt, s.UserId)), false)
                .ToDictionary(r => r.UserId, r => r.Rank);
            var date = today.AddDays(-daysAgo);
            foreach (var s in scored)
            {
                db.DailySnapshots.Add(new DailySnapshot
                {
                    SeasonId = season.Id,
                    UserId = s.UserId,
                    Date = date,
                    ProjectedCorrect = s.Score.ProjectedCorrect,
                    ClinchedCorrect = s.Score.ClinchedCorrect,
                    Rank = ranks[s.UserId],
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
