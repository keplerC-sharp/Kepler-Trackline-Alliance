using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kepler_Trackline_Alliance.Controllers;

public class SessionController : Controller
{
    private readonly SessionService _service;

    public SessionController(SessionService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Start()
    {
        await _service.StartSessionAsync(1);
        return RedirectToAction("Index", "Dashboard");
    }
}