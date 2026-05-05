using Microsoft.AspNetCore.Mvc;
using Kepler_Trackline_Alliance.Models;

namespace Kepler_Trackline_Alliance.Controllers;

/// <summary>
/// Root application controller. Orchestrates initial redirection based on 
/// authentication status and provides global error handling infrastructure.
/// </summary>
public class HomeController : Controller
{
    /// <summary>
    /// Root redirection logic.
    /// Authenticated operators are sent to the Queue Monitor; 
    /// unauthenticated users are directed to the Security Gateway.
    /// </summary>
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Queue");
        return RedirectToAction("Login", "Auth");
    }

    /// <summary>
    /// Catch-all handler for unhandled application exceptions.
    /// Captures Diagnostic IDs for administrative trace capabilities.
    /// </summary>
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    public IActionResult Privacy() => View();
}
