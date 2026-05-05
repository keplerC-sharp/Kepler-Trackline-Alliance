using Microsoft.AspNetCore.Mvc;
using Kepler_Trackline_Alliance.Models;

namespace Kepler_Trackline_Alliance.Controllers;

public class HomeController : Controller
{
    // Redirigir raíz: si está logueado -> Queue, si no -> Login
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Queue");
        return RedirectToAction("Login", "Auth");
    }

    // Página de error global
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    public IActionResult Privacy() => View();
}
