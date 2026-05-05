using Kepler_Trackline_Alliance.Data;
using Kepler_Trackline_Alliance.Hubs;
using Kepler_Trackline_Alliance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── LOGGING INFRASTRUCTURE ────────────────────────────────────────────────
// Clean standard providers to ensure high-visibility console output for track operators.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── DATABASE PERSISTENCE ──────────────────────────────────────────────────
// Initializes the MySQL provider with automatic server version detection.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))));

// ── IDENTITY & ACCESS CONTROL ─────────────────────────────────────────────
// Implements secure cookie-based authentication with sliding expiration for session continuity.
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

// ── REAL-TIME TELEMETRY ───────────────────────────────────────────────────
// SignalR enables instantaneous push notifications for queue updates.
builder.Services.AddSignalR();

// ── BUSINESS LOGIC LAYER ──────────────────────────────────────────────────
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<QueueService>();
builder.Services.AddScoped<SessionService>();

// ── MVC INFRASTRUCTURE ────────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        // Standardizes on camelCase for JSON serialization to maintain compatibility with JS clients.
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

var app = builder.Build();

// ── GLOBAL EXCEPTION HANDLER ──────────────────────────────────────────────
// Centralized fault handling to differentiate between API and View requests.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError("Unhandled exception captured at {Path}.", context.Request.Path);

        // API responses require JSON format to prevent client-side UI breakage.
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
            context.Request.Path.StartsWithSegments("/Queue") ||
            context.Request.Path.StartsWithSegments("/Session"))
        {
            context.Response.StatusCode  = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"ok\":false,\"error\":\"Internal System Error. Contact Pit Wall Support.\"}");
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
