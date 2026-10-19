using Acompañar.Proyecto.Models;
using AcompañAR.Proyecto.DTO;
using AcompañAR.Proyecto.Repositories;

namespace AcompañAR.Services;

public enum EstadoOperacionMedicamento
{
    Exito, DatosInvalidos, SinConexion, SinVinculacion, NoEncontrado
}

public sealed record ResultadoMedicamento(
    EstadoOperacionMedicamento Estado, string Mensaje, long? IdMedicamento = null);

public sealed class MedicamentoService
{
    private readonly MedicamentoRepository _repository;

    public MedicamentoService(MedicamentoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultadoMedicamento> GuardarAsync(
        int idAcompanante, int idMayor, int? id, MedicamentoRequest? solicitud,
        CancellationToken cancellationToken)
    {
        if (idAcompanante <= 0 || idMayor <= 0 || id is <= 0)
            return new(EstadoOperacionMedicamento.DatosInvalidos, "Identificador inválido.");
        if (solicitud is not null && Validar(solicitud) is string error)
            return new(EstadoOperacionMedicamento.DatosInvalidos, error);
        if (!_repository.ConexionConfigurada)
            return new(EstadoOperacionMedicamento.SinConexion, "Falta configurar MySQL.");

        Medicamento? medicamento = solicitud is null ? null : new Medicamento
        {
            IdMayor = idMayor,
            Nombre = solicitud.Nombre!.Trim(),
            Dosis = solicitud.Dosis!.Trim(),
            Frecuencia = solicitud.TipoFrecuencia switch
            {
                "DIARIA" => "Una vez por dia",
                "HORAS" => $"Cada {solicitud.Intervalo} horas",
                "DIAS" => $"Cada {solicitud.Intervalo} dias",
                _ => "Toma unica"
            },
            Horario = TimeOnly.ParseExact(solicitud.Horario!, "HH:mm").ToTimeSpan(),
            FechaInicio = solicitud.FechaInicio.ToDateTime(TimeOnly.MinValue),
            FechaFin = (solicitud.TipoFrecuencia == "UNICA" ? solicitud.FechaInicio : solicitud.FechaFin)
                ?.ToDateTime(TimeOnly.MinValue),
            Estado = true
        };

        var intervalo = solicitud?.TipoFrecuencia switch
        {
            "HORAS" => TimeSpan.FromHours(solicitud.Intervalo!.Value),
            "DIAS" => TimeSpan.FromDays(solicitud.Intervalo!.Value),
            _ => TimeSpan.FromDays(1)
        };

        var resultado = await _repository.GuardarAsync(
            idAcompanante, idMayor, id, medicamento, intervalo, cancellationToken);
        if (!resultado.Vinculado)
            return new(EstadoOperacionMedicamento.SinVinculacion, "No tenés una vinculación aceptada con esta persona mayor.");
        if (resultado.IdMedicamento is null)
            return new(EstadoOperacionMedicamento.NoEncontrado, "El medicamento no existe o ya fue dado de baja.");
        return new(EstadoOperacionMedicamento.Exito,
            solicitud is null ? "Medicamento dado de baja." : "Medicamento guardado.", resultado.IdMedicamento);
    }

    private static string? Validar(MedicamentoRequest solicitud)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Nombre) || solicitud.Nombre.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(solicitud.Dosis) || solicitud.Dosis.Trim().Length > 50)
            return "Ingresá nombre (hasta 100 caracteres) y dosis (hasta 50).";
        if (!TimeOnly.TryParseExact(solicitud.Horario, "HH:mm", out _) || solicitud.FechaInicio.Year < 1000 ||
            (solicitud.FechaFin.HasValue && solicitud.FechaFin < solicitud.FechaInicio))
            return "Revisá el horario y las fechas del tratamiento.";
        if (solicitud.TipoFrecuencia is not ("DIARIA" or "HORAS" or "DIAS" or "UNICA") ||
            (solicitud.TipoFrecuencia == "HORAS" && (solicitud.Intervalo is null or < 1 or > 168)) ||
            (solicitud.TipoFrecuencia == "DIAS" && (solicitud.Intervalo is null or < 1 or > 365)))
            return "Elegí una frecuencia válida: entre 1 y 168 horas o entre 1 y 365 días.";
        if (solicitud.TipoFrecuencia == "UNICA" && solicitud.FechaFin.HasValue && solicitud.FechaFin != solicitud.FechaInicio)
            return "La toma única debe finalizar en la misma fecha de inicio.";
        return null;
    }

}
