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
    public async Task<IActionResult> End(uint sessionId, string? email, [FromServices] EmailService emailService)
    {
        try
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status  = "COMPLETED";
                session.EndedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(email))
                {
                    var completedCount = await _context.QueueEntries
                        .CountAsync(q => q.SessionId == sessionId && q.Status == "COMPLETED");
                    
                    var duration = (session.EndedAt - session.StartedAt)?.ToString(@"hh\:mm\:ss") ?? "N/A";
                    var reportBody = $@"
                        <div style=""font-family: 'Arial', sans-serif; background-color: #0A0A0B; color: #E8E8F0; padding: 40px 20px; text-align: center;"">
                            <div style=""max-width: 600px; margin: 0 auto; background-color: #1C1C24; border: 1px solid #2A2A36; border-top: 4px solid #00E5FF; border-radius: 6px; padding: 30px; box-shadow: 0 10px 30px rgba(0,0,0,0.5);"">
                                
                                <h1 style=""color: #00E5FF; margin-top: 0; text-transform: uppercase; font-size: 24px; letter-spacing: 2px;"">Trackline Session Report</h1>
                                <p style=""color: #6B6B80; font-size: 14px; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 30px;"">Session <strong>{(string.IsNullOrEmpty(session.SessionCode) ? session.Id.ToString() : session.SessionCode)}</strong> successfully completed.</p>
                                
                                <table style=""width: 100%; border-collapse: collapse; margin-bottom: 30px;"">
                                    <tr>
                                        <td style=""padding: 15px; border-bottom: 1px solid #2A2A36; text-align: left; color: #6B6B80; text-transform: uppercase; font-size: 12px; letter-spacing: 1px;"">Start Time</td>
                                        <td style=""padding: 15px; border-bottom: 1px solid #2A2A36; text-align: right; color: #E8E8F0; font-weight: bold;"">{session.StartedAt?.ToString("MMM dd, yyyy HH:mm:ss")}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 15px; border-bottom: 1px solid #2A2A36; text-align: left; color: #6B6B80; text-transform: uppercase; font-size: 12px; letter-spacing: 1px;"">End Time</td>
                                        <td style=""padding: 15px; border-bottom: 1px solid #2A2A36; text-align: right; color: #E8E8F0; font-weight: bold;"">{session.EndedAt?.ToString("MMM dd, yyyy HH:mm:ss")}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding: 15px; border-bottom: 1px solid #2A2A36; text-align: left; color: #6B6B80; text-transform: uppercase; font-size: 12px; letter-spacing: 1px;"">Duration</td>
                                        <td style=""padding: 15px; border-bottom: 1px solid #2A2A36; text-align: right; color: #E8E8F0; font-weight: bold;"">{duration}</td>
                                    </tr>
                                </table>

                                <div style=""background-color: #0A0A0B; border: 1px solid #2A2A36; border-radius: 4px; padding: 20px; margin-bottom: 30px;"">
                                    <h2 style=""margin: 0; font-size: 36px; color: #00FF88;"">{completedCount}</h2>
                                    <div style=""color: #6B6B80; font-size: 12px; text-transform: uppercase; letter-spacing: 2px;"">Completed Turns</div>
                                </div>

                                <p style=""color: #6B6B80; font-size: 12px; margin-top: 30px;"">&copy; {DateTime.Now.Year} Kepler-Trackline Alliance. Automated dispatch.</p>
                            </div>
                        </div>";
                    
                    var emailResult = await emailService.SendEmailAsync(email, $"Session Report - {session.Id}", reportBody);
                    
                    if (emailResult.Success)
                    {
                        TempData["Success"] = "Session finalized successfully. Report sent to the email provided.";
                    }
                    else
                    {
                        TempData["Error"] = $"Session finalized, but the email report could not be sent: {emailResult.ErrorMessage}";
                    }
                }
                else
                {
                    TempData["Success"] = "Session finalized successfully.";
                }
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
