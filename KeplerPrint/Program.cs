using KeplerPrint.Services;

var builder = WebApplication.CreateBuilder(args);

// ── SERVICES ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSingleton<PrintService>();

// ── SECURITY: CORS ────────────────────────────────────────────────────────
// Configured to allow communication from the main track dashboard on the LAN.
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

// ── HUB: STATUS & HEALTH ──────────────────────────────────────────────────
app.MapGet("/", () => new
{
    status    = "KeplerPrint — APEX CONTROL Dispatch Node",
    timestamp = DateTime.Now,
    endpoints = new[] {
        "GET  /api/print/health",
        "POST /api/print/pilot",
        "POST /api/print/vehicle",
        "POST /api/print/turn"
    }
});

app.Run();
