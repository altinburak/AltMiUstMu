namespace AltMiUstMu.Core.Entities;

public class TeamRecord
{
    public int SeasonId { get; set; }
    public int TeamId { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public DateTime UpdatedAt { get; set; }
    public RecordSource Source { get; set; } = RecordSource.Api;

    public int GamesPlayed => Wins + Losses;

    public Team? Team { get; set; }
}
