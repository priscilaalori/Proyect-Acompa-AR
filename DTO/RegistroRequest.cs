using System;

namespace Acompañar.Proyecto.DTO
{
    public class RegistroRequest
    {
        public string Nombre { get; set; }

        public string Apellido { get; set; }

        public string DNI { get; set; }

        public DateTime FechaNacimiento { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }

        public string Telefono { get; set; }

        public string TipoUsuario { get; set; }
    }
}