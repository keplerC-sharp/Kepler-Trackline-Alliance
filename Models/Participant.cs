namespace Kepler_Trackline_Alliance.Models;

public class Participant
{
    public uint   Id           { get; set; }
    public string FullName     { get; set; } = "";
    public string Document     { get; set; } = ""; // Cédula/ID
    public string GridId       { get; set; } = ""; // Código único de pista
    public string Category     { get; set; } = "Casual"; // GT3, F1, Pista, Casual
    public int    Age          { get; set; }
    public string Grade        { get; set; } = "B"; // S, A, B
    public int    SeasonPoints { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.Now;
}
