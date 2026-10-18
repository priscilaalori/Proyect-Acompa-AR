namespace AcompañAR.Proyecto.DTO;

public sealed class MedicamentoRequest
{
    public string? Nombre { get; set; }
    public string? Dosis { get; set; }
    public string? TipoFrecuencia { get; set; }
    public int? Intervalo { get; set; }
    public string? Horario { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
}
