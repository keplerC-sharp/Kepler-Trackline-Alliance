using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Models;
using Kepler_Trackline_Alliance.Services;
using Kepler_Trackline_Alliance.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Kepler_Trackline_Alliance.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly EmailService _email;

    public AuthController(AppDbContext context, EmailService email)
    {
        _context = context;
        _email = email;
    }

    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new Operator
            {
                Identifier = model.Identifier,
                FullName = model.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = "OPERATOR"
            };

            _context.Operators.Add(user);
            await _context.SaveChangesAsync();

            await _email.SendEmailAsync(model.Email, "Welcome", "Registration successful");

            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }
}