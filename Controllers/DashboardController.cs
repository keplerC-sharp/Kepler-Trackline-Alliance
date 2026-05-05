using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kepler_Trackline_Alliance.Controllers;

/// <summary>
/// Provides administrative views and APIs for participant management and historical data access.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly QueueService _queueService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        AppDbContext context,
        QueueService queueService,
        ILogger<DashboardController> logger)
    {
        _context      = context;
        _queueService = queueService;
        _logger       = logger;
    }

    /// <summary>
    /// Renders the Driver Registry view (Participant onboarding form).
    /// </summary>
    public IActionResult Index() => View();

    /// <summary>
    /// Renders the Ticket History view for audit and reprint purposes.
    /// </summary>
    public IActionResult Tickets() => View();

    /// <summary>
    /// API: Retrieves the complete list of registered participants.
    /// Used for auto-completion and master list displays.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetParticipants()
    {
        try
        {
            var list = await _context.Participants
                .OrderBy(p => p.FullName)
                .Select(p => new
                {
                    id           = p.Id,
                    fullName     = p.FullName,
                    gridId       = p.GridId,
                    grade        = p.Grade,
                    seasonPoints = p.SeasonPoints
                })
                .ToListAsync();
            return Json(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch master participant list.");
            return Json(new List<object>());
        }
    }

    /// <summary>
    /// API: Persists a new participant to the database.
    /// Validates uniqueness of Grid ID to prevent duplicate records.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegisterParticipant([FromBody] RegisterParticipantRequest req)
    {
        try
        {
            if (req == null
                || string.IsNullOrWhiteSpace(req.FullName)
                || string.IsNullOrWhiteSpace(req.GridId))
                return Json(new { ok = false, error = "Full Name and Grid ID are mandatory." });

            var gridId = req.GridId.Trim().ToUpperInvariant();

            var existing = await _context.Participants
                .FirstOrDefaultAsync(p => p.GridId == gridId);

            if (existing != null)
                return Json(new { ok = false, error = $"Grid ID {gridId} is already registered in the system." });

            var participant = new Participant
            {
                FullName     = req.FullName.Trim(),
                GridId       = gridId,
                Grade        = req.Grade ?? "B",
                SeasonPoints = req.SeasonPoints ?? 0
            };

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            return Json(new { ok = true, participantId = participant.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failure for Grid ID: {GridId}", req?.GridId);
            return Json(new { ok = false, error = "Internal server error during participant registration." });
        }
    }

    /// <summary>
    /// API: Assigns an existing participant to an active track session queue.
    /// Offloads position and priority logic to the QueueService for consistency.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AssignToQueue([FromBody] AssignToQueueRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Invalid request payload." });

            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Id == req.SessionId && s.Status == "LIVE");

            if (session == null)
                return Json(new { ok = false, error = "No active track session found for the provided ID." });

            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.Id == req.ParticipantId);

            if (participant == null)
                return Json(new { ok = false, error = "Participant record not found." });

            var alreadyIn = await _context.QueueEntries.AnyAsync(q =>
                q.SessionId     == req.SessionId &&
                q.ParticipantId == participant.Id &&
                q.Status != "COMPLETED" && q.Status != "CANCELLED");

            if (alreadyIn)
                return Json(new { ok = false, error = "Participant is already active in the current session." });

            var operatorId = uint.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var oid) ? oid : 1u;

            await _queueService.AddAsync(req.SessionId, participant, operatorId);

            var entry = await _context.QueueEntries
                .Where(q => q.SessionId == req.SessionId
                         && q.ParticipantId == participant.Id
                         && q.Status == "QUEUED")
                .OrderByDescending(q => q.EnteredAt)
                .FirstAsync();

            return Json(new { ok = true, entryId = entry.Id, position = entry.Position });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Queue assignment failed for Participant {PId} in Session {SId}",
                req?.ParticipantId, req?.SessionId);
            return Json(new { ok = false, error = "Internal server error during queue assignment." });
        }
    }

    /// <summary>
    /// API: Returns the total count of registered participants in the master database.
    /// Used for display metrics in the administrative dashboard.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTotalParticipants()
    {
        try
        {
            var total = await _context.Participants.CountAsync();
            return Json(new { total });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count total participants.");
            return Json(new { total = 0 });
        }
    }
}

public record RegisterParticipantRequest(
    string  FullName,
    string  GridId,
    string? Grade,
    int?    SeasonPoints);

public record AssignToQueueRequest(
    uint ParticipantId,
    uint SessionId);
