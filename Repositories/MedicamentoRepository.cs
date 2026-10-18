using Acompañar.Proyecto.Models;
using MySql.Data.MySqlClient;

namespace AcompañAR.Proyecto.Repositories;

public sealed class MedicamentoRepository
{
    private readonly string _cadenaConexion;

    public MedicamentoRepository(IConfiguration configuration)
    {
        _cadenaConexion = configuration.GetConnectionString("MySql") ?? string.Empty;
    }

    public bool ConexionConfigurada => !string.IsNullOrWhiteSpace(_cadenaConexion);

    // La vinculación se verifica dentro de la misma transacción que la escritura.
    public async Task<(bool Vinculado, long? IdMedicamento)> GuardarAsync(
        int idAcompanante, int idMayor, int? id, Medicamento? medicamento, CancellationToken ct)
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
            // Conservamos los avisos enviados y sus cumplimientos.
            comando.CommandText = "UPDATE recordatorio SET estado = 'CANCELADO' WHERE id_medicamento = @id AND estado = 'PENDIENTE'";
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
        await transaccion.CommitAsync(ct);
        return (true, identificador);
    }
}