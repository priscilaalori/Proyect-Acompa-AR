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

        public async Task<long> RegistrarContactoAsync(
            int idMayor,
            string nombre,
            string? apellido,
            string telefono,
            string tipoVinculo,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                INSERT INTO contacto_confianza
                (id_mayor, nombre, apellido, telefono,
                 tipo_vinculo, estado)
                VALUES
                (@idMayor, @nombre, @apellido, @telefono,
                 @tipoVinculo, TRUE);
                """,
                conexion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comando.Parameters
                .Add("@nombre", MySqlDbType.VarChar)
                .Value = nombre;

            comando.Parameters
                .Add("@apellido", MySqlDbType.VarChar)
                .Value = (object?)apellido ?? DBNull.Value;

            comando.Parameters
                .Add("@telefono", MySqlDbType.VarChar)
                .Value = telefono;

            comando.Parameters
                .Add("@tipoVinculo", MySqlDbType.VarChar)
                .Value = tipoVinculo;

            await comando.ExecuteNonQueryAsync(cancellationToken);

            return comando.LastInsertedId;
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

        public async Task<int> ActualizarContactoAsync(
            int idMayor,
            int idContacto,
            string nombre,
            string? apellido,
            string telefono,
            string tipoVinculo,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                UPDATE contacto_confianza
                SET nombre = @nombre,
                    apellido = @apellido,
                    telefono = @telefono,
                    tipo_vinculo = @tipoVinculo
                WHERE id_contacto = @idContacto
                    AND id_mayor = @idMayor
                    AND estado = TRUE;
                """,
                conexion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comando.Parameters
                .Add("@idContacto", MySqlDbType.Int32)
                .Value = idContacto;

            comando.Parameters
                .Add("@nombre", MySqlDbType.VarChar)
                .Value = nombre;

            comando.Parameters
                .Add("@apellido", MySqlDbType.VarChar)
                .Value = (object?)apellido ?? DBNull.Value;

            comando.Parameters
                .Add("@telefono", MySqlDbType.VarChar)
                .Value = telefono;

            comando.Parameters
                .Add("@tipoVinculo", MySqlDbType.VarChar)
                .Value = tipoVinculo;

            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<int> DesactivarContactoAsync(
            int idMayor,
            int idContacto,
            CancellationToken cancellationToken)
        {
            using var conexion =
                new MySqlConnection(_cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                UPDATE contacto_confianza
                SET estado = FALSE
                WHERE id_contacto = @idContacto
                    AND id_mayor = @idMayor
                    AND estado = TRUE;
                """,
                conexion);

            comando.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comando.Parameters
                .Add("@idContacto", MySqlDbType.Int32)
                .Value = idContacto;

            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
