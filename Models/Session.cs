namespace Kepler_Trackline_Alliance.Models;

public class Session
{
    public uint Id { get; set; }
    public uint OperatorId { get; set; }
    public Operator Operator { get; set; }

    public string SessionCode { get; set; }
    public string Status { get; set; } = "STANDBY";

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}