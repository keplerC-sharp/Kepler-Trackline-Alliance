using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kepler_Trackline_Alliance.Controllers;

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

    // ── GET /Dashboard/Index — Driver Registry (formulario de participantes) ──
    public IActionResult Index() => View();

    // ── GET /Dashboard/Tickets — Historial de tiquetes ──
    public IActionResult Tickets() => View();

    // ─────────────────────────────────────────────────────────────────────────
    // API GET /Dashboard/GetParticipants
    // Lista todos los participantes registrados
    // ─────────────────────────────────────────────────────────────────────────
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
            _logger.LogError(ex, "Error al obtener participantes");
            return Json(new List<object>());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API POST /Dashboard/RegisterParticipant
    // Registra un participante en la BD (sin asignarlo a ninguna cola)
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> RegisterParticipant([FromBody] RegisterParticipantRequest req)
    {
        try
        {
            if (req == null
                || string.IsNullOrWhiteSpace(req.FullName)
                || string.IsNullOrWhiteSpace(req.GridId))
                return Json(new { ok = false, error = "Nombre y Grid ID son requeridos" });

            var gridId = req.GridId.Trim().ToUpperInvariant();

            var existing = await _context.Participants
                .FirstOrDefaultAsync(p => p.GridId == gridId);

            if (existing != null)
                return Json(new { ok = false, error = $"Grid ID {gridId} ya está registrado" });

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
            _logger.LogError(ex, "Error al registrar participante {GridId}", req?.GridId);
            return Json(new { ok = false, error = "Error interno al registrar" });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API POST /Dashboard/AssignToQueue
    // Asigna un participante ya existente a la cola de una sesión activa
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> AssignToQueue([FromBody] AssignToQueueRequest req)
    {
        try
        {
            if (req == null)
                return Json(new { ok = false, error = "Datos inválidos" });

            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Id == req.SessionId && s.Status == "LIVE");

            if (session == null)
                return Json(new { ok = false, error = "No hay sesión activa con ese ID" });

            var participant = await _context.Participants
                .FirstOrDefaultAsync(p => p.Id == req.ParticipantId);

            if (participant == null)
                return Json(new { ok = false, error = "Participante no encontrado" });

            var alreadyIn = await _context.QueueEntries.AnyAsync(q =>
                q.SessionId     == req.SessionId &&
                q.ParticipantId == participant.Id);

            if (alreadyIn)
                return Json(new { ok = false, error = "El participante ya está en la cola activa" });

            // Delegar toda la lógica de posición/prioridad al servicio
            var operatorId = uint.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var oid) ? oid : 1u;

            await _queueService.AddAsync(req.SessionId, participant, operatorId);

            // Obtener la entrada recién creada para devolver la posición
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
            _logger.LogError(ex, "Error al asignar participante {PId} a sesión {SId}",
                req?.ParticipantId, req?.SessionId);
            return Json(new { ok = false, error = "Error interno al asignar turno" });
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
