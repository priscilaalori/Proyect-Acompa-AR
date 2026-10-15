using AcompañAR.Proyecto.Repositories;
using AcompañAR.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace AcompañAR.Services
{
    public class RecuperacionPasswordService
    {
        private readonly RecuperacionPasswordRepository _repository;
        private readonly EmailService _emailService;
        private readonly UsuarioRepository _usuarioRepository;

        public RecuperacionPasswordService(
    RecuperacionPasswordRepository repository,
    EmailService emailService,
    UsuarioRepository usuarioRepository)
        {
            _repository = repository;
            _emailService = emailService;
            _usuarioRepository = usuarioRepository;
        }

        public async Task SolicitarRecuperacionAsync(string email)
        {
            int? idUsuario =
                await _repository.ObtenerIdUsuarioPorEmailAsync(email);

           
            if (idUsuario == null)
            {
                return;
            }

            // token aleatorio criptográficamente seguro.
            byte[] bytesToken = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToBase64String(bytesToken)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Guarda solamente el hash del token.
            string tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(token)));

            DateTime fechaExpiracion = DateTime.UtcNow.AddMinutes(30);

            await _repository.GuardarTokenAsync(
                idUsuario.Value,
                tokenHash,
                fechaExpiracion);

          
            string enlace =
                $"https://localhost:7005/nueva-password.html?token={Uri.EscapeDataString(token)}";

            string mensaje =
                "Recibimos una solicitud para restablecer tu contraseña de AcompañAR.\n\n" +
                $"Abrí este enlace para continuar: {enlace}\n\n" +
                "El enlace vence en 30 minutos. Si no solicitaste este cambio, " +
                "podés ignorar este correo.";

            await _emailService.EnviarCorreoAsync(
                email,
                "Recuperación de contraseña - AcompañAR",
                mensaje);
        }
        public async Task<bool> RestablecerPasswordAsync(
    string token,
    string nuevaPassword)
        {
            string tokenHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(token)));

            int? idUsuario =
                await _repository.ObtenerIdUsuarioPorTokenValidoAsync(tokenHash);

            if (idUsuario == null)
            {
                return false;
            }

            await _usuarioRepository.ActualizarPasswordAsync(
                idUsuario.Value,
                nuevaPassword);

            await _repository.MarcarTokenComoUtilizadoAsync(tokenHash);

            return true;
        }

    }
}