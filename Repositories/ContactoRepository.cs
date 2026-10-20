using MySql.Data.MySqlClient;
using AcompañAR.Proyecto.DTO;

namespace AcompañAR.Proyecto.Repositories
{
    public class ContactoRepository
    {
        private readonly string _cadenaConexion;

        public ContactoRepository(IConfiguration configuration)
        {
            _cadenaConexion = configuration
                .GetConnectionString("MySql") ?? string.Empty;
        }

        public bool ConexionConfigurada =>
            !string.IsNullOrWhiteSpace(_cadenaConexion);

        public async Task<bool> ExisteMayorAsync(
            int idMayor,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM usuario
                WHERE id_usuario = @idMayor
                  AND tipo_usuario = 'MAYOR'
                  AND estado = TRUE;
                """,
                conexion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            var resultado =
                await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado) > 0;
        }

        public async Task<bool> TieneVinculacionAceptadaAsync(
            int idAcompanante,
            int idMayor,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM vinculacion v
                JOIN usuario a ON a.id_usuario = v.id_acompanante
                JOIN usuario m ON m.id_usuario = v.id_mayor
                WHERE v.id_acompanante = @idAcompanante
                    AND v.id_mayor = @idMayor
                    AND v.estado = 'ACEPTADA'
                    AND a.estado = TRUE
                    AND a.tipo_usuario = 'ACOMPANANTE'
                    AND m.estado = TRUE
                    AND m.tipo_usuario = 'MAYOR';
                """,
                conexion);

            comando.Parameters
                .Add("@idAcompanante", MySqlDbType.Int32)
                .Value = idAcompanante;

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            var resultado = await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado) > 0;
        }

        public async Task<List<ContactoResponse>> ObtenerContactosAsync(
            int idMayor,
            CancellationToken cancellationToken)
        {
            var contactos = new List<ContactoResponse>();

            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT id_contacto, nombre, apellido,
                       telefono, tipo_vinculo
                FROM contacto_confianza
                WHERE id_mayor = @idMayor
                    AND estado = TRUE;
                """,
                conexion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            using var lector =
                await comando.ExecuteReaderAsync(cancellationToken);

            while (await lector.ReadAsync(cancellationToken))
            {
                contactos.Add(new ContactoResponse
                {
                    IdContacto = lector.GetInt32(0),
                    Nombre = lector.GetString(1),
                    Apellido = lector.IsDBNull(2)
                        ? null : lector.GetString(2),
                    Telefono = lector.GetString(3),
                    TipoVinculo = lector.GetString(4)
                });
            }

            return contactos;
        }

        public async Task<bool> ExisteTelefonoAsync(
            int idMayor,
            string telefono,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM contacto_confianza
                WHERE id_mayor = @idMayor
                    AND telefono = @telefono;
                """,
                conexion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comando.Parameters
                .Add("@telefono", MySqlDbType.VarChar)
                .Value = telefono;

            var resultado =
                await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado) > 0;
        }

        public async Task<(bool Vinculado, bool Duplicado, long? IdContacto)> RegistrarContactoAsync(
            int idAcompanante,
            int idMayor,
            string nombre,
            string? apellido,
            string telefono,
            string tipoVinculo,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            using var transaccion = conexion.BeginTransaction();

            using var consulta = new MySqlCommand(
                """
                SELECT v.id_vinculacion
                FROM vinculacion v
                JOIN usuario a ON a.id_usuario = v.id_acompanante
                JOIN usuario m ON m.id_usuario = v.id_mayor
                WHERE v.id_acompanante = @idAcompanante
                    AND v.id_mayor = @idMayor
                    AND v.estado = 'ACEPTADA'
                    AND a.estado = TRUE
                    AND a.tipo_usuario = 'ACOMPANANTE'
                    AND m.estado = TRUE
                    AND m.tipo_usuario = 'MAYOR'
                FOR UPDATE;
                """,
                conexion,
                transaccion);

            consulta.Parameters.AddWithValue("@idAcompanante", idAcompanante);
            consulta.Parameters.AddWithValue("@idMayor", idMayor);

            if (await consulta.ExecuteScalarAsync(cancellationToken) is null)
            {
                return (false, false, null);
            }

            using var verificar = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM contacto_confianza
                WHERE id_mayor = @idMayor
                    AND telefono = @telefono;
                """,
                conexion,
                transaccion);

            verificar.Parameters.AddWithValue("@idMayor", idMayor);
            verificar.Parameters.AddWithValue("@telefono", telefono);

            var cantidad = Convert.ToInt32(
                await verificar.ExecuteScalarAsync(cancellationToken));

            if (cantidad > 0)
            {
                return (true, true, null);
            }

            using var insertar = new MySqlCommand(
                """
                INSERT INTO contacto_confianza
                 (id_mayor, nombre, apellido, telefono, tipo_vinculo, estado)
                VALUES
                 (@idMayor, @nombre, @apellido, @telefono, @tipoVinculo, TRUE);
                """,
                conexion,
                transaccion);

            insertar.Parameters.AddWithValue("@idMayor", idMayor);
            insertar.Parameters.AddWithValue("@nombre", nombre);
            insertar.Parameters.AddWithValue(
                "@apellido", (object?)apellido ?? DBNull.Value);
            insertar.Parameters.AddWithValue("@telefono", telefono);
            insertar.Parameters.AddWithValue("@tipoVinculo", tipoVinculo);

            await insertar.ExecuteNonQueryAsync(cancellationToken);

            var idContacto = insertar.LastInsertedId;

            await transaccion.CommitAsync(cancellationToken);

            return (true, false, idContacto);
        }

        public async Task<bool> ExisteContactoAsync(
            int idMayor,
            int idContacto,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM contacto_confianza
                WHERE id_contacto = @idContacto
                    AND id_mayor = @idMayor
                    AND estado = TRUE;
                """,
                conexion);

            comando.Parameters
                .Add("@idContacto", MySqlDbType.Int32)
                .Value = idContacto;

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            var resultado =
                await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado) > 0;
        }

        public async Task<bool> ExisteTelefonoEnOtroContactoAsync(
            int idMayor,
            int idContacto,
            string telefono,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM contacto_confianza
                WHERE id_mayor = @idMayor
                    AND telefono = @telefono
                    AND id_contacto <> @idContacto;
                """,
                conexion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comando.Parameters
                .Add("@telefono", MySqlDbType.VarChar)
                .Value = telefono;

            comando.Parameters
                .Add("@idContacto", MySqlDbType.Int32)
                .Value = idContacto;

            var resultado =
                await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado) > 0;
        }

        public async Task<(bool Vinculado, bool Encontrado, bool Duplicado)> ActualizarContactoAsync(
            int idAcompanante,
            int idMayor,
            int idContacto,
            string nombre,
            string? apellido,
            string telefono,
            string tipoVinculo,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            using var transaccion = conexion.BeginTransaction();

            using var comando = new MySqlCommand(
                """
                SELECT v.id_vinculacion
                FROM vinculacion v
                JOIN usuario a ON a.id_usuario = v.id_acompanante
                JOIN usuario m ON m.id_usuario = v.id_mayor
                WHERE v.id_acompanante = @idAcompanante
                    AND v.id_mayor = @idMayor
                    AND v.estado = 'ACEPTADA'
                    AND a.estado = TRUE
                    AND a.tipo_usuario = 'ACOMPANANTE'
                    AND m.estado = TRUE
                    AND m.tipo_usuario = 'MAYOR'
                FOR UPDATE;
                """,
                conexion,
                transaccion);

            comando.Parameters.AddWithValue("@idAcompanante", idAcompanante);
            comando.Parameters.AddWithValue("@idMayor", idMayor);

            if (await comando.ExecuteScalarAsync(cancellationToken) is null)
            {
                return (false, false, false);
            }

            comando.Parameters.Clear();

            comando.CommandText =
                """
                SELECT id_contacto
                FROM contacto_confianza
                WHERE id_contacto = @idContacto
                    AND id_mayor = @idMayor
                    AND estado = TRUE
                FOR UPDATE;
                """;

            comando.Parameters.AddWithValue("@idContacto", idContacto);
            comando.Parameters.AddWithValue("@idMayor", idMayor);

            if (await comando.ExecuteScalarAsync(cancellationToken) is null)
            {
                return (true, false, false);
            }

            comando.Parameters.Clear();

            comando.CommandText =
                """
                SELECT COUNT(*)
                FROM contacto_confianza
                WHERE id_mayor = @idMayor
                    AND telefono = @telefono
                    AND id_contacto <> @idContacto;
                """;

            comando.Parameters.AddWithValue("@idMayor", idMayor);
            comando.Parameters.AddWithValue("@telefono", telefono);
            comando.Parameters.AddWithValue("@idContacto", idContacto);

            var cantidad = Convert.ToInt32(
                await comando.ExecuteScalarAsync(cancellationToken));

            if (cantidad > 0)
            {
                return (true, true, true);
            }

            comando.Parameters.Clear();

            comando.CommandText =
                """
                UPDATE contacto_confianza
                SET nombre = @nombre,
                    apellido = @apellido,
                    telefono = @telefono,
                    tipo_vinculo = @tipoVinculo
                WHERE id_contacto = @idContacto
                    AND id_mayor = @idMayor
                    AND estado = TRUE;
                """;

            comando.Parameters.AddWithValue("@idContacto", idContacto);
            comando.Parameters.AddWithValue("@idMayor", idMayor);
            comando.Parameters.AddWithValue("@nombre", nombre);
            comando.Parameters.AddWithValue(
                "@apellido", (object?)apellido ?? DBNull.Value);
            comando.Parameters.AddWithValue("@telefono", telefono);
            comando.Parameters.AddWithValue("@tipoVinculo", tipoVinculo);

            await comando.ExecuteNonQueryAsync(cancellationToken);

            await transaccion.CommitAsync(cancellationToken);

            return (true, true, false);
        }

        public async Task<(bool Vinculado, bool Encontrado)> DesactivarContactoAsync(
            int idAcompanante,
            int idMayor,
            int idContacto,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            using var transaccion = conexion.BeginTransaction();

            using var comando = new MySqlCommand(
                """
                SELECT v.id_vinculacion
                FROM vinculacion v
                JOIN usuario a ON a.id_usuario = v.id_acompanante
                JOIN usuario m ON m.id_usuario = v.id_mayor
                WHERE v.id_acompanante = @idAcompanante
                    AND v.id_mayor = @idMayor
                    AND v.estado = 'ACEPTADA'
                    AND a.estado = TRUE
                    AND a.tipo_usuario = 'ACOMPANANTE'
                    AND m.estado = TRUE
                    AND m.tipo_usuario = 'MAYOR'
                FOR UPDATE;
                """,
                conexion,
                transaccion);

            comando.Parameters.AddWithValue("@idAcompanante", idAcompanante);
            comando.Parameters.AddWithValue("@idMayor", idMayor);

            if (await comando.ExecuteScalarAsync(cancellationToken) is null)
            {
                return (false, false);
            }
            comando.Parameters.Clear();

            comando.CommandText =
                """
                UPDATE contacto_confianza
                SET estado = FALSE
                WHERE id_contacto = @idContacto
                    AND id_mayor = @idMayor
                    AND estado = TRUE;
                """;

            comando.Parameters.AddWithValue("@idContacto", idContacto);
            comando.Parameters.AddWithValue("@idMayor", idMayor);

            var filasAfectadas =
                await comando.ExecuteNonQueryAsync(cancellationToken);

            await transaccion.CommitAsync(cancellationToken);

            return (true, filasAfectadas > 0);
        }
    }
}
