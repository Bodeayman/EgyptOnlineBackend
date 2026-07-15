
using System.Net;
using System.Net.Mail;
using Serilog;

namespace EgyptOnline.Infrastructure
{


    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var _host = _configuration["SMTP:host"];
            var _port = _configuration["SMTP:port"];
            var _username = _configuration["SMTP:username"];
            var _password = _configuration["SMTP:password"];

            // Validate SMTP configuration
            if (string.IsNullOrEmpty(_host))
            {
                Log.Error("SMTP host is not configured. Cannot send email to {Email}", toEmail);
                throw new InvalidOperationException("SMTP host is not configured in appsettings");
            }

            if (string.IsNullOrEmpty(_port))
            {
                Log.Error("SMTP port is not configured. Cannot send email to {Email}", toEmail);
                throw new InvalidOperationException("SMTP port is not configured in appsettings");
            }

            if (string.IsNullOrEmpty(_username))
            {
                Log.Error("SMTP username is not configured. Cannot send email to {Email}", toEmail);
                throw new InvalidOperationException("SMTP username is not configured in appsettings");
            }

            if (string.IsNullOrEmpty(_password))
            {
                Log.Error("SMTP password is not configured. Cannot send email to {Email}", toEmail);
                throw new InvalidOperationException("SMTP password is not configured in appsettings");
            }

            try
            {
                var client = new SmtpClient(_host, Convert.ToInt16(_port))
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_username, _password)
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(_username, "معاك 'تغيير كلمة المرور'"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
                Log.Information("Email sent successfully to {Email} with subject: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send email to {Email}. Subject: {Subject}", toEmail, subject);
                throw;
            }
        }
    }


}
