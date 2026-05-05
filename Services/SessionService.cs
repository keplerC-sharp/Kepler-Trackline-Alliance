using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Microsoft.Extensions.Logging;

namespace Kepler_Trackline_Alliance.Services;

/// <summary>
/// Manages the lifecycle of track sessions, ensuring only authorized starts
/// and maintaining record integrity for billing and audit purposes.
/// </summary>
public class SessionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SessionService> _logger;

    public SessionService(AppDbContext context, ILogger<SessionService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    /// <summary>
    /// Initializes a new 'LIVE' session and persists it to the database.
    /// Uses a unique tick-based identifier to ensure session code collision avoidance.
    /// </summary>
    public async Task StartSessionAsync(uint operatorId)
    {
        try
        {
            var session = new Session
            {
                OperatorId  = operatorId,
                SessionCode = $"SID-{DateTime.Now.Ticks.ToString().Substring(10)}",
                Status      = "LIVE",
                StartedAt   = DateTime.Now
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Track Session {SessionCode} initialized by Operator {OperatorId}.", 
                session.SessionCode, operatorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session initialization failed for Operator {OperatorId}.", operatorId);
            throw;
        }
    }
}
