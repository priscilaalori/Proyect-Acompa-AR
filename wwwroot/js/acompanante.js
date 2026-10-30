const mensaje = document.getElementById("panelMensaje");
const configuracion = document.getElementById("configuracion");
const reintentar = document.getElementById("reintentar");
let idMayorActual = null;
const estados = { PENDIENTE: "Pendiente", ENVIADO: "Enviado", CANCELADO: "Cancelado", PROGRAMADO: "Programado", REALIZADO: "Realizado", CUMPLIDO: "Cumplido", NO_CUMPLIDO: "No cumplido", ACEPTADA: "Aceptada", RECHAZADA: "Rechazada" };
const fecha = valor => new Intl.DateTimeFormat("es-AR").format(new Date(`${valor}T12:00:00`));
const diaLocal = (dias = 0) => new Date(Date.now() + dias * 24 * 60 * 60 * 1000).toLocaleDateString("en-CA");
let seccionActual = "recordatorios";

function mostrarSeccion(nombre) {
    seccionActual = nombre;
    for (const seccion of document.querySelectorAll(".panel-seccion[data-seccion]")) {
        seccion.hidden = seccion.dataset.seccion !== nombre;
    }
    for (const boton of document.querySelectorAll(".panel-nav")) {
        if (boton.dataset.seccion === nombre) {
            boton.setAttribute("aria-current", "page");
        } else {
            boton.removeAttribute("aria-current");
        }
    }
    cerrarMenu();
    mostrarAlertas();
}

function abrirMenu() {
    document.getElementById("menu").classList.add("abierto");
    document.getElementById("fondoMenu").hidden = false;
    document.getElementById("abrirMenu").setAttribute("aria-expanded", "true");
}

function cerrarMenu() {
    document.getElementById("menu").classList.remove("abierto");
    document.getElementById("fondoMenu").hidden = true;
    document.getElementById("abrirMenu").setAttribute("aria-expanded", "false");
}

document.getElementById("abrirMenu").addEventListener("click", abrirMenu);
document.getElementById("cerrarMenu").addEventListener("click", cerrarMenu);
document.getElementById("fondoMenu").addEventListener("click", cerrarMenu);
for (const boton of document.querySelectorAll(".panel-nav")) {
    boton.addEventListener("click", () => mostrarSeccion(boton.dataset.seccion));
}

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
        lista.append(item);
    }
}

async function cargar() {
    configuracion.hidden = true;
    reintentar.hidden = true;
    mensaje.textContent = "Cargando información…";
    try {
        const respuesta = await fetch(`/api/acompanante/panel${idMayorActual ? `?idMayor=${idMayorActual}` : ""}`, { cache: "no-store" });
        if (respuesta.status === 401) {
            window.location.replace("login.html");
            return;
        }
        const datos = await respuesta.json();
        if (!respuesta.ok) throw new Error(datos.mensaje || datos.title || "No pudimos cargar la información.");
        document.getElementById("saludo").textContent = `¡Hola, ${datos.acompanante}!`;
        document.getElementById("vinculacion").hidden = false;
        await cargarSolicitudes();
        if (!datos.mayor) {
            document.getElementById("personaMayor").textContent = "Sin persona vinculada";
            document.getElementById("selectorMayor").hidden = true;
            document.getElementById("avatarMayor").textContent = "?";
            mensaje.textContent = "Todavía no tenés un adulto mayor con una vinculación aceptada. Cuando tengas una vinculación aceptada, podrás ver aquí su información.";
            for (const id of ["cantidadRecordatorios", "cantidadMedicamentos", "cantidadTurnos", "cantidadHoy", "cantidadManana"]) document.getElementById(id).textContent = "";
            mostrarSeccion("vinculacion");
            return;
        }
        document.getElementById("personaMayor").textContent = `${datos.mayor.nombre} ${datos.mayor.apellido}`;
        document.getElementById("avatarMayor").textContent = datos.mayor.nombre.charAt(0).toUpperCase();
        idMayorActual = datos.mayor.idUsuario;
        const selector = document.getElementById("mayorElegido");
        selector.replaceChildren(...datos.mayores.map(m => new Option(`${m.nombre} ${m.apellido}`, m.idUsuario)));
        selector.value = idMayorActual;
        document.getElementById("selectorMayor").hidden = datos.mayores.length < 2;
        const describirMedicamento = m => [
            m.nombre, `Dosis: ${m.dosis}`, `${m.frecuencia} · Horario: ${m.horario}`,
            `Desde ${fecha(m.fechaInicio)}${m.fechaFin ? ` hasta ${fecha(m.fechaFin)}` : " · Sin fecha de finalización"}`
        ];
        const medicamentos = datos.medicamentos.filter(m => !m.soloLectura);
        mostrarLista("medicamentos", medicamentos, "No hay medicamentos activos configurados.", describirMedicamento);
        document.getElementById("cantidadMedicamentos").textContent = medicamentos.length;
        mostrarLista("historialMedicamentos", datos.medicamentos.filter(m => m.soloLectura),
            "Todavía no hay tomas únicas respondidas.", m => [...describirMedicamento(m), "Finalizado · Solo consulta"]);
        await buscarRecordatorios();
        mostrarLista("turnos", datos.turnos, "No hay turnos médicos configurados.", t => [
            t.especialidad, `Profesional: ${t.medico}`, `${fecha(t.fecha)} · ${t.hora}`,
            t.direccion, t.motivo ? `Motivo: ${t.motivo}` : null, `Estado: ${estados[t.estado] || t.estado}`
        ]);
        document.getElementById("cantidadTurnos").textContent = datos.turnos.filter(t => t.estado === "PROGRAMADO").length;
        turnosAlertas = datos.turnos;
        await cargarAlertas();
        const proximos = datos.recordatorios.filter(r => r.estado !== "CANCELADO");
        document.getElementById("cantidadHoy").textContent = proximos.filter(r => r.fecha === diaLocal(0)).length;
        document.getElementById("cantidadManana").textContent = proximos.filter(r => r.fecha === diaLocal(1)).length;
        mensaje.textContent = "La información configurada para su cuidado.";
        configuracion.hidden = false;
        mostrarSeccion(seccionActual);

        cargarContactos(idMayorActual);
    } catch (error) {
        mensaje.textContent = error.message === "Failed to fetch" ? "No pudimos conectar con el servidor." : error.message;
        reintentar.hidden = false;
    }
}

async function cargarSolicitudes() {
    const aviso = document.getElementById("vinculacionMensaje");
    try {
        const respuesta = await fetch("/api/acompanante/solicitudes", { cache: "no-store" });
        const datos = await respuesta.json();
        if (!respuesta.ok) throw new Error(datos.mensaje || datos.title || "No pudimos cargar tus solicitudes.");
        mostrarLista("solicitudes", datos, "Todavía no enviaste solicitudes.", s => [
            `${s.nombre} ${s.apellido}`, `Vínculo: ${s.tipoVinculo}`,
            `Estado: ${estados[s.estado] || s.estado}`, `Enviada el ${fecha(s.fecha)}`
        ]);
    } catch (error) {
        aviso.textContent = error.message === "Failed to fetch" ? "No pudimos conectar con el servidor." : error.message;
    }
}

document.getElementById("vinculacionForm").addEventListener("submit", async event => {
    event.preventDefault();
    const aviso = document.getElementById("vinculacionMensaje");
    const boton = event.submitter;
    boton.disabled = true;
    try {
        const respuesta = await fetch("/api/acompanante/solicitudes", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                codigo: document.getElementById("vinculacionCodigo").value,
                tipoVinculo: document.getElementById("vinculacionTipo").value
            })
        });
        const datos = await respuesta.json().catch(() => ({}));
        aviso.textContent = datos.mensaje || datos.title || "No pudimos enviar la solicitud.";
        if (respuesta.ok) {
            event.target.reset();
            await cargarSolicitudes();
        }
    } catch {
        aviso.textContent = "No pudimos conectar con el servidor.";
    } finally {
        boton.disabled = false;
    }
});

document.getElementById("mayorElegido").addEventListener("change", event => {
    idMayorActual = Number(event.target.value);
    for (const id of ["medicamentoForm", "turnoForm", "contactoForm"]) document.getElementById(id).hidden = true;
    cargar();
});

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
    if (medicamento?.soloLectura) { avisoMedicamento.textContent = "Este medicamento quedó en el historial y no se puede modificar."; return; }
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
    if (medicamento.soloLectura) return;
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
    if (medicamento.soloLectura) return;
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
const recordatoriosEstado = document.getElementById("recordatoriosEstado");
// Por defecto, hoy y mañana. "en-CA" da la fecha local como AAAA-MM-DD.
recordatoriosDesde.value = diaLocal(0);
recordatoriosHasta.value = diaLocal(1);
let recordatoriosCargados = [];
let tipoElegido = "";

function marcarAtajos() {
    const mismoDia = recordatoriosDesde.value === recordatoriosHasta.value;
    document.getElementById("verHoy").setAttribute("aria-pressed", mismoDia && recordatoriosDesde.value === diaLocal(0));
    document.getElementById("verManana").setAttribute("aria-pressed", mismoDia && recordatoriosDesde.value === diaLocal(1));
}

async function buscarRecordatorios() {
    const mensajeRecordatorios = document.getElementById("recordatoriosMensaje");
    mensajeRecordatorios.textContent = "";
    marcarAtajos();
    try {
        const respuesta = await fetch(`/api/acompanante/mayores/${idMayorActual}/recordatorios?desde=${recordatoriosDesde.value}&hasta=${recordatoriosHasta.value}`, { cache: "no-store" });
        const datos = await respuesta.json().catch(() => ({}));
        if (!respuesta.ok) throw new Error(datos.mensaje || "No pudimos consultar los recordatorios.");
        recordatoriosCargados = datos;
        mostrarRecordatorios();
    } catch (error) {
        mensajeRecordatorios.textContent = error.message;
    }
}

function mostrarRecordatorios() {
    const lista = document.getElementById("recordatorios");
    lista.replaceChildren();
    const filtrados = recordatoriosCargados.filter(r => {
        const coincideEstado = recordatoriosEstado.value === "" ||
            (recordatoriosEstado.value === "PENDIENTE" ? !r.cumplimiento : r.cumplimiento === recordatoriosEstado.value);
        return coincideEstado && (!tipoElegido || r.tipo === tipoElegido);
    });
    const dias = [];
    for (const r of filtrados) {
        const ultimo = dias[dias.length - 1];
        if (ultimo && ultimo.fecha === r.fecha) ultimo.recordatorios.push(r);
        else dias.push({ fecha: r.fecha, recordatorios: [r] });
    }
    for (const dia of dias) {
        const encabezado = document.createElement("h3");
        encabezado.className = "panel-dia";
        const prefijo = dia.fecha === diaLocal(0) ? "Hoy · " : dia.fecha === diaLocal(1) ? "Mañana · " : "";
        const nombreDia = new Intl.DateTimeFormat("es-AR", { weekday: "long" }).format(new Date(`${dia.fecha}T12:00:00`)) + " " + fecha(dia.fecha).slice(0, -5);
        encabezado.textContent = prefijo + nombreDia;
        const cantidad = document.createElement("span");
        cantidad.textContent = dia.recordatorios.length === 1 ? "1 recordatorio" : `${dia.recordatorios.length} recordatorios`;
        encabezado.append(cantidad);
        lista.append(encabezado);
        const grupo = document.createElement("ul");
        grupo.className = "panel-lista";
        for (const r of dia.recordatorios) {
            const item = document.createElement("li");
            item.className = "panel-recordatorio";
            const hora = document.createElement("span");
            hora.className = "panel-hora";
            hora.textContent = r.hora;
            const detalle = document.createElement("div");
            const nombre = document.createElement("p");
            const negrita = document.createElement("strong");
            negrita.textContent = r.nombre;
            nombre.append(negrita);
            const tipo = document.createElement("p");
            tipo.textContent = r.tipo;
            detalle.append(nombre, tipo);
            const cumplimiento = r.cumplimiento || "PENDIENTE";
            const etiqueta = document.createElement("span");
            etiqueta.className = `panel-estado estado-${cumplimiento.toLowerCase()}`;
            etiqueta.textContent = estados[r.estado] || r.estado;
            item.append(hora, detalle, etiqueta);
            if (!r.cumplimiento) agregarAccionesRecordatorio(item, r);
            grupo.append(item);
        }
        lista.append(grupo);
    }
    if (!filtrados.length) {
        const vacio = document.createElement("p");
        vacio.className = "panel-vacio";
        vacio.textContent = "No hay recordatorios con estos filtros.";
        lista.append(vacio);
    }
    document.getElementById("cantidadRecordatorios").textContent = filtrados.length || "";
}

recordatoriosDesde.addEventListener("change", buscarRecordatorios);
recordatoriosHasta.addEventListener("change", buscarRecordatorios);
recordatoriosEstado.addEventListener("change", mostrarRecordatorios);

document.getElementById("verHoy").addEventListener("click", () => {
    recordatoriosDesde.value = diaLocal(0);
    recordatoriosHasta.value = diaLocal(0);
    buscarRecordatorios();
});

document.getElementById("verManana").addEventListener("click", () => {
    recordatoriosDesde.value = diaLocal(1);
    recordatoriosHasta.value = diaLocal(1);
    buscarRecordatorios();
});

document.getElementById("verFiltros").addEventListener("click", event => {
    const filtros = document.getElementById("filtros");
    const abrir = filtros.hidden;
    filtros.hidden = !abrir;
    event.target.textContent = abrir ? "Ocultar filtros" : "Filtros";
    event.target.setAttribute("aria-expanded", abrir);
});

for (const chip of document.querySelectorAll(".panel-chip")) {
    chip.addEventListener("click", () => {
        tipoElegido = chip.dataset.tipo;
        for (const otro of document.querySelectorAll(".panel-chip")) otro.setAttribute("aria-pressed", otro === chip);
        mostrarRecordatorios();
    });
}

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
        await cargar();
        mensajeRecordatorios.textContent = datos.mensaje;
    } catch (error) {
        mensajeRecordatorios.textContent = error.message;
    }
}

// Alertas: lo que no confirmó a tiempo o no cumplió en las últimas 24 horas.
const minutosEspera = document.getElementById("minutosEspera");
minutosEspera.value = localStorage.getItem("minutosAlerta") || "20";
let recordatoriosAlertas = [];
let turnosAlertas = [];

const alertasVistas = () => JSON.parse(localStorage.getItem("alertasVistas") || "[]");

async function cargarAlertas() {
    if (!idMayorActual) {
        recordatoriosAlertas = [];
        mostrarAlertas();
        return;
    }
    try {
        const respuesta = await fetch(`/api/acompanante/mayores/${idMayorActual}/recordatorios?desde=${diaLocal(-1)}&hasta=${diaLocal(0)}`, { cache: "no-store" });
        if (!respuesta.ok) return;
        recordatoriosAlertas = await respuesta.json();
        mostrarAlertas();
    } catch {
        // Si falla la consulta no mostramos nada nuevo, para no romper el panel.
    }
}

function mostrarAlertas() {
    const vistas = alertasVistas();
    const minutos = Number(minutosEspera.value || 20);
    const ahora = new Date();
    const limite = new Date(ahora.getTime() - 24 * 60 * 60 * 1000);
    const tomas = [];
    const turnos = [];
    for (const r of recordatoriosAlertas) {
        if (r.tipo !== "Medicamento" || vistas.includes("r" + r.idRecordatorio)) continue;
        const cuando = new Date(`${r.fecha}T${r.hora}:00`);
        if (cuando < limite || cuando > ahora) continue;
        let texto = "";
        if (r.cumplimiento === "NO_CUMPLIDO") {
            texto = `No tomó ${r.nombre} de las ${r.hora}.`;
        } else if (!r.cumplimiento && ahora - cuando >= minutos * 60 * 1000) {
            texto = `No confirmó la toma de ${r.nombre} de las ${r.hora}.`;
        }
        if (texto) tomas.push({ clave: "r" + r.idRecordatorio, cuando, hora: r.hora, texto });
    }
    for (const t of turnosAlertas) {
        if (t.estado !== "PROGRAMADO" || vistas.includes("t" + t.idTurno)) continue;
        const cuando = new Date(`${t.fecha}T${t.hora}:00`);
        if (cuando < limite || cuando > ahora || ahora - cuando < minutos * 60 * 1000) continue;
        turnos.push({ clave: "t" + t.idTurno, cuando, hora: t.hora, texto: `No marcó que fue al turno de ${t.especialidad} de las ${t.hora}.` });
    }
    // Primero las tomas y después los turnos, cada grupo de la más vieja a la más nueva.
    const alertas = [
        ...tomas.sort((a, b) => a.cuando - b.cuando),
        ...turnos.sort((a, b) => a.cuando - b.cuando)
    ];
    const lista = document.getElementById("alertas");
    lista.replaceChildren();
    for (const alerta of alertas) {
        const item = document.createElement("li");
        item.className = "panel-alerta";
        const detalle = document.createElement("p");
        const texto = document.createElement("strong");
        texto.textContent = alerta.texto;
        const cuando = document.createElement("small");
        const dia = alerta.cuando.toLocaleDateString("en-CA") === diaLocal(0) ? "Hoy" : "Ayer";
        cuando.textContent = `${dia} · ${alerta.hora}`;
        detalle.append(texto, cuando);
        const boton = document.createElement("button");
        boton.type = "button";
        boton.className = "panel-boton";
        boton.textContent = "Entendido";
        boton.addEventListener("click", () => {
            localStorage.setItem("alertasVistas", JSON.stringify([...vistas, alerta.clave]));
            mostrarAlertas();
        });
        item.append(detalle, boton);
        lista.append(item);
    }
    if (!alertas.length) {
        const vacio = document.createElement("li");
        vacio.className = "panel-vacio";
        vacio.textContent = "Está todo al día.";
        lista.append(vacio);
    }
    document.getElementById("cantidadAlertas").textContent = alertas.length || "";
    document.getElementById("textoAlertas").textContent = alertas.length === 1 ? "1 sin revisar" : `${alertas.length} sin revisar`;
    document.getElementById("bannerAlertas").hidden = !alertas.length || seccionActual === "alertas";
}

minutosEspera.addEventListener("change", () => {
    localStorage.setItem("minutosAlerta", minutosEspera.value);
    mostrarAlertas();
});

document.getElementById("verAlertas").addEventListener("click", () => mostrarSeccion("alertas"));

setInterval(() => {
    if (idMayorActual) cargarAlertas();
}, 60000);
