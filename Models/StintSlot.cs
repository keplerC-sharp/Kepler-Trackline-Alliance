namespace Kepler_Trackline_Alliance.Models;

public class StintSlot
{
    public uint Id { get; set; }

    public uint SessionId { get; set; }
    public uint? QueueEntryId { get; set; }

    public int SlotOrder { get; set; }
    public string SlotStatus { get; set; }
}