namespace Kepler_Trackline_Alliance.Models;

public class Operator
{
    public uint     Id           { get; set; }
    public string   Identifier   { get; set; } = "";
    public string   FullName     { get; set; } = "";
    public string   PasswordHash { get; set; } = "";
    public string   Role         { get; set; } = "OPERATOR";
    public DateTime CreatedAt    { get; set; } = DateTime.Now;
}
