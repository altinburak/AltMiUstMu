using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;

namespace AltMiUstMu.Tests;

public class ScoringEngineTests
{
    private static TeamState State(decimal line, int wins, int losses, int games = 82) => new(line, wins, losses, games);

    [Fact]
    public void Zero_games_played_is_pending_for_both_sides()
    {
        var team = State(47.5m, 0, 0);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().Be(PickStatus.Pending);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().Be(PickStatus.Pending);
        ScoringEngine.ProjectedWins(0, 0, 82).Should().BeNull();
    }

    [Fact]
    public void Over_is_clinched_once_wins_exceed_the_line()
    {
        var team = State(47.5m, 48, 20);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().Be(PickStatus.ClinchedWin);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().Be(PickStatus.ClinchedLoss);
    }

    [Fact]
    public void Wins_equal_to_floor_of_line_is_not_yet_clinched()
    {
        var team = State(47.5m, 47, 20);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().Be(PickStatus.TrendingWin);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().Be(PickStatus.TrendingLoss);
    }

    [Fact]
    public void Under_is_clinched_once_wins_plus_remaining_fall_below_the_line()
    {
        // 30 wins, 46 losses -> 6 remaining, max 36 < 36.5.
        var team = State(36.5m, 30, 46);
        team.MaxPossibleWins.Should().Be(36);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().Be(PickStatus.ClinchedWin);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().Be(PickStatus.ClinchedLoss);
    }

    [Fact]
    public void Under_is_not_clinched_while_max_possible_wins_still_beats_the_line()
    {
        // 30-45 -> 7 remaining, max 37 > 36.5.
        var team = State(36.5m, 30, 45);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().NotBe(PickStatus.ClinchedWin);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().NotBe(PickStatus.ClinchedLoss);
    }

    [Theory]
    [InlineData(30, 20, 47.5, PickSide.Over, PickStatus.TrendingWin)] // pace 49.2
    [InlineData(30, 20, 47.5, PickSide.Under, PickStatus.TrendingLoss)]
    [InlineData(20, 30, 47.5, PickSide.Under, PickStatus.TrendingWin)] // pace 32.8
    [InlineData(20, 30, 47.5, PickSide.Over, PickStatus.TrendingLoss)]
    public void Undecided_picks_follow_the_projection(int wins, int losses, double line, PickSide side, PickStatus expected)
    {
        ScoringEngine.Evaluate(side, State((decimal)line, wins, losses)).Should().Be(expected);
    }

    [Fact]
    public void Projection_exactly_on_a_whole_line_is_pending()
    {
        // 10-10 over 82 games projects to exactly 41.
        var team = State(41m, 10, 10);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().Be(PickStatus.Pending);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().Be(PickStatus.Pending);
    }

    [Fact]
    public void Projected_wins_is_rounded_to_one_decimal()
    {
        ScoringEngine.ProjectedWins(30, 20, 82).Should().Be(49.2m);
        ScoringEngine.ProjectedWins(41, 41, 82).Should().Be(41m);
    }

    [Fact]
    public void Whole_number_line_hit_exactly_at_season_end_is_a_push()
    {
        var team = State(41m, 41, 41);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().Be(PickStatus.Push);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().Be(PickStatus.Push);
    }

    [Fact]
    public void Whole_number_line_needs_strictly_more_wins_to_clinch_over()
    {
        ScoringEngine.Evaluate(PickSide.Over, State(41m, 41, 30)).Should().NotBe(PickStatus.ClinchedWin);
        ScoringEngine.Evaluate(PickSide.Over, State(41m, 42, 30)).Should().Be(PickStatus.ClinchedWin);
    }

    [Fact]
    public void Whole_number_line_where_max_equals_line_is_not_clinched_for_under()
    {
        // 35-41: 6 remaining, max exactly 41 -> either push or under win, never clinched yet.
        var team = State(41m, 35, 41);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().NotBe(PickStatus.ClinchedWin);
        ScoringEngine.Evaluate(PickSide.Over, team).Should().NotBe(PickStatus.ClinchedLoss);
    }

    [Fact]
    public void At_season_end_every_half_point_line_is_clinched()
    {
        ScoringEngine.Evaluate(PickSide.Over, State(47.5m, 47, 35)).Should().Be(PickStatus.ClinchedLoss);
        ScoringEngine.Evaluate(PickSide.Under, State(47.5m, 47, 35)).Should().Be(PickStatus.ClinchedWin);
        ScoringEngine.Evaluate(PickSide.Over, State(47.5m, 48, 34)).Should().Be(PickStatus.ClinchedWin);
    }

    [Fact]
    public void Score_counts_clinched_and_trending_separately()
    {
        var teams = new Dictionary<int, TeamState>
        {
            [1] = State(47.5m, 48, 10), // over clinched
            [2] = State(30.5m, 5, 70),  // under clinched (max 12)
            [3] = State(40.5m, 30, 20), // over trending
            [4] = State(40.5m, 30, 20), // under trending loss
            [5] = State(41m, 41, 41),   // push
            [6] = State(41m, 0, 0),     // pending
        };
        var picks = new[]
        {
            new PickInput(1, PickSide.Over),
            new PickInput(2, PickSide.Over),
            new PickInput(3, PickSide.Over),
            new PickInput(4, PickSide.Under),
            new PickInput(5, PickSide.Over),
            new PickInput(6, PickSide.Under),
        };

        var score = ScoringEngine.Score(picks, teams);

        score.ClinchedCorrect.Should().Be(1);
        score.ClinchedWrong.Should().Be(1);
        score.TrendingCorrect.Should().Be(1);
        score.TrendingWrong.Should().Be(1);
        score.Pushes.Should().Be(1);
        score.Pending.Should().Be(1);
        score.ProjectedCorrect.Should().Be(2);
    }

    [Fact]
    public void Rank_orders_by_projection_then_clinched_then_completion_time()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var entries = new[]
        {
            new RankInput("a", 20, 5, t0.AddHours(2), "A"),
            new RankInput("b", 22, 3, t0.AddHours(5), "B"),
            new RankInput("c", 20, 7, t0.AddHours(9), "C"),
            new RankInput("d", 20, 5, t0.AddHours(1), "D"),
        };

        var ranks = ScoringEngine.Rank(entries, seasonFinished: false);

        ranks.Select(r => r.UserId).Should().Equal("b", "c", "d", "a");
        ranks.Select(r => r.Rank).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Rank_shares_position_when_all_keys_are_equal()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var ranks = ScoringEngine.Rank(
            [
                new RankInput("a", 20, 5, t0, "A"),
                new RankInput("b", 20, 5, t0, "B"),
                new RankInput("c", 19, 5, t0, "C"),
            ],
            seasonFinished: false);

        ranks.Select(r => r.Rank).Should().Equal(1, 1, 3);
    }

    [Fact]
    public void Finished_season_ranks_by_final_correct_picks_not_projection()
    {
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var entries = new[]
        {
            new RankInput("projection-leader", 25, 18, t0, "A"),
            new RankInput("final-leader", 19, 19, t0.AddHours(1), "B"),
        };

        ScoringEngine.Rank(entries, seasonFinished: true)[0].UserId.Should().Be("final-leader");
        ScoringEngine.Rank(entries, seasonFinished: false)[0].UserId.Should().Be("projection-leader");
    }

    [Fact]
    public void Season_is_complete_only_when_every_team_played_all_games()
    {
        var done = Enumerable.Repeat((41, 41), 30).ToList();
        ScoringEngine.IsSeasonComplete(done, 82, 30).Should().BeTrue();

        done[3] = (41, 40);
        ScoringEngine.IsSeasonComplete(done, 82, 30).Should().BeFalse();
        ScoringEngine.IsSeasonComplete(Enumerable.Repeat((41, 41), 29), 82, 30).Should().BeFalse();
    }

    [Fact]
    public void Shortened_seasons_use_games_per_team_for_clinch_math()
    {
        // 66-game season: 30-30 with 6 remaining, max 36 < 36.5.
        var team = new TeamState(36.5m, 30, 30, 66);
        ScoringEngine.Evaluate(PickSide.Under, team).Should().Be(PickStatus.ClinchedWin);
        ScoringEngine.ProjectedWins(30, 30, 66).Should().Be(33m);
    }
}
