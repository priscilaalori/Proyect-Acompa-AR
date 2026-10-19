using MySql.Data.MySqlClient;

public sealed class TurnoMedicoRepository
{
    private readonly string _cadenaConexion;

    public TurnoMedicoRepository(IConfiguration configuration)
    {
        _cadenaConexion = configuration.GetConnectionString("MySql") ?? string.Empty;
    }

    public bool ConexionConfigurada =>
        !string.IsNullOrWhiteSpace(_cadenaConexion);

    public async Task<(bool Vinculado, long? IdTurno)> GuardarAsync(
        int idAcompanante,
        int idMayor,
        int? id,
        string? especialidad,
        string? nombreMedico,
        DateOnly fecha,
        string? hora,
        string? direccion,
        string? motivo,
        CancellationToken ct)
    {
        await using var conexion = new MySqlConnection(_cadenaConexion);
        await conexion.OpenAsync(ct);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        await using var comando = new MySqlCommand("""
            SELECT v.id_vinculacion
            FROM vinculacion v
            JOIN usuario m
                ON m.id_usuario = v.id_mayor
                AND m.estado = TRUE
                AND m.tipo_usuario = 'MAYOR'
            JOIN usuario a
                ON a.id_usuario = v.id_acompanante
                AND a.estado = TRUE
                AND a.tipo_usuario = 'ACOMPANANTE'
            WHERE v.id_mayor = @mayor
              AND v.id_acompanante = @usuario
              AND v.estado = 'ACEPTADA'
            FOR UPDATE;
            """, conexion, transaccion);

        comando.Parameters.AddWithValue("@mayor", idMayor);
        comando.Parameters.AddWithValue("@usuario", idAcompanante);

        var vinculacion = await comando.ExecuteScalarAsync(ct);

        if (vinculacion is null)
        {
            await transaccion.RollbackAsync(ct);
            return (false, null);
        }

        comando.Parameters.Clear();

        if (id.HasValue)
        {
            comando.CommandText = """
                SELECT id_turno
                FROM turno_medico
                WHERE id_turno = @id
                  AND id_mayor = @mayor
                  AND estado = 'PROGRAMADO'
                FOR UPDATE;
                """;

            comando.Parameters.AddWithValue("@id", id.Value);
            comando.Parameters.AddWithValue("@mayor", idMayor);

            var existente = await comando.ExecuteScalarAsync(ct);

            if (existente is null)
            {
                await transaccion.RollbackAsync(ct);
                return (true, null);
            }

            // Al editar o cancelar, el recordatorio anterior ya no sirve.
            comando.CommandText = """
                UPDATE recordatorio
                SET estado = 'CANCELADO'
                WHERE id_turno = @id
                  AND estado = 'PENDIENTE';
                """;

            await comando.ExecuteNonQueryAsync(ct);
        }

        comando.Parameters.Clear();
        comando.Parameters.AddWithValue("@mayor", idMayor);

        if (especialidad is null)
        {
            // Baja lógica: conservamos el turno y su historial.
            comando.Parameters.AddWithValue("@id", id!.Value);

            comando.CommandText = """
                UPDATE turno_medico
                SET estado = 'CANCELADO'
                WHERE id_turno = @id
                  AND id_mayor = @mayor
                  AND estado = 'PROGRAMADO';
                """;

            await comando.ExecuteNonQueryAsync(ct);

            await transaccion.CommitAsync(ct);
            return (true, id.Value);
        }

        comando.Parameters.AddWithValue("@especialidad", especialidad);
        comando.Parameters.AddWithValue("@medico", nombreMedico);
        comando.Parameters.AddWithValue("@fecha", fecha.ToDateTime(TimeOnly.MinValue));
        comando.Parameters.AddWithValue("@hora", TimeOnly.ParseExact(hora!, "HH:mm").ToTimeSpan());
        comando.Parameters.AddWithValue("@direccion", direccion);
        comando.Parameters.AddWithValue("@motivo",
            string.IsNullOrWhiteSpace(motivo) ? DBNull.Value : motivo);

        if (id.HasValue)
        {
            comando.Parameters.AddWithValue("@id", id.Value);

            comando.CommandText = """
                UPDATE turno_medico
                SET especialidad = @especialidad,
                    nombre_medico = @medico,
                    fecha = @fecha,
                    hora = @hora,
                    direccion = @direccion,
                    motivo = @motivo
                WHERE id_turno = @id
                  AND id_mayor = @mayor
                  AND estado = 'PROGRAMADO';
                """;

            await comando.ExecuteNonQueryAsync(ct);

            // Como en la carga de datos, el recordatorio sale un día antes del turno.
            comando.CommandText = """
                INSERT INTO recordatorio (id_turno, fecha_hora, estado)
                VALUES (@id, GREATEST(NOW(), TIMESTAMP(@fecha, @hora) - INTERVAL 1 DAY), 'PENDIENTE');
                """;

            await comando.ExecuteNonQueryAsync(ct);

            await transaccion.CommitAsync(ct);
            return (true, id.Value);
        }

        comando.CommandText = """
            INSERT INTO turno_medico
                (id_mayor, especialidad, nombre_medico,
                 fecha, hora, direccion, motivo, estado)
            VALUES
                (@mayor, @especialidad, @medico,
                 @fecha, @hora, @direccion, @motivo, 'PROGRAMADO');
            """;

        await comando.ExecuteNonQueryAsync(ct);

        var nuevoId = comando.LastInsertedId;

        comando.Parameters.AddWithValue("@id", nuevoId);
        comando.CommandText = """
            INSERT INTO recordatorio (id_turno, fecha_hora, estado)
            VALUES (@id, GREATEST(NOW(), TIMESTAMP(@fecha, @hora) - INTERVAL 1 DAY), 'PENDIENTE');
            """;

        await comando.ExecuteNonQueryAsync(ct);

        await transaccion.CommitAsync(ct);
        return (true, nuevoId);
    }
}