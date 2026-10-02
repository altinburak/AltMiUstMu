namespace AltMiUstMu.Core.Entities;

public class TeamLine
{
    public int SeasonId { get; set; }
    public int TeamId { get; set; }
    public decimal Line { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Team? Team { get; set; }
}
