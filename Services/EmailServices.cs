using System.Net;
using System.Net.Mail;

namespace AcompañAR.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnviarCorreoAsync(
            string destinatario,
            string asunto,
            string mensaje)
        {
            string remitente = _configuration["EmailSettings:SenderEmail"]
                ?? throw new InvalidOperationException(
                    "No está configurado el correo remitente.");

            string password = _configuration["EmailSettings:AppPassword"]
                ?? throw new InvalidOperationException(
                    "No está configurada la contraseña de aplicación.");

            using var correo = new MailMessage();
            correo.From = new MailAddress(remitente);
            correo.To.Add(destinatario);
            correo.Subject = asunto;
            correo.Body = mensaje;
            correo.IsBodyHtml = false;

            using var smtp = new SmtpClient("smtp.gmail.com", 587);
            smtp.EnableSsl = true;
            smtp.Credentials = new NetworkCredential(remitente, password);

            await smtp.SendMailAsync(correo);
        }
    }
}