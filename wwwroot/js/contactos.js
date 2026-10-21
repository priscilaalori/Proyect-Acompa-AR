async function cargarContactos(idMayor) {
    const lista = document.getElementById("contactos");
    const mensaje = document.getElementById("contactosMensaje");

    lista.replaceChildren();
    mensaje.textContent = "Cargando contactos...";

    try {
        const respuesta = await fetch(
            `/api/mayores/${idMayor}/contactos`,
            { cache: "no-store" }
        );

        if (respuesta.status === 401) {
            window.location.replace("login.html");
            return;
        }

        if (respuesta.status === 403) {
            mensaje.textContent = "No tenés permiso para consultar estos contactos.";
            return;
        }

        if (!respuesta.ok) {
            throw new Error("No se pudieron cargar los contactos.");
        }

        const contactos = await respuesta.json();

        if (contactos.length === 0) {
            mensaje.textContent = "No hay contactos registrados.";
            return;
        }

        for (const contacto of contactos) {
            const item = document.createElement("li");

            const nombre = document.createElement("h3");
            nombre.textContent =
                `${contacto.nombre} ${contacto.apellido ?? ""}`.trim();

            const datos = document.createElement("p");
            datos.textContent =
                `${contacto.tipoVinculo} - ${contacto.telefono}`;

            const acciones = document.createElement("div");
            acciones.className = "medicamento-acciones";

            const botonEditar = document.createElement("button");
            botonEditar.type = "button";
            botonEditar.className = "panel-boton";
            botonEditar.textContent = "Editar";

            botonEditar.addEventListener("click", () => {
                abrirFormularioEditarContacto(contacto);
            });

            const botonBaja = document.createElement("button");
            botonBaja.type = "button";
            botonBaja.className = "panel-boton";
            botonBaja.textContent = "Dar de baja";

            botonBaja.addEventListener("click", () => {
                darDeBajaContacto(contacto);
            });

            acciones.append(botonEditar, botonBaja);
            item.append(nombre, datos, acciones);
            lista.append(item);
        }

        mensaje.textContent = `${contactos.length} contactos registrados.`;

    } catch (error) {
        mensaje.textContent = error.message;
    }
}

const formularioContacto = document.getElementById("contactoForm");
const botonNuevoContacto = document.getElementById("nuevoContacto");
const botonCancelarContacto = document.getElementById("cancelarContacto");

botonNuevoContacto.addEventListener("click", () => {
    formularioContacto.reset();
    formularioContacto.hidden = false;
    idContactoEditando = null;

    document.getElementById("tituloFormularioContacto").textContent =
        "Agregar contacto";

    formularioContacto.querySelector('button[type="submit"]').disabled = false;

    document.getElementById("contactoNombre").focus();
});

botonCancelarContacto.addEventListener("click", () => {
    formularioContacto.hidden = true;
    botonNuevoContacto.focus();

    idContactoEditando = null;
});

let guardandoContacto = false;

formularioContacto.addEventListener("submit", async event => {
    event.preventDefault();

    if (guardandoContacto) return;

    if (!idMayorActual) {
        document.getElementById("contactosMensaje").textContent =
            "No encontramos una persona mayor vinculada.";
        return;
    }

    const datos = {
        nombre: document.getElementById("contactoNombre").value.trim(),
        apellido: document.getElementById("contactoApellido").value.trim() || null,
        telefono: document.getElementById("contactoTelefono").value.trim(),
        tipoVinculo: document.getElementById("contactoVinculo").value.trim()
    };

    const editando = idContactoEditando !== null;

    const ruta = editando
        ? `/api/mayores/${idMayorActual}/contactos/${idContactoEditando}`
        : `/api/mayores/${idMayorActual}/contactos`;

    const metodo = editando ? "PUT" : "POST";

    const mensaje = document.getElementById("contactosMensaje");
    const botonGuardar = formularioContacto.querySelector('button[type="submit"]');

    guardandoContacto = true;
    botonGuardar.disabled = true;
    mensaje.textContent = "Guardando contacto...";

    try {
        const respuesta = await fetch(ruta, {
            method: metodo,
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(datos)
        });

        if (respuesta.status === 401) {
            window.location.replace("login.html");
            return;
        }

        const resultado = await respuesta.json().catch(() => ({}));

        if (!respuesta.ok) {
            throw new Error(
                resultado.mensaje ||
                (editando
                    ? "No se pudo modificar el contacto."
                    : "No se pudo registrar el contacto.")
            );
        }

        formularioContacto.reset();
        formularioContacto.hidden = true;
        idContactoEditando = null;

        await cargarContactos(idMayorActual);

        mensaje.textContent = resultado.mensaje;

    } catch (error) {
        mensaje.textContent = error.message;
    } finally {
        guardandoContacto = false;
        botonGuardar.disabled = false;
    }
});

let idContactoEditando = null;

function abrirFormularioEditarContacto(contacto) {
    formularioContacto.reset();

    idContactoEditando = contacto.idContacto;

    document.getElementById("tituloFormularioContacto").textContent =
        "Editar contacto";

    document.getElementById("contactoNombre").value =
        contacto.nombre;

    document.getElementById("contactoApellido").value =
        contacto.apellido ?? "";

    document.getElementById("contactoTelefono").value =
        contacto.telefono;

    document.getElementById("contactoVinculo").value =
        contacto.tipoVinculo;

    formularioContacto.hidden = false;

    formularioContacto.querySelector('button[type="submit"]').disabled = false;

    document.getElementById("contactoNombre").focus();
}

async function darDeBajaContacto(contacto) {
    if (guardandoContacto || !idMayorActual) return;

    const confirmar = window.confirm(
        `¿Querés dar de baja a ${contacto.nombre} ${contacto.apellido ?? ""}?`
    );

    if (!confirmar) return;

    const mensaje = document.getElementById("contactosMensaje");

    guardandoContacto = true;
    mensaje.textContent = "Desactivando contacto...";

    try {
        const respuesta = await fetch(
            `/api/mayores/${idMayorActual}/contactos/${contacto.idContacto}`,
            {
                method: "DELETE"
            }
        );

        if (respuesta.status === 401) {
            window.location.replace("login.html");
            return;
        }

        const resultado = await respuesta.json().catch(() => ({}));

        if (!respuesta.ok) {
            throw new Error(
                resultado.mensaje || "No se pudo desactivar el contacto."
            );
        }

        if (idContactoEditando === contacto.idContacto) {
            formularioContacto.reset();
            formularioContacto.hidden = true;
            idContactoEditando = null;
        }

        await cargarContactos(idMayorActual);

        mensaje.textContent = resultado.mensaje;

    } catch (error) {
        mensaje.textContent = error.message;
    } finally {
        guardandoContacto = false;
    }
}