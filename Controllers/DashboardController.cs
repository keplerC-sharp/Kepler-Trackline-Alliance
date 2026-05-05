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
                || string.IsNullOrWhiteSpace(req.Document)
                || string.IsNullOrWhiteSpace(req.GridId))
                return Json(new { ok = false, error = "Nombre, Documento y Grid ID son requeridos" });

            var gridId   = req.GridId.Trim().ToUpperInvariant();
            var document = req.Document.Trim();

            var existing = await _context.Participants
                .FirstOrDefaultAsync(p => p.GridId == gridId || p.Document == document);

            if (existing != null)
            {
                var field = existing.GridId == gridId ? "Grid ID" : "Documento";
                return Json(new { ok = false, error = $"{field} ya está registrado" });
            }

            var participant = new Participant
            {
                FullName     = req.FullName.Trim(),
                Document     = document,
                GridId       = gridId,
                Category     = req.Category ?? "Casual",
                Age          = req.Age ?? 0,
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

    // ── API POST /Dashboard/UpdateParticipant ────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> UpdateParticipant([FromBody] UpdateParticipantRequest req)
    {
        try
        {
            if (req == null || req.Id == 0)
                return Json(new { ok = false, error = "ID de participante inválido" });

            var p = await _context.Participants.FindAsync(req.Id);
            if (p == null)
                return Json(new { ok = false, error = "Participante no encontrado" });

            if (!string.IsNullOrWhiteSpace(req.FullName)) p.FullName = req.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(req.Category)) p.Category = req.Category;
            if (req.Age.HasValue) p.Age = req.Age.Value;
            if (req.SeasonPoints.HasValue) p.SeasonPoints = req.SeasonPoints.Value;
            if (!string.IsNullOrWhiteSpace(req.Grade)) p.Grade = req.Grade;

            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar participante {Id}", req?.Id);
            return Json(new { ok = false, error = "Error al actualizar datos" });
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
                q.ParticipantId == participant.Id &&
                q.Status != "COMPLETED" && q.Status != "CANCELLED");

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
    string  Document,
    string  GridId,
    string? Category,
    int?    Age,
    string? Grade,
    int?    SeasonPoints);

public record UpdateParticipantRequest(
    uint    Id,
    string? FullName,
    string? Category,
    int?    Age,
    int?    SeasonPoints,
    string? Grade);

public record AssignToQueueRequest(
    uint ParticipantId,
    uint SessionId);
