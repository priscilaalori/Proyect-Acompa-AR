const mensaje = document.getElementById("panelMensaje");
const configuracion = document.getElementById("configuracion");
const reintentar = document.getElementById("reintentar");
let idMayorActual = null;
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

            if (id === "turnos" && elemento.estado === "CANCELADO" && detalle.startsWith("Estado:")) {
                parrafo.innerHTML = 'Estado: <span class="estado-cancelado">Cancelado</span>';
            } else {
                parrafo.textContent = detalle;
            }

            item.append(parrafo);
        }
        if (id === "medicamentos") {
            agregarAcciones(item, elemento);
        }

        if (id === "turnos") {
            agregarAccionesTurno(item, elemento);
        }

        if (id === "recordatorios" && elemento.idRecordatorio && !elemento.cumplimiento) {
            agregarAccionesRecordatorio(item, elemento);
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
        idMayorActual = datos.mayor.idUsuario;
        mostrarLista("medicamentos", datos.medicamentos, "No hay medicamentos activos configurados.", m => [
            m.nombre, `Dosis: ${m.dosis}`, `${m.frecuencia} · Horario: ${m.horario}`,
            `Desde ${fecha(m.fechaInicio)}${m.fechaFin ? ` hasta ${fecha(m.fechaFin)}` : " · Sin fecha de finalización"}`
        ]);
        mostrarLista("recordatorios", datos.recordatorios, "No hay recordatorios configurados.", r => [
            r.nombre, r.tipo, `${fecha(r.fecha)} · ${r.hora}`, `Estado: ${estados[r.estado] || r.estado}`
        ]);
        buscarRecordatorios();
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

const formulario = document.getElementById("medicamentoForm");
const avisoMedicamento = document.getElementById("medicamentoMensaje");
const campo = nombre => document.getElementById(`med${nombre}`);
let idEditando = null;
let guardando = false;

function actualizarFrecuencia() {
    const tipo = campo("Frecuencia").value;
    const intervalo = tipo === "HORAS" || tipo === "DIAS";
    document.getElementById("intervaloGrupo").hidden = !intervalo;
    campo("Intervalo").required = intervalo;
    campo("Intervalo").disabled = !intervalo;
    campo("Intervalo").max = tipo === "HORAS" ? 168 : 365;
    document.getElementById("intervaloEtiqueta").textContent = tipo === "HORAS" ? "Cantidad de horas" : "Cantidad de días";
    document.getElementById("finGrupo").hidden = tipo === "UNICA";
    campo("Fin").disabled = tipo === "UNICA";
    campo("Fin").min = campo("Inicio").value;
    const hora = campo("Horario").value || "el horario indicado";
    const textos = {
        DIARIA: `Todos los días a las ${hora}.`,
        HORAS: `Cada ${campo("Intervalo").value || "…"} horas desde la primera toma, incluso durante la noche.`,
        DIAS: `Cada ${campo("Intervalo").value || "…"} días a las ${hora}.`,
        UNICA: `Una sola toma a las ${hora}.`,
        LEGADA: "La frecuencia anterior se conserva hasta que guardes una nueva pauta."
    };
    document.getElementById("resumenFrecuencia").textContent = textos[tipo];
}

function abrirFormulario(medicamento = null) {
    if (guardando) return;
    formulario.reset();
    idEditando = medicamento?.idMedicamento ?? null;
    document.getElementById("tituloFormulario").textContent = medicamento ? "Editar medicamento" : "Agregar medicamento";
    campo("Nombre").value = medicamento?.nombre ?? "";
    campo("Dosis").value = medicamento?.dosis ?? "";
    campo("Horario").value = medicamento?.horario ?? "08:00";
    const hoy = new Date();
    const fechaLocal = `${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, "0")}-${String(hoy.getDate()).padStart(2, "0")}`;
    campo("Inicio").value = medicamento?.fechaInicio ?? fechaLocal;
    campo("Fin").value = medicamento?.fechaFin ?? "";
    const frecuencia = (medicamento?.frecuencia ?? "Una vez por dia").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();
    const horas = /^cada (\d+) horas?$/.exec(frecuencia);
    const dias = /^cada (\d+) dias?$/.exec(frecuencia);
    campo("Frecuencia").value = horas ? "HORAS" : dias ? "DIAS" : frecuencia === "toma unica" ? "UNICA" :
        ["una vez por dia", "diaria", "todos los dias"].includes(frecuencia) ? "DIARIA" : "LEGADA";
    campo("Intervalo").value = horas?.[1] ?? dias?.[1] ?? "8";
    avisoMedicamento.textContent = "";
    actualizarFrecuencia();
    formulario.hidden = false;
    campo("Nombre").focus();
}

function agregarAcciones(item, medicamento) {
    const acciones = document.createElement("div");
    acciones.className = "medicamento-acciones";
    for (const [texto, accion] of [
        ["Editar", () => abrirFormulario(medicamento)],
        ["Dar de baja", () => darDeBaja(medicamento)]
    ]) {
        const boton = document.createElement("button");
        boton.type = "button";
        boton.className = "panel-boton";
        boton.textContent = texto;
        boton.setAttribute("aria-label", `${texto}: ${medicamento.nombre}`);
        boton.addEventListener("click", accion);
        acciones.append(boton);
    }
    item.append(acciones);
}
function agregarAccionesTurno(item, turno) {
   
    if (turno.estado !== "PROGRAMADO") return;

    const acciones = document.createElement("div");
    acciones.className = "medicamento-acciones";

    for (const [texto, accion] of [
        ["Modificar", () => abrirFormularioTurnoEdicion(turno)],
        ["Cancelar turno", () => cancelarTurno(turno)]
    ]) {
        const boton = document.createElement("button");
        boton.type = "button";
        boton.className = "panel-boton";
        boton.textContent = texto;
        boton.setAttribute(
            "aria-label",
            `${texto}: ${turno.especialidad}`
        );
        boton.addEventListener("click", accion);
        acciones.append(boton);
    }

    item.append(acciones);
}

async function modificarMedicamento(metodo, id, datos) {
    if (guardando) return;
    guardando = true;
    const botones = [...document.querySelectorAll("#configuracion button, #medicamentoForm input, #medicamentoForm select")];
    const previos = botones.map(b => b.disabled);
    botones.forEach(b => b.disabled = true);
    avisoMedicamento.textContent = "Guardando cambios…";
    try {
        const respuesta = await fetch(`/api/acompanante/mayores/${idMayorActual}/medicamentos${id ? `/${id}` : ""}`, {
            method: metodo,
            headers: { "Content-Type": "application/json" },
            ...(datos ? { body: JSON.stringify(datos) } : {})
        });
        if (respuesta.status === 401) { window.location.replace("login.html"); return; }
        const resultado = await respuesta.json().catch(() => ({}));
        if (!respuesta.ok) throw new Error(resultado.mensaje || resultado.title || "No se pudo realizar el cambio. Revisá tu vinculación e intentá nuevamente.");
        formulario.hidden = true;
        idEditando = null;
        await cargar();
        avisoMedicamento.textContent = resultado.mensaje;
    } catch (error) {
        avisoMedicamento.textContent = error.message === "Failed to fetch" ? "No pudimos conectar con el servidor. Conservamos los datos para reintentar." : error.message;
    } finally {
        botones.forEach((b, i) => b.disabled = previos[i]);
        guardando = false;
    }
}

async function darDeBaja(medicamento) {
    if (guardando || !window.confirm(`¿Dar de baja ${medicamento.nombre}? Se cancelarán sus recordatorios pendientes y se conservará el historial.`)) return;
    await modificarMedicamento("DELETE", medicamento.idMedicamento);
}

formulario.addEventListener("submit", async event => {
    event.preventDefault();
    if (campo("Frecuencia").value === "LEGADA") {
        avisoMedicamento.textContent = "Elegí una repetición para guardar el medicamento.";
        campo("Frecuencia").focus();
        return;
    }
    const datos = {
        nombre: campo("Nombre").value.trim(), dosis: campo("Dosis").value.trim(),
        tipoFrecuencia: campo("Frecuencia").value,
        intervalo: ["HORAS", "DIAS"].includes(campo("Frecuencia").value) ? Number(campo("Intervalo").value) : null,
        horario: campo("Horario").value, fechaInicio: campo("Inicio").value,
        fechaFin: campo("Frecuencia").value === "UNICA" ? campo("Inicio").value : campo("Fin").value || null
    };
    if (idEditando && !window.confirm("¿Guardar la nueva pauta? Se cancelarán los recordatorios pendientes de este medicamento para evitar avisos con la configuración anterior.")) return;
    await modificarMedicamento(idEditando ? "PUT" : "POST", idEditando, datos);
});
formulario.addEventListener("input", actualizarFrecuencia);
document.getElementById("nuevoMedicamento").addEventListener("click", () => abrirFormulario());
document.getElementById("cancelarMedicamento").addEventListener("click", () => {
    formulario.hidden = true;
    document.getElementById("nuevoMedicamento").focus();
});

const formularioTurno = document.getElementById("turnoForm");
const avisoTurno = document.getElementById("turnoMensaje");

const campoTurno = nombre =>
    document.getElementById(`turno${nombre}`);

let guardandoTurno = false;
let idTurnoEditando = null;

function abrirFormularioTurno() {
    if (guardandoTurno) return;

    formularioTurno.reset();
    idTurnoEditando = null;

    document.getElementById("tituloFormularioTurno").textContent =
        "Agregar turno médico";

    formularioTurno.hidden = false;
    avisoTurno.textContent = "";

    campoTurno("Especialidad").focus();
}

function abrirFormularioTurnoEdicion(turno) {
    if (guardandoTurno) return;

    formularioTurno.reset();
    idTurnoEditando = turno.idTurno;

    document.getElementById("tituloFormularioTurno").textContent =
        "Modificar turno médico";

    campoTurno("Especialidad").value = turno.especialidad;
    campoTurno("Medico").value = turno.medico;
    campoTurno("Fecha").value = turno.fecha;
    campoTurno("Hora").value = turno.hora;
    campoTurno("Direccion").value = turno.direccion;
    campoTurno("Motivo").value = turno.motivo || "";

    avisoTurno.textContent = "";
    formularioTurno.hidden = false;

    campoTurno("Especialidad").focus();
}

document.getElementById("nuevoTurno").addEventListener("click", () => {
    abrirFormularioTurno();
});

document.getElementById("cancelarTurno").addEventListener("click", () => {
    formularioTurno.hidden = true;
    avisoTurno.textContent = "";
});

async function guardarTurno(metodo, id, datos) {
    if (guardandoTurno) return;

    if (!idMayorActual) {
        avisoTurno.textContent =
            "No encontramos una persona mayor vinculada.";
        return;
    }

    guardandoTurno = true;

    const botones = [
        ...formularioTurno.querySelectorAll("button, input, textarea, select")
    ];
    const estadosPrevios = botones.map(boton => boton.disabled);

    botones.forEach(boton => boton.disabled = true);
    avisoTurno.textContent = "Guardando turno médico…";

    try {
        const ruta =
            `/api/acompanante/mayores/${idMayorActual}/turnos` +
            (id ? `/${id}` : "");

        const respuesta = await fetch(ruta, {
            method: metodo,
            headers: {
                "Content-Type": "application/json"
            },
            ...(datos ? { body: JSON.stringify(datos) } : {})
        });

        if (respuesta.status === 401) {
            window.location.replace("login.html");
            return;
        }

        const resultado = await respuesta.json().catch(() => ({}));

        if (!respuesta.ok) {
            throw new Error(
                resultado.mensaje ||
                resultado.title ||
                "No se pudo guardar el turno médico."
            );
        }

        formularioTurno.hidden = true;
        formularioTurno.reset();
        idTurnoEditando = null;

        await cargar();

        avisoTurno.textContent =
            resultado.mensaje || "Turno médico guardado correctamente.";

    } catch (error) {
        avisoTurno.textContent =
            error.message === "Failed to fetch"
                ? "No pudimos conectar con el servidor. Intentá nuevamente."
                : error.message;
    } finally {
        botones.forEach((boton, i) => {
            boton.disabled = estadosPrevios[i];
        });

        guardandoTurno = false;
    }
}

formularioTurno.addEventListener("submit", async event => {
    event.preventDefault();

    const datos = {
        especialidad: campoTurno("Especialidad").value.trim(),
        nombreMedico: campoTurno("Medico").value.trim(),
        fecha: campoTurno("Fecha").value,
        hora: campoTurno("Hora").value,
        direccion: campoTurno("Direccion").value.trim(),
        motivo: campoTurno("Motivo").value.trim() || null
    };

    await guardarTurno(
        idTurnoEditando ? "PUT" : "POST",
        idTurnoEditando,
        datos
    );
});

async function cancelarTurno(turno) {
    if (guardandoTurno) return;

    const confirmar = window.confirm(
        `¿Querés cancelar el turno de ${turno.especialidad} ` +
        `del ${fecha(turno.fecha)} a las ${turno.hora}?`
    );

    if (!confirmar) return;

    await guardarTurno("DELETE", turno.idTurno, null);
}
const recordatoriosDesde = document.getElementById("recordatoriosDesde");
const recordatoriosHasta = document.getElementById("recordatoriosHasta");
// Por defecto, la próxima semana. "en-CA" da la fecha local como AAAA-MM-DD.
recordatoriosDesde.value = new Date().toLocaleDateString("en-CA");
recordatoriosHasta.value = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toLocaleDateString("en-CA");

async function buscarRecordatorios() {
    const mensajeRecordatorios = document.getElementById("recordatoriosMensaje");
    mensajeRecordatorios.textContent = "";
    try {
        const respuesta = await fetch(`/api/acompanante/mayores/${idMayorActual}/recordatorios?desde=${recordatoriosDesde.value}&hasta=${recordatoriosHasta.value}`, { cache: "no-store" });
        const datos = await respuesta.json().catch(() => ({}));
        if (!respuesta.ok) throw new Error(datos.mensaje || "No pudimos consultar los recordatorios.");
        mostrarLista("recordatorios", datos, "No hay recordatorios pendientes en esas fechas.", r => [
            r.nombre, r.tipo, `${fecha(r.fecha)} · ${r.hora}`, `Estado: ${estados[r.estado] || r.estado}`,
            r.cumplimiento ? `Cumplimiento: ${r.cumplimiento === "CUMPLIDO" ? "Cumplido" : "No cumplido"}` : null
        ]);
    } catch (error) {
        mensajeRecordatorios.textContent = error.message;
    }
}

document.getElementById("recordatoriosForm").addEventListener("submit", event => {
    event.preventDefault();
    buscarRecordatorios();
});

function agregarAccionesRecordatorio(item, recordatorio) {
    const acciones = document.createElement("div");
    acciones.className = "medicamento-acciones";
    for (const [texto, estado] of [["Se cumplió", "CUMPLIDO"], ["No se cumplió", "NO_CUMPLIDO"]]) {
        const boton = document.createElement("button");
        boton.type = "button";
        boton.className = "panel-boton";
        boton.textContent = texto;
        boton.setAttribute("aria-label", `${texto}: ${recordatorio.nombre} del ${fecha(recordatorio.fecha)} a las ${recordatorio.hora}`);
        boton.addEventListener("click", () => registrarCumplimiento(recordatorio, estado, texto));
        acciones.append(boton);
    }
    item.append(acciones);
}

async function registrarCumplimiento(recordatorio, estado, texto) {
    if (!window.confirm(`¿Registrar "${texto}" para ${recordatorio.nombre} del ${fecha(recordatorio.fecha)} a las ${recordatorio.hora}? Después no se puede cambiar.`)) return;
    const mensajeRecordatorios = document.getElementById("recordatoriosMensaje");
    try {
        const respuesta = await fetch(`/api/recordatorios/${recordatorio.idRecordatorio}/cumplimiento`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ estado })
        });
        const datos = await respuesta.json().catch(() => ({}));
        if (!respuesta.ok) throw new Error(datos.mensaje || "No pudimos registrar el cumplimiento.");
        await buscarRecordatorios();
        mensajeRecordatorios.textContent = datos.mensaje;
    } catch (error) {
        mensajeRecordatorios.textContent = error.message;
    }
}
