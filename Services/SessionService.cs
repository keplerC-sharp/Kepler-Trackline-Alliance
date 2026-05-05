using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Microsoft.Extensions.Logging;

namespace Kepler_Trackline_Alliance.Services;

public class SessionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SessionService> _logger;

    public SessionService(AppDbContext context, ILogger<SessionService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task StartSessionAsync(uint operatorId)
    {
        try
        {
            var session = new Session
            {
                OperatorId  = operatorId,
                SessionCode = $"SESSION_{DateTime.Now.Ticks}",
                Status      = "LIVE",
                StartedAt   = DateTime.Now
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar sesión para operador {OperatorId}", operatorId);
            throw;
        }
    }
}
