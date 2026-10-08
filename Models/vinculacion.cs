namespace Acompañar.Proyecto.Models
{
    public class Vinculacion
    {
        public int IdVinculacion { get; set; }

        public int IdMayor { get; set; }

        public int IdAcompanante { get; set; }

        public string TipoVinculo { get; set; }

        public string Estado { get; set; }

        public DateTime FechaVinculacion { get; set; }
    }
}