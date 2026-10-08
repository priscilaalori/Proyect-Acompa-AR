using System;

namespace Acompañar.Proyecto.Models
{
    public class Recordatorio
    {
        public int IdRecordatorio { get; set; }

        public int? IdMedicamento { get; set; }

        public int? IdTurno { get; set; }

        public DateTime FechaHora { get; set; }

        public string Estado { get; set; }
    }
}