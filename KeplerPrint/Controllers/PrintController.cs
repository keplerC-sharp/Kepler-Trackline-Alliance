using KeplerPrint.Models;
using KeplerPrint.Services;
using Microsoft.AspNetCore.Mvc;

namespace KeplerPrint.Controllers
{
    /// <summary>
    /// Dispatch controller for hardware print jobs.
    /// Exposes endpoints for local network ticket generation.
    /// </summary>
    [ApiController]
    [Route("api/print")]
    public class PrintController : ControllerBase
    {
        private readonly PrintService             _print;
        private readonly ILogger<PrintController> _logger;

        public PrintController(PrintService print, ILogger<PrintController> logger)
        {
            _print  = print;
            _logger = logger;
        }

        /// <summary>
        /// Connectivity probe for the frontend to verify print server readiness.
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health() =>
            Ok(new { ok = true, timestamp = DateTime.Now, cluster = "Kepler-TrackAlliance-Uplink" });

        /// <summary>
        /// Dispatches a Pilot ID ticket to the local thermal printer.
        /// </summary>
        [HttpPost("pilot")]
        public IActionResult Pilot([FromBody] PrintPilotoRequest req)
        {
            try { 
                _print.PrintPilotTicket(req); 
                return Ok(new { ok = true }); 
            }
            catch (Exception ex) { 
                _logger.LogError(ex, "Hardware print failure for Pilot Ticket.");
                return StatusCode(500, new { ok = false, error = ex.Message }); 
            }
        }

        /// <summary>
        /// Dispatches a Vehicle Technical ticket to the local thermal printer.
        /// </summary>
        [HttpPost("vehicle")]
        public IActionResult Vehicle([FromBody] PrintVehiculoRequest req)
        {
            try { 
                _print.PrintVehicleTicket(req); 
                return Ok(new { ok = true }); 
            }
            catch (Exception ex) { 
                _logger.LogError(ex, "Hardware print failure for Vehicle Ticket.");
                return StatusCode(500, new { ok = false, error = ex.Message }); 
            }
        }

        /// <summary>
        /// Dispatches a Queue Entry (Turn) ticket to the local thermal printer.
        /// </summary>
        [HttpPost("turn")]
        public IActionResult Turn([FromBody] PrintTurnoRequest req)
        {
            try { 
                _print.PrintTurnTicket(req); 
                return Ok(new { ok = true }); 
            }
            catch (Exception ex) { 
                _logger.LogError(ex, "Hardware print failure for Turn Ticket.");
                return StatusCode(500, new { ok = false, error = ex.Message }); 
            }
        }

        /// <summary>
        /// Dispatches a comprehensive registration dossier for offline record keeping.
        /// </summary>
        [HttpPost("full")]
        public IActionResult Full([FromBody] PrintRegistroCompletoRequest req)
        {
            try { 
                _print.PrintFullRegistration(req); 
                return Ok(new { ok = true }); 
            }
            catch (Exception ex) { 
                _logger.LogError(ex, "Hardware print failure for Full Registration.");
                return StatusCode(500, new { ok = false, error = ex.Message }); 
            }
        }
    }
}
