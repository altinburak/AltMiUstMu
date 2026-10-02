namespace AltMiUstMu.Core.Entities;

public class Team
{
    public int Id { get; set; }
    public string Abbreviation { get; set; } = "";
    public string City { get; set; } = "";
    public string Name { get; set; } = "";
    public Conference Conference { get; set; }
    public string Division { get; set; } = "";
    public string PrimaryColor { get; set; } = "#000000";
    public string SecondaryColor { get; set; } = "#FFFFFF";
    public string EspnId { get; set; } = "";

    public string FullName => $"{City} {Name}";
}
