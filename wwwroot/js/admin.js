const mensaje = document.getElementById("panelMensaje");
const reintentar = document.getElementById("reintentar");
const estados = { PENDIENTE: "Pendiente", ACEPTADA: "Aceptada", RECHAZADA: "Rechazada" };
const fecha = valor => new Intl.DateTimeFormat("es-AR").format(new Date(`${valor}T12:00:00`));

const formulario = document.getElementById("vinculacionForm");
const avisoVinculacion = document.getElementById("vinculacionMensaje");
const campo = nombre => document.getElementById(`vin${nombre}`);
let idEditando = null;
let guardando = false;

function llenarSelect(id, elementos, describir) {
    const select = document.getElementById(id);
    select.replaceChildren();
    const vacio = document.createElement("option");
    vacio.value = "";
    vacio.textContent = "Elegí una opción";
    select.append(vacio);
    for (const elemento of elementos) {
        const opcion = document.createElement("option");
        opcion.value = elemento.idUsuario;
        opcion.textContent = describir(elemento);
        select.append(opcion);
    }
}

function mostrarLista(elementos) {
    const lista = document.getElementById("vinculaciones");
    lista.replaceChildren();
    if (!elementos.length) {
        const item = document.createElement("li");
        item.className = "panel-vacio";
        item.textContent = "No hay vinculaciones registradas.";
        lista.append(item);
        return;
    }
    for (const vinculacion of elementos) {
        const item = document.createElement("li");
        const encabezado = document.createElement("h3");
        encabezado.textContent = `${vinculacion.mayor} (persona mayor) y ${vinculacion.acompanante} (acompañante)`;
        item.append(encabezado);
        for (const detalle of [
            `Vínculo: ${vinculacion.tipoVinculo}`,
            `Estado: ${estados[vinculacion.estado] || vinculacion.estado}`,
            `Fecha de vinculación: ${fecha(vinculacion.fechaVinculacion)}`
        ]) {
            const parrafo = document.createElement("p");
            parrafo.textContent = detalle;
            item.append(parrafo);
        }
        agregarAcciones(item, vinculacion);
        lista.append(item);
    }
}

function agregarAcciones(item, vinculacion) {
    const acciones = document.createElement("div");
    acciones.className = "medicamento-acciones";
    for (const [texto, accion] of [
        ["Modificar", () => abrirFormulario(vinculacion)],
        ["Eliminar", () => eliminarVinculacion(vinculacion)]
    ]) {
        const boton = document.createElement("button");
        boton.type = "button";
        boton.className = "panel-boton";
        boton.textContent = texto;
        boton.setAttribute("aria-label", `${texto}: ${vinculacion.mayor} y ${vinculacion.acompanante}`);
        boton.addEventListener("click", accion);
        acciones.append(boton);
    }
    item.append(acciones);
}

async function cargar() {
    reintentar.hidden = true;
    mensaje.textContent = "Cargando información…";
    try {
        const [respuestaUsuarios, respuestaVinculaciones] = await Promise.all([
            fetch("/api/admin/usuarios", { cache: "no-store" }),
            fetch("/api/admin/vinculaciones", { cache: "no-store" })
        ]);
        if (respuestaUsuarios.status === 401 || respuestaVinculaciones.status === 401) {
            window.location.replace("login.html");
            return;
        }
        if (respuestaUsuarios.status === 403 || respuestaVinculaciones.status === 403) {
            mensaje.textContent = "Esta página es solo para administradores.";
            return;
        }
        const usuarios = await respuestaUsuarios.json().catch(() => ({}));
        const vinculaciones = await respuestaVinculaciones.json().catch(() => ({}));
        if (!respuestaUsuarios.ok) throw new Error(usuarios.mensaje || usuarios.title || "No pudimos cargar los usuarios.");
        if (!respuestaVinculaciones.ok) throw new Error(vinculaciones.mensaje || vinculaciones.title || "No pudimos cargar las vinculaciones.");
        const mayores = usuarios.filter(u => u.tipoUsuario === "MAYOR");
        const acompanantes = usuarios.filter(u => u.tipoUsuario === "ACOMPANANTE");
        llenarSelect("vinMayor", mayores, u => `${u.nombre} ${u.apellido}${u.codigoMayor ? ` (${u.codigoMayor})` : ""}`);
        llenarSelect("vinAcompanante", acompanantes, u => `${u.nombre} ${u.apellido}`);
        mostrarLista(vinculaciones);
        mensaje.textContent = "Vinculaciones entre personas mayores y acompañantes.";
    } catch (error) {
        mensaje.textContent = error.message === "Failed to fetch" ? "No pudimos conectar con el servidor." : error.message;
        reintentar.hidden = false;
    }
}

document.getElementById("cerrarSesion").addEventListener("click", async event => {
    event.target.disabled = true;
    try {
        const respuesta = await fetch("/api/auth/logout", { method: "POST" });
        if (!respuesta.ok) throw new Error();
        window.location.replace("login.html");
    } catch {
        mensaje.textContent = "No pudimos cerrar la sesión. Intentá nuevamente.";
        event.target.disabled = false;
    }
});
reintentar.addEventListener("click", cargar);
window.addEventListener("pageshow", cargar);

function abrirFormulario(vinculacion = null) {
    if (guardando) return;
    formulario.reset();
    idEditando = vinculacion?.idVinculacion ?? null;
    document.getElementById("tituloFormularioVinculacion").textContent = vinculacion ? "Modificar vinculación" : "Agregar vinculación";
    campo("Mayor").disabled = Boolean(vinculacion);
    campo("Acompanante").disabled = Boolean(vinculacion);
    campo("Mayor").value = vinculacion?.idMayor ?? "";
    campo("Acompanante").value = vinculacion?.idAcompanante ?? "";
    campo("Tipo").value = vinculacion?.tipoVinculo ?? "";
    campo("Estado").value = vinculacion?.estado ?? "PENDIENTE";
    avisoVinculacion.textContent = "";
    formulario.hidden = false;
    (vinculacion ? campo("Tipo") : campo("Mayor")).focus();
}

async function guardarVinculacion(metodo, id, datos) {
    if (guardando) return;
    guardando = true;
    const botones = [...document.querySelectorAll("#vinculaciones button, #vinculacionForm button, #vinculacionForm input, #vinculacionForm select")];
    const previos = botones.map(b => b.disabled);
    botones.forEach(b => b.disabled = true);
    avisoVinculacion.textContent = "Guardando cambios…";
    try {
        const respuesta = await fetch(`/api/admin/vinculaciones${id ? `/${id}` : ""}`, {
            method: metodo,
            headers: { "Content-Type": "application/json" },
            ...(datos ? { body: JSON.stringify(datos) } : {})
        });
        if (respuesta.status === 401) { window.location.replace("login.html"); return; }
        const resultado = await respuesta.json().catch(() => ({}));
        if (!respuesta.ok) throw new Error(resultado.mensaje || resultado.title || "No se pudo guardar la vinculación.");
        formulario.hidden = true;
        idEditando = null;
        await cargar();
        avisoVinculacion.textContent = resultado.mensaje;
    } catch (error) {
        avisoVinculacion.textContent = error.message === "Failed to fetch" ? "No pudimos conectar con el servidor. Intentá nuevamente." : error.message;
    } finally {
        botones.forEach((b, i) => b.disabled = previos[i]);
        guardando = false;
    }
}

async function eliminarVinculacion(vinculacion) {
    if (guardando || !window.confirm(`¿Eliminar la vinculación entre ${vinculacion.mayor} y ${vinculacion.acompanante}?`)) return;
    await guardarVinculacion("DELETE", vinculacion.idVinculacion);
}

formulario.addEventListener("submit", async event => {
    event.preventDefault();
    const datos = {
        idMayor: Number(campo("Mayor").value),
        idAcompanante: Number(campo("Acompanante").value),
        tipoVinculo: campo("Tipo").value.trim(),
        estado: campo("Estado").value
    };
    await guardarVinculacion(idEditando ? "PUT" : "POST", idEditando, datos);
});

document.getElementById("nuevaVinculacion").addEventListener("click", () => abrirFormulario());
document.getElementById("cancelarVinculacion").addEventListener("click", () => {
    formulario.hidden = true;
    avisoVinculacion.textContent = "";
    document.getElementById("nuevaVinculacion").focus();
});
