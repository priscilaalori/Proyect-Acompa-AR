const formulario = document.getElementById("registroForm");

formulario.addEventListener("submit", async function (event) {

    event.preventDefault();

    limpiarErrores();

    let formularioValido = true;

    // Obtener valores
    const nombre = document.getElementById("nombre").value.trim();
    const apellido = document.getElementById("apellido").value.trim();
    const dni = document.getElementById("dni").value.trim();
    const fechaNacimiento = document.getElementById("fechaNacimiento").value;
    const email = document.getElementById("email").value.trim();
    const password = document.getElementById("password").value;
    const telefono = document.getElementById("telefono").value.trim();

    const tipoUsuario = document.querySelector(
        'input[name="tipoUsuario"]:checked'
    );


    // -------------------------
    // NOMBRE
    // -------------------------

    if (nombre === "") {

        mostrarError("nombre", "El nombre es obligatorio.");
        formularioValido = false;

    } else if (!/^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$/.test(nombre)) {

        mostrarError("nombre", "El nombre solo puede contener letras.");
        formularioValido = false;
    }


    // -------------------------
    // APELLIDO
    // -------------------------

    if (apellido === "") {

        mostrarError("apellido", "El apellido es obligatorio.");
        formularioValido = false;

    } else if (!/^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$/.test(apellido)) {

        mostrarError("apellido", "El apellido solo puede contener letras.");
        formularioValido = false;
    }


    // -------------------------
    // DNI
    // -------------------------

    if (dni === "") {

        mostrarError("dni", "El DNI es obligatorio.");
        formularioValido = false;

    } else if (!/^\d+$/.test(dni)) {

        mostrarError("dni", "El DNI solo puede contener números.");
        formularioValido = false;

    } else if (dni.length > 9) {

        mostrarError(
            "dni",
            "El DNI no puede tener más de 9 caracteres."
        );

        formularioValido = false;
    }


    // -------------------------
    // FECHA DE NACIMIENTO
    // -------------------------

    if (fechaNacimiento === "") {

        mostrarError(
            "fechaNacimiento",
            "La fecha de nacimiento es obligatoria."
        );

        formularioValido = false;

    } else {

        
        const hoy = new Date();
        const año = hoy.getFullYear();
        const mes = String(hoy.getMonth() + 1).padStart(2, "0");
        const dia = String(hoy.getDate()).padStart(2, "0");

        const fechaHoy = `${año}-${mes}-${dia}`;

        
        if (fechaNacimiento >= fechaHoy) {

            mostrarError(
                "fechaNacimiento",
                "La fecha de nacimiento debe ser anterior a la fecha actual."
            );

            formularioValido = false;
        }
    }


    // -------------------------
    // EMAIL
    // -------------------------

    const emailRegex =
        /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (email === "") {

        mostrarError("email", "El email es obligatorio.");
        formularioValido = false;

    } else if (!emailRegex.test(email)) {

        mostrarError("email", "Ingresá un email válido.");
        formularioValido = false;
    }


    // -------------------------
    // CONTRASEÑA
    // -------------------------

    if (password === "") {

        mostrarError("password", "La contraseña es obligatoria.");
        formularioValido = false;

    } else if (password.length < 8 || password.length > 16) {

        mostrarError(
            "password",
            "La contraseña debe tener entre 8 y 16 caracteres."
        );

        formularioValido = false;
    }


    // -------------------------
    // TELÉFONO
    // -------------------------

    if (telefono === "") {

        mostrarError("telefono", "El teléfono es obligatorio.");
        formularioValido = false;

    } else if (!/^[0-9\s+\-()]+$/.test(telefono)) {

        mostrarError(
            "telefono",
            "Ingresá un número de teléfono válido."
        );

        formularioValido = false;
    }
    else if (telefono.length > 12) {

        mostrarError(
            "telefono",
            "El teléfono no puede tener más de 12 caracteres."
        );

        formularioValido = false;
    }


    // -------------------------
    // TIPO DE USUARIO
    // -------------------------

    if (!tipoUsuario) {

        mostrarErrorTipoUsuario(
            "Seleccioná si sos Persona mayor o Acompañante."
        );

        formularioValido = false;
    }


    // -------------------------
    // RESULTADO Y ENVÍO A LA API
    // -------------------------

    if (formularioValido) {

        try {

            
            const datosRegistro = {
                nombre: nombre,
                apellido: apellido,
                dni: dni,
                fechaNacimiento: fechaNacimiento,
                email: email,
                password: password,
                telefono: telefono,
                tipoUsuario: tipoUsuario.value
            };

            
            const respuesta = await fetch("/api/auth/registro", {
                method: "POST",

                headers: {
                    "Content-Type": "application/json"
                },

                body: JSON.stringify(datosRegistro)
            });

            
            const resultado = await respuesta.json();

            
            if (!respuesta.ok) {

                const mensaje = resultado.mensaje
                    || resultado.error
                    || "No se pudo completar el registro.";

              
                if (respuesta.status === 409) {

                    if (mensaje.toLowerCase().includes("correo")) {
                        mostrarError("email", mensaje);

                    } else if (mensaje.toLowerCase().includes("dni")) {
                        mostrarError("dni", mensaje);

                    } else {
                        alert(mensaje);
                    }

                } else {
                    alert(mensaje);
                }

                return;
            }

            
            mostrarMensajeRegistro(
                resultado.mensaje || "¡Tu cuenta se creó correctamente!",
                resultado.codigoMayor
            );

            formulario.reset();

           

        } catch (error) {

            console.error("Error al comunicarse con la API:", error);

            alert(
                "No se pudo conectar con el servidor. " +
                "Verificá que la aplicación esté ejecutándose e intentá nuevamente."
            );

        }
    }

});


// =====================================
// MOSTRAR ERROR
// =====================================

function mostrarError(idCampo, mensaje) {

    const campo = document.getElementById(idCampo);

    campo.classList.add("input-error");

    const mensajeError = document.createElement("small");

    mensajeError.classList.add("mensaje-error");

    mensajeError.textContent = mensaje;

    campo.parentElement.appendChild(mensajeError);
}


// =====================================
// ERROR TIPO DE USUARIO
// =====================================

function mostrarErrorTipoUsuario(mensaje) {

    const contenedor = document.querySelector(".tipo-usuario");

    contenedor.classList.add("tipo-error");

    const mensajeError = document.createElement("small");

    mensajeError.classList.add("mensaje-error");

    mensajeError.textContent = mensaje;

    contenedor.parentElement.appendChild(mensajeError);
}


// =====================================
// LIMPIAR ERRORES
// =====================================

function limpiarErrores() {

    document
        .querySelectorAll(".input-error")
        .forEach(campo => {
            campo.classList.remove("input-error");
        });

    document
        .querySelectorAll(".mensaje-error")
        .forEach(mensaje => {
            mensaje.remove();
        });

    document
        .querySelectorAll(".tipo-error")
        .forEach(contenedor => {
            contenedor.classList.remove("tipo-error");
        });
}

function mostrarMensajeRegistro(mensaje, codigoMayor) {

    const cartel = document.getElementById("mensajeRegistro");

    cartel.replaceChildren();

    const icono = document.createElement("div");
    icono.className = "mensaje-icono";
    icono.textContent = "💜";

    const titulo = document.createElement("h3");
    titulo.textContent = "¡Te damos la bienvenida!";

    const texto = document.createElement("p");
    texto.textContent = mensaje;

    const detalle = document.createElement("p");
    detalle.className = "mensaje-detalle";
    detalle.textContent =
        "Ya sos parte de AcompañAR.";

    if (codigoMayor) {
        detalle.textContent =
            "Tu código de persona mayor es " + codigoMayor +
            ". Compartilo con tu acompañante para que pueda vincularse.";
    }

    const boton = document.createElement("a");
    boton.classList.add("btn-volver-login");
    boton.href = "index.html";
    boton.textContent = "Ir al inicio de sesión";

    cartel.append(icono, titulo, texto, detalle, boton);

    
    formulario.hidden = true;
    document.getElementById("registroHeader").hidden = true;

  
    cartel.hidden = false;

    cartel.scrollIntoView({
        behavior: "smooth",
        block: "center"
    });
}