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

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly EmailService _email;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, EmailService email, ILogger<AuthController> logger)
    {
        _context = context;
        _email   = email;
        _logger  = logger;
    }

    // ── GET /Auth/Login ──────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Queue");
        return View();
    }

    // ── POST /Auth/Login ─────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var op = await _context.Operators
                .FirstOrDefaultAsync(o => o.Identifier == model.Identifier);

            // Verificar contraseña usando BCrypt
            if (op == null || !BCrypt.Net.BCrypt.Verify(model.Password, op.PasswordHash))
            {
                ModelState.AddModelError("", "Credenciales inválidas");
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

            return RedirectToAction("Index", "Queue");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el login para {Identifier}", model.Identifier);
            ModelState.AddModelError("", "Error del servidor. Intenta de nuevo.");
            return View(model);
        }
    }

    // ── GET /Auth/Logout ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // Limpiar cualquier cookie remanente
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cerrar sesión");
        }
        return RedirectToAction("Login");
    }

    // ── GET /Auth/Register → redirige según autenticación ────────────────
    // Si está autenticado → pantalla de registro de operadores
    // Si no → al login
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return View(new RegisterViewModel());
        return RedirectToAction("Login");
    }

    // ── POST /Auth/Register → crear nuevo operador (solo si autenticado) ─
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
                ModelState.AddModelError("Identifier", "El identificador es requerido");
                return View(model);
            }

            if (await _context.Operators.AnyAsync(o => o.Identifier.ToLower() == identifier.ToLower()))
            {
                ModelState.AddModelError("Identifier", "Ese Identifier ya está en uso");
                return View(model);
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);
            _logger.LogInformation("Registrando nuevo operador: {Identifier}. Hash generado: {Hash}", identifier, hashedPassword);

            var user = new Operator
            {
                Identifier   = identifier,
                FullName     = model.FullName?.Trim() ?? "",
                PasswordHash = hashedPassword,
                Role         = "OPERATOR"
            };

            _context.Operators.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Operador '{model.FullName}' creado exitosamente.";
            return RedirectToAction("Register");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar operador {Identifier}", model.Identifier);
            ModelState.AddModelError("", "Error al crear la cuenta. Intenta de nuevo.");
            return View(model);
        }
    }

    // ── GET /Auth/Me → info del usuario actual (para JS) ─────────────────
    [HttpGet]
    [Authorize]
    public IActionResult Me()
    {
        var name = User.FindFirstValue(ClaimTypes.GivenName)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? "Operador";
        var id   = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "OPERATOR";
        return Json(new { name, id, role });
    }
}
