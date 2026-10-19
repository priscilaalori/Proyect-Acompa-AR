
using AcompañAR.Proyecto.DTO;
using AcompañAR.Proyecto.Repositories;
using System.Globalization;

namespace AcompañAR.Services;

public enum EstadoOperacionTurnoMedico
{
    Exito,
    DatosInvalidos,
    SinConexion,
    SinVinculacion,
    NoEncontrado
}

public sealed record ResultadoTurnoMedico(
    EstadoOperacionTurnoMedico Estado,
    string Mensaje,
    long? IdTurno = null);

public sealed class TurnoMedicoService
{
    private readonly TurnoMedicoRepository _repository;

    public TurnoMedicoService(TurnoMedicoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ResultadoTurnoMedico> GuardarAsync(
        int idAcompanante,
        int idMayor,
        int? id,
        TurnoMedicoRequest? solicitud,
        CancellationToken cancellationToken)
    {
        if (idAcompanante <= 0 || idMayor <= 0 || id is <= 0)
            return new(EstadoOperacionTurnoMedico.DatosInvalidos,
                "Identificador inválido.");

        if (solicitud is not null &&
            Validar(solicitud) is string error)
            return new(EstadoOperacionTurnoMedico.DatosInvalidos, error);

        if (!_repository.ConexionConfigurada)
            return new(EstadoOperacionTurnoMedico.SinConexion,
                "Falta configurar MySQL.");

        var resultado = await _repository.GuardarAsync(
            idAcompanante,
            idMayor,
            id,
            solicitud?.Especialidad?.Trim(),
            solicitud?.NombreMedico?.Trim(),
            solicitud?.Fecha ?? default,
            solicitud?.Hora,
            solicitud?.Direccion?.Trim(),
            solicitud?.Motivo?.Trim(),
            cancellationToken);

        if (!resultado.Vinculado)
            return new(EstadoOperacionTurnoMedico.SinVinculacion,
                "No tenés una vinculación aceptada con esta persona mayor.");

        if (resultado.IdTurno is null)
            return new(EstadoOperacionTurnoMedico.NoEncontrado,
                "El turno no existe o ya no se puede modificar.");

        return new(
            EstadoOperacionTurnoMedico.Exito,
            solicitud is null ? "Turno médico cancelado." : "Turno médico guardado.",
            resultado.IdTurno);
    }

    private static string? Validar(TurnoMedicoRequest solicitud)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Especialidad) ||
            solicitud.Especialidad.Trim().Length > 100)
            return "Ingresá una especialidad de hasta 100 caracteres.";

        if (string.IsNullOrWhiteSpace(solicitud.NombreMedico) ||
            solicitud.NombreMedico.Trim().Length > 150)
            return "Ingresá el nombre del profesional, hasta 150 caracteres.";

        if (solicitud.Fecha.Year < 1000)
            return "Ingresá una fecha válida para el turno.";

        if (!TimeOnly.TryParseExact(
                solicitud.Hora,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
            return "Ingresá un horario válido.";

        if (string.IsNullOrWhiteSpace(solicitud.Direccion) ||
            solicitud.Direccion.Trim().Length > 200)
            return "Ingresá una dirección de hasta 200 caracteres.";

        if (solicitud.Motivo?.Trim().Length > 255)
            return "El motivo no puede superar los 255 caracteres.";

        return null;
    }
}