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

            item.append(nombre, datos);
            lista.append(item);
        }

        mensaje.textContent = `${contactos.length} contactos registrados.`;

    } catch (error) {
        mensaje.textContent = error.message;
    }
}