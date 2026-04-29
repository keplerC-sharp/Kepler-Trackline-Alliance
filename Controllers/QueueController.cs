using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Kepler_Trackline_Alliance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kepler_Trackline_Alliance.Controllers;

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

    // ── GET /Queue/Index ─────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        try
        {
            var session = await _context.Sessions
                .Where(s => s.Status == "LIVE")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            if (session == null)
                return View(new QueueViewModel { Entries = new(), SessionId = 0 });

            var entries = await _queue.GetQueueAsync(session.Id);
            return View(new QueueViewModel { Entries = entries, SessionId = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar la cola principal");
            return View(new QueueViewModel { Entries = new(), SessionId = 0 });
        }
    }

    // ── API GET /Queue/GetQueue?sessionId=1 ───────────────────────────────
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
            _logger.LogError(ex, "Error al obtener cola para sesión {SessionId}", sessionId);
            return Json(new List<object>());
        }
    }

    // ── API GET /Queue/GetStats?sessionId=1 ───────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetStats(uint sessionId)
    {
        try
        {
            var entries = await _context.QueueEntries
                .Where(q => q.SessionId == sessionId)
                .ToListAsync();

            return Json(new
            {
                pending   = entries.Count(e => e.Status == "QUEUED"),
                active    = entries.Count(e => e.Status == "ON_TRACK"),
                upNext    = entries.Count(e => e.Status == "UP_NEXT"),
                completed = entries.Count(e => e.Status == "COMPLETED"),
                cancelled = entries.Count(e => e.Status == "CANCELLED")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener stats para sesión {SessionId}", sessionId);
            return Json(new { pending = 0, active = 0, upNext = 0, completed = 0, cancelled = 0 });
        }
    }

    // ── API POST /Queue/Advance ───────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Advance([FromBody] AdvanceRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Datos inválidos" });

            var operatorId = uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : 1u;
            await _queue.AdvanceQueueAsync(req.SessionId, operatorId);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al avanzar cola para sesión {SessionId}", req?.SessionId);
            return Json(new { ok = false, error = "Error al avanzar la cola" });
        }
    }

    // ── API POST /Queue/AddParticipant ────────────────────────────────────
    // sessionId == 0  → solo crear/encontrar el participante, sin asignar a cola
    // sessionId  > 0  → crear/encontrar participante Y asignarlo a la cola
    [HttpPost]
    public async Task<IActionResult> AddParticipant([FromBody] AddParticipantRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Datos inválidos" });

            if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.GridId))
                return Json(new { ok = false, error = "FullName y GridId son requeridos" });

            var operatorId = uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : 1u;

            // Buscar o crear participante
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

            // Si no hay sesión activa, solo devolver el participante creado
            if (req.SessionId == 0)
                return Json(new { ok = true, participantId = participant.Id, assignedToQueue = false });

            // Verificar que no esté ya en la cola activa
            var alreadyIn = await _context.QueueEntries.AnyAsync(q =>
                q.SessionId     == req.SessionId &&
                q.ParticipantId == participant.Id &&
                q.Status != "COMPLETED" && q.Status != "CANCELLED");

            if (alreadyIn)
                return Json(new { ok = false, error = "Participante ya está en cola activa" });

            await _queue.AddAsync(req.SessionId, participant, operatorId);
            return Json(new { ok = true, participantId = participant.Id, assignedToQueue = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar participante a sesión {SessionId}", req?.SessionId);
            return Json(new { ok = false, error = "Error interno al agregar el participante" });
        }
    }

    // ── API GET /Queue/SearchParticipants ─────────────────────────────────
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
            _logger.LogError(ex, "Error al buscar participantes '{Q}'", q);
            return Json(new List<object>());
        }
    }

    // ── API GET /Queue/GetAllParticipants ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAllParticipants()
    {
        try
        {
            var list = await _context.Participants
                .OrderBy(p => p.FullName)
                .ToListAsync();
            return Json(list.Select(p => new
            {
                id = p.Id, fullName = p.FullName, gridId = p.GridId,
                grade = p.Grade, seasonPoints = p.SeasonPoints
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener todos los participantes");
            return Json(new List<object>());
        }
    }

    // ── API POST /Queue/Cancel ────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Cancel([FromBody] CancelRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Datos inválidos" });

            var entry = await _context.QueueEntries.FindAsync(req.EntryId);
            if (entry == null)
                return Json(new { ok = false, error = "Entrada no encontrada" });

            entry.Status = "CANCELLED";
            await _context.SaveChangesAsync();

            var log = new SessionLog
            {
                SessionId  = entry.SessionId,
                OperatorId = uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : null,
                ActionType = "ENTRY_CANCELLED",
                Notes      = $"Entrada #{entry.Id} cancelada"
            };
            _context.SessionLogs.Add(log);
            await _context.SaveChangesAsync();

            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cancelar entrada {EntryId}", req?.EntryId);
            return Json(new { ok = false, error = "Error al cancelar la entrada" });
        }
    }

    // ── API GET /Queue/GetLog?sessionId=1 ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetLog(uint sessionId)
    {
        try
        {
            var logs = await _context.SessionLogs
                .Where(l => l.SessionId == sessionId)
                .OrderByDescending(l => l.CreatedAt)
                .Take(50)
                .Select(l => new { l.ActionType, l.Notes, l.CreatedAt })
                .ToListAsync();
            return Json(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener log para sesión {SessionId}", sessionId);
            return Json(new List<object>());
        }
    }

    // ── API GET /Queue/GetActiveSession ───────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetActiveSession()
    {
        try
        {
            var session = await _context.Sessions
                .Where(s => s.Status == "LIVE")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync();

            if (session == null) return Json(null);

            return Json(new
            {
                id          = session.Id,
                sessionCode = session.SessionCode,
                status      = session.Status,
                startedAt   = session.StartedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sesión activa");
            return Json(null);
        }
    }
}

public record AdvanceRequest(uint SessionId);
public record CancelRequest(uint EntryId);
public record AddParticipantRequest(
    uint    SessionId,
    string  FullName,
    string  GridId,
    string? Grade,
    int?    SeasonPoints);
