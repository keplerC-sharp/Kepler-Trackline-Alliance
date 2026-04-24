using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;

namespace Kepler_Trackline_Alliance.Services;

public class SessionService
{
    private readonly AppDbContext _context;

    public SessionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task StartSessionAsync(uint operatorId)
    {
        try
        {
            var session = new Session
            {
                OperatorId = operatorId,
                SessionCode = $"SESSION_{DateTime.Now.Ticks}",
                Status = "LIVE",
                StartedAt = DateTime.Now
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}