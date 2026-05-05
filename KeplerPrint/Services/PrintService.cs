using ESCPOS_NET.Emitters;
using KeplerPrint.Helpers;
using KeplerPrint.Models;

namespace KeplerPrint.Services
{
    public class PrintService
    {
        private readonly IConfiguration        _config;
        private readonly ILogger<PrintService> _logger;

        public PrintService(IConfiguration config, ILogger<PrintService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private string NombreImpresora =>
            _config["Impresora:Nombre"] ?? "XP-58";

        private static readonly string[] F1Art =
        {
            "       .-----------.          ",
            "     ./             \\.        ",
            "    /                 \\       ",
            "   / ._______________. \\      ",
            "  | |               | |       ",
            "  | |               | |       ",
            "  | |               | |       ",
            "   \\ `_______________' /      ",
            "    \\                 /       ",
            "     `-.___________.`         ",
            "        |_________|           ",
        };

        private byte[] BuildTicket(Action<List<byte[]>> contenido)
        {
            var e      = new EPSON();
            var chunks = new List<byte[]>();
            chunks.Add(e.Initialize());
            chunks.Add(e.CenterAlign());
            foreach (var line in F1Art)
                chunks.Add(e.PrintLine(line));
            contenido(chunks);
            chunks.Add(e.PrintLine("================================"));
            chunks.Add(e.CenterAlign());
            chunks.Add(e.PrintLine("KEPLER-TRACKALLIANCE"));
            chunks.Add(e.PrintLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
            chunks.Add(e.FeedLines(4));
            chunks.Add(e.FullCut());
            int total  = chunks.Sum(c => c.Length);
            var result = new byte[total];
            int offset = 0;
            foreach (var chunk in chunks)
            { Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length); offset += chunk.Length; }
            return result;
        }

        private void Imprimir(byte[] datos)
        {
            _logger.LogInformation("Enviando {Bytes} bytes a '{Impresora}'", datos.Length, NombreImpresora);
            RawPrinterHelper.SendBytesToPrinter(NombreImpresora, datos);
        }

        public void ImprimirTicketPiloto(PrintPilotoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("REGISTRO DE PILOTO"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.PrintLine($"Nombre  : {req.Nombre}"));
                    chunks.Add(e.PrintLine($"ID      : {req.DriverId}"));
                    chunks.Add(e.PrintLine($"Licencia: {req.Licencia}"));
                    chunks.Add(e.PrintLine($"Email   : {req.Email}"));
                    chunks.Add(e.PrintLine($"Fecha   : {req.Fecha}"));
                });
                Imprimir(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Error piloto"); throw; }
        }

        public void ImprimirTicketVehiculo(PrintVehiculoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("REGISTRO DE VEHICULO"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.PrintLine($"Modelo  : {req.Modelo}"));
                    chunks.Add(e.PrintLine($"Categ.  : {req.Categoria}"));
                    chunks.Add(e.PrintLine($"VIN     : {req.Vin}"));
                    chunks.Add(e.PrintLine($"Garage  : {req.Garage}"));
                    chunks.Add(e.PrintLine($"Fecha   : {req.Fecha}"));
                });
                Imprimir(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Error vehiculo"); throw; }
        }

        public void ImprimirTicketTurno(PrintTurnoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight));
                    chunks.Add(e.PrintLine($"TURNO: {req.Turno}"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.PrintLine($"Piloto  : {req.Nombre}"));
                    chunks.Add(e.PrintLine($"Vehiculo: {req.Vehiculo}"));
                    chunks.Add(e.PrintLine($"Duracion: {req.Duracion} min"));
                    chunks.Add(e.PrintLine($"Emitido : {req.CreatedAt}"));
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("*** PENDIENTE ***"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                });
                Imprimir(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Error turno"); throw; }
        }

        public void ImprimirRegistroCompleto(PrintRegistroCompletoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight));
                    chunks.Add(e.PrintLine($"TURNO: {req.Turno}"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("================================"));
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("-- PILOTO --"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine($"Nombre  : {req.Nombre}"));
                    chunks.Add(e.PrintLine($"ID      : {req.DriverId}"));
                    chunks.Add(e.PrintLine($"Licencia: {req.Licencia}"));
                    chunks.Add(e.PrintLine($"Email   : {req.Email}"));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("-- VEHICULO --"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine($"Modelo  : {req.Modelo}"));
                    chunks.Add(e.PrintLine($"Categ.  : {req.Categoria}"));
                    chunks.Add(e.PrintLine($"VIN     : {req.Vin}"));
                    chunks.Add(e.PrintLine($"Garage  : {req.Garage}"));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.PrintLine($"Duracion: {req.Duracion} min"));
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("*** PENDIENTE ***"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                });
                Imprimir(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Error registro completo"); throw; }
        }
    }
}
