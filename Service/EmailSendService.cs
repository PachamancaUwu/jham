using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace jhampro.Service
{
    public class EmailSendService
    {
        private readonly IConfiguration _config;

        public EmailSendService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<string> SendEmailAsync(string to, string subject, string htmlContent)
        {
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("SENDGRID_API_KEY es NULL. Asegúrate de cargar .env.local");
            }

            var client = new SendGridClient(apiKey);

            var from = new EmailAddress("mathyplay0@gmail.com", "JHAM Abogados");
            var toEmail = new EmailAddress(to);

            var msg = MailHelper.CreateSingleEmail(from, toEmail, subject, "", htmlContent);

            var response = await client.SendEmailAsync(msg);

            Console.WriteLine("SendGrid status: " + response.StatusCode);

            var body = await response.Body.ReadAsStringAsync();
            Console.WriteLine("SendGrid body: " + body);

            return response.StatusCode.ToString();
        }
    }
}