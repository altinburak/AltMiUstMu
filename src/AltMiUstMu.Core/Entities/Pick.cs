namespace AltMiUstMu.Core.Entities;

public class Pick
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public int SeasonId { get; set; }
    public int TeamId { get; set; }
    public PickSide Side { get; set; }

    /// <summary>When the pick for this team was first made. The max over all 30 picks is the "completed" time.</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Team? Team { get; set; }
}
