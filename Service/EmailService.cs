using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace jhampro.Service
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            var smtp = _config.GetSection("Smtp");
            var host = smtp["Host"];
            var port = int.Parse(smtp["Port"] ?? "587");
            var user = smtp["Username"];
            var pass = smtp["Password"];
            var from = smtp["From"] ?? "no-reply@localhost";
            var enableSsl = bool.Parse(smtp["EnableSsl"] ?? "true");

            using var message = new MailMessage(from, to, subject, body);
            message.IsBodyHtml = isHtml;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = enableSsl
            };

            await client.SendMailAsync(message);
        }
    }
}
