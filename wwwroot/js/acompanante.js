const mensaje = document.getElementById("panelMensaje");
const configuracion = document.getElementById("configuracion");
const reintentar = document.getElementById("reintentar");
const estados = { PENDIENTE: "Pendiente", ENVIADO: "Enviado", CANCELADO: "Cancelado", PROGRAMADO: "Programado", REALIZADO: "Realizado" };
const fecha = valor => new Intl.DateTimeFormat("es-AR").format(new Date(`${valor}T12:00:00`));

function mostrarLista(id, elementos, vacio, describir) {
    const lista = document.getElementById(id);
    lista.replaceChildren();
    if (!elementos.length) {
        const item = document.createElement("li");
        item.className = "panel-vacio";
        item.textContent = vacio;
        lista.append(item);
        return;
    }
    for (const elemento of elementos) {
        const item = document.createElement("li");
        const [titulo, ...detalles] = describir(elemento);
        const encabezado = document.createElement("h3");
        encabezado.textContent = titulo;
        item.append(encabezado);
        for (const detalle of detalles.filter(Boolean)) {
            const parrafo = document.createElement("p");
            parrafo.textContent = detalle;
            item.append(parrafo);
        }
        lista.append(item);
    }
}

async function cargar() {
    configuracion.hidden = true;
    reintentar.hidden = true;
    mensaje.textContent = "Cargando información…";
    try {
        const respuesta = await fetch("/api/acompanante/panel", { cache: "no-store" });
        if (respuesta.status === 401) {
            window.location.replace("login.html");
            return;
        }
        const datos = await respuesta.json();
        if (!respuesta.ok) throw new Error(datos.mensaje || datos.title || "No pudimos cargar la información.");
        document.getElementById("saludo").textContent = `¡Hola, ${datos.acompanante}!`;
        if (!datos.mayor) {
            document.getElementById("personaMayor").textContent = "Todavía no tenés un adulto mayor con una vinculación aceptada.";
            mensaje.textContent = "Cuando tengas una vinculación aceptada, podrás ver aquí su información.";
            return;
        }
        document.getElementById("personaMayor").textContent = `Estás acompañando a ${datos.mayor.nombre} ${datos.mayor.apellido}.`;
        mostrarLista("medicamentos", datos.medicamentos, "No hay medicamentos activos configurados.", m => [
            m.nombre, `Dosis: ${m.dosis}`, `${m.frecuencia} · Horario: ${m.horario}`,
            `Desde ${fecha(m.fechaInicio)}${m.fechaFin ? ` hasta ${fecha(m.fechaFin)}` : " · Sin fecha de finalización"}`
        ]);
        mostrarLista("recordatorios", datos.recordatorios, "No hay recordatorios configurados.", r => [
            r.nombre, r.tipo, `${fecha(r.fecha)} · ${r.hora}`, `Estado: ${estados[r.estado] || r.estado}`
        ]);
        mostrarLista("turnos", datos.turnos, "No hay turnos médicos configurados.", t => [
            t.especialidad, `Profesional: ${t.medico}`, `${fecha(t.fecha)} · ${t.hora}`,
            t.direccion, t.motivo ? `Motivo: ${t.motivo}` : null, `Estado: ${estados[t.estado] || t.estado}`
        ]);
        mensaje.textContent = "La información configurada para su cuidado.";
        configuracion.hidden = false;
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
