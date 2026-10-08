using System;

namespace Acompañar.Proyecto.Models
{
    public class Cumplimiento
    {
        public int IdCumplimiento { get; set; }

        public int IdRecordatorio { get; set; }

        public DateTime FechaHoraRegistro { get; set; }

        public string Estado { get; set; }
    }
}