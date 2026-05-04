    using KeplerPrint.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<PrintService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => new
{
    status    = "KeplerPrint — Servidor de Impresión APEX CONTROL",
    timestamp = DateTime.Now,
    endpoints = new[] {
        "GET  /api/print/health",
        "POST /api/print/piloto",
        "POST /api/print/vehiculo",
        "POST /api/print/turno"
    }
});

app.Run();
