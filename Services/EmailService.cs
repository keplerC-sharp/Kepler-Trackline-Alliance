using System.Net;
using System.Net.Mail;

namespace Kepler_Trackline_Alliance.Services;

/// <summary>
/// Infrastructure service for automated email communication.
/// Handles external SMTP handshake and provides fault-tolerant dispatch.
/// </summary>
public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously dispatches an email message.
    /// Gracefully degrades if SMTP configuration is missing to ensure system uptime.
    /// </summary>
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var host = _config["SMTP:Host"];
            var portStr = _config["SMTP:Port"];
            var user = _config["SMTP:User"];
            var pass = _config["SMTP:Pass"];

            // Verify infrastructure readiness before attempting handshake.
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
            {
                _logger.LogWarning("SMTP Infrastructure not configured. Skipping email dispatch to {To}.", to);
                return;
            }

            var port = int.TryParse(portStr, out var p) ? p : 587;

            var smtp = new SmtpClient(host)
            {
                Port        = port,
                Credentials = new NetworkCredential(user, pass),
                EnableSsl   = true
            };

            var mail = new MailMessage(user!, to, subject, body);
            await smtp.SendMailAsync(mail);
            
            _logger.LogInformation("Email successfully dispatched to {To}.", to);
        }
        catch (Exception ex)
        {
            // Fail-safe: Email dispatch errors should never interrupt primary application flow.
            _logger.LogError(ex, "Mailing failure to {To} with subject '{Subject}'.", to, subject);
        }
    }
}
