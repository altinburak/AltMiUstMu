using AltMiUstMu.Core.Entities;

namespace AltMiUstMu.Core.Catalog;

public sealed record TeamSeed(
    string Abbreviation,
    string City,
    string Name,
    Conference Conference,
    string Division,
    string PrimaryColor,
    string SecondaryColor,
    string EspnId,
    decimal PlaceholderLine);

/// <summary>
/// The 30 NBA teams. ESPN ids were verified against the live standings endpoint.
/// Placeholder lines are rough guesses so the app works out of the box; the admin must enter the real ones.
/// </summary>
public static class TeamCatalog
{
    public static readonly IReadOnlyList<TeamSeed> Teams =
    [
        // East - Atlantic
        new("BOS", "Boston", "Celtics", Conference.East, "Atlantik", "#007A33", "#BA9653", "2", 50.5m),
        new("BKN", "Brooklyn", "Nets", Conference.East, "Atlantik", "#000000", "#FFFFFF", "17", 23.5m),
        new("NYK", "New York", "Knicks", Conference.East, "Atlantik", "#006BB6", "#F58426", "18", 52.5m),
        new("PHI", "Philadelphia", "76ers", Conference.East, "Atlantik", "#006BB6", "#ED174C", "20", 43.5m),
        new("TOR", "Toronto", "Raptors", Conference.East, "Atlantik", "#CE1141", "#000000", "28", 44.5m),

        // East - Central
        new("CHI", "Chicago", "Bulls", Conference.East, "Merkez", "#CE1141", "#000000", "4", 33.5m),
        new("CLE", "Cleveland", "Cavaliers", Conference.East, "Merkez", "#860038", "#FDBB30", "5", 51.5m),
        new("DET", "Detroit", "Pistons", Conference.East, "Merkez", "#C8102E", "#1D42BA", "8", 53.5m),
        new("IND", "Indiana", "Pacers", Conference.East, "Merkez", "#002D62", "#FDBB30", "11", 31.5m),
        new("MIL", "Milwaukee", "Bucks", Conference.East, "Merkez", "#00471B", "#EEE1C6", "15", 35.5m),

        // East - Southeast
        new("ATL", "Atlanta", "Hawks", Conference.East, "Güneydoğu", "#E03A3E", "#C1D32F", "1", 46.5m),
        new("CHA", "Charlotte", "Hornets", Conference.East, "Güneydoğu", "#1D1160", "#00788C", "30", 41.5m),
        new("MIA", "Miami", "Heat", Conference.East, "Güneydoğu", "#98002E", "#F9A01B", "14", 41.5m),
        new("ORL", "Orlando", "Magic", Conference.East, "Güneydoğu", "#0077C0", "#C4CED4", "19", 47.5m),
        new("WAS", "Washington", "Wizards", Conference.East, "Güneydoğu", "#002B5C", "#E31837", "27", 21.5m),

        // West - Northwest
        new("DEN", "Denver", "Nuggets", Conference.West, "Kuzeybatı", "#0E2240", "#FEC524", "7", 53.5m),
        new("MIN", "Minnesota", "Timberwolves", Conference.West, "Kuzeybatı", "#0C2340", "#78BE20", "16", 47.5m),
        new("OKC", "Oklahoma City", "Thunder", Conference.West, "Kuzeybatı", "#007AC1", "#EF3B24", "25", 61.5m),
        new("POR", "Portland", "Trail Blazers", Conference.West, "Kuzeybatı", "#E03A3E", "#000000", "22", 41.5m),
        new("UTA", "Utah", "Jazz", Conference.West, "Kuzeybatı", "#002B5C", "#F9A01B", "26", 24.5m),

        // West - Pacific
        new("GSW", "Golden State", "Warriors", Conference.West, "Pasifik", "#1D428A", "#FFC72C", "9", 40.5m),
        new("LAC", "LA", "Clippers", Conference.West, "Pasifik", "#C8102E", "#1D428A", "12", 41.5m),
        new("LAL", "Los Angeles", "Lakers", Conference.West, "Pasifik", "#552583", "#FDB927", "13", 48.5m),
        new("PHX", "Phoenix", "Suns", Conference.West, "Pasifik", "#1D1160", "#E56020", "21", 42.5m),
        new("SAC", "Sacramento", "Kings", Conference.West, "Pasifik", "#5A2D81", "#63727A", "23", 27.5m),

        // West - Southwest
        new("DAL", "Dallas", "Mavericks", Conference.West, "Güneybatı", "#00538C", "#B8C4CA", "6", 33.5m),
        new("HOU", "Houston", "Rockets", Conference.West, "Güneybatı", "#CE1141", "#000000", "10", 51.5m),
        new("MEM", "Memphis", "Grizzlies", Conference.West, "Güneybatı", "#5D76A9", "#12173F", "29", 30.5m),
        new("NOP", "New Orleans", "Pelicans", Conference.West, "Güneybatı", "#0C2340", "#C8102E", "3", 29.5m),
        new("SAS", "San Antonio", "Spurs", Conference.West, "Güneybatı", "#C4CED4", "#000000", "24", 57.5m),
    ];
}
