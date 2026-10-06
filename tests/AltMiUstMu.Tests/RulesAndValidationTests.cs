using System.Text.Json;
using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Core.Catalog;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Sync;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Results;

namespace AltMiUstMu.Tests;

public class PickRulesTests
{
    private static readonly DateTime LockAt = new(2026, 10, 20, 23, 0, 0, DateTimeKind.Utc);

    private static Season Upcoming() => new() { Id = 1, Label = "2026-27", LockAt = LockAt, Status = SeasonStatus.Upcoming };

    [Fact]
    public void Picks_are_open_before_lock_and_closed_at_and_after_lock()
    {
        PickRules.IsLocked(Upcoming(), LockAt.AddSeconds(-1)).Should().BeFalse();
        PickRules.IsLocked(Upcoming(), LockAt).Should().BeTrue();
        PickRules.IsLocked(Upcoming(), LockAt.AddDays(3)).Should().BeTrue();
    }

    [Fact]
    public void Active_season_is_locked_even_if_lock_time_was_moved_forward()
    {
        var season = Upcoming();
        season.Status = SeasonStatus.Active;
        PickRules.IsLocked(season, LockAt.AddDays(-5)).Should().BeTrue();
    }

    [Fact]
    public void Other_users_picks_are_hidden_before_lock_but_pundits_are_always_visible()
    {
        var before = LockAt.AddHours(-1);
        PickRules.CanViewPicks(Upcoming(), before, "owner", ownerIsPundit: false, viewerId: "someone").Should().BeFalse();
        PickRules.CanViewPicks(Upcoming(), before, "owner", ownerIsPundit: false, viewerId: null).Should().BeFalse();
        PickRules.CanViewPicks(Upcoming(), before, "owner", ownerIsPundit: false, viewerId: "owner").Should().BeTrue();
        PickRules.CanViewPicks(Upcoming(), before, "kaan", ownerIsPundit: true, viewerId: null).Should().BeTrue();
        PickRules.CanViewPicks(Upcoming(), LockAt, "owner", ownerIsPundit: false, viewerId: null).Should().BeTrue();
    }

    [Fact]
    public void Shared_picks_are_visible_to_everyone_before_lock()
    {
        var before = LockAt.AddHours(-1);
        PickRules.CanViewPicks(Upcoming(), before, "owner", ownerIsPundit: false, viewerId: null, ownerShared: true).Should().BeTrue();
        PickRules.CanViewPicks(Upcoming(), before, "owner", ownerIsPundit: false, viewerId: "someone", ownerShared: true).Should().BeTrue();
    }

    [Fact]
    public void Expected_status_moves_forward_with_clock_and_records()
    {
        PickRules.ExpectedStatus(Upcoming(), LockAt.AddMinutes(-1), false).Should().Be(SeasonStatus.Upcoming);
        PickRules.ExpectedStatus(Upcoming(), LockAt.AddMinutes(1), false).Should().Be(SeasonStatus.Active);
        PickRules.ExpectedStatus(Upcoming(), LockAt.AddDays(180), true).Should().Be(SeasonStatus.Finished);

        var finished = Upcoming();
        finished.Status = SeasonStatus.Finished;
        PickRules.ExpectedStatus(finished, LockAt.AddDays(1), false).Should().Be(SeasonStatus.Finished);
    }

    [Fact]
    public void Season_end_year_comes_from_label()
    {
        Upcoming().EndYear.Should().Be(2027);
    }
}

public class SyncValidatorTests
{
    private static (List<IncomingRecord> Incoming, Dictionary<int, ExistingRecord> Existing) Valid(int games = 10)
    {
        var incoming = Enumerable.Range(1, 30).Select(i => new IncomingRecord(i, i.ToString(System.Globalization.CultureInfo.InvariantCulture), $"T{i}", games / 2, games - games / 2)).ToList();
        var existing = Enumerable.Range(1, 30).ToDictionary(i => i, _ => new ExistingRecord(4, 4, false));
        return (incoming, existing);
    }

    [Fact]
    public void Accepts_thirty_sane_teams()
    {
        var (incoming, existing) = Valid();
        SyncValidator.Validate(incoming, existing, 82, 30).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_missing_team()
    {
        var (incoming, existing) = Valid();
        incoming.RemoveAt(5);
        var result = SyncValidator.Validate(incoming, existing, 82, 30);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Expected 30"));
    }

    [Fact]
    public void Rejects_unknown_and_duplicate_teams()
    {
        var (incoming, existing) = Valid();
        incoming[0] = incoming[0] with { TeamId = null, Abbreviation = "XXX" };
        incoming[1] = incoming[1] with { TeamId = 3 };
        var result = SyncValidator.Validate(incoming, existing, 82, 30);
        result.Errors.Should().Contain(e => e.Contains("Unknown team"));
        result.Errors.Should().Contain(e => e.Contains("appears 2 times"));
    }

    [Fact]
    public void Rejects_more_games_than_the_schedule()
    {
        var (incoming, existing) = Valid();
        incoming[7] = incoming[7] with { Wins = 50, Losses = 33 };
        SyncValidator.Validate(incoming, existing, 82, 30).Errors.Should().ContainSingle(e => e.Contains("exceeds 82"));
    }

    [Fact]
    public void Rejects_negative_numbers()
    {
        var (incoming, existing) = Valid();
        incoming[2] = incoming[2] with { Wins = -1 };
        SyncValidator.Validate(incoming, existing, 82, 30).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_games_played_going_down_unless_record_is_manual()
    {
        var (incoming, existing) = Valid(games: 6);
        SyncValidator.Validate(incoming, existing, 82, 30).Errors.Should().HaveCount(30);

        foreach (var key in existing.Keys.ToList())
        {
            existing[key] = existing[key] with { IsManual = true };
        }

        SyncValidator.Validate(incoming, existing, 82, 30).IsValid.Should().BeTrue();
    }
}

public class EspnMappingTests
{
    private const string SampleJson = """
    {
      "children": [
        { "abbreviation": "East", "standings": { "season": 2027, "seasonType": 2, "entries": [
          { "team": { "id": "18", "abbreviation": "NY" }, "stats": [ { "name": "wins", "value": 10.0 }, { "name": "losses", "value": 4.0 }, { "type": "total", "displayValue": "10-4" } ] },
          { "team": { "id": "27", "abbreviation": "WSH" }, "stats": [ { "name": "losses", "value": 12.0 }, { "name": "wins", "value": 2.0 } ] }
        ] } },
        { "abbreviation": "West", "standings": { "entries": [
          { "team": { "id": "999", "abbreviation": "UTAH" }, "stats": [ { "name": "wins", "value": 5.0 }, { "name": "losses", "value": 9.0 } ] }
        ] } }
      ]
    }
    """;

    [Fact]
    public void Parses_espn_standings_shape()
    {
        using var doc = JsonDocument.Parse(SampleJson);
        var records = EspnResultsProvider.Parse(doc.RootElement);
        records.Should().HaveCount(3);
        records[0].Should().Be(new ProviderTeamRecord("18", "NY", 10, 4));
        records[1].Should().Be(new ProviderTeamRecord("27", "WSH", 2, 12));
    }

    [Fact]
    public void Throws_on_unexpected_shape()
    {
        using var doc = JsonDocument.Parse("""{ "foo": [] }""");
        var act = () => EspnResultsProvider.Parse(doc.RootElement);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Maps_by_espn_id_then_by_alias_abbreviation()
    {
        var teams = TeamCatalog.Teams.Select((t, i) => new Team { Id = i + 1, Abbreviation = t.Abbreviation, EspnId = t.EspnId }).ToList();
        var mapped = TeamMapper.Map(
            [
                new ProviderTeamRecord("18", "NY", 1, 0),
                new ProviderTeamRecord("999", "UTAH", 1, 0),
                new ProviderTeamRecord("998", "ZZZ", 1, 0),
            ],
            teams);

        mapped[0].Abbreviation.Should().Be("NYK");
        mapped[1].Abbreviation.Should().Be("UTA");
        mapped[1].TeamId.Should().NotBeNull();
        mapped[2].TeamId.Should().BeNull();
    }

    [Fact]
    public void Catalog_has_thirty_unique_teams_split_evenly()
    {
        TeamCatalog.Teams.Should().HaveCount(30);
        TeamCatalog.Teams.Select(t => t.Abbreviation).Should().OnlyHaveUniqueItems();
        TeamCatalog.Teams.Select(t => t.EspnId).Should().OnlyHaveUniqueItems();
        TeamCatalog.Teams.Count(t => t.Conference == Conference.East).Should().Be(15);
    }
}

public class TextAndConfigTests
{
    [Theory]
    [InlineData("İnan Özdemir", "INAN OZDEMIR")]
    [InlineData("inan  ozdemir", "INAN OZDEMIR")]
    [InlineData("Şükrü Ağaç", "SUKRU AGAC")]
    [InlineData("ılık", "ILIK")]
    public void Fold_is_culture_independent(string input, string expected)
    {
        TextNormalizer.Fold(input).Should().Be(expected);
    }

    [Fact]
    public void Slugify_produces_ascii()
    {
        TextNormalizer.Slugify("Kadıköy Potası!").Should().Be("kadikoy-potasi");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstu")]
    [InlineData("kötü<script>")]
    [InlineData("Kaan Kural")]
    [InlineData("admin")]
    [InlineData("f*ck")]
    [InlineData("fuckface")]
    [InlineData("sikt1r")]
    [InlineData("1234")]
    public void Display_name_rules_reject_bad_names(string name)
    {
        // "f*ck" fails the character rule; the rest are length, reserved or profanity violations.
        NameRules.ValidateDisplayName(name).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("Göktuğ")]
    [InlineData("PotaKralı 23")]
    [InlineData("çağrı_k")]
    public void Display_name_rules_accept_normal_names(string name)
    {
        NameRules.ValidateDisplayName(name).Should().BeEmpty();
    }

    [Fact]
    public void Railway_database_url_is_converted()
    {
        var cs = ConnectionStringResolver.Normalize("postgres://user:p%40ss@db.railway.internal:6543/railway");
        cs.Should().Contain("Host=db.railway.internal").And.Contain("Port=6543").And.Contain("Database=railway")
            .And.Contain("Username=user").And.Contain("Password=p@ss").And.Contain("SSL Mode=Prefer");
    }

    [Fact]
    public void Plain_connection_strings_pass_through()
    {
        ConnectionStringResolver.Normalize("Host=localhost;Database=x").Should().Be("Host=localhost;Database=x");
    }
}
