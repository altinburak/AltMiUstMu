using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Sync;

namespace AltMiUstMu.Infrastructure.Results;

/// <summary>
/// Maps provider records to our teams: first by ESPN id (stable), then by abbreviation using an explicit
/// alias table because ESPN uses its own short codes for six teams.
/// </summary>
public static class TeamMapper
{
    public static readonly IReadOnlyDictionary<string, string> EspnAbbreviationAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NY"] = "NYK",
        ["SA"] = "SAS",
        ["GS"] = "GSW",
        ["NO"] = "NOP",
        ["UTAH"] = "UTA",
        ["WSH"] = "WAS",
    };

    public static List<IncomingRecord> Map(IEnumerable<ProviderTeamRecord> records, IReadOnlyCollection<Team> teams)
    {
        var byEspnId = teams.Where(t => t.EspnId.Length > 0).ToDictionary(t => t.EspnId, StringComparer.Ordinal);
        var byAbbr = teams.ToDictionary(t => t.Abbreviation, StringComparer.OrdinalIgnoreCase);

        var result = new List<IncomingRecord>();
        foreach (var r in records)
        {
            Team? team = null;
            if (!byEspnId.TryGetValue(r.ExternalId, out team))
            {
                var abbr = EspnAbbreviationAliases.TryGetValue(r.Abbreviation, out var alias) ? alias : r.Abbreviation;
                byAbbr.TryGetValue(abbr, out team);
            }

            result.Add(new IncomingRecord(team?.Id, r.ExternalId, team?.Abbreviation ?? r.Abbreviation, r.Wins, r.Losses));
        }

        return result;
    }
}
