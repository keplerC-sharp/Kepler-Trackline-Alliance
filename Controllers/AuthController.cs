using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Kepler_Trackline_Alliance.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kepler_Trackline_Alliance.Controllers;

/// <summary>
/// Manages identity and access control for the application.
/// Utilizes Cookie-based authentication and BCrypt for secure credential storage.
/// </summary>
public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, ILogger<AuthController> logger)
    {
        _context = context;
        _logger  = logger;
    }

    /// <summary>
    /// Serves the login interface. 
    /// Redirects authenticated users directly to the track dashboard to optimize flow.
    /// </summary>
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Queue");
        return View();
    }

    /// <summary>
    /// Processes authentication attempts.
    /// Validates credentials against PBKDF2/BCrypt hashes.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var op = await _context.Operators
                .FirstOrDefaultAsync(o => o.Identifier == model.Identifier);

            if (op == null || !BCrypt.Net.BCrypt.Verify(model.Password, op.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid credentials. Please verify your ID and password.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, op.Id.ToString()),
                new(ClaimTypes.Name,           op.Identifier),
                new(ClaimTypes.GivenName,      op.FullName),
                new(ClaimTypes.Role,           op.Role)
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = model.Remember });

            _logger.LogInformation("Operator {Identifier} authenticated successfully.", op.Identifier);
            return RedirectToAction("Index", "Queue");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failure for identifier {Identifier}.", model.Identifier);
            ModelState.AddModelError("", "Server error during authentication process. Please retry.");
            return View(model);
        }
    }

    /// <summary>
    /// Terminates the current session and clears authentication cookies.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout sequence failed.");
        }
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Serves the operator onboarding view. Restricted to active staff.
    /// </summary>
    [HttpGet]
    [Authorize]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    /// <summary>
    /// Registers a new administrative operator.
    /// Enforces identifier uniqueness to maintain record integrity.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var identifier = model.Identifier?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                ModelState.AddModelError("Identifier", "Identifier is mandatory.");
                return View(model);
            }

            if (await _context.Operators.AnyAsync(o => o.Identifier.ToLower() == identifier.ToLower()))
            {
                ModelState.AddModelError("Identifier", "Identifier already exists in the master record.");
                return View(model);
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);
            
            var user = new Operator
            {
                Identifier   = identifier,
                FullName     = model.FullName?.Trim() ?? "",
                PasswordHash = hashedPassword,
                Role         = "OPERATOR"
            };

            _context.Operators.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Operator '{model.FullName}' registered successfully.";
            return RedirectToAction("Register");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operator registration failed for {Identifier}.", model.Identifier);
            ModelState.AddModelError("", "Failed to create operator account. Database write error.");
            return View(model);
        }
    }

    /// <summary>
    /// Returns the profile data of the currently authenticated operator.
    /// Used for client-side layout synchronization.
    /// </summary>
    [HttpGet]
    [Authorize]
    public IActionResult Me()
    {
        var name = User.FindFirstValue(ClaimTypes.GivenName)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? "Operator";
        var id   = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "OPERATOR";
        return Json(new { name, id, role });
    }
}
