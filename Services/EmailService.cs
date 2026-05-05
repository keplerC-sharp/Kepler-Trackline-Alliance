using System.Net;
using System.Net.Mail;

namespace Kepler_Trackline_Alliance.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var host = _config["SMTP:Host"];
            var portStr = _config["SMTP:Port"];
            var user = _config["SMTP:User"];
            var pass = _config["SMTP:Pass"];

            // Si no hay configuración SMTP, simplemente se omite
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
            {
                _logger.LogWarning("SMTP no configurado. Se omite el envío de email a {To}", to);
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
        }
        catch (Exception ex)
        {
            // El email nunca debe tirar la app
            _logger.LogError(ex, "Error al enviar email a {To} con asunto '{Subject}'", to, subject);
        }
    }
}
