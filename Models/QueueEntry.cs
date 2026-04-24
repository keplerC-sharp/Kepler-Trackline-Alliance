namespace Kepler_Trackline_Alliance.Models;

public class QueueEntry
{
    public uint Id { get; set; }

    public uint SessionId { get; set; }
    public Session Session { get; set; }

    public uint ParticipantId { get; set; }
    public Participant Participant { get; set; }

    public int Position { get; set; }
    public string Priority { get; set; }
    public string Status { get; set; } = "QUEUED";

    public decimal? EstimatedStartS { get; set; }
    public decimal? SessionTimeS { get; set; }

    public DateTime EnteredAt { get; set; } = DateTime.Now;
}