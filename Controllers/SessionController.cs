using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kepler_Trackline_Alliance.Controllers;

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
            _logger.LogError(ex, "Error al iniciar sesión");
            TempData["Error"] = "No se pudo iniciar la sesión. Intenta de nuevo.";
        }
        return RedirectToAction("Index", "Queue");
    }

    [HttpPost]
    public async Task<IActionResult> End(uint sessionId)
    {
        try
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status  = "COMPLETED";
                session.EndedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al finalizar sesión {SessionId}", sessionId);
            TempData["Error"] = "No se pudo finalizar la sesión.";
        }
        return RedirectToAction("Index", "Queue");
    }
}
