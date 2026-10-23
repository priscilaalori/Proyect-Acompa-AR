using AcompañAR.Services;
using Acompañar.Proyecto.DTO;
using AcompañAR.Proyecto.DTO;
using AcompañAR.Proyecto.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MySql.Data.MySqlClient;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<AcompañAR.Services.EmailService>();
builder.Services.AddTransient<AcompañAR.Services.RecuperacionPasswordService>();
builder.Services.AddTransient<AcompañAR.Repositories.RecuperacionPasswordRepository>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddScoped<ContactoRepository>();
builder.Services.AddScoped<TurnoMedicoRepository>();
builder.Services.AddScoped<TurnoMedicoService>();
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<MedicamentoRepository>();
builder.Services.AddScoped<MedicamentoService>();
builder.Services.AddScoped<RecordatorioRepository>();
builder.Services.AddScoped<VinculacionRepository>();
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

// Android utiliza HTTP local durante el desarrollo.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
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
                       email, tipo_usuario, password, codigo_mayor
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

            var passwordIngresadaHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(password)));

            var coincide = string.Equals(
                passwordGuardada,
                passwordIngresadaHash,
                StringComparison.OrdinalIgnoreCase);
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
                    tipoUsuario = lector.GetString(4),
                    codigoMayor = lector.IsDBNull(6) ? null : lector.GetString(6)
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
    app.MapPost("/api/auth/registro", async (
        RegistroRequest solicitud,
        UsuarioRepository repositorio,
        CancellationToken cancellationToken) =>
    {
        var nombre = solicitud.Nombre?.Trim();
        var apellido = solicitud.Apellido?.Trim();
        var documento = solicitud.DNI?.Trim();
        var correoElectronico = solicitud.Email?.Trim();
        var telefono = solicitud.Telefono?.Trim();
        var tipoUsuario = solicitud.TipoUsuario?.Trim().ToUpperInvariant();
        var contraseña = solicitud.Password;
        var fechaNacimiento = solicitud.FechaNacimiento;

        if (string.IsNullOrWhiteSpace(nombre)
            || nombre.Length > 100
            || string.IsNullOrWhiteSpace(apellido)
            || apellido.Length > 100
            || string.IsNullOrWhiteSpace(documento)
            || documento.Length > 9
            || string.IsNullOrWhiteSpace(correoElectronico)
            || correoElectronico.Length > 150
            || !MailAddress.TryCreate(correoElectronico, out var direccionCorreo)
            || direccionCorreo.Address != correoElectronico
            || string.IsNullOrEmpty(contraseña)
            || contraseña.Length < 8
            || contraseña.Length > 16
            || string.IsNullOrWhiteSpace(telefono)
            || telefono.Length > 12
            || fechaNacimiento.Date >= DateTime.Today
            || (tipoUsuario != "MAYOR" && tipoUsuario != "ACOMPANANTE"))
        {
            return Results.BadRequest(new
            {
                mensaje = "Los datos ingresados no son válidos."
            });
        }

        if (!repositorio.ConexionConfigurada)
        {
            return Results.Problem(
                title: "Falta configurar la conexión con MySQL.",
                statusCode: 500);
        }

        try
        {
            var existeCorreoElectronico =
                await repositorio.ExisteEmailAsync(
                    correoElectronico, cancellationToken);

            if (existeCorreoElectronico)
            {
                return Results.Conflict(new
                {
                    mensaje = "Ya existe una cuenta registrada con ese correo electrónico."
                });
            }

            var existeDocumento =
                await repositorio.ExisteDniAsync(
                    documento, cancellationToken);

            if (existeDocumento)
            {
                return Results.Conflict(new
                {
                    mensaje = "Ya existe una cuenta registrada con ese DNI."
                });
            }

            var identificadorUsuario =
                await repositorio.RegistrarUsuarioAsync(
                    solicitud, cancellationToken);

            string? codigoMayor = null;

            if (tipoUsuario == "MAYOR")
            {
                codigoMayor = $"MAY-{identificadorUsuario:D6}";

                await repositorio.AsignarCodigoMayorAsync(
                    identificadorUsuario, codigoMayor, cancellationToken);
            }

            return Results.Json(new
            {
                mensaje = "Usuario registrado correctamente.",
                idUsuario = identificadorUsuario,
                codigoMayor
            }, statusCode: 201);
        }
        catch (MySqlException excepcion)
        {
            app.Logger.LogError(
                excepcion,
                "Ocurrió un error al registrar un usuario.");

            return Results.Problem(
                title: "No se pudo registrar el usuario.",
                statusCode: 503);
        }
    })
    .WithName("RegistrarUsuario")
    .WithTags("Autenticación")
    .WithOpenApi();
}
app.MapPost("/api/auth/solicitar-recuperacion",
    async (
        AcompañAR.DTO.SolicitudRecuperacionRequest request,
        AcompañAR.Services.RecuperacionPasswordService servicio) =>
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new
            {
                mensaje = "Ingresá un correo electrónico válido."
            });
        }

        try
        {
            await servicio.SolicitarRecuperacionAsync(
                request.Email.Trim());

            return Results.Ok(new
            {
                mensaje = "Si el correo está registrado, recibirás instrucciones para recuperar tu contraseña."
            });
        }
        catch (Exception)
        {
            return Results.Problem(
                "No se pudo procesar la solicitud. Intentá nuevamente más tarde.");
        }
    });

app.MapPost("/api/auth/restablecer-password",
    async (
        AcompañAR.DTO.RestablecerPasswordRequest request,
        AcompañAR.Services.RecuperacionPasswordService servicio) =>
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest(new
            {
                mensaje = "El enlace de recuperación no es válido."
            });
        }

        if (string.IsNullOrWhiteSpace(request.NuevaPassword) ||
            request.NuevaPassword.Length < 8 ||
            request.NuevaPassword.Length > 16)
        {
            return Results.BadRequest(new
            {
                mensaje = "La contraseña debe tener entre 8 y 16 caracteres."
            });
        }

        try
        {
            bool resultado = await servicio.RestablecerPasswordAsync(
                request.Token,
                request.NuevaPassword);

            if (!resultado)
            {
                return Results.BadRequest(new
                {
                    mensaje = "El enlace venció o ya fue utilizado. Solicitá uno nuevo."
                });
            }

            return Results.Ok(new
            {
                mensaje = "¡Contraseña actualizada correctamente!"
            });
        }
        catch (Exception)
        {
            return Results.Problem(
                "No se pudo actualizar la contraseña. Intentá nuevamente.");
        }
    });






if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/mayores/{idMayor:int}/contactos", async (
        int idMayor,
        HttpContext contexto,
        ContactoRepository repositorio,
        CancellationToken cancellationToken) =>
    {
        if (idMayor <= 0)
        {
            return Results.BadRequest(new
            {
                mensaje = "El ID de la persona mayor no es válido."
            });
        }

        if (!repositorio.ConexionConfigurada)
        {
            return Results.Problem(
                title: "Falta configurar la conexión con MySQL.",
                statusCode: 500);
        }

        if (!contexto.User.IsInRole("ACOMPANANTE"))
        {
            return Results.Forbid();
        }

        if (!int.TryParse(
            contexto.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var idAcompanante))
        {
            return Results.Unauthorized();
        }

        try
        {
            var tieneVinculacion = await repositorio
                .TieneVinculacionAceptadaAsync(
                idAcompanante,
                idMayor,
                cancellationToken);

            if (!tieneVinculacion)
            {
                return Results.Forbid();
            }

            var contactos = await repositorio
                .ObtenerContactosAsync(idMayor, cancellationToken);

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
    .WithOpenApi()
    .RequireAuthorization();
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/mayores/{idMayor:int}/contactos", async (
        int idMayor,
        CrearContactoRequest solicitud,
        HttpContext contexto,
        ContactoRepository repositorio,
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

        if (!repositorio.ConexionConfigurada)
        {
            return Results.Problem(
                title: "Falta configurar la conexión con MySQL.",
                statusCode: 500);
        }

        if (!contexto.User.IsInRole("ACOMPANANTE"))
        {
            return Results.Forbid();
        }

        if (!int.TryParse(
            contexto.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var idAcompanante))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await repositorio.RegistrarContactoAsync(
                idAcompanante,
                idMayor,
                nombre,
                apellido,
                telefono,
                tipoVinculo,
                cancellationToken);

            if (!resultado.Vinculado)
            {
                return Results.Forbid();
            }

            if (resultado.Duplicado)
            {
                return Results.Conflict(new
                {
                    mensaje = "Este contacto ya está registrado."
                });
            }

            return Results.Json(new
            {
                mensaje = "Contacto registrado correctamente.",
                idContacto = resultado.IdContacto
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
    .WithOpenApi()
    .RequireAuthorization();
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
                ORDER BY v.fecha_vinculacion, v.id_vinculacion;
                """, conexion);
            comando.Parameters.Add("@id", MySqlDbType.Int32).Value =
                int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Si no viene ?idMayor= o no es una de sus personas, se muestra la vinculación más vieja.
            int.TryParse(contexto.Request.Query["idMayor"], out var idElegido);
            int idMayor = 0;
            object? mayor = null;
            var mayores = new List<object>();
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
                while (await lector.ReadAsync(cancellationToken))
                {
                    var actual = new { idUsuario = lector.GetInt32(0), nombre = lector.GetString(1), apellido = lector.GetString(2) };
                    mayores.Add(actual);
                    if (mayor == null || actual.idUsuario == idElegido)
                    {
                        idMayor = actual.idUsuario;
                        mayor = actual;
                    }
                }
            if (mayor == null)
                return Results.Ok(new { acompanante = contexto.User.Identity!.Name, mayor, mayores,
                    medicamentos = Array.Empty<object>(), recordatorios = Array.Empty<object>(), turnos = Array.Empty<object>() });

            // Las tres consultas se limitan al adulto obtenido de la vinculación del usuario autenticado.
            comando.Parameters.Clear();
            comando.Parameters.Add("@idMayor", MySqlDbType.Int32).Value = idMayor;
            comando.CommandText = $$"""
                SELECT nombre, dosis, frecuencia, TIME_FORMAT(horario, '%H:%i'),
                       DATE_FORMAT(fecha_inicio, '%Y-%m-%d'), DATE_FORMAT(fecha_fin, '%Y-%m-%d'), id_medicamento,
                       {{MedicamentoRepository.TomaUnicaRespondidaSql}} soloLectura
                FROM medicamento m WHERE id_mayor = @idMayor AND estado = TRUE
                ORDER BY horario, id_medicamento;
                """;
            var medicamentos = new List<object>();
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
                while (await lector.ReadAsync(cancellationToken))
                    medicamentos.Add(new { idMedicamento = lector.GetInt32(6), nombre = lector.GetString(0), dosis = lector.GetString(1),
                        frecuencia = lector.GetString(2), horario = lector.GetString(3),
                        fechaInicio = lector.GetString(4), fechaFin = lector.IsDBNull(5) ? null : lector.GetString(5),
                        soloLectura = Convert.ToBoolean(lector.GetValue(7)) });

            comando.CommandText = """
    SELECT t.especialidad, t.nombre_medico, DATE_FORMAT(t.fecha, '%Y-%m-%d'),
           TIME_FORMAT(t.hora, '%H:%i'), t.direccion, t.motivo, t.estado,
           t.id_turno
    FROM turno_medico t WHERE t.id_mayor = @idMayor ORDER BY t.fecha, t.hora, t.id_turno;
    """;
            var turnos = new List<object>();
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
                while (await lector.ReadAsync(cancellationToken))
                    turnos.Add(new { especialidad = lector.GetString(0), medico = lector.GetString(1),
                        fecha = lector.GetString(2), hora = lector.GetString(3), direccion = lector.GetString(4),
                        motivo = lector.IsDBNull(5) ? null : lector.GetString(5), estado = lector.GetString(6),
                        idTurno = lector.GetInt32(7)
                    });

            comando.CommandText = """
                SELECT COALESCE(m.nombre, t.especialidad),
                       CASE WHEN r.id_medicamento IS NOT NULL THEN 'Medicamento' ELSE 'Turno médico' END,
                       DATE_FORMAT(r.fecha_hora, '%Y-%m-%d'), TIME_FORMAT(r.fecha_hora, '%H:%i'), COALESCE(c.estado, r.estado)
                FROM recordatorio r
                LEFT JOIN medicamento m ON m.id_medicamento = r.id_medicamento
                LEFT JOIN turno_medico t ON t.id_turno = r.id_turno
                LEFT JOIN cumplimiento c ON c.id_recordatorio = r.id_recordatorio
                WHERE m.id_mayor = @idMayor OR t.id_mayor = @idMayor
                ORDER BY r.fecha_hora, r.id_recordatorio;
                """;
            var recordatorios = new List<object>();
            using (var lector = await comando.ExecuteReaderAsync(cancellationToken))
                while (await lector.ReadAsync(cancellationToken))
                    recordatorios.Add(new { nombre = lector.GetString(0), tipo = lector.GetString(1),
                        fecha = lector.GetString(2), hora = lector.GetString(3), estado = lector.GetString(4) });

            return Results.Ok(new { acompanante = contexto.User.Identity!.Name, mayor, mayores, medicamentos, recordatorios, turnos });
        }
        catch (MySqlException ex)
        {
            app.Logger.LogError(ex, "Falló la consulta del panel del acompañante.");
            return Results.Problem(title: "No pudimos cargar la información. Intentá más tarde.", statusCode: 503);
        }
    }).RequireAuthorization().WithTags("Acompañante");
}


if (app.Environment.IsDevelopment())
{
    app.MapPut("/api/mayores/{idMayor:int}/contactos/{idContacto:int}",
        async (
            int idMayor,
            int idContacto,
            ActualizarContactoRequest solicitud,
            HttpContext contexto,
            ContactoRepository repositorio,
            CancellationToken cancellationToken) =>
        {
            var nombre = solicitud.Nombre?.Trim();
            var apellido = solicitud.Apellido?.Trim();
            var telefono = solicitud.Telefono?.Trim();
            var tipoVinculo = solicitud.TipoVinculo?.Trim();

            if (idMayor <= 0 ||
                idContacto <= 0 ||
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

            if (!repositorio.ConexionConfigurada)
            {
                return Results.Problem(
                    title: "Falta configurar la conexión con MySQL.",
                    statusCode: 500);
            }

            if (!contexto.User.IsInRole("ACOMPANANTE"))
            {
                return Results.Forbid();
            }

            if (!int.TryParse(
                contexto.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var idAcompanante))
            {
                return Results.Unauthorized();
            }

            try
            {
                var resultado = await repositorio.ActualizarContactoAsync(
                    idAcompanante,
                    idMayor,
                    idContacto,
                    nombre,
                    apellido,
                    telefono,
                    tipoVinculo,
                    cancellationToken);

                if (!resultado.Vinculado)
                {
                    return Results.Forbid();
                }

                if (!resultado.Encontrado)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "El contacto no existe o está desactivado."
                    });
                }

                if (resultado.Duplicado)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "El teléfono ya pertenece a otro contacto."
                    });
                }

                return Results.Ok(new
                {
                    mensaje = "Contacto actualizado correctamente."
                });
            }
            catch (MySqlException ex)
            {
                app.Logger.LogError(
                    ex,
                    "Falló la actualización del contacto de confianza.");

                return Results.Problem(
                    title: "No se pudo actualizar el contacto.",
                    statusCode: 503);
            }
        })
    .WithName("ActualizarContacto")
    .WithTags("Contactos")
    .WithOpenApi()
    .RequireAuthorization();
}


if (app.Environment.IsDevelopment())
{
    app.MapDelete("/api/mayores/{idMayor:int}/contactos/{idContacto:int}",
        async (
            int idMayor,
            int idContacto,
            HttpContext contexto,
            ContactoRepository repositorio,
            CancellationToken cancellationToken) =>
        {
            if (idMayor <= 0 || idContacto <= 0)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Los IDs ingresados no son válidos."
                });
            }

            if (!repositorio.ConexionConfigurada)
            {
                return Results.Problem(
                    title: "Falta configurar la conexión con MySQL.",
                    statusCode: 500);
            }

            if (!contexto.User.IsInRole("ACOMPANANTE"))
            {
                return Results.Forbid();
            }

            if (!int.TryParse(
                contexto.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var idAcompanante))
            {
                return Results.Unauthorized();
            }

            try
            {
                var resultado = await repositorio.DesactivarContactoAsync(
                    idAcompanante,
                    idMayor,
                    idContacto,
                    cancellationToken);

                if (!resultado.Vinculado)
                {
                    return Results.Forbid();
                }

                if (!resultado.Encontrado)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "El contacto no existe o ya está desactivado."
                    });
                }

                return Results.Ok(new
                {
                    mensaje = "Contacto desactivado correctamente."
                });
            }
            catch (MySqlException ex)
            {
                app.Logger.LogError(
                    ex,
                    "Falló la desactivación del contacto de confianza.");

                return Results.Problem(
                    title: "No se pudo desactivar el contacto.",
                    statusCode: 503);
            }
        })
    .WithName("DesactivarContacto")
    .WithTags("Contactos")
    .WithOpenApi()
    .RequireAuthorization();
}

var medicamentosApi = app.MapGroup("/api/acompanante/mayores/{idMayor:int}/medicamentos")
    .RequireAuthorization().WithTags("Medicamentos");
medicamentosApi.MapPost("", (int idMayor, MedicamentoRequest solicitud,
    HttpContext contexto, MedicamentoService servicio, CancellationToken ct) =>
    ProcesarMedicamento(contexto, servicio, idMayor, null, solicitud, ct));
medicamentosApi.MapPut("/{id:int}", (int idMayor, int id, MedicamentoRequest solicitud,
    HttpContext contexto, MedicamentoService servicio, CancellationToken ct) =>
    ProcesarMedicamento(contexto, servicio, idMayor, id, solicitud, ct));
medicamentosApi.MapDelete("/{id:int}", (int idMayor, int id,
    HttpContext contexto, MedicamentoService servicio, CancellationToken ct) =>
    ProcesarMedicamento(contexto, servicio, idMayor, id, null, ct));

async Task<IResult> ProcesarMedicamento(HttpContext contexto, MedicamentoService servicio,
    int idMayor, int? id, MedicamentoRequest? solicitud, CancellationToken ct)
{
    if (!contexto.User.IsInRole("ACOMPANANTE")) return Results.Forbid();
    if (!int.TryParse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier), out var idAcompanante))
        return Results.Unauthorized();
    try
    {
        var resultado = await servicio.GuardarAsync(idAcompanante, idMayor, id, solicitud, ct);
        return resultado.Estado switch
        {
            EstadoOperacionMedicamento.DatosInvalidos => Results.BadRequest(new { mensaje = resultado.Mensaje }),
            EstadoOperacionMedicamento.SinConexion => Results.Problem(title: resultado.Mensaje, statusCode: 500),
            EstadoOperacionMedicamento.SinVinculacion => Results.Forbid(),
            EstadoOperacionMedicamento.NoEncontrado => Results.NotFound(new { mensaje = resultado.Mensaje }),
            _ => Results.Json(new { idMedicamento = resultado.IdMedicamento, mensaje = resultado.Mensaje },
                statusCode: id.HasValue ? 200 : 201)
        };
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo guardar el medicamento.");
        return Results.Problem(title: "No se pudo guardar el medicamento. Intentá nuevamente.", statusCode: 503);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { mensaje = ex.Message });
    }
}

var turnosApi = app.MapGroup(
    "/api/acompanante/mayores/{idMayor:int}/turnos")
    .RequireAuthorization()
    .WithTags("Turnos médicos");

turnosApi.MapPost("", (
    int idMayor,
    TurnoMedicoRequest solicitud,
    HttpContext contexto,
    TurnoMedicoService servicio,
    CancellationToken ct) =>
    ProcesarTurnoMedico(contexto, servicio, idMayor, null, solicitud, ct));

turnosApi.MapPut("/{id:int}", (
    int idMayor,
    int id,
    TurnoMedicoRequest solicitud,
    HttpContext contexto,
    TurnoMedicoService servicio,
    CancellationToken ct) =>
    ProcesarTurnoMedico(contexto, servicio, idMayor, id, solicitud, ct));

turnosApi.MapDelete("/{id:int}", (
    int idMayor,
    int id,
    HttpContext contexto,
    TurnoMedicoService servicio,
    CancellationToken ct) =>
    ProcesarTurnoMedico(contexto, servicio, idMayor, id, null, ct));


async Task<IResult> ProcesarTurnoMedico(
    HttpContext contexto,
    TurnoMedicoService servicio,
    int idMayor,
    int? id,
    TurnoMedicoRequest? solicitud,
    CancellationToken ct)
{
    if (!contexto.User.IsInRole("ACOMPANANTE"))
        return Results.Forbid();

    if (!int.TryParse(
        contexto.User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var idAcompanante))
        return Results.Unauthorized();

    try
    {
        var resultado = await servicio.GuardarAsync(
            idAcompanante, idMayor, id, solicitud, ct);

        return resultado.Estado switch
        {
            EstadoOperacionTurnoMedico.DatosInvalidos =>
                Results.BadRequest(new { mensaje = resultado.Mensaje }),

            EstadoOperacionTurnoMedico.SinConexion =>
                Results.Problem(
                    title: resultado.Mensaje,
                    statusCode: 500),

            EstadoOperacionTurnoMedico.SinVinculacion =>
                Results.Forbid(),

            EstadoOperacionTurnoMedico.NoEncontrado =>
                Results.NotFound(new { mensaje = resultado.Mensaje }),

            _ => Results.Json(
                new
                {
                    idTurno = resultado.IdTurno,
                    mensaje = resultado.Mensaje
                },
                statusCode: id.HasValue ? 200 : 201)
        };
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo guardar el turno médico.");

        return Results.Problem(
    title: "No se pudo guardar el turno médico. Intentá nuevamente.",
    statusCode: 503);
    }
}

app.MapGet("/api/mayor/panel", async (HttpContext contexto, IConfiguration config,
    MedicamentoRepository medicamentosRepository, CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("MAYOR")) return Results.Forbid();
    try
    {
        await medicamentosRepository.AsegurarTomasActualesAsync(
            int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        using var conexion = new MySqlConnection(config.GetConnectionString("MySql"));
        await conexion.OpenAsync(ct);
        using var comando = new MySqlCommand("", conexion);
        comando.Parameters.AddWithValue("@mayor", int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        async Task<List<Dictionary<string, object?>>> Leer(string sql)
        {
            comando.CommandText = sql;
            var filas = new List<Dictionary<string, object?>>();
            using var lector = await comando.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
            {
                var fila = new Dictionary<string, object?>();
                for (int i = 0; i < lector.FieldCount; i++)
                    fila[lector.GetName(i)] = lector.IsDBNull(i) ? null : lector.GetValue(i);
                filas.Add(fila);
            }
            return filas;
        }
        var medicamentos = await Leer($$"""
            SELECT nombre, dosis, frecuencia, TIME_FORMAT(horario, '%H:%i') horario,
                   DATE_FORMAT(fecha_inicio, '%Y-%m-%d') fechaInicio, DATE_FORMAT(fecha_fin, '%Y-%m-%d') fechaFin,
                   {{MedicamentoRepository.TomaUnicaRespondidaSql}} soloLectura
            FROM medicamento m WHERE id_mayor = @mayor AND estado = TRUE ORDER BY horario, id_medicamento
            """);
        var turnos = await Leer("""
            SELECT especialidad, nombre_medico medico, direccion, motivo, estado,
                   DATE_FORMAT(fecha, '%Y-%m-%d') fecha, TIME_FORMAT(hora, '%H:%i') hora
            FROM turno_medico WHERE id_mayor = @mayor AND estado <> 'CANCELADO' ORDER BY fecha, hora, id_turno
            """);
        var recordatorios = await Leer("""
            SELECT r.id_recordatorio idRecordatorio, COALESCE(m.nombre, t.especialidad) nombre,
                   IF(m.id_medicamento IS NOT NULL, 'MEDICAMENTO', 'TURNO') tipo,
                   COALESCE(m.dosis, t.direccion) detalle,
                   DATE_FORMAT(COALESCE(TIMESTAMP(t.fecha, t.hora), r.fecha_hora), '%Y-%m-%d') fecha,
                   TIME_FORMAT(COALESCE(TIMESTAMP(t.fecha, t.hora), r.fecha_hora), '%H:%i') hora,
                   COALESCE(c.estado, r.estado) estado, c.estado cumplimiento,
                   IF(COALESCE(TIMESTAMP(t.fecha, t.hora), r.fecha_hora) <= NOW(), 1, 0) disponible
            FROM recordatorio r
            LEFT JOIN medicamento m ON m.id_medicamento = r.id_medicamento
            LEFT JOIN turno_medico t ON t.id_turno = r.id_turno
            LEFT JOIN cumplimiento c ON c.id_recordatorio = r.id_recordatorio
            WHERE (m.id_mayor = @mayor AND m.estado = TRUE OR t.id_mayor = @mayor AND t.estado <> 'CANCELADO')
              AND r.estado <> 'CANCELADO'
              AND (c.id_cumplimiento IS NULL OR DATE(c.fecha_hora_registro) = CURDATE())
            ORDER BY COALESCE(TIMESTAMP(t.fecha, t.hora), r.fecha_hora), r.id_recordatorio
            """);
        var contactos = await Leer("""
            SELECT id_contacto idContacto, nombre, apellido, telefono, tipo_vinculo tipoVinculo
            FROM contacto_confianza WHERE id_mayor = @mayor AND estado = TRUE
            ORDER BY nombre, apellido, id_contacto
            """);
        var solicitudes = await Leer("""
            SELECT v.id_vinculacion idVinculacion, a.nombre, a.apellido, a.email,
                   a.telefono, v.tipo_vinculo tipoVinculo,
                   DATE_FORMAT(v.fecha_vinculacion, '%Y-%m-%d') fecha
            FROM vinculacion v JOIN usuario a ON a.id_usuario = v.id_acompanante
            WHERE v.id_mayor = @mayor AND v.estado = 'PENDIENTE'
              AND a.estado = TRUE AND a.tipo_usuario = 'ACOMPANANTE'
            ORDER BY v.fecha_vinculacion DESC, v.id_vinculacion DESC
            """);
        return Results.Ok(new { medicamentos, turnos, recordatorios, contactos, solicitudes });
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo cargar el panel de la persona mayor.");
        return Results.Problem(title: "No pudimos cargar tus cuidados. Intentá nuevamente.", statusCode: 503);
    }
}).RequireAuthorization().WithTags("Mayor");

app.MapPost("/api/mayor/solicitudes/{idVinculacion:int}/respuesta", async (
    int idVinculacion, ResponderSolicitudRequest solicitud, HttpContext contexto,
    IConfiguration config, CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("MAYOR")) return Results.Forbid();
    if (idVinculacion <= 0 || solicitud.Estado is not ("ACEPTADA" or "RECHAZADA"))
        return Results.BadRequest(new { mensaje = "Elegí aceptar o rechazar la solicitud." });
    try
    {
        using var conexion = new MySqlConnection(config.GetConnectionString("MySql"));
        await conexion.OpenAsync(ct);
        // La condición PENDIENTE hace atómica la respuesta y evita sobrescribir otra decisión.
        using var comando = new MySqlCommand("""
            UPDATE vinculacion v
            JOIN usuario a ON a.id_usuario = v.id_acompanante AND a.estado = TRUE AND a.tipo_usuario = 'ACOMPANANTE'
            JOIN usuario m ON m.id_usuario = v.id_mayor AND m.estado = TRUE AND m.tipo_usuario = 'MAYOR'
            SET v.estado = @estado
            WHERE v.id_vinculacion = @id AND v.id_mayor = @mayor AND v.estado = 'PENDIENTE'
            """, conexion);
        comando.Parameters.AddWithValue("@id", idVinculacion);
        comando.Parameters.AddWithValue("@mayor", int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        comando.Parameters.AddWithValue("@estado", solicitud.Estado);
        if (await comando.ExecuteNonQueryAsync(ct) == 0)
            return Results.Conflict(new { mensaje = "La solicitud ya no está pendiente o no está disponible para tu cuenta." });
        return Results.Ok(new { mensaje = solicitud.Estado == "ACEPTADA" ? "Solicitud aceptada." : "Solicitud rechazada." });
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo responder la solicitud de vinculación.");
        return Results.Problem(title: "No pudimos guardar tu respuesta. Intentá nuevamente.", statusCode: 503);
    }
}).RequireAuthorization().WithTags("Mayor");

app.MapGet("/api/acompanante/solicitudes", async (HttpContext contexto, IConfiguration config, CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ACOMPANANTE")) return Results.Forbid();
    try
    {
        using var conexion = new MySqlConnection(config.GetConnectionString("MySql"));
        await conexion.OpenAsync(ct);
        using var comando = new MySqlCommand("""
            SELECT m.nombre, m.apellido, v.tipo_vinculo, v.estado, DATE_FORMAT(v.fecha_vinculacion, '%Y-%m-%d')
            FROM vinculacion v JOIN usuario m ON m.id_usuario = v.id_mayor
            WHERE v.id_acompanante = @acompanante
            ORDER BY v.fecha_vinculacion DESC, v.id_vinculacion DESC
            """, conexion);
        comando.Parameters.AddWithValue("@acompanante", int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        var solicitudes = new List<object>();
        using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            solicitudes.Add(new { nombre = lector.GetString(0), apellido = lector.GetString(1), tipoVinculo = lector.GetString(2),
                estado = lector.GetString(3), fecha = lector.GetString(4) });
        return Results.Ok(solicitudes);
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudieron cargar las solicitudes del acompañante.");
        return Results.Problem(title: "No pudimos cargar tus solicitudes. Intentá nuevamente.", statusCode: 503);
    }
}).RequireAuthorization().WithTags("Acompañante");

app.MapPost("/api/acompanante/solicitudes", async (EnviarSolicitudRequest solicitud, HttpContext contexto,
    IConfiguration config, CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ACOMPANANTE")) return Results.Forbid();
    var codigo = solicitud.Codigo?.Trim().ToUpperInvariant();
    if (string.IsNullOrEmpty(codigo))
        return Results.BadRequest(new { mensaje = "Ingresá el código de vinculación." });
    if (solicitud.TipoVinculo is not ("Familiar" or "Cuidador"))
        return Results.BadRequest(new { mensaje = "Elegí el tipo de vínculo." });
    try
    {
        using var conexion = new MySqlConnection(config.GetConnectionString("MySql"));
        await conexion.OpenAsync(ct);
        using var comando = new MySqlCommand(
            "SELECT id_usuario FROM usuario WHERE codigo_mayor = @codigo AND estado = TRUE AND tipo_usuario = 'MAYOR'", conexion);
        comando.Parameters.AddWithValue("@codigo", codigo);
        comando.Parameters.AddWithValue("@acompanante", int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        comando.Parameters.AddWithValue("@tipo", solicitud.TipoVinculo);
        var idMayor = await comando.ExecuteScalarAsync(ct);
        if (idMayor is null)
            return Results.NotFound(new { mensaje = "No encontramos un adulto mayor con ese código." });
        comando.Parameters.AddWithValue("@mayor", idMayor);

        comando.CommandText = "SELECT estado FROM vinculacion WHERE id_mayor = @mayor AND id_acompanante = @acompanante";
        var estado = await comando.ExecuteScalarAsync(ct) as string;
        if (estado == "PENDIENTE")
            return Results.Conflict(new { mensaje = "Ya enviaste una solicitud a esta persona. Esperá su respuesta." });
        if (estado == "ACEPTADA")
            return Results.Conflict(new { mensaje = "Ya acompañás a esta persona." });

        comando.CommandText = estado == "RECHAZADA"
            ? "UPDATE vinculacion SET estado = 'PENDIENTE', tipo_vinculo = @tipo, fecha_vinculacion = NOW() WHERE id_mayor = @mayor AND id_acompanante = @acompanante"
            : "INSERT INTO vinculacion (id_mayor, id_acompanante, tipo_vinculo) VALUES (@mayor, @acompanante, @tipo)";
        await comando.ExecuteNonQueryAsync(ct);
        return Results.Ok(new { mensaje = "Solicitud enviada. Cuando la acepte, vas a poder ver su información." });
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo enviar la solicitud de vinculación.");
        return Results.Problem(title: "No pudimos enviar la solicitud. Intentá nuevamente.", statusCode: 503);
    }
}).RequireAuthorization().WithTags("Acompañante");

app.MapGet("/api/acompanante/mayores/{idMayor:int}/recordatorios", async (int idMayor, DateOnly? desde, DateOnly? hasta,
    HttpContext contexto, RecordatorioRepository repositorio, CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ACOMPANANTE")) return Results.Forbid();
    if (desde is null || hasta is null || hasta < desde)
        return Results.BadRequest(new { mensaje = "Revisá las fechas: la fecha hasta no puede ser anterior a la fecha desde." });
    try
    {
        var recordatorios = await repositorio.ListarPendientesAsync(
            int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!), idMayor, desde.Value, hasta.Value, ct);
        return recordatorios is null ? Results.Forbid() : Results.Ok(recordatorios);
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudieron consultar los recordatorios.");
        return Results.Problem(title: "No se pudieron consultar los recordatorios. Intentá nuevamente.", statusCode: 503);
    }
}).RequireAuthorization().WithTags("Recordatorios");

app.MapPost("/api/recordatorios/{idRecordatorio:int}/cumplimiento", async (int idRecordatorio, CumplimientoRequest solicitud,
    HttpContext contexto, RecordatorioRepository repositorio, CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("MAYOR") && !contexto.User.IsInRole("ACOMPANANTE")) return Results.Forbid();
    if (solicitud.Estado is not ("CUMPLIDO" or "NO_CUMPLIDO"))
        return Results.BadRequest(new { mensaje = "Indicá si se cumplió (CUMPLIDO) o no (NO_CUMPLIDO)." });
    try
    {
        var (existe, yaRegistrado) = await repositorio.RegistrarCumplimientoAsync(
            int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!), idRecordatorio, solicitud.Estado, ct);
        if (!existe) return Results.NotFound(new { mensaje = "El recordatorio no existe." });
        if (yaRegistrado) return Results.Conflict(new { mensaje = "El cumplimiento ya fue registrado." });
        return Results.Json(new { mensaje = "Cumplimiento registrado." }, statusCode: 201);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { mensaje = ex.Message });
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo registrar el cumplimiento.");
        return Results.Problem(title: "No se pudo registrar el cumplimiento. Intentá nuevamente.", statusCode: 503);
    }
}).RequireAuthorization().WithTags("Recordatorios");

var adminApi = app.MapGroup("/api/admin")
    .RequireAuthorization()
    .WithTags("Admin");

adminApi.MapGet("/usuarios", async (
    HttpContext contexto,
    VinculacionRepository repositorio,
    CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ADMIN")) return Results.Forbid();

    if (!repositorio.ConexionConfigurada)
        return Results.Problem(title: "Falta configurar la conexión con MySQL.", statusCode: 500);

    try
    {
        var usuarios = await repositorio.ListarUsuariosAsync(ct);
        return Results.Ok(usuarios);
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudieron consultar los usuarios.");
        return Results.Problem(title: "No se pudieron consultar los usuarios. Intentá nuevamente.", statusCode: 503);
    }
});

adminApi.MapGet("/vinculaciones", async (
    HttpContext contexto,
    VinculacionRepository repositorio,
    CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ADMIN")) return Results.Forbid();

    if (!repositorio.ConexionConfigurada)
        return Results.Problem(title: "Falta configurar la conexión con MySQL.", statusCode: 500);

    try
    {
        var vinculaciones = await repositorio.ListarVinculacionesAsync(ct);
        return Results.Ok(vinculaciones);
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudieron consultar las vinculaciones.");
        return Results.Problem(title: "No se pudieron consultar las vinculaciones. Intentá nuevamente.", statusCode: 503);
    }
});

adminApi.MapPost("/vinculaciones", async (
    VinculacionRequest solicitud,
    HttpContext contexto,
    VinculacionRepository repositorio,
    CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ADMIN")) return Results.Forbid();

    var tipoVinculo = solicitud.TipoVinculo?.Trim() ?? "";
    var estado = solicitud.Estado?.Trim().ToUpperInvariant() ?? "";

    if (solicitud.IdMayor <= 0
        || solicitud.IdAcompanante <= 0
        || solicitud.IdMayor == solicitud.IdAcompanante
        || string.IsNullOrWhiteSpace(tipoVinculo)
        || tipoVinculo.Length > 50
        || (estado != "PENDIENTE" && estado != "ACEPTADA" && estado != "RECHAZADA"))
    {
        return Results.BadRequest(new
        {
            mensaje = "Los datos de la vinculación no son válidos."
        });
    }

    if (!repositorio.ConexionConfigurada)
        return Results.Problem(title: "Falta configurar la conexión con MySQL.", statusCode: 500);

    try
    {
        var resultado = await repositorio.RegistrarVinculacionAsync(
            solicitud.IdMayor, solicitud.IdAcompanante, tipoVinculo, estado, ct);

        if (!resultado.MayorValido)
            return Results.BadRequest(new { mensaje = "La persona mayor no existe o no está activa." });

        if (!resultado.AcompananteValido)
            return Results.BadRequest(new { mensaje = "El acompañante no existe o no está activo." });

        if (resultado.Duplicado)
            return Results.Conflict(new { mensaje = "Esa persona mayor y ese acompañante ya están vinculados." });

        return Results.Json(new
        {
            mensaje = "Vinculación creada correctamente.",
            idVinculacion = resultado.IdVinculacion
        }, statusCode: 201);
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo crear la vinculación.");
        return Results.Problem(title: "No se pudo crear la vinculación. Intentá nuevamente.", statusCode: 503);
    }
});

adminApi.MapPut("/vinculaciones/{idVinculacion:int}", async (
    int idVinculacion,
    VinculacionRequest solicitud,
    HttpContext contexto,
    VinculacionRepository repositorio,
    CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ADMIN")) return Results.Forbid();

    var tipoVinculo = solicitud.TipoVinculo?.Trim() ?? "";
    var estado = solicitud.Estado?.Trim().ToUpperInvariant() ?? "";

    if (idVinculacion <= 0
        || string.IsNullOrWhiteSpace(tipoVinculo)
        || tipoVinculo.Length > 50
        || (estado != "PENDIENTE" && estado != "ACEPTADA" && estado != "RECHAZADA"))
    {
        return Results.BadRequest(new
        {
            mensaje = "Los datos de la vinculación no son válidos."
        });
    }

    if (!repositorio.ConexionConfigurada)
        return Results.Problem(title: "Falta configurar la conexión con MySQL.", statusCode: 500);

    try
    {
        var encontrada = await repositorio.ActualizarVinculacionAsync(
            idVinculacion, tipoVinculo, estado, ct);

        if (!encontrada)
            return Results.NotFound(new { mensaje = "La vinculación no existe." });

        return Results.Ok(new
        {
            mensaje = "Vinculación actualizada correctamente."
        });
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo actualizar la vinculación.");
        return Results.Problem(title: "No se pudo actualizar la vinculación. Intentá nuevamente.", statusCode: 503);
    }
});

adminApi.MapDelete("/vinculaciones/{idVinculacion:int}", async (
    int idVinculacion,
    HttpContext contexto,
    VinculacionRepository repositorio,
    CancellationToken ct) =>
{
    if (!contexto.User.IsInRole("ADMIN")) return Results.Forbid();

    if (idVinculacion <= 0)
        return Results.BadRequest(new { mensaje = "El ID de la vinculación no es válido." });

    if (!repositorio.ConexionConfigurada)
        return Results.Problem(title: "Falta configurar la conexión con MySQL.", statusCode: 500);

    try
    {
        var encontrada = await repositorio.EliminarVinculacionAsync(
            idVinculacion, ct);

        if (!encontrada)
            return Results.NotFound(new { mensaje = "La vinculación no existe." });

        return Results.Ok(new
        {
            mensaje = "Vinculación eliminada correctamente."
        });
    }
    catch (MySqlException ex)
    {
        app.Logger.LogError(ex, "No se pudo eliminar la vinculación.");
        return Results.Problem(title: "No se pudo eliminar la vinculación. Intentá nuevamente.", statusCode: 503);
    }
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
