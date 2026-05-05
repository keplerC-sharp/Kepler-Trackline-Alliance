using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kepler_Trackline_Alliance.Controllers;

/// <summary>
/// Exposed endpoints for session lifecycle control (Initialization and Finalization).
/// Restricted to authorized personnel only.
/// </summary>
[Authorize]
public class SessionController : Controller
{
    private readonly SessionService _service;
    private readonly AppDbContext   _context;
    private readonly ILogger<SessionController> _logger;

    public SessionController(SessionService service, AppDbContext context, ILogger<SessionController> logger)
    {
        _service = service;
        _context = context;
        _logger  = logger;
    }

    /// <summary>
    /// POST handler to initiate a new live track session.
    /// Redirects to the queue monitor upon success.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Start()
    {
        try
        {
            var operatorId = uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : 1u;
            await _service.StartSessionAsync(operatorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate session.");
            TempData["Error"] = "Unable to start session. Please verify database connectivity.";
        }
        return RedirectToAction("Index", "Queue");
    }

    /// <summary>
    /// POST handler to gracefully close an active session.
    /// Updates status and captures end-timestamp for analytics.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> End(uint sessionId)
    {
        try
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status  = "COMPLETED";
                session.EndedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session termination failed for ID: {SessionId}", sessionId);
            TempData["Error"] = "Session closure failed. Internal server error.";
        }
        return RedirectToAction("Index", "Queue");
    }
}
