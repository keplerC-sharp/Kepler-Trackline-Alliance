using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace Kepler_Trackline_Alliance.Services;

/// <summary>
/// Orchestrates the track queue lifecycle, ensuring atomic transitions 
/// between participant states (Queued -> Up Next -> On Track -> Completed).
/// </summary>
public class QueueService
{
    private readonly AppDbContext _context;
    private readonly ILogger<QueueService> _logger;
    private readonly IHubContext<Hubs.QueueHub> _hubContext;

    public QueueService(
        AppDbContext context, 
        ILogger<QueueService> logger,
        IHubContext<Hubs.QueueHub> hubContext)
    {
        _context    = context;
        _logger     = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Triggers a SignalR broadcast to notify all connected clients of a state change.
    /// Used instead of polling to maintain real-time UI synchronization across stations.
    /// </summary>
    private async Task NotifyUpdateAsync(uint sessionId)
    {
        await _hubContext.Clients.Group($"Session_{sessionId}").SendAsync("QueueUpdated");
    }

    /// <summary>
    /// Adds a participant to the session queue with intelligent position calculation.
    /// Prioritizes Grade S pilots by inserting them before the first non-priority pilot.
    /// </summary>
    public async Task AddAsync(uint sessionId, Participant participant, uint operatorId = 1)
    {
        try
        {
            var lastPosition = await _context.QueueEntries
                .Where(q => q.SessionId == sessionId)
                .MaxAsync(q => (int?)q.Position) ?? 0;

            // Business rule: Grade S pilots receive 'HIGH' priority status immediately.
            var priority = participant.Grade == "S" ? "HIGH" : "NORMAL";

            int position;
            if (priority == "HIGH")
            {
                // Find the first pilot that doesn't have high priority to perform a 'cut in line' operation.
                var firstNormal = await _context.QueueEntries
                    .Where(q => q.SessionId == sessionId &&
                                q.Status   == "QUEUED"   &&
                                q.Priority == "NORMAL")
                    .OrderBy(q => q.Position)
                    .Select(q => (int?)q.Position)
                    .FirstOrDefaultAsync();

                if (firstNormal.HasValue)
                {
                    // Shift all subsequent entries to make room for the priority pilot.
                    await _context.QueueEntries
                        .Where(q => q.SessionId == sessionId &&
                                    q.Position  >= firstNormal.Value)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Position, x => x.Position + 1));
                    position = firstNormal.Value;
                }
                else
                {
                    position = lastPosition + 1;
                }
            }
            else
            {
                position = lastPosition + 1;
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
                ActionType = "ENTRY_ADDED",
                Notes      = $"Pilot {participant.GridId} added at position {position}"
            });
            await _context.SaveChangesAsync();
            await NotifyUpdateAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure adding participant {ParticipantId} to session {SessionId}",
                participant.Id, sessionId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the ordered list of active queue entries.
    /// Sorting priority: On Track > Up Next > Queued (by Position).
    /// </summary>
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
            _logger.LogError(ex, "Failed to retrieve queue for session {SessionId}", sessionId);
            return new List<QueueEntry>();
        }
    }

    /// <summary>
    /// Advances the queue by transitioning the current active turn to 'Completed'
    /// and promoting the next eligible participant to 'On Track'.
    /// </summary>
    public async Task AdvanceQueueAsync(uint sessionId, uint operatorId)
    {
        try
        {
            await FinishTurnAsync(sessionId, operatorId);
            await StartTurnAsync(sessionId, operatorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure advancing queue for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Completes the currently active turn without starting the next one.
    /// </summary>
    public async Task FinishTurnAsync(uint sessionId, uint operatorId)
    {
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
                ActionType = "ENTRY_COMPLETED",
                Notes      = $"Participant {onTrack.ParticipantId} completed stint"
            });
            await _context.SaveChangesAsync();
            await NotifyUpdateAsync(sessionId);
        }
    }

    /// <summary>
    /// Starts the next eligible turn in the queue (Up Next or first Queued).
    /// </summary>
    public async Task StartTurnAsync(uint sessionId, uint operatorId)
    {
        // Transition candidate (Up Next or first Queued) to active state.
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
                ActionType = "ENTRY_ON_TRACK",
                Notes      = $"Participant {upNext.ParticipantId} is now ON_TRACK"
            });
        }
        else
        {
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
                    ActionType = "ENTRY_ON_TRACK",
                    Notes      = $"Participant {next.ParticipantId} skipped status and is now ON_TRACK"
                });
            }
        }

        // Populate the 'Up Next' slot to maintain race flow visibility.
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
                ActionType = "ENTRY_PROMOTED",
                Notes      = $"Participant {nextQueued.ParticipantId} promoted to UP_NEXT"
            });
        }

        await _context.SaveChangesAsync();
        await NotifyUpdateAsync(sessionId);
    }

    /// <summary>
    /// Removes a participant from the active queue and logs the cancellation.
    /// Does not delete the record to preserve audit trails.
    /// </summary>
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
                ActionType = "ENTRY_CANCELLED",
                Notes      = $"Entry #{entryId} cancelled by operator"
            });
            await _context.SaveChangesAsync();
            await NotifyUpdateAsync(entry.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel entry {EntryId}", entryId);
            throw;
        }
    }

    /// <summary>
    /// Manually elevates a participant's priority within the session.
    /// Useful for operational overrides or platinum member handling.
    /// </summary>
    public async Task PromoteEntryAsync(uint entryId)
    {
        var entry = await _context.QueueEntries.FindAsync(entryId);
        if (entry == null) return;

        entry.Priority = "HIGH";
        await _context.SaveChangesAsync();
        await NotifyUpdateAsync(entry.SessionId);
    }
}
