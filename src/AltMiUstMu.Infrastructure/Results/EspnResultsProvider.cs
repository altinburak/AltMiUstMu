using System.Globalization;
using System.Text.Json;
using AltMiUstMu.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace AltMiUstMu.Infrastructure.Results;

/// <summary>
/// ESPN public standings endpoint. Verified response shape (2026):
/// <code>
/// { "children": [ { "abbreviation": "East", "standings": { "season": 2027, "seasonType": 2,
///     "entries": [ { "team": { "id": "1", "abbreviation": "ATL", ... },
///                    "stats": [ { "name": "wins", "value": 46.0 }, { "name": "losses", "value": 36.0 }, ... ] } ] } } ] }
/// </code>
/// Without query parameters ESPN returns the *preseason* table, so season and seasontype=2 (regular season)
/// are always passed explicitly.
/// </summary>
public sealed class EspnResultsProvider(HttpClient http, ILogger<EspnResultsProvider> logger) : IResultsProvider
{
    public const string BaseUrl = "https://site.api.espn.com/apis/v2/sports/basketball/nba/";

    public string Name => "ESPN";

    public async Task<IReadOnlyList<ProviderTeamRecord>> GetStandingsAsync(int seasonEndYear, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"standings?season={seasonEndYear}&seasontype=2");
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var records = Parse(doc.RootElement);
        logger.LogInformation("ESPN returned {Count} team records for season {Season}", records.Count, seasonEndYear);
        return records;
    }

    public static IReadOnlyList<ProviderTeamRecord> Parse(JsonElement root)
    {
        if (!root.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("ESPN response has no 'children' array.");
        }

        var result = new List<ProviderTeamRecord>();
        foreach (var conference in children.EnumerateArray())
        {
            if (!conference.TryGetProperty("standings", out var standings)
                || !standings.TryGetProperty("entries", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("ESPN conference node has no standings.entries array.");
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var team = entry.GetProperty("team");
                var id = team.GetProperty("id").GetString() ?? "";
                var abbr = team.TryGetProperty("abbreviation", out var a) ? a.GetString() ?? "" : "";
                int? wins = null, losses = null;

                foreach (var stat in entry.GetProperty("stats").EnumerateArray())
                {
                    if (!stat.TryGetProperty("name", out var nameEl) || !stat.TryGetProperty("value", out var valueEl)
                        || valueEl.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }

                    switch (nameEl.GetString())
                    {
                        case "wins": wins = (int)Math.Round(valueEl.GetDouble()); break;
                        case "losses": losses = (int)Math.Round(valueEl.GetDouble()); break;
                    }
                }

                if (wins is null || losses is null)
                {
                    throw new InvalidDataException($"ESPN entry for team {abbr} ({id}) lacks wins/losses.");
                }

                result.Add(new ProviderTeamRecord(id, abbr, wins.Value, losses.Value));
            }
        }

        return result;
    }
}
