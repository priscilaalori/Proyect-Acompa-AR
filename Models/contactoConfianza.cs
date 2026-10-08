using System;

namespace Acompañar.Proyecto.Models
{
    public class ContactoConfianza
    {
        public int IdContacto { get; set; }

        public int IdMayor { get; set; }

        public string Nombre { get; set; }

        public string Apellido { get; set; }

        public string Telefono { get; set; }

        public string TipoVinculo { get; set; }

        public bool Estado { get; set; }
    }
}