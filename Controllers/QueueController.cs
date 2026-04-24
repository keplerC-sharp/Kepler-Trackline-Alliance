using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kepler_Trackline_Alliance.Controllers;

public class QueueController : Controller
{
    private readonly QueueService _queue;

    public QueueController(QueueService queue)
    {
        _queue = queue;
    }

    public async Task<IActionResult> Index(uint sessionId)
    {
        var data = await _queue.GetQueueAsync(sessionId);
        return View(data);
    }
}