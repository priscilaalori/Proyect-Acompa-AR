using MySql.Data.MySqlClient;

namespace AcompañAR.Proyecto.Repositories
{
    public class VinculacionRepository
    {
        private readonly string _cadenaConexion;

        public VinculacionRepository(IConfiguration configuration)
        {
            _cadenaConexion = configuration
                .GetConnectionString("MySql") ?? string.Empty;
        }

        public bool ConexionConfigurada =>
            !string.IsNullOrWhiteSpace(_cadenaConexion);

        public async Task<List<object>> ListarUsuariosAsync(
            CancellationToken cancellationToken)
        {
            var usuarios = new List<object>();

            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT id_usuario, nombre, apellido,
                       tipo_usuario, codigo_mayor
                FROM usuario
                WHERE estado = TRUE
                  AND tipo_usuario IN ('MAYOR', 'ACOMPANANTE')
                ORDER BY tipo_usuario, apellido, nombre;
                """,
                conexion);

            using var lector =
                await comando.ExecuteReaderAsync(cancellationToken);

            while (await lector.ReadAsync(cancellationToken))
            {
                usuarios.Add(new
                {
                    idUsuario = lector.GetInt32(0),
                    nombre = lector.GetString(1),
                    apellido = lector.GetString(2),
                    tipoUsuario = lector.GetString(3),
                    codigoMayor = lector.IsDBNull(4)
                        ? null : lector.GetString(4)
                });
            }

            return usuarios;
        }

        public async Task<List<object>> BuscarUsuariosAsync(
            string tipo,
            string buscar,
            CancellationToken ct)
        {
            var usuarios = new List<object>();

            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(ct);

            using var comando = new MySqlCommand(
                """
                SELECT id_usuario, nombre, apellido,
                       tipo_usuario, codigo_mayor, email
                FROM usuario
                WHERE estado = TRUE
                    AND tipo_usuario = @tipo
                    AND (
                        nombre LIKE @patron
                        OR apellido LIKE @patron
                        OR CONCAT(nombre, ' ', apellido) LIKE @patron
                        OR codigo_mayor = @buscar
                        OR email = @buscar
                        OR id_usuario = @id
                    )
                ORDER BY apellido, nombre, id_usuario
                LIMIT 10;
                """,
                conexion);

            comando.Parameters.AddWithValue("@tipo", tipo);
            comando.Parameters.AddWithValue("@buscar", buscar);
            comando.Parameters.AddWithValue("@patron", $"%{buscar}%");

            int.TryParse(buscar, out int idBuscado);
            comando.Parameters.AddWithValue("@id", idBuscado);

            using var lector = await comando.ExecuteReaderAsync(ct);

            while (await lector.ReadAsync(ct))
            {
                usuarios.Add(new
                {
                    idUsuario = lector.GetInt32(0),
                    nombre = lector.GetString(1),
                    apellido = lector.GetString(2),
                    tipoUsuario = lector.GetString(3),
                    codigoMayor = lector.IsDBNull(4)
                        ? null : lector.GetString(4),
                    email = lector.GetString(5)
                });
            }

            return usuarios;
        }

        public async Task<List<object>> ListarVinculacionesAsync(
            CancellationToken cancellationToken)
        {
            var vinculaciones = new List<object>();

            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT v.id_vinculacion, v.id_mayor, v.id_acompanante,
                       m.nombre, m.apellido,
                       a.nombre, a.apellido,
                       v.tipo_vinculo, v.estado,
                       DATE_FORMAT(v.fecha_vinculacion, '%Y-%m-%d')
                FROM vinculacion v
                JOIN usuario m ON m.id_usuario = v.id_mayor
                JOIN usuario a ON a.id_usuario = v.id_acompanante
                ORDER BY v.id_vinculacion;
                """,
                conexion);

            using var lector =
                await comando.ExecuteReaderAsync(cancellationToken);

            while (await lector.ReadAsync(cancellationToken))
            {
                vinculaciones.Add(new
                {
                    idVinculacion = lector.GetInt32(0),
                    idMayor = lector.GetInt32(1),
                    idAcompanante = lector.GetInt32(2),
                    mayor = $"{lector.GetString(3)} {lector.GetString(4)}",
                    acompanante = $"{lector.GetString(5)} {lector.GetString(6)}",
                    tipoVinculo = lector.GetString(7),
                    estado = lector.GetString(8),
                    fechaVinculacion = lector.GetString(9)
                });
            }

            return vinculaciones;
        }

        public async Task<(List<object> Vinculaciones, long Total)> BuscarVinculacionesAsync(
            string buscar,
            string tipoBusqueda,
            string estado,
            int pagina,
            int limite,
            CancellationToken ct)
        {
            var vinculaciones = new List<object>();

            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(ct);

            const string filtro =
                """
                FROM vinculacion v
                JOIN usuario m ON m.id_usuario = v.id_mayor
                JOIN usuario a ON a.id_usuario = v.id_acompanante
                WHERE (@estado = '' OR v.estado = @estado)
                    AND (
                        @buscar = ''
                        OR (@tipoBusqueda = 'nombre' AND (
                            m.nombre LIKE @patron
                            OR m.apellido LIKE @patron
                            OR CONCAT(m.nombre, ' ', m.apellido) LIKE @patron
                            OR a.nombre LIKE @patron
                            OR a.apellido LIKE @patron
                            OR CONCAT(a.nombre, ' ', a.apellido) LIKE @patron
                        ))
                        OR (@tipoBusqueda = 'vinculacion'
                            AND v.id_vinculacion = @id)
                        OR (@tipoBusqueda = 'mayor'
                            AND v.id_mayor = @id)
                        OR (@tipoBusqueda = 'acompanante'
                            AND v.id_acompanante = @id)
                   )
                """;

            using var comando = new MySqlCommand("", conexion);

            comando.Parameters.AddWithValue("@buscar", buscar);
            comando.Parameters.AddWithValue("@tipoBusqueda", tipoBusqueda);
            comando.Parameters.AddWithValue("@estado", estado);
            comando.Parameters.AddWithValue("@patron", $"%{buscar}%");

            int.TryParse(buscar, out int idBuscado);
            comando.Parameters.AddWithValue("@id", idBuscado);

            comando.CommandText = $"SELECT COUNT(*) {filtro}";

            long total = Convert.ToInt64(
                await comando.ExecuteScalarAsync(ct));

            comando.CommandText =
                $"""
                SELECT v.id_vinculacion, v.id_mayor, v.id_acompanante,
                        m.nombre, m.apellido,
                        a.nombre, a.apellido,
                        v.tipo_vinculo, v.estado,
                        DATE_FORMAT(v.fecha_vinculacion, '%Y-%m-%d')
                {filtro}
                ORDER BY v.id_vinculacion DESC
                LIMIT @limite OFFSET @offset;
                """;

            comando.Parameters.AddWithValue("@limite", limite);
            comando.Parameters.AddWithValue(
                "@offset", (pagina - 1) * limite);

            using var lector = await comando.ExecuteReaderAsync(ct);

            while (await lector.ReadAsync(ct))
            {
                vinculaciones.Add(new
                {
                    idVinculacion = lector.GetInt32(0),
                    idMayor = lector.GetInt32(1),
                    idAcompanante = lector.GetInt32(2),
                    mayor = $"{lector.GetString(3)} {lector.GetString(4)}",
                    acompanante = $"{lector.GetString(5)} {lector.GetString(6)}",
                    tipoVinculo = lector.GetString(7),
                    estado = lector.GetString(8),
                    fechaVinculacion = lector.GetString(9)
                });
            }

            return (vinculaciones, total);
        }

        public async Task<(bool MayorValido, bool AcompananteValido, bool Duplicado, long? IdVinculacion)> RegistrarVinculacionAsync(
            int idMayor,
            int idAcompanante,
            string tipoVinculo,
            string estado,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var transaccion = conexion.BeginTransaction();

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM usuario
                WHERE id_usuario = @idMayor
                    AND tipo_usuario = 'MAYOR'
                    AND estado = TRUE;
                """,
                conexion,
                transaccion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            if (Convert.ToInt32(
                await comando.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                return (false, false, false, null);
            }

            comando.Parameters.Clear();

            comando.CommandText =
                """
                SELECT COUNT(*)
                FROM usuario
                WHERE id_usuario = @idAcompanante
                    AND tipo_usuario = 'ACOMPANANTE'
                    AND estado = TRUE;
                """;

            comando.Parameters
                .Add("@idAcompanante", MySqlDbType.Int32)
                .Value = idAcompanante;

            if (Convert.ToInt32(
                await comando.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                return (true, false, false, null);
            }

            comando.Parameters.Clear();

            comando.CommandText =
                """
                SELECT COUNT(*)
                FROM vinculacion
                WHERE id_mayor = @idMayor
                    AND id_acompanante = @idAcompanante;
                """;

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comando.Parameters
                .Add("@idAcompanante", MySqlDbType.Int32)
                .Value = idAcompanante;

            if (Convert.ToInt32(
                await comando.ExecuteScalarAsync(cancellationToken)) > 0)
            {
                return (true, true, true, null);
            }

            comando.Parameters.Clear();

            comando.CommandText =
                """
                INSERT INTO vinculacion
                 (id_mayor, id_acompanante, tipo_vinculo, estado)
                VALUES
                 (@idMayor, @idAcompanante, @tipoVinculo, @estado);
                """;

            comando.Parameters.AddWithValue("@idMayor", idMayor);
            comando.Parameters.AddWithValue("@idAcompanante", idAcompanante);
            comando.Parameters.AddWithValue("@tipoVinculo", tipoVinculo);
            comando.Parameters.AddWithValue("@estado", estado);

            await comando.ExecuteNonQueryAsync(cancellationToken);

            var idVinculacion = comando.LastInsertedId;

            await transaccion.CommitAsync(cancellationToken);

            return (true, true, false, idVinculacion);
        }

        public async Task<bool> ActualizarVinculacionAsync(
            int idVinculacion,
            string tipoVinculo,
            string estado,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                UPDATE vinculacion
                SET tipo_vinculo = @tipoVinculo,
                    estado = @estado
                WHERE id_vinculacion = @idVinculacion;
                """,
                conexion);

            comando.Parameters
                .Add("@tipoVinculo", MySqlDbType.VarChar)
                .Value = tipoVinculo;

            comando.Parameters
                .Add("@estado", MySqlDbType.VarChar)
                .Value = estado;

            comando.Parameters
                .Add("@idVinculacion", MySqlDbType.Int32)
                .Value = idVinculacion;

            return await comando
                .ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> EliminarVinculacionAsync(
            int idVinculacion,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                DELETE FROM vinculacion
                WHERE id_vinculacion = @idVinculacion;
                """,
                conexion);

            comando.Parameters
                .Add("@idVinculacion", MySqlDbType.Int32)
                .Value = idVinculacion;

            return await comando
                .ExecuteNonQueryAsync(cancellationToken) > 0;
        }
    }
}
