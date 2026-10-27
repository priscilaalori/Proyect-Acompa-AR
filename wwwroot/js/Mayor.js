
document.addEventListener("DOMContentLoaded", () => {
    const saludo = document.getElementById("saludoMayor");
    const listaTurnos = document.getElementById("listaTurnosMayor");
    const listaRecordatorios = document.getElementById("listaRecordatoriosMayor");
    const listaContactos = document.getElementById("listaContactosMayor");
    const btnCerrarSesion = document.getElementById("btnCerrarSesion");

    // Carga los datos de la persona mayor
    async function cargarPanelMayor() {
        try {
            const respuesta = await fetch("/api/mayor/panel-web", {
                method: "GET",
                cache: "no-store"
            });

            if (respuesta.status === 401) {
                window.location.replace("login.html");
                return;
            }

            if (!respuesta.ok) {
                throw new Error("No se pudo cargar la información.");
            }

            const datos = await respuesta.json();

            // Saludo personalizado
            if (saludo) {
                saludo.textContent =
                    `¡Hola, ${datos.nombre || ""}!`;
            }

            // Mostramos cada sección
            mostrarTurnos(datos.turnos || []);
            mostrarRecordatorios(datos.recordatorios || []);
            mostrarContactos(datos.contactos || []);

        } catch (error) {
            console.error("Error al cargar el panel:", error);

            mostrarMensaje(listaTurnos, "No se pudieron cargar los turnos.");
            mostrarMensaje(listaRecordatorios, "No se pudieron cargar los recordatorios.");
            mostrarMensaje(listaContactos, "No se pudieron cargar los contactos.");
        }
    }

    // Crea un elemento de texto de forma segura
    function agregarTexto(contenedor, etiqueta, valor) {
        const parrafo = document.createElement("p");
        const titulo = document.createElement("strong");

        titulo.textContent = etiqueta + ": ";
        parrafo.appendChild(titulo);
        parrafo.appendChild(
            document.createTextNode(valor ?? "No disponible")
        );

        contenedor.appendChild(parrafo);
    }

    // Crea una tarjeta para mostrar información
    function crearTarjeta(contenedor, titulo) {
        const tarjeta = document.createElement("article");
        tarjeta.classList.add("tarjeta-mayor");

        const encabezado = document.createElement("h3");
        encabezado.textContent = titulo;

        tarjeta.appendChild(encabezado);
        contenedor.appendChild(tarjeta);

        return tarjeta;
    }

    function mostrarMensaje(contenedor, mensaje) {
        if (!contenedor) return;

        contenedor.replaceChildren();

        const parrafo = document.createElement("p");
        parrafo.textContent = mensaje;
        contenedor.appendChild(parrafo);
    }

    // Turnos médicos
    function mostrarTurnos(turnos) {
        if (!listaTurnos) return;

        listaTurnos.replaceChildren();

        if (turnos.length === 0) {
            mostrarMensaje(listaTurnos, "No tenés turnos médicos registrados.");
            return;
        }

        turnos.forEach(turno => {
            const tarjeta = crearTarjeta(
                listaTurnos,
                turno.especialidad || "Turno médico"
            );

            agregarTexto(tarjeta, "Profesional", turno.medico || turno.nombreMedico);
            agregarTexto(tarjeta, "Fecha", turno.fecha);
            agregarTexto(tarjeta, "Hora", turno.hora);
            agregarTexto(tarjeta, "Dirección", turno.direccion);
            agregarTexto(tarjeta, "Motivo", turno.motivo);

            const parrafoEstado = document.createElement("p");
            const etiquetaEstado = document.createElement("strong");

            etiquetaEstado.textContent = "Estado: ";

            const textoEstado = document.createElement("span");
            textoEstado.textContent = turno.estado || "Sin estado";

            const estado = (turno.estado || "").toUpperCase();

            if (estado === "CANCELADO") {
                textoEstado.classList.add("estado-cancelado");
            } else if (estado === "PROGRAMADO") {
                textoEstado.classList.add("estado-programado");
            }

            parrafoEstado.appendChild(etiquetaEstado);
            parrafoEstado.appendChild(textoEstado);
            tarjeta.appendChild(parrafoEstado);

        });
    }

    // Recordatorios
    function mostrarRecordatorios(recordatorios) {
        if (!listaRecordatorios) return;

        listaRecordatorios.replaceChildren();

        if (recordatorios.length === 0) {
            mostrarMensaje(
                listaRecordatorios,
                "No tenés recordatorios registrados."
            );
            return;
        }

        recordatorios.forEach(recordatorio => {
            const tarjeta = crearTarjeta(
                listaRecordatorios,
                recordatorio.nombre || "Recordatorio"
            );

            agregarTexto(tarjeta, "Tipo", recordatorio.tipo);
            agregarTexto(tarjeta, "Fecha", recordatorio.fecha);
            agregarTexto(tarjeta, "Hora", recordatorio.hora);
            agregarTexto(tarjeta, "Estado", recordatorio.estado);
        });
    }

    // Contactos importantes
    function mostrarContactos(contactos) {
        if (!listaContactos) return;

        listaContactos.replaceChildren();

        if (contactos.length === 0) {
            mostrarMensaje(
                listaContactos,
                "No tenés contactos importantes registrados."
            );
            return;
        }

        contactos.forEach(contacto => {
            const nombre =
                contacto.nombreCompleto ||
                [contacto.nombre, contacto.apellido]
                    .filter(Boolean)
                    .join(" ") ||
                contacto.nombreContacto ||
                "Contacto";

            const tarjeta = crearTarjeta(listaContactos, nombre);

            agregarTexto(
                tarjeta,
                "Teléfono",
                contacto.telefono || contacto.telefonoContacto
            );

            agregarTexto(
                tarjeta,
                "Vínculo",
                contacto.vinculo || contacto.tipoVinculo || contacto.relacion
            );
        });
    }

    // Cerrar sesión

    if (btnCerrarSesion) {
        btnCerrarSesion.addEventListener("click", async () => {
            try {
                await fetch("/api/auth/logout", {
                    method: "POST",
                    credentials: "same-origin"
                });
            } catch (error) {
                console.error("Error al cerrar sesión:", error);
            } finally {
                window.location.replace("/login.html");
            }
        });
    }

    cargarPanelMayor();
});
