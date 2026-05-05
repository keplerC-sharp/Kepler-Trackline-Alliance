using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace Kepler_Trackline_Alliance.Services;

public class QueueService
{
    private readonly AppDbContext _context;
    private readonly ILogger<QueueService> _logger;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<Hubs.QueueHub> _hubContext;

    public QueueService(
        AppDbContext context, 
        ILogger<QueueService> logger,
        Microsoft.AspNetCore.SignalR.IHubContext<Hubs.QueueHub> hubContext)
    {
        _context    = context;
        _logger     = logger;
        _hubContext = hubContext;
    }

    private async Task NotifyUpdateAsync(uint sessionId)
    {
        await _hubContext.Clients.Group($"Session_{sessionId}").SendAsync("QueueUpdated");
    }

    // ── Agregar participante a la cola ────────────────────────────────────
    public async Task AddAsync(uint sessionId, Participant participant, uint operatorId = 1)
    {
        try
        {
            var last = await _context.QueueEntries
                .Where(q => q.SessionId == sessionId)
                .MaxAsync(q => (int?)q.Position) ?? 0;

            var priority = participant.Grade == "S" ? "HIGH" : "NORMAL";

            int position;
            if (priority == "HIGH")
            {
                var firstNormal = await _context.QueueEntries
                    .Where(q => q.SessionId == sessionId &&
                                q.Status   == "QUEUED"   &&
                                q.Priority == "NORMAL")
                    .OrderBy(q => q.Position)
                    .Select(q => (int?)q.Position)
                    .FirstOrDefaultAsync();

                if (firstNormal.HasValue)
                {
                    await _context.QueueEntries
                        .Where(q => q.SessionId == sessionId &&
                                    q.Position  >= firstNormal.Value)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Position, x => x.Position + 1));
                    position = firstNormal.Value;
                }
                else
                {
                    position = last + 1;
                }
            }
            else
            {
                position = last + 1;
            }

            var entry = new QueueEntry
            {
                SessionId     = sessionId,
                ParticipantId = participant.Id,
                Position      = position,
                Priority      = priority,
                Status        = "QUEUED"
            };

            _context.QueueEntries.Add(entry);
            await _context.SaveChangesAsync();

            _context.SessionLogs.Add(new SessionLog
            {
                SessionId  = sessionId,
                OperatorId = operatorId,
                ActionType = "ADDED",
                Notes      = $"Participante {participant.GridId} agregado a posición {position}"
            });
            await _context.SaveChangesAsync();
            await NotifyUpdateAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar participante {ParticipantId} a sesión {SessionId}",
                participant.Id, sessionId);
            throw;
        }
    }

    // ── Obtener cola ordenada ─────────────────────────────────────────────
    public async Task<List<QueueEntry>> GetQueueAsync(uint sessionId)
    {
        try
        {
            return await _context.QueueEntries
                .Include(q => q.Participant)
                .Where(q => q.SessionId == sessionId &&
                            q.Status != "COMPLETED"   &&
                            q.Status != "CANCELLED")
                .OrderBy(q =>
                    q.Status == "ON_TRACK" ? 0 :
                    q.Status == "UP_NEXT"  ? 1 : 2)
                .ThenByDescending(q => q.Priority == "HIGH")
                .ThenBy(q => q.Position)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cola para sesión {SessionId}", sessionId);
            return new List<QueueEntry>();
        }
    }

    // ── Avanzar la cola ───────────────────────────────────────────────────
    public async Task AdvanceQueueAsync(uint sessionId, uint operatorId)
    {
        try
        {
            // 1. Completar el ON_TRACK actual
            var onTrack = await _context.QueueEntries
                .FirstOrDefaultAsync(q => q.SessionId == sessionId && q.Status == "ON_TRACK");

            if (onTrack != null)
            {
                onTrack.Status      = "COMPLETED";
                onTrack.CompletedAt = DateTime.Now;
                _context.SessionLogs.Add(new SessionLog
                {
                    SessionId  = sessionId,
                    OperatorId = operatorId,
                    ActionType = "FINISHED",
                    Notes      = $"Participante {onTrack.ParticipantId} completó su turno"
                });
            }

            // 2. Promover UP_NEXT → ON_TRACK
            var upNext = await _context.QueueEntries
                .FirstOrDefaultAsync(q => q.SessionId == sessionId && q.Status == "UP_NEXT");

            if (upNext != null)
            {
                upNext.Status    = "ON_TRACK";
                upNext.StartedAt = DateTime.Now;
                _context.SessionLogs.Add(new SessionLog
                {
                    SessionId  = sessionId,
                    OperatorId = operatorId,
                    ActionType = "ON_TRACK",
                    Notes      = $"Participante {upNext.ParticipantId} pasó a ON_TRACK"
                });
            }
            else
            {
                // Si no hay UP_NEXT, promover el primero QUEUED
                var next = await _context.QueueEntries
                    .Where(q => q.SessionId == sessionId && q.Status == "QUEUED")
                    .OrderByDescending(q => q.Priority == "HIGH")
                    .ThenBy(q => q.Position)
                    .FirstOrDefaultAsync();

                if (next != null)
                {
                    next.Status    = "ON_TRACK";
                    next.StartedAt = DateTime.Now;
                    _context.SessionLogs.Add(new SessionLog
                    {
                        SessionId  = sessionId,
                        OperatorId = operatorId,
                        ActionType = "ON_TRACK",
                        Notes      = $"Participante {next.ParticipantId} pasó a ON_TRACK"
                    });
                }
            }

            // 3. Promover el siguiente QUEUED → UP_NEXT (si hay)
            var nextQueued = await _context.QueueEntries
                .Where(q => q.SessionId == sessionId && q.Status == "QUEUED")
                .OrderByDescending(q => q.Priority == "HIGH")
                .ThenBy(q => q.Position)
                .FirstOrDefaultAsync();

            if (nextQueued != null)
            {
                nextQueued.Status = "UP_NEXT";
                _context.SessionLogs.Add(new SessionLog
                {
                    SessionId  = sessionId,
                    OperatorId = operatorId,
                    ActionType = "PROMOTED",
                    Notes      = $"Participante {nextQueued.ParticipantId} promovido a UP_NEXT"
                });
            }

            await _context.SaveChangesAsync();
            await NotifyUpdateAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al avanzar cola para sesión {SessionId}", sessionId);
            throw;
        }
    }

    public async Task CancelEntryAsync(uint entryId, uint? operatorId)
    {
        try
        {
            var entry = await _context.QueueEntries.FindAsync(entryId);
            if (entry == null) return;

            entry.Status = "CANCELLED";
            _context.SessionLogs.Add(new SessionLog
            {
                SessionId  = entry.SessionId,
                OperatorId = operatorId,
                ActionType = "CANCELLED",
                Notes      = $"Entrada #{entryId} cancelada"
            });
            await _context.SaveChangesAsync();
            await NotifyUpdateAsync(entry.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cancelar entrada {EntryId}", entryId);
            throw;
        }
    }

    public async Task PromoteEntryAsync(uint entryId)
    {
        var entry = await _context.QueueEntries.FindAsync(entryId);
        if (entry == null) return;

        entry.Priority = "HIGH";
        await _context.SaveChangesAsync();
        await NotifyUpdateAsync(entry.SessionId);
    }
}
