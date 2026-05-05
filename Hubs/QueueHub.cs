using Microsoft.AspNetCore.SignalR;

namespace Kepler_Trackline_Alliance.Hubs;

/// <summary>
/// Real-time communication hub for track synchronization.
/// Organizes clients into session-specific groups to optimize broadcast delivery.
/// </summary>
public class QueueHub : Hub
{
    /// <summary>
    /// Joins the caller to a specific session group.
    /// Ensures that updates for one track session do not interfere with others.
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
    }

    /// <summary>
    /// Removes the caller from a specific session group.
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
    }
}
