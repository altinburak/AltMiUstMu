using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Core.Catalog;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace AltMiUstMu.Tests;

internal sealed class FixedTime(DateTime utc) : TimeProvider
{
    public DateTime Now { get; set; } = utc;

    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
}

internal sealed class FakeLock(bool available = true) : IDistributedLock
{
    public Task<IAsyncDisposable?> TryAcquireAsync(long key, CancellationToken ct) =>
        Task.FromResult<IAsyncDisposable?>(available ? new Handle() : null);

    private sealed class Handle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeProvider(Func<IReadOnlyList<ProviderTeamRecord>> data) : IResultsProvider
{
    public string Name => "Fake";

    public Task<IReadOnlyList<ProviderTeamRecord>> GetStandingsAsync(int seasonEndYear, CancellationToken ct) => Task.FromResult(data());
}

internal sealed class CountingInvalidator : ICacheInvalidator
{
    public int Calls { get; private set; }

    public List<string> Players { get; } = [];

    public Task InvalidateAsync(CancellationToken ct = default)
    {
        Calls++;
        return Task.CompletedTask;
    }

    public Task InvalidatePlayerAsync(string userId, CancellationToken ct = default)
    {
        Players.Add(userId);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory database with 30 teams, a season and lines, plus helpers.</summary>
internal sealed class TestWorld : IDisposable
{
    public static readonly DateTime LockAt = new(2026, 10, 20, 23, 0, 0, DateTimeKind.Utc);

    public TestWorld()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new AppDbContext(options);

        Season = new Season { Label = "2026-27", LockAt = LockAt, Status = SeasonStatus.Upcoming, GamesPerTeam = 82 };
        Db.Seasons.Add(Season);
        Db.SaveChanges();

        var id = 1;
        foreach (var t in TeamCatalog.Teams)
        {
            var team = new Team { Id = id++, Abbreviation = t.Abbreviation, City = t.City, Name = t.Name, Conference = t.Conference, EspnId = t.EspnId };
            Db.Teams.Add(team);
            Db.TeamLines.Add(new TeamLine { SeasonId = Season.Id, TeamId = team.Id, Line = 40.5m });
        }

        Db.SaveChanges();
    }

    public AppDbContext Db { get; }
    public Season Season { get; }
    public FixedTime Time { get; } = new(LockAt.AddDays(-2));
    public CountingInvalidator Cache { get; } = new();

    public AppUser AddUser(string name, bool pundit = false)
    {
        var user = new AppUser { Id = Guid.NewGuid().ToString(), UserName = name, Email = $"{name}@x.test", DisplayName = name, DisplayNameKey = name.ToUpperInvariant(), IsPundit = pundit };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    public ScoreService Scores() => new(Db, Time);

    public PickService Picks() => new(Db, Time, new AuditService(Db, Time), Scores(), Cache);

    public SyncService Sync(IResultsProvider provider, bool lockAvailable = true) =>
        new(Db, provider, new FakeLock(lockAvailable), Scores(), Cache, Time, NullLogger<SyncService>.Instance);

    public List<ProviderTeamRecord> Standings(int wins, int losses) =>
        TeamCatalog.Teams.Select(t => new ProviderTeamRecord(t.EspnId, t.Abbreviation, wins, losses)).ToList();

    public void Dispose() => Db.Dispose();
}

public class PickLockTests
{
    [Fact]
    public async Task User_can_create_and_change_a_pick_before_lock()
    {
        using var w = new TestWorld();
        var user = w.AddUser("ali");
        var service = w.Picks();

        (await service.SetPickAsync(user.Id, 1, PickSide.Over)).Should().Be(PickResult.Saved);
        (await service.SetPickAsync(user.Id, 1, PickSide.Over)).Should().Be(PickResult.Unchanged);
        (await service.SetPickAsync(user.Id, 1, PickSide.Under)).Should().Be(PickResult.Saved);

        w.Db.Picks.Should().ContainSingle(p => p.UserId == user.Id && p.TeamId == 1 && p.Side == PickSide.Under);
    }

    [Fact]
    public async Task Sharing_makes_picks_public_and_later_changes_refresh_the_profile()
    {
        using var w = new TestWorld();
        var user = w.AddUser("zeynep");
        var other = w.AddUser("can");
        var service = w.Picks();

        await service.SetPickAsync(user.Id, 1, PickSide.Over);
        w.Cache.Players.Should().BeEmpty("unshared picks are private, nothing public to refresh");

        (await service.MarkPicksSharedAsync(user.Id)).Should().BeTrue();
        w.Db.Users.Single(u => u.Id == user.Id).PicksSharedSeasonId.Should().Be(w.Season.Id);
        w.Cache.Players.Should().Equal(user.Id);

        await service.SetPickAsync(user.Id, 1, PickSide.Under);
        await service.SetPickAsync(other.Id, 1, PickSide.Under);
        w.Cache.Players.Should().Equal(user.Id, user.Id);
    }

    [Fact]
    public async Task Pundits_cannot_be_marked_as_shared()
    {
        using var w = new TestWorld();
        var pundit = w.AddUser("kaan", pundit: true);

        (await w.Picks().MarkPicksSharedAsync(pundit.Id)).Should().BeFalse();
        (await w.Picks().MarkPicksSharedAsync("missing")).Should().BeFalse();
    }

    [Fact]
    public async Task Picks_are_rejected_server_side_after_lock()
    {
        using var w = new TestWorld();
        var user = w.AddUser("veli");
        var service = w.Picks();
        await service.SetPickAsync(user.Id, 2, PickSide.Over);

        w.Time.Now = TestWorld.LockAt;
        (await service.SetPickAsync(user.Id, 2, PickSide.Under)).Should().Be(PickResult.Locked);
        (await service.SetPickAsync(user.Id, 3, PickSide.Under)).Should().Be(PickResult.Locked);

        w.Db.ChangeTracker.Clear();
        w.Db.Picks.Where(p => p.UserId == user.Id).Should().ContainSingle().Which.Side.Should().Be(PickSide.Over);
    }

    [Fact]
    public async Task Active_season_is_locked_even_before_lock_time()
    {
        using var w = new TestWorld();
        var user = w.AddUser("ayse");
        w.Season.Status = SeasonStatus.Active;
        await w.Db.SaveChangesAsync();

        (await w.Picks().SetPickAsync(user.Id, 1, PickSide.Over)).Should().Be(PickResult.Locked);
    }

    [Fact]
    public async Task Pundits_cannot_pick_through_the_user_flow_and_unknown_teams_are_rejected()
    {
        using var w = new TestWorld();
        var pundit = w.AddUser("kaan", pundit: true);
        var user = w.AddUser("fatma");

        (await w.Picks().SetPickAsync(pundit.Id, 1, PickSide.Over)).Should().Be(PickResult.NotAllowed);
        (await w.Picks().SetPickAsync(user.Id, 999, PickSide.Over)).Should().Be(PickResult.InvalidTeam);
    }

    [Fact]
    public async Task Admin_can_edit_pundit_picks_after_lock_and_every_change_is_audited()
    {
        using var w = new TestWorld();
        var pundit = w.AddUser("inan", pundit: true);
        w.Time.Now = TestWorld.LockAt.AddDays(10);
        var actor = new AuditActor("admin-id", "Admin");

        var changes = await w.Picks().SetPunditPicksAsync(actor, pundit.Id, new Dictionary<int, PickSide?> { [1] = PickSide.Over, [2] = PickSide.Under });
        changes.Should().Be(2);

        changes = await w.Picks().SetPunditPicksAsync(actor, pundit.Id, new Dictionary<int, PickSide?> { [1] = PickSide.Under, [2] = PickSide.Under, [3] = null });
        changes.Should().Be(1);

        w.Db.AdminAuditLogs.Should().HaveCount(3);
        w.Db.AdminAuditLogs.Should().Contain(a => a.OldValue == "ÜST" && a.NewValue == "ALT");
        w.Cache.Calls.Should().Be(2);
    }
}

public class SyncServiceTests
{
    [Fact]
    public async Task Invalid_provider_data_writes_nothing_and_logs_a_failed_run()
    {
        using var w = new TestWorld();
        var standings = w.Standings(5, 5);
        standings.RemoveAt(0);

        var outcome = await w.Sync(new FakeProvider(() => standings)).RunAsync(SyncTrigger.Cli);

        outcome.Success.Should().BeFalse();
        w.Db.ChangeTracker.Clear();
        w.Db.TeamRecords.Should().BeEmpty();
        w.Db.SyncRuns.Should().ContainSingle().Which.Status.Should().Be(SyncRunStatus.Failed);
        w.Cache.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Successful_sync_updates_records_scores_and_snapshots_and_activates_the_season()
    {
        using var w = new TestWorld();
        var user = w.AddUser("mehmet");
        foreach (var team in w.Db.Teams.ToList())
        {
            await w.Picks().SetPickAsync(user.Id, team.Id, PickSide.Over);
        }

        w.Time.Now = TestWorld.LockAt.AddDays(30);
        var outcome = await w.Sync(new FakeProvider(() => w.Standings(10, 4))).RunAsync(SyncTrigger.Cron);

        outcome.Success.Should().BeTrue(outcome.Message);
        w.Db.ChangeTracker.Clear();
        w.Db.TeamRecords.Should().HaveCount(30).And.OnlyContain(r => r.Wins == 10 && r.Losses == 4);
        w.Db.Seasons.Single().Status.Should().Be(SeasonStatus.Active);

        var score = w.Db.UserScores.Single();
        score.IsComplete.Should().BeTrue();
        score.Rank.Should().Be(1);
        score.ProjectedCorrect.Should().Be(30); // 10-4 pace = 58.6 > 40.5
        score.ClinchedCorrect.Should().Be(0);
        w.Db.DailySnapshots.Should().ContainSingle();
        w.Cache.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Manual_records_are_not_overwritten()
    {
        using var w = new TestWorld();
        w.Db.TeamRecords.Add(new TeamRecord { SeasonId = w.Season.Id, TeamId = 1, Wins = 50, Losses = 1, Source = RecordSource.Manual });
        await w.Db.SaveChangesAsync();

        var outcome = await w.Sync(new FakeProvider(() => w.Standings(3, 3))).RunAsync(SyncTrigger.Admin);

        outcome.Success.Should().BeTrue(outcome.Message);
        w.Db.ChangeTracker.Clear();
        w.Db.TeamRecords.Single(r => r.TeamId == 1).Wins.Should().Be(50);
        w.Db.TeamRecords.Count(r => r.Wins == 3).Should().Be(29);
    }

    [Fact]
    public async Task Season_is_finished_when_every_team_has_played_all_games()
    {
        using var w = new TestWorld();
        w.Time.Now = TestWorld.LockAt.AddDays(200);

        var outcome = await w.Sync(new FakeProvider(() => w.Standings(41, 41))).RunAsync(SyncTrigger.Cli);

        outcome.Success.Should().BeTrue(outcome.Message);
        w.Db.ChangeTracker.Clear();
        w.Db.Seasons.Single().Status.Should().Be(SeasonStatus.Finished);
    }

    [Fact]
    public async Task Concurrent_run_is_skipped_when_lock_is_held()
    {
        using var w = new TestWorld();
        var outcome = await w.Sync(new FakeProvider(() => w.Standings(1, 1)), lockAvailable: false).RunAsync(SyncTrigger.Cli);

        outcome.Success.Should().BeFalse();
        w.Db.SyncRuns.Should().ContainSingle().Which.Status.Should().Be(SyncRunStatus.Skipped);
        w.Db.TeamRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task Provider_exceptions_fail_the_run_without_throwing()
    {
        using var w = new TestWorld();
        var outcome = await w.Sync(new FakeProvider(() => throw new HttpRequestException("boom"))).RunAsync(SyncTrigger.Cli);

        outcome.Success.Should().BeFalse();
        outcome.Message.Should().Contain("boom");
    }
}
