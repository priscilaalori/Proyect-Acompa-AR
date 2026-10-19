using MySql.Data.MySqlClient;

namespace AcompañAR.Proyecto.Repositories;

public sealed class RecordatorioRepository
{
    private readonly string _cadenaConexion;

    public RecordatorioRepository(IConfiguration configuration)
    {
        _cadenaConexion = configuration.GetConnectionString("MySql") ?? string.Empty;
    }

    // Devuelve null si el acompañante no tiene una vinculación aceptada con la persona mayor.
    public async Task<List<object>?> ListarPendientesAsync(
        int idAcompanante, int idMayor, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        using var conexion = new MySqlConnection(_cadenaConexion);
        await conexion.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT v.id_vinculacion FROM vinculacion v
            JOIN usuario m ON m.id_usuario = v.id_mayor AND m.estado = TRUE AND m.tipo_usuario = 'MAYOR'
            JOIN usuario a ON a.id_usuario = v.id_acompanante AND a.estado = TRUE AND a.tipo_usuario = 'ACOMPANANTE'
            WHERE v.id_mayor = @mayor AND v.id_acompanante = @usuario AND v.estado = 'ACEPTADA';
            """, conexion);
        comando.Parameters.AddWithValue("@mayor", idMayor);
        comando.Parameters.AddWithValue("@usuario", idAcompanante);
        if (await comando.ExecuteScalarAsync(ct) is null) return null;

        comando.Parameters.AddWithValue("@desde", desde.ToDateTime(TimeOnly.MinValue));
        comando.Parameters.AddWithValue("@hasta", hasta.AddDays(1).ToDateTime(TimeOnly.MinValue));
        comando.CommandText = """
            SELECT r.id_recordatorio, COALESCE(m.nombre, t.especialidad),
                   CASE WHEN r.id_medicamento IS NOT NULL THEN 'Medicamento' ELSE 'Turno médico' END,
                   DATE_FORMAT(r.fecha_hora, '%Y-%m-%d'), TIME_FORMAT(r.fecha_hora, '%H:%i'), r.estado, c.estado
            FROM recordatorio r
            LEFT JOIN medicamento m ON m.id_medicamento = r.id_medicamento
            LEFT JOIN turno_medico t ON t.id_turno = r.id_turno
            LEFT JOIN cumplimiento c ON c.id_recordatorio = r.id_recordatorio
            WHERE (m.id_mayor = @mayor OR t.id_mayor = @mayor) AND r.estado = 'PENDIENTE'
              AND r.fecha_hora >= @desde AND r.fecha_hora < @hasta
            ORDER BY r.fecha_hora, r.id_recordatorio;
            """;
        var recordatorios = new List<object>();
        using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            recordatorios.Add(new { idRecordatorio = lector.GetInt32(0), nombre = lector.GetString(1), tipo = lector.GetString(2),
                fecha = lector.GetString(3), hora = lector.GetString(4), estado = lector.GetString(5),
                cumplimiento = lector.IsDBNull(6) ? null : lector.GetString(6) });
        return recordatorios;
    }

    // Lo puede registrar la persona mayor o un acompañante con vinculación aceptada.
    public async Task<(bool Existe, bool YaRegistrado)> RegistrarCumplimientoAsync(
        int idUsuario, int idRecordatorio, string estado, CancellationToken ct)
    {
        using var conexion = new MySqlConnection(_cadenaConexion);
        await conexion.OpenAsync(ct);
        using var transaccion = conexion.BeginTransaction();
        using var comando = new MySqlCommand("""
            SELECT r.id_recordatorio FROM recordatorio r
            LEFT JOIN medicamento m ON m.id_medicamento = r.id_medicamento
            LEFT JOIN turno_medico t ON t.id_turno = r.id_turno
            WHERE r.id_recordatorio = @id AND r.estado <> 'CANCELADO'
              AND (COALESCE(m.id_mayor, t.id_mayor) = @usuario OR EXISTS (
                  SELECT 1 FROM vinculacion v WHERE v.id_mayor = COALESCE(m.id_mayor, t.id_mayor)
                    AND v.id_acompanante = @usuario AND v.estado = 'ACEPTADA'))
            FOR UPDATE;
            """, conexion, transaccion);
        comando.Parameters.AddWithValue("@id", idRecordatorio);
        comando.Parameters.AddWithValue("@usuario", idUsuario);
        if (await comando.ExecuteScalarAsync(ct) is null) return (false, false);
        comando.CommandText = "SELECT id_cumplimiento FROM cumplimiento WHERE id_recordatorio = @id";
        if (await comando.ExecuteScalarAsync(ct) is not null) return (true, true);
        comando.Parameters.AddWithValue("@estado", estado);
        comando.CommandText = "INSERT INTO cumplimiento (id_recordatorio, estado) VALUES (@id, @estado)";
        await comando.ExecuteNonQueryAsync(ct);
        await transaccion.CommitAsync(ct);
        return (true, false);
    }
}
