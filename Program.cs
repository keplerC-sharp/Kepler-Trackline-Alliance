using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Hubs;
using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── DB ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))));

// ── Auth cookies ─────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/Auth/Login";
        options.LogoutPath        = "/Auth/Logout";
        options.AccessDeniedPath  = "/Auth/Login";
        options.ExpireTimeSpan    = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// ── SignalR ──────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<QueueService>();
builder.Services.AddScoped<SessionService>();

// ── MVC ──────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

var app = builder.Build();

// ── Manejo global de excepciones ─────────────────────────────────────────
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError("Excepción no controlada en la petición {Path}", context.Request.Path);

        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
            context.Request.Path.StartsWithSegments("/Queue") ||
            context.Request.Path.StartsWithSegments("/Session"))
        {
            context.Response.StatusCode  = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"ok\":false,\"error\":\"Error interno del servidor\"}");
        }
        else
        {
            context.Response.Redirect("/Home/Error");
        }
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<QueueHub>("/queueHub");

app.Run();
