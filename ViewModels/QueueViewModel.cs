using Kepler_Trackline_Alliance.Models;

namespace Kepler_Trackline_Alliance.ViewModels;

public class QueueViewModel
{
    public List<QueueEntry> Entries  { get; set; } = new();
    public uint             SessionId { get; set; }
    public string           SessionCode { get; set; } = "";
}
