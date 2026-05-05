using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Kepler_Trackline_Alliance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kepler_Trackline_Alliance.Controllers;

/// <summary>
/// Handles all queue-related operations, including real-time status updates,
/// participant management, and track analytics.
/// </summary>
[Authorize]
public class QueueController : Controller
{
    private readonly QueueService  _queue;
    private readonly AppDbContext  _context;
    private readonly ILogger<QueueController> _logger;

    public QueueController(QueueService queue, AppDbContext context, ILogger<QueueController> logger)
    {
        _queue   = queue;
        _context = context;
        _logger  = logger;
    }

    /// <summary>
    /// Renders the main Track Control Dashboard.
    /// Redirects or handles the state where no session is currently LIVE.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var session = await _context.Sessions
                .Where(s => s.Status == "LIVE")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return View(new QueueViewModel { 
                    Entries = new(), 
                    SessionId = 0, 
                    SessionCode = "N/A" 
                });
            }

            var entries = await _queue.GetQueueAsync(session.Id);
            return View(new QueueViewModel 
            { 
                Entries     = entries, 
                SessionId   = session.Id,
                SessionCode = session.SessionCode 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the primary Track Control view.");
            return View(new QueueViewModel { Entries = new(), SessionId = 0 });
        }
    }

    /// <summary>
    /// Serves the public-facing Waiting Room view.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/Queue/WaitingRoom")]
    public IActionResult WaitingRoom() => View();

    /// <summary>
    /// Returns the current queue state as JSON for SignalR client synchronization.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQueue(uint sessionId)
    {
        try
        {
            var entries = await _queue.GetQueueAsync(sessionId);
            return Json(entries.Select(e => new
            {
                id              = e.Id,
                position        = e.Position,
                priority        = e.Priority,
                status          = e.Status,
                estimatedStartS = e.EstimatedStartS,
                sessionTimeS    = e.SessionTimeS,
                enteredAt       = e.EnteredAt,
                startedAt       = e.StartedAt,
                completedAt     = e.CompletedAt,
                participant     = e.Participant == null ? null : new
                {
                    id           = e.Participant.Id,
                    fullName     = e.Participant.FullName,
                    gridId       = e.Participant.GridId,
                    grade        = e.Participant.Grade,
                    seasonPoints = e.Participant.SeasonPoints
                }
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch queue JSON for session {SessionId}", sessionId);
            return Json(new List<object>());
        }
    }

    /// <summary>
    /// Returns the currently active (LIVE) session details.
    /// Used by registration and waiting room modules to synchronize state.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetActiveSession()
    {
        try
        {
            var session = await _context.Sessions
                .Where(s => s.Status == "LIVE")
                .OrderByDescending(s => s.StartedAt)
                .Select(s => new { s.Id, s.SessionCode })
                .FirstOrDefaultAsync();

            return Json(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve the active session.");
            return Json(null);
        }
    }

    /// <summary>
    /// Processes the advancement of the queue (Finishing current, starting next).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Advance([FromBody] AdvanceRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Invalid request payload." });

            var operatorId = uint.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : 1u;

            await _queue.AdvanceQueueAsync(req.SessionId, operatorId);

            var nowOnTrack = await _context.QueueEntries
                .Include(q => q.Participant)
                .FirstOrDefaultAsync(q => q.SessionId == req.SessionId
                                       && q.Status    == "ON_TRACK");

            return Json(new
            {
                ok = true,
                newOnTrack = nowOnTrack?.Participant == null ? null : new
                {
                    fullName = nowOnTrack.Participant.FullName,
                    gridId   = nowOnTrack.Participant.GridId,
                    position = nowOnTrack.Position
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advancement process failed for session {SessionId}", req?.SessionId);
            return Json(new { ok = false, error = "Critical error during queue advancement." });
        }
    }

    /// <summary>
    /// Completes the currently active turn without starting the next one.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> FinishTurn([FromBody] AdvanceRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Invalid request payload." });

            var operatorId = uint.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : 1u;

            await _queue.FinishTurnAsync(req.SessionId, operatorId);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Finish turn failed for session {SessionId}", req?.SessionId);
            return Json(new { ok = false, error = "Critical error finishing turn." });
        }
    }

    /// <summary>
    /// Starts the next eligible turn in the queue.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> StartTurn([FromBody] AdvanceRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Invalid request payload." });

            var operatorId = uint.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : 1u;

            await _queue.StartTurnAsync(req.SessionId, operatorId);

            var nowOnTrack = await _context.QueueEntries
                .Include(q => q.Participant)
                .FirstOrDefaultAsync(q => q.SessionId == req.SessionId
                                       && q.Status    == "ON_TRACK");

            return Json(new
            {
                ok = true,
                newOnTrack = nowOnTrack?.Participant == null ? null : new
                {
                    fullName = nowOnTrack.Participant.FullName,
                    gridId   = nowOnTrack.Participant.GridId,
                    position = nowOnTrack.Position
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start turn failed for session {SessionId}", req?.SessionId);
            return Json(new { ok = false, error = "Critical error starting turn." });
        }
    }

    /// <summary>
    /// Adds a participant to the database and optionally assigns them to an active queue.
    /// Centralizes participant lookup/creation to ensure data integrity.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddParticipant([FromBody] AddParticipantRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Invalid request payload." });

            if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.GridId))
                return Json(new { ok = false, error = "Full Name and Grid ID are mandatory." });

            var operatorId = uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : 1u;

            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.GridId == req.GridId);

            if (participant == null)
            {
                participant = new Participant
                {
                    FullName     = req.FullName,
                    GridId       = req.GridId,
                    Grade        = req.Grade ?? "B",
                    SeasonPoints = req.SeasonPoints ?? 0
                };
                _context.Participants.Add(participant);
                await _context.SaveChangesAsync();
            }

            if (req.SessionId == 0)
                return Json(new { ok = true, participantId = participant.Id, assignedToQueue = false });

            var alreadyIn = await _context.QueueEntries.AnyAsync(q =>
                q.SessionId     == req.SessionId &&
                q.ParticipantId == participant.Id &&
                q.Status != "COMPLETED" && q.Status != "CANCELLED");

            if (alreadyIn)
                return Json(new { ok = false, error = "Participant is already active in this queue." });

            await _queue.AddAsync(req.SessionId, participant, operatorId);
            return Json(new { ok = true, participantId = participant.Id, assignedToQueue = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Onboarding failure for session {SessionId}", req?.SessionId);
            return Json(new { ok = false, error = "Internal server error during participant registration." });
        }
    }

    /// <summary>
    /// Performs an asynchronous search for participants by name or ID.
    /// Used for auto-complete functionality in the registry.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchParticipants(string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new List<object>());

            var results = await _context.Participants
                .Where(p => p.FullName.Contains(q) || p.GridId.Contains(q))
                .OrderBy(p => p.FullName)
                .Take(10)
                .ToListAsync();

            return Json(results.Select(p => new
            {
                id = p.Id, fullName = p.FullName, gridId = p.GridId,
                grade = p.Grade, seasonPoints = p.SeasonPoints
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search query failed for string: '{Q}'", q);
            return Json(new List<object>());
        }
    }

    /// <summary>
    /// Cancels an active queue entry and logs the administrative action.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Cancel([FromBody] CancelRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Invalid request payload." });

            var operatorId = uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? (uint?)oid : null;
            await _queue.CancelEntryAsync(req.EntryId, operatorId);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancellation failed for entry {EntryId}", req?.EntryId);
            return Json(new { ok = false, error = "Failed to cancel the specified entry." });
        }
    }

    /// <summary>
    /// Calculates high-level session analytics, including throughput and average stint duration.
    /// Useful for operational performance monitoring.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAdvancedStats(uint sessionId)
    {
        try
        {
            var completedEntries = await _context.QueueEntries
                .Where(q => q.SessionId == sessionId && q.Status == "COMPLETED" && q.StartedAt != null && q.CompletedAt != null)
                .ToListAsync();

            double avgMinutes = 0;
            if (completedEntries.Any())
            {
                avgMinutes = completedEntries.Average(e => (e.CompletedAt!.Value - e.StartedAt!.Value).TotalMinutes);
            }

            var advisorStats = await _context.SessionLogs
                .Where(l => l.SessionId == sessionId && l.ActionType == "ENTRY_COMPLETED" && l.OperatorId != null)
                .GroupBy(l => l.OperatorId)
                .Select(g => new
                {
                    operatorId = g.Key,
                    count      = g.Count()
                })
                .ToListAsync();

            var operatorIds = advisorStats.Select(s => s.operatorId).ToList();
            var operators = await _context.Operators
                .Where(o => operatorIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.FullName);

            double turnRate = 0;
            if (completedEntries.Count > 1)
            {
                var firstComp = completedEntries.Min(e => e.CompletedAt!.Value);
                var lastComp = completedEntries.Max(e => e.CompletedAt!.Value);
                var totalHours = (lastComp - firstComp).TotalHours;
                if (totalHours > 0) turnRate = Math.Round(completedEntries.Count / totalHours, 1);
            }

            var totalQueued = await _context.QueueEntries
                .CountAsync(q => q.SessionId == sessionId && q.Status == "QUEUED");

            var nextPilot = await _context.QueueEntries
                .Include(q => q.Participant)
                .Where(q => q.SessionId == sessionId && q.Status == "UP_NEXT")
                .Select(q => q.Participant.FullName)
                .FirstOrDefaultAsync();

            return Json(new
            {
                avgTimeMinutes = Math.Round(avgMinutes, 1),
                turnRate       = turnRate,
                totalQueued    = totalQueued,
                nextPilotName  = nextPilot ?? "None",
                byAdvisor      = advisorStats.Select(s => new
                {
                    name  = operators.ContainsKey(s.operatorId!.Value) ? operators[s.operatorId!.Value] : $"Op #{s.operatorId}",
                    count = s.count
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics calculation failed for session {SessionId}", sessionId);
            return Json(new { avgTimeMinutes = 0, byAdvisor = new List<object>(), turnRate = 0, totalQueued = 0 });
        }
    }

    /// <summary>
    /// Attaches an administrative comment to a session log.
    /// Used for internal reporting and audit compliance.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddComment([FromBody] CommentRequest req)
    {
        try
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Comment))
                return Json(new { ok = false, error = "Comment cannot be empty." });

            var entry = await _context.QueueEntries.FindAsync(req.EntryId);
            if (entry == null)
                return Json(new { ok = false, error = "Session entry not found." });

            var operatorId = uint.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid)
                ? (uint?)oid : null;

            _context.SessionLogs.Add(new SessionLog
            {
                SessionId  = entry.SessionId,
                OperatorId = operatorId,
                ActionType = "ADVISOR_COMMENT",
                Notes      = req.Comment.Trim()
            });
            await _context.SaveChangesAsync();

            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist comment for entry {EntryId}", req?.EntryId);
            return Json(new { ok = false, error = "Database write failure during comment save." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Promote([FromBody] PromoteRequest req)
    {
        try
        {
            if (req == null) return Json(new { ok = false });
            await _queue.PromoteEntryAsync(req.EntryId);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Priority elevation failed for entry {EntryId}", req?.EntryId);
            return Json(new { ok = false });
        }
    }
}

public record AdvanceRequest(uint SessionId);
public record CancelRequest(uint EntryId);
public record PromoteRequest(uint EntryId);
public record AddParticipantRequest(
    uint    SessionId,
    string  FullName,
    string  GridId,
    string? Grade,
    int?    SeasonPoints);

public record CommentRequest(uint EntryId, string Comment);
