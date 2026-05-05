using ESCPOS_NET.Emitters;
using KeplerPrint.Helpers;
using KeplerPrint.Models;

namespace KeplerPrint.Services
{
    /// <summary>
    /// Core infrastructure service for thermal printing operations.
    /// Utilizes ESC/POS commands to generate standardized track tickets.
    /// </summary>
    public class PrintService
    {
        private readonly IConfiguration        _config;
        private readonly ILogger<PrintService> _logger;

        public PrintService(IConfiguration config, ILogger<PrintService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private string PrinterName =>
            _config["Impresora:Nombre"] ?? "XP-58";

        // ASCII representation of a high-tech racing component for visual branding.
        private static readonly string[] AssetArt =
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

        /// <summary>
        /// Orchestrates the construction of a raw byte-sequence for thermal printers.
        /// Standardizes the header and footer across all ticket types.
        /// </summary>
        private byte[] BuildTicket(Action<List<byte[]>> content)
        {
            var e      = new EPSON();
            var chunks = new List<byte[]>();
            chunks.Add(e.Initialize());
            chunks.Add(e.CenterAlign());
            foreach (var line in AssetArt)
                chunks.Add(e.PrintLine(line));
            content(chunks);
            chunks.Add(e.PrintLine("================================"));
            chunks.Add(e.CenterAlign());
            chunks.Add(e.PrintLine("KEPLER-TRACKALLIANCE"));
            chunks.Add(e.PrintLine(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")));
            chunks.Add(e.FeedLines(4));
            chunks.Add(e.FullCut());
            int total  = chunks.Sum(c => c.Length);
            var result = new byte[total];
            int offset = 0;
            foreach (var chunk in chunks)
            { Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length); offset += chunk.Length; }
            return result;
        }

        /// <summary>
        /// Dispatches the prepared byte payload to the physical printer spooler.
        /// </summary>
        private void DispatchToHardware(byte[] data)
        {
            _logger.LogInformation("Dispatching {Bytes} bytes to hardware: '{Printer}'", data.Length, PrinterName);
            RawPrinterHelper.SendBytesToPrinter(PrinterName, data);
        }

        public void PrintPilotTicket(PrintPilotoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("PILOT REGISTRATION TICKET"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.PrintLine($"Name    : {req.Nombre}"));
                    chunks.Add(e.PrintLine($"Grid ID : {req.DriverId}"));
                    chunks.Add(e.PrintLine($"License : {req.Licencia}"));
                    chunks.Add(e.PrintLine($"Email   : {req.Email}"));
                    chunks.Add(e.PrintLine($"Date    : {req.Fecha}"));
                });
                DispatchToHardware(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Hardware Dispatch Failure: Pilot Ticket."); throw; }
        }

        public void PrintVehicleTicket(PrintVehiculoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("VEHICLE ONBOARDING TICKET"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.PrintLine($"Model   : {req.Modelo}"));
                    chunks.Add(e.PrintLine($"Class   : {req.Categoria}"));
                    chunks.Add(e.PrintLine($"VIN     : {req.Vin}"));
                    chunks.Add(e.PrintLine($"Garage  : {req.Garage}"));
                    chunks.Add(e.PrintLine($"Date    : {req.Fecha}"));
                });
                DispatchToHardware(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Hardware Dispatch Failure: Vehicle Ticket."); throw; }
        }

        public void PrintTurnTicket(PrintTurnoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight));
                    chunks.Add(e.PrintLine($"TURN: {req.Turno}"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.PrintLine($"Pilot   : {req.Nombre}"));
                    chunks.Add(e.PrintLine($"Vehicle : {req.Vehiculo}"));
                    chunks.Add(e.PrintLine($"Stint   : {req.Duracion} min"));
                    chunks.Add(e.PrintLine($"Issued  : {req.CreatedAt}"));
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("*** PENDING ENTRY ***"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                });
                DispatchToHardware(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Hardware Dispatch Failure: Turn Ticket."); throw; }
        }

        public void PrintFullRegistration(PrintRegistroCompletoRequest req)
        {
            try {
                var e = new EPSON();
                var bytes = BuildTicket(chunks => {
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight));
                    chunks.Add(e.PrintLine($"TURN: {req.Turno}"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine("================================"));
                    chunks.Add(e.LeftAlign());
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("-- PILOT PROFILE --"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine($"Name    : {req.Nombre}"));
                    chunks.Add(e.PrintLine($"Grid ID : {req.DriverId}"));
                    chunks.Add(e.PrintLine($"License : {req.Licencia}"));
                    chunks.Add(e.PrintLine($"Email   : {req.Email}"));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("-- VEHICLE SPECS --"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                    chunks.Add(e.PrintLine($"Model   : {req.Modelo}"));
                    chunks.Add(e.PrintLine($"Class   : {req.Categoria}"));
                    chunks.Add(e.PrintLine($"VIN     : {req.Vin}"));
                    chunks.Add(e.PrintLine($"Garage  : {req.Garage}"));
                    chunks.Add(e.PrintLine("--------------------------------"));
                    chunks.Add(e.CenterAlign());
                    chunks.Add(e.PrintLine($"Stint   : {req.Duracion} min"));
                    chunks.Add(e.SetStyles(PrintStyle.Bold));
                    chunks.Add(e.PrintLine("*** PENDING ENTRY ***"));
                    chunks.Add(e.SetStyles(PrintStyle.None));
                });
                DispatchToHardware(bytes);
            } catch (Exception ex) { _logger.LogError(ex, "Hardware Dispatch Failure: Comprehensive Ticket."); throw; }
        }
    }
}
