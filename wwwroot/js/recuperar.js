
const recuperarForm = document.getElementById("recuperarForm");
const recuperarMensaje = document.getElementById("recuperarMensaje");

recuperarForm.addEventListener("submit", async function (event) {
    event.preventDefault();

    const email = document.getElementById("email").value.trim();
    const boton = recuperarForm.querySelector('button[type="submit"]');

    boton.disabled = true;
    boton.textContent = "Enviando...";
    recuperarMensaje.textContent = "";
    recuperarMensaje.style.color = "";

    try {
        const respuesta = await fetch("/api/auth/solicitar-recuperacion", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ email: email })
        });

        const datos = await respuesta.json().catch(() => ({}));

        if (!respuesta.ok) {
            throw new Error(
                datos.mensaje ||
                "No se pudo procesar la solicitud. Intentá nuevamente."
            );
        }

        recuperarMensaje.textContent =
            datos.mensaje ||
            "Si el correo está registrado, recibirás instrucciones para recuperar tu contraseña.";

        recuperarMensaje.style.color = "#7654b8";
        recuperarForm.reset();

    } catch (error) {
        console.error("Error al solicitar la recuperación:", error);

        recuperarMensaje.textContent =
            "No pudimos procesar la solicitud. Intentá nuevamente más tarde.";

        recuperarMensaje.style.color = "#c0392b";

    } finally {
        boton.disabled = false;
        boton.textContent = "Enviar instrucciones";
    }
});
