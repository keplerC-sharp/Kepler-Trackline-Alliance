namespace Kepler_Trackline_Alliance.Models;

public class Participant
{
    public uint   Id           { get; set; }
    public string FullName     { get; set; } = "";
    public string GridId       { get; set; } = "";
    public string Grade        { get; set; } = "B";
    public int    SeasonPoints { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.Now;
}
