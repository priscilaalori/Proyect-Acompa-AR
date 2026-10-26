using Acompañar.Proyecto.Models;
using MySql.Data.MySqlClient;
using AcompañAR.Services;

namespace AcompañAR.Proyecto.Repositories;

public sealed class MedicamentoRepository
{
    // El alias m se comparte con las consultas del panel y la validación de escritura.
    public const string TomaUnicaRespondidaSql = """
        (m.frecuencia IN ('Toma unica', 'Toma única') AND EXISTS (
            SELECT 1 FROM recordatorio r JOIN cumplimiento c ON c.id_recordatorio = r.id_recordatorio
            WHERE r.id_medicamento = m.id_medicamento))
        """;
    private readonly string _cadenaConexion;

    public MedicamentoRepository(IConfiguration configuration)
    {
        _cadenaConexion = configuration.GetConnectionString("MySql") ?? string.Empty;
    }

    public bool ConexionConfigurada => !string.IsNullOrWhiteSpace(_cadenaConexion);

    // Repara datos sin agenda y renueva tratamientos sin fecha de fin.
    public async Task AsegurarTomasActualesAsync(int idMayor, CancellationToken ct)
    {
        using var conexion = new MySqlConnection(_cadenaConexion);
        await conexion.OpenAsync(ct);
        using var transaccion = conexion.BeginTransaction();
        using var comando = new MySqlCommand("""
            SELECT id_medicamento, fecha_inicio, horario, fecha_fin, frecuencia
            FROM medicamento WHERE id_mayor = @mayor AND estado = TRUE
              AND fecha_inicio < CURDATE() + INTERVAL 2 DAY
              AND (fecha_fin IS NULL OR fecha_fin >= CURDATE())
            ORDER BY id_medicamento FOR UPDATE
            """, conexion, transaccion);
        comando.Parameters.AddWithValue("@mayor", idMayor);
        var medicamentos = new List<(int Id, DateTime Inicio, DateTime? Fin, string Frecuencia)>();
        using (var lector = await comando.ExecuteReaderAsync(ct))
            while (await lector.ReadAsync(ct))
                medicamentos.Add((lector.GetInt32(0), lector.GetDateTime(1).Date + (TimeSpan)lector.GetValue(2),
                    lector.IsDBNull(3) ? null : lector.GetDateTime(3), lector.GetString(4)));
        comando.CommandText = "SELECT CURDATE()";
        var hoy = Convert.ToDateTime(await comando.ExecuteScalarAsync(ct)).Date;
        comando.Parameters.AddWithValue("@medicamento", 0);
        comando.Parameters.AddWithValue("@toma", hoy);
        comando.CommandText = """
            INSERT INTO recordatorio (id_medicamento, fecha_hora, estado)
            SELECT @medicamento, @toma, 'PENDIENTE' FROM DUAL
            WHERE NOT EXISTS (
                SELECT 1 FROM recordatorio r
                LEFT JOIN cumplimiento c ON c.id_recordatorio = r.id_recordatorio
                WHERE r.id_medicamento = @medicamento AND r.fecha_hora = @toma
                  AND (r.estado <> 'CANCELADO' OR c.id_cumplimiento IS NOT NULL))
            """;
        foreach (var medicamento in medicamentos)
            foreach (var toma in AgendaMedicamento.Tomas(medicamento.Inicio, medicamento.Fin,
                medicamento.Frecuencia, hoy, hoy.AddDays(2)))
            {
                comando.Parameters["@medicamento"].Value = medicamento.Id;
                comando.Parameters["@toma"].Value = toma;
                await comando.ExecuteNonQueryAsync(ct);
            }
        await transaccion.CommitAsync(ct);
    }

    // La vinculación se verifica dentro de la misma transacción que la escritura.
    public async Task<(bool Vinculado, long? IdMedicamento)> GuardarAsync(
        int idAcompanante, int idMayor, int? id, Medicamento? medicamento, TimeSpan intervalo, CancellationToken ct)
    {
        using var conexion = new MySqlConnection(_cadenaConexion);
        await conexion.OpenAsync(ct);
        using var transaccion = conexion.BeginTransaction();
        using var comando = new MySqlCommand("""
            SELECT v.id_vinculacion FROM vinculacion v
            JOIN usuario m ON m.id_usuario = v.id_mayor AND m.estado = TRUE AND m.tipo_usuario = 'MAYOR'
            JOIN usuario a ON a.id_usuario = v.id_acompanante AND a.estado = TRUE AND a.tipo_usuario = 'ACOMPANANTE'
            WHERE v.id_mayor = @mayor AND v.id_acompanante = @usuario AND v.estado = 'ACEPTADA'
            FOR UPDATE;
            """, conexion, transaccion);
        comando.Parameters.AddWithValue("@mayor", idMayor);
        comando.Parameters.AddWithValue("@usuario", idAcompanante);
        if (await comando.ExecuteScalarAsync(ct) is null) return new(false, null);
        comando.Parameters.AddWithValue("@id", id.HasValue ? id.Value : DBNull.Value);
        if (id.HasValue)
        {
            comando.CommandText = "SELECT id_medicamento FROM medicamento WHERE id_medicamento = @id AND id_mayor = @mayor AND estado = TRUE FOR UPDATE";
            if (await comando.ExecuteScalarAsync(ct) is null)
                return new(true, null);
            comando.CommandText = $"SELECT {TomaUnicaRespondidaSql} FROM medicamento m WHERE m.id_medicamento = @id";
            if (Convert.ToBoolean(await comando.ExecuteScalarAsync(ct)))
                throw new InvalidOperationException("La toma única ya fue respondida. El medicamento quedó en el historial y no se puede modificar ni dar de baja.");
            // Conservamos los avisos enviados y sus cumplimientos.
            comando.CommandText = """
                UPDATE recordatorio r LEFT JOIN cumplimiento c ON c.id_recordatorio = r.id_recordatorio
                SET r.estado = 'CANCELADO'
                WHERE r.id_medicamento = @id AND r.estado = 'PENDIENTE' AND c.id_cumplimiento IS NULL
                """;
            await comando.ExecuteNonQueryAsync(ct);
        }
        if (medicamento is null)
            comando.CommandText = "UPDATE medicamento SET estado = FALSE WHERE id_medicamento = @id AND id_mayor = @mayor";
        else
        {
            comando.Parameters.AddWithValue("@nombre", medicamento.Nombre);
            comando.Parameters.AddWithValue("@dosis", medicamento.Dosis);
            comando.Parameters.AddWithValue("@frecuencia", medicamento.Frecuencia);
            comando.Parameters.AddWithValue("@horario", medicamento.Horario);
            comando.Parameters.AddWithValue("@inicio", medicamento.FechaInicio);
            var fin = medicamento.FechaFin;
            comando.Parameters.AddWithValue("@fin", fin.HasValue ? fin.Value : DBNull.Value);
            comando.CommandText = id.HasValue
                ? "UPDATE medicamento SET nombre=@nombre, dosis=@dosis, frecuencia=@frecuencia, horario=@horario, fecha_inicio=@inicio, fecha_fin=@fin WHERE id_medicamento=@id AND id_mayor=@mayor"
                : "INSERT INTO medicamento (id_mayor,nombre,dosis,frecuencia,horario,fecha_inicio,fecha_fin,estado) VALUES (@mayor,@nombre,@dosis,@frecuencia,@horario,@inicio,@fin,TRUE)";
        }
        await comando.ExecuteNonQueryAsync(ct);
        var identificador = id.HasValue ? id.Value : comando.LastInsertedId;
        if (medicamento is not null)
        {
            // Incluye las tomas de hoy aunque ya haya pasado la hora para poder responderlas.
            comando.Parameters.AddWithValue("@medicamento", identificador);
            comando.Parameters.AddWithValue("@fechaHora", DateTime.MinValue);
            comando.CommandText = """
                INSERT INTO recordatorio (id_medicamento, fecha_hora, estado)
                SELECT @medicamento, @fechaHora, 'PENDIENTE' FROM DUAL
                WHERE NOT EXISTS (
                    SELECT 1 FROM recordatorio r
                    LEFT JOIN cumplimiento c ON c.id_recordatorio = r.id_recordatorio
                    WHERE r.id_medicamento = @medicamento AND r.fecha_hora = @fechaHora
                      AND (r.estado <> 'CANCELADO' OR c.id_cumplimiento IS NOT NULL))
                """;
            var hasta = (medicamento.FechaFin ?? DateTime.Today.AddDays(30)).AddDays(1);
            foreach (var toma in AgendaMedicamento.Tomas(medicamento.FechaInicio + medicamento.Horario,
                medicamento.FechaFin, medicamento.Frecuencia, DateTime.Today, hasta))
            {
                comando.Parameters["@fechaHora"].Value = toma;
                await comando.ExecuteNonQueryAsync(ct);
            }
        }
        await transaccion.CommitAsync(ct);
        return (true, identificador);
    }
}
