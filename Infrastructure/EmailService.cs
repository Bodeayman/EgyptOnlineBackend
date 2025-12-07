
using System.Net;
using System.Net.Mail;

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
        }
    }


}
