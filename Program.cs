using AcompañAR.Proyecto.DTO;
using MySql.Data.MySqlClient;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// Endpoint de diagnóstico disponible sólo durante el desarrollo.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/prueba-mysql", async () =>
    {
        var cadenaConexion =
            app.Configuration.GetConnectionString("MySql");

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            return Results.Problem(
                title: "Falta configurar la conexión MySql.",
                statusCode: 500);
        }

        try
        {
            using var conexion = new MySqlConnection(cadenaConexion);

            await conexion.OpenAsync();

            using var comando = new MySqlCommand(
                "SELECT COUNT(*) FROM usuario;",
                conexion);

            var resultado = await comando.ExecuteScalarAsync();
            var cantidadUsuarios = Convert.ToInt64(resultado);

            return Results.Ok(new
            {
                mensaje = "Conexión con MySQL exitosa.",
                cantidadUsuarios
            });
        }
        catch (MySqlException ex)
        {
            app.Logger.LogError(
                ex,
                "Falló la prueba de conexión con MySQL.");

            return Results.Problem(
                title: "No se pudo consultar MySQL.",
                detail: "Revisá el registro de errores del servidor.",
                statusCode: 503);
        }
    })
    .WithName("ProbarConexionMySql")
    .WithOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/auth/login", async (
        LoginRequest solicitud,
        HttpContext contexto,
        CancellationToken cancellationToken) =>
    {

        var email = solicitud.Email?.Trim();
        var password = solicitud.Password;

        if (string.IsNullOrWhiteSpace(email)
            || email.Length > 150
            || !MailAddress.TryCreate(email, out var direccion)
            || direccion.Address != email
            || string.IsNullOrEmpty(password)
            || password.Length > 1024)
        {
            return Results.BadRequest(new
            {
                mensaje = "Ingresá un email y una contraseña válidos."
            });
        }


        var cadenaConexion =
            app.Configuration.GetConnectionString("MySql");

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            return Results.Problem(
                title: "Falta configurar la conexión con MySQL.",
                statusCode: 500);
        }

        try
        {
            using var conexion =
                new MySqlConnection(cadenaConexion);

            await conexion.OpenAsync(cancellationToken);


            using var comando = new MySqlCommand(
                """
                SELECT id_usuario, nombre, apellido,
                       email, tipo_usuario, password
                FROM usuario
                WHERE email = @email
                  AND estado = TRUE
                LIMIT 1;
                """,
                conexion);

            comando.Parameters
                .Add("@email", MySqlDbType.VarChar)
                .Value = email;

            using var lector =
                await comando.ExecuteReaderAsync(cancellationToken);

            if (!await lector.ReadAsync(cancellationToken))
            {
                return Results.Json(new
                {
                    mensaje = "Email o contraseña incorrectos."
                }, statusCode: 401);
            }


            var passwordGuardada = lector.GetString(5);

            var coincide = CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(passwordGuardada)),
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(password)));

            if (!coincide)
            {
                return Results.Json(new
                {
                    mensaje = "Email o contraseña incorrectos."
                }, statusCode: 401);
            }

            await contexto.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, lector.GetInt32(0).ToString()),
                    new Claim(ClaimTypes.Name, lector.GetString(1)),
                    new Claim(ClaimTypes.Role, lector.GetString(4))
                }, CookieAuthenticationDefaults.AuthenticationScheme)));

            return Results.Ok(new
            {
                mensaje = "Credenciales correctas.",
                usuario = new
                {
                    idUsuario = lector.GetInt32(0),
                    nombre = lector.GetString(1),
                    apellido = lector.GetString(2),
                    email = lector.GetString(3),
                    tipoUsuario = lector.GetString(4)
                }
            });
        }
        catch (MySqlException ex)
        {
            app.Logger.LogError(
                ex,
                "Falló la validación de credenciales.");

            return Results.Problem(
                title: "No se pudo validar el ingreso. Intentá más tarde.",
                statusCode: 503);
        }
    })
    .WithName("ValidarLogin")
    .WithTags("Autenticación")
    .WithOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/mayores/{idMayor:int}/contactos", async (
        int idMayor,
        CancellationToken cancellationToken) =>
    {
        if (idMayor <= 0)
        {
            return Results.BadRequest(new
            {
                mensaje = "El ID de la persona mayor no es válido."
            });
        }

        var cadenaConexion =
            app.Configuration.GetConnectionString("MySql");

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            return Results.Problem(
                title: "Falta configurar la conexión con MySQL.",
                statusCode: 500);
        }

        try
        {
            using var conexion =
                new MySqlConnection(cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comandoMayor = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM usuario
                WHERE id_usuario = @idMayor
                  AND tipo_usuario = 'MAYOR'
                  AND estado = TRUE;
                """,
                conexion);

            comandoMayor.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            var resultadoMayor =
                await comandoMayor.ExecuteScalarAsync(cancellationToken);

            if (Convert.ToInt32(resultadoMayor) == 0)
            {
                return Results.NotFound(new
                {
                    mensaje = "La persona mayor no existe."
                });
            }

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

            var contactos = new List<object>();

            while (await lector.ReadAsync(cancellationToken))
            {
                contactos.Add(new
                {
                    idContacto = lector.GetInt32(0),
                    nombre = lector.GetString(1),
                    apellido = lector.IsDBNull(2)
                        ? null : lector.GetString(2),
                    telefono = lector.GetString(3),
                    tipoVinculo = lector.GetString(4)
                });
            }

            return Results.Ok(contactos);
        }
        catch (MySqlException ex)
        {
            app.Logger.LogError(
                ex,
                "Falló la consulta de contactos de confianza.");

            return Results.Problem(
                title: "No se pudieron consultar los contactos.",
                statusCode: 503);
        }
    })
    .WithName("ConsultarContactos")
    .WithTags("Contactos")
    .WithOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/mayores/{idMayor:int}/contactos", async (
        int idMayor,
        CrearContactoRequest solicitud,
        CancellationToken cancellationToken) =>
    {
        var nombre = solicitud.Nombre?.Trim();
        var apellido = solicitud.Apellido?.Trim();
        var telefono = solicitud.Telefono?.Trim();
        var tipoVinculo = solicitud.TipoVinculo?.Trim();

        if (idMayor <= 0 ||
            string.IsNullOrWhiteSpace(nombre) ||
            nombre.Length > 100 ||
            (apellido?.Length ?? 0) > 100 ||
            string.IsNullOrWhiteSpace(telefono) ||
            telefono.Length > 30 ||
            string.IsNullOrWhiteSpace(tipoVinculo) ||
            tipoVinculo.Length > 50)
        {
            return Results.BadRequest(new
            {
                mensaje = "Los datos del contacto no son válidos."
            });
        }

        var cadenaConexion =
            app.Configuration.GetConnectionString("MySql");

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            return Results.Problem(
                title: "Falta configurar la conexión con MySQL.",
                statusCode: 500);
        }

        try
        {
            using var conexion =
                new MySqlConnection(cadenaConexion);

            await conexion.OpenAsync(cancellationToken);

            using var comandoMayor = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM usuario
                WHERE id_usuario = @idMayor
                  AND tipo_usuario = 'MAYOR'
                  AND estado = TRUE;
                """,
                conexion);

            comandoMayor.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            var resultadoMayor =
                await comandoMayor.ExecuteScalarAsync(cancellationToken);

            if (Convert.ToInt32(resultadoMayor) == 0)
            {
                return Results.NotFound(new
                {
                    mensaje = "La persona mayor no existe."
                });
            }

            using var comandoDuplicado = new MySqlCommand(
                """
                SELECT COUNT(*)
                FROM contacto_confianza
                WHERE id_mayor = @idMayor
                  AND telefono = @telefono;
                """,
                conexion);

            comandoDuplicado.Parameters
                .Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comandoDuplicado.Parameters
                .Add("@telefono", MySqlDbType.VarChar)
                .Value = telefono;

            var resultadoDuplicado =
                await comandoDuplicado.ExecuteScalarAsync(cancellationToken);

            if (Convert.ToInt32(resultadoDuplicado) > 0)
            {
                return Results.Conflict(new
                {
                    mensaje = "Este contacto ya está registrado."
                });
            }

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

            comando.Parameters.Add("@idMayor", MySqlDbType.Int32)
                .Value = idMayor;

            comando.Parameters.Add("@nombre", MySqlDbType.VarChar)
                .Value = nombre;

            comando.Parameters.Add("@apellido", MySqlDbType.VarChar)
                .Value = (object?)apellido ?? DBNull.Value;

            comando.Parameters.Add("@telefono", MySqlDbType.VarChar)
                .Value = telefono;

            comando.Parameters.Add("@tipoVinculo", MySqlDbType.VarChar)
                .Value = tipoVinculo;

            await comando.ExecuteNonQueryAsync(cancellationToken);

            return Results.Json(new
            {
                mensaje = "Contacto registrado correctamente.",
                idContacto = comando.LastInsertedId
            }, statusCode: 201);
        }
        catch (MySqlException ex)
        {
            app.Logger.LogError(
                ex,
                "Falló el registro del contacto de confianza.");

            return Results.Problem(
                title: "No se pudo registrar el contacto.",
                statusCode: 503);
        }
    })
    .WithName("RegistrarContacto")
    .WithTags("Contactos")
    .WithOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/auth/logout", async (HttpContext contexto) =>
    {
        await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    });

    app.MapGet("/api/acompanante/panel", async (
        HttpContext contexto, CancellationToken cancellationToken) =>
    {
        contexto.Response.Headers.CacheControl = "no-store";
        if (!contexto.User.IsInRole("ACOMPANANTE"))
            return Results.Json(new { mensaje = "Esta página es para personas acompañantes." }, statusCode: 403);

        var cadena = app.Configuration.GetConnectionString("MySql");
        if (string.IsNullOrWhiteSpace(cadena))
            return Results.Problem(title: "Falta configurar la conexión con MySQL.", statusCode: 500);

        try
        {
            using var conexion = new MySqlConnection(cadena);
            await conexion.OpenAsync(cancellationToken);
            using var comando = new MySqlCommand(
                """
                SELECT u.id_usuario, u.nombre, u.apellido
                FROM vinculacion v
                JOIN usuario u ON u.id_usuario = v.id_mayor
                JOIN usuario a ON a.id_usuario = v.id_acompanante
                WHERE v.id_acompanante = @id AND v.estado = 'ACEPTADA'
                  AND u.estado = TRUE AND u.tipo_usuario = 'MAYOR'
                  AND a.estado = TRUE AND a.tipo_usuario = 'ACOMPANANTE'
                ORDER BY v.fecha_vinculacion, v.id_vinculacion
                LIMIT 1;
                """, conexion);
            comando.Parameters.Add("@id", MySqlDbType.Int32).Value =
                int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            int idMayor;
            object mayor;
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
            {
                if (!await lector.ReadAsync(cancellationToken))
                    return Results.Ok(new { acompanante = contexto.User.Identity!.Name, mayor = (object?)null,
                        medicamentos = Array.Empty<object>(), recordatorios = Array.Empty<object>(), turnos = Array.Empty<object>() });
                idMayor = lector.GetInt32(0);
                mayor = new { idUsuario = idMayor, nombre = lector.GetString(1), apellido = lector.GetString(2) };
            }

            // Las tres consultas se limitan al adulto obtenido de la vinculación del usuario autenticado.
            comando.Parameters.Clear();
            comando.Parameters.Add("@idMayor", MySqlDbType.Int32).Value = idMayor;
            comando.CommandText = """
                SELECT nombre, dosis, frecuencia, TIME_FORMAT(horario, '%H:%i'),
                       DATE_FORMAT(fecha_inicio, '%Y-%m-%d'), DATE_FORMAT(fecha_fin, '%Y-%m-%d')
                FROM medicamento WHERE id_mayor = @idMayor AND estado = TRUE
                ORDER BY horario, id_medicamento;
                """;
            var medicamentos = new List<object>();
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
                while (await lector.ReadAsync(cancellationToken))
                    medicamentos.Add(new { nombre = lector.GetString(0), dosis = lector.GetString(1),
                        frecuencia = lector.GetString(2), horario = lector.GetString(3),
                        fechaInicio = lector.GetString(4), fechaFin = lector.IsDBNull(5) ? null : lector.GetString(5) });

            comando.CommandText = """
                SELECT t.especialidad, t.nombre_medico, DATE_FORMAT(t.fecha, '%Y-%m-%d'),
                       TIME_FORMAT(t.hora, '%H:%i'), t.direccion, t.motivo, t.estado
                FROM turno_medico t WHERE t.id_mayor = @idMayor ORDER BY t.fecha, t.hora, t.id_turno;
                """;
            var turnos = new List<object>();
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
                while (await lector.ReadAsync(cancellationToken))
                    turnos.Add(new { especialidad = lector.GetString(0), medico = lector.GetString(1),
                        fecha = lector.GetString(2), hora = lector.GetString(3), direccion = lector.GetString(4),
                        motivo = lector.IsDBNull(5) ? null : lector.GetString(5), estado = lector.GetString(6) });

            comando.CommandText = """
                SELECT COALESCE(m.nombre, t.especialidad),
                       CASE WHEN r.id_medicamento IS NOT NULL THEN 'Medicamento' ELSE 'Turno médico' END,
                       DATE_FORMAT(r.fecha_hora, '%Y-%m-%d'), TIME_FORMAT(r.fecha_hora, '%H:%i'), r.estado
                FROM recordatorio r
                LEFT JOIN medicamento m ON m.id_medicamento = r.id_medicamento
                LEFT JOIN turno_medico t ON t.id_turno = r.id_turno
                WHERE m.id_mayor = @idMayor OR t.id_mayor = @idMayor
                ORDER BY r.fecha_hora, r.id_recordatorio;
                """;
            var recordatorios = new List<object>();
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
                while (await lector.ReadAsync(cancellationToken))
                    recordatorios.Add(new { nombre = lector.GetString(0), tipo = lector.GetString(1),
                        fecha = lector.GetString(2), hora = lector.GetString(3), estado = lector.GetString(4) });

            return Results.Ok(new { acompanante = contexto.User.Identity!.Name, mayor, medicamentos, recordatorios, turnos });
        }
        catch (MySqlException ex)
        {
            app.Logger.LogError(ex, "Falló la consulta del panel del acompañante.");
            return Results.Problem(title: "No pudimos cargar la información. Intentá más tarde.", statusCode: 503);
        }
    }).RequireAuthorization().WithTags("Acompañante");
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
