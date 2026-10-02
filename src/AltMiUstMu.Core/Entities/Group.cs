namespace AltMiUstMu.Core.Entities;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public int SeasonId { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<GroupMember> Members { get; set; } = [];
}

public class GroupMember
{
    public int GroupId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime JoinedAt { get; set; }

    public Group? Group { get; set; }
}
