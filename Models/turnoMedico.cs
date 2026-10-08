using System;

namespace Acompañar.Proyecto.Models
{
    public class TurnoMedico
    {
        public int IdTurno { get; set; }

        public int IdMayor { get; set; }

        public string Especialidad { get; set; }

        public string NombreMedico { get; set; }

        public DateTime Fecha { get; set; }

        public TimeSpan Hora { get; set; }

        public string Direccion { get; set; }

        public string Motivo { get; set; }

        public string Estado { get; set; }
    }
}