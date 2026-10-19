namespace AcompañAR.Proyecto.DTO;

public sealed class TurnoMedicoRequest
{
    public string? Especialidad { get; set; }

    public string? NombreMedico { get; set; }

    public DateOnly Fecha { get; set; }

    public string? Hora { get; set; }

    public string? Direccion { get; set; }

    public string? Motivo { get; set; }
}