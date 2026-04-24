namespace Kepler_Trackline_Alliance.Models;

public class SessionLog
{
    public uint Id { get; set; }
    public uint SessionId { get; set; }
    public uint? OperatorId { get; set; }

    public string ActionType { get; set; }
    public string Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}