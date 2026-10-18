
using Acompañar.Proyecto.DTO;
using AcompañAR.Proyecto.DTO;
using MySql.Data.MySqlClient;

namespace AcompañAR.Proyecto.Repositories
{
    public class UsuarioRepository
    {
        private readonly string _cadenaConexion;

        public UsuarioRepository(IConfiguration configuration)
        {
            _cadenaConexion = configuration
                .GetConnectionString("MySql") ?? string.Empty;
        }

        public bool ConexionConfigurada =>
            !string.IsNullOrWhiteSpace(_cadenaConexion);

        public async Task<bool> ExisteEmailAsync(
            string email,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM usuario
                WHERE email = @email;
                """,
                conexion);

            comando.Parameters
                .Add("@email", MySqlDbType.VarChar)
                .Value = email;

            var resultado =
                await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado) > 0;
        }

        public async Task<bool> ExisteDniAsync(
            string dni,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM usuario
                WHERE dni = @dni;
                """,
                conexion);

            comando.Parameters
                .Add("@dni", MySqlDbType.VarChar)
                .Value = dni;

            var resultado =
                await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado) > 0;
        }

        public async Task<long> RegistrarUsuarioAsync(
            RegistroRequest usuario,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                INSERT INTO usuario
                (
                    nombre,
                    apellido,
                    dni,
                    fecha_nacimiento,
                    email,
                    password,
                    telefono,
                    tipo_usuario,
                    estado,
                    codigo_mayor
                )
                VALUES
                (
                    @nombre,
                    @apellido,
                    @dni,
                    @fechaNacimiento,
                    @email,
                    @password,
                    @telefono,
                    @tipoUsuario,
                    TRUE,
                    NULL
                );
                """,
                conexion);

            comando.Parameters.Add("@nombre", MySqlDbType.VarChar)
                .Value = usuario.Nombre;

            comando.Parameters.Add("@apellido", MySqlDbType.VarChar)
                .Value = usuario.Apellido;

            comando.Parameters.Add("@dni", MySqlDbType.VarChar)
                .Value = usuario.DNI;

            comando.Parameters.Add("@fechaNacimiento", MySqlDbType.Date)
                .Value = usuario.FechaNacimiento.Date;

            comando.Parameters.Add("@email", MySqlDbType.VarChar)
                .Value = usuario.Email;

            comando.Parameters.Add("@password", MySqlDbType.VarChar)
                .Value = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(
                            usuario.Password)));

            comando.Parameters.Add("@telefono", MySqlDbType.VarChar)
                .Value = usuario.Telefono;

            comando.Parameters.Add("@tipoUsuario", MySqlDbType.VarChar)
                .Value = usuario.TipoUsuario;

            await comando.ExecuteNonQueryAsync(cancellationToken);

            return comando.LastInsertedId;
        }

        public async Task AsignarCodigoMayorAsync(
            long idUsuario,
            string codigoMayor,
            CancellationToken cancellationToken)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            using var comando = new MySqlCommand(
                """
                UPDATE usuario
                SET codigo_mayor = @codigoMayor
                WHERE id_usuario = @idUsuario;
                """,
                conexion);

            comando.Parameters.Add("@codigoMayor", MySqlDbType.VarChar)
                .Value = codigoMayor;

            comando.Parameters.Add("@idUsuario", MySqlDbType.Int64)
                .Value = idUsuario;

            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task ActualizarPasswordAsync(
            int idUsuario,
            string nuevaPassword,
            CancellationToken cancellationToken = default)
        {
            using var conexion = new MySqlConnection(_cadenaConexion);
            await conexion.OpenAsync(cancellationToken);

            const string consulta = @"
        UPDATE usuario
        SET password = @Password
        WHERE id_usuario = @IdUsuario;";

            using var comando = new MySqlCommand(consulta, conexion);

            comando.Parameters.Add("@Password", MySqlDbType.VarChar).Value =
                Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(nuevaPassword)));

            comando.Parameters.Add("@IdUsuario", MySqlDbType.Int32).Value =
                idUsuario;

            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

    }
}