using System;

namespace Acompañar.Proyecto.Models
{
    public class Medicamento
    {
        public int IdMedicamento { get; set; }

        public int IdMayor { get; set; }

        public string Nombre { get; set; }

        public string Dosis { get; set; }

        public string Frecuencia { get; set; }

        public TimeSpan Horario { get; set; }

        public DateTime FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public bool Estado { get; set; }
    }
}