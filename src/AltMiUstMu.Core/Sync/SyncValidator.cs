namespace AltMiUstMu.Core.Sync;

/// <summary>A team record as reported by a results provider, already mapped (or not) to our team id.</summary>
public sealed record IncomingRecord(int? TeamId, string ExternalId, string Abbreviation, int Wins, int Losses);

public sealed record ExistingRecord(int Wins, int Losses, bool IsManual);

public sealed record SyncValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Sanity checks for provider data. If anything looks off we write nothing at all: a missed day is
/// harmless, a corrupt leaderboard is not.
/// </summary>
public static class SyncValidator
{
    public static SyncValidationResult Validate(
        IReadOnlyList<IncomingRecord> incoming,
        IReadOnlyDictionary<int, ExistingRecord> existing,
        int gamesPerTeam,
        int expectedTeams)
    {
        var errors = new List<string>();

        if (incoming.Count != expectedTeams)
        {
            errors.Add($"Expected {expectedTeams} teams but provider returned {incoming.Count}.");
        }

        foreach (var r in incoming.Where(r => r.TeamId is null))
        {
            errors.Add($"Unknown team from provider: id={r.ExternalId} abbr={r.Abbreviation}.");
        }

        foreach (var dup in incoming.Where(r => r.TeamId is not null).GroupBy(r => r.TeamId).Where(g => g.Count() > 1))
        {
            errors.Add($"Team {dup.First().Abbreviation} appears {dup.Count()} times.");
        }

        var mappedIds = incoming.Where(r => r.TeamId is not null).Select(r => r.TeamId!.Value).ToHashSet();
        foreach (var missing in existing.Keys.Where(id => !mappedIds.Contains(id)))
        {
            errors.Add($"Team id {missing} missing from provider response.");
        }

        foreach (var r in incoming)
        {
            if (r.Wins < 0 || r.Losses < 0)
            {
                errors.Add($"{r.Abbreviation}: negative wins/losses ({r.Wins}-{r.Losses}).");
            }
            else if (r.Wins + r.Losses > gamesPerTeam)
            {
                errors.Add($"{r.Abbreviation}: {r.Wins + r.Losses} games exceeds {gamesPerTeam}.");
            }

            if (r.TeamId is { } id && existing.TryGetValue(id, out var old) && !old.IsManual
                && r.Wins + r.Losses < old.Wins + old.Losses)
            {
                errors.Add($"{r.Abbreviation}: games played decreased from {old.Wins + old.Losses} to {r.Wins + r.Losses}.");
            }
        }

        return new SyncValidationResult(errors);
    }
}
