using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Microsoft.EntityFrameworkCore;

namespace Kepler_Trackline_Alliance.Services;

public class QueueService
{
    private readonly AppDbContext _context;

    public QueueService(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(uint sessionId, Participant participant)
    {
        try
        {
            var last = await _context.QueueEntries
                .Where(q => q.SessionId == sessionId)
                .MaxAsync(q => (int?)q.Position) ?? 0;

            var priority = participant.Grade == "S" ? "HIGH" : "NORMAL";

            var entry = new QueueEntry
            {
                SessionId = sessionId,
                ParticipantId = participant.Id,
                Position = last + 1,
                Priority = priority
            };

            _context.QueueEntries.Add(entry);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public async Task<List<QueueEntry>> GetQueueAsync(uint sessionId)
    {
        return await _context.QueueEntries
            .Include(q => q.Participant)
            .Where(q => q.SessionId == sessionId)
            .OrderByDescending(q => q.Priority == "HIGH")
            .ThenBy(q => q.Position)
            .ToListAsync();
    }
}