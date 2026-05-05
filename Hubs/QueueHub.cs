using Microsoft.AspNetCore.SignalR;

namespace Kepler_Trackline_Alliance.Hubs;

public class QueueHub : Hub
{
    // Los clientes se unen a grupos por SessionId para no recibir actualizaciones de otras sesiones
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
    }

    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Session_{sessionId}");
    }
}
