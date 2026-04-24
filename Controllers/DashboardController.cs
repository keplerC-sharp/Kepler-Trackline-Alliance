using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kepler_Trackline_Alliance.Controllers;

public class DashboardController : Controller
{
    private readonly QueueService _queue;

    public DashboardController(QueueService queue)
    {
        _queue = queue;
    }

    public async Task<IActionResult> Index()
    {
        var data = await _queue.GetQueueAsync(1);
        return View(data);
    }
}