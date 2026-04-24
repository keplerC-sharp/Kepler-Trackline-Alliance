using System.Net;
using System.Net.Mail;

namespace Kepler_Trackline_Alliance.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var smtp = new SmtpClient(_config["SMTP:Host"])
            {
                Port = int.Parse(_config["SMTP:Port"]),
                Credentials = new NetworkCredential(
                    _config["SMTP:User"],
                    _config["SMTP:Pass"]
                ),
                EnableSsl = true
            };

            var mail = new MailMessage(_config["SMTP:User"], to, subject, body);

            await smtp.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}