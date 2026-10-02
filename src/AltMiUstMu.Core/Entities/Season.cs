namespace AltMiUstMu.Core.Entities;

public class Season
{
    public int Id { get; set; }

    /// <summary>Human readable label, e.g. "2026-27".</summary>
    public string Label { get; set; } = "";

    /// <summary>Moment (UTC) after which picks are frozen.</summary>
    public DateTime LockAt { get; set; }

    public SeasonStatus Status { get; set; } = SeasonStatus.Upcoming;

    public int GamesPerTeam { get; set; } = 82;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// ESPN (and most US sources) identify a season by the year it ends in: "2026-27" -> 2027.
    /// </summary>
    public int EndYear
    {
        get
        {
            var dash = Label.IndexOf('-');
            if (dash > 0 && int.TryParse(Label[..dash], out var start))
            {
                return start + 1;
            }

            return LockAt.Month >= 7 ? LockAt.Year + 1 : LockAt.Year;
        }
    }
}
