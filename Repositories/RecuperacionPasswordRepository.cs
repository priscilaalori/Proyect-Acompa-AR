using MySql.Data.MySqlClient;

namespace AcompañAR.Repositories
{
    public class RecuperacionPasswordRepository
    {
        private readonly string _connectionString;

        public RecuperacionPasswordRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("MySql")
                ?? throw new InvalidOperationException(
                    "No se encontró la cadena de conexión MySql.");
        }

        public async Task<int?> ObtenerIdUsuarioPorEmailAsync(string email)
        {
            using var conexion = new MySqlConnection(_connectionString);
            await conexion.OpenAsync();

            const string consulta = @"
                SELECT id_usuario
                FROM usuario
                WHERE email = @Email
                  AND estado = TRUE
                LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@Email", email);

            var resultado = await comando.ExecuteScalarAsync();

            if (resultado == null || resultado == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(resultado);
        }


        public async Task GuardarTokenAsync( int idUsuario, string tokenHash,DateTime fechaExpiracion)
        {
            using var conexion = new MySqlConnection(_connectionString);
            await conexion.OpenAsync();

            const string consulta = @"
        INSERT INTO recuperacion_password
            (id_usuario, token_hash, fecha_expiracion, utilizado)
        VALUES
            (@IdUsuario, @TokenHash, @FechaExpiracion, FALSE);";

            using var comando = new MySqlCommand(consulta, conexion);

            comando.Parameters.AddWithValue("@IdUsuario", idUsuario);
            comando.Parameters.AddWithValue("@TokenHash", tokenHash);
            comando.Parameters.AddWithValue("@FechaExpiracion", fechaExpiracion);

            await comando.ExecuteNonQueryAsync();
        }
        public async Task<int?> ObtenerIdUsuarioPorTokenValidoAsync(
    string tokenHash)
        {
            using var conexion = new MySqlConnection(_connectionString);
            await conexion.OpenAsync();

            const string consulta = @"
        SELECT id_usuario
        FROM recuperacion_password
        WHERE token_hash = @TokenHash
          AND utilizado = FALSE
          AND fecha_expiracion > UTC_TIMESTAMP()
        LIMIT 1;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@TokenHash", tokenHash);

            var resultado = await comando.ExecuteScalarAsync();

            if (resultado == null || resultado == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(resultado);
        }
        public async Task MarcarTokenComoUtilizadoAsync(string tokenHash)
        {
            using var conexion = new MySqlConnection(_connectionString);
            await conexion.OpenAsync();

            const string consulta = @"
        UPDATE recuperacion_password
        SET utilizado = TRUE
        WHERE token_hash = @TokenHash
          AND utilizado = FALSE;";

            using var comando = new MySqlCommand(consulta, conexion);
            comando.Parameters.AddWithValue("@TokenHash", tokenHash);

            await comando.ExecuteNonQueryAsync();
        }

    }
}