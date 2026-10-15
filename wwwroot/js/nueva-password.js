
const nuevaPasswordForm = document.getElementById("nuevaPasswordForm");
const nuevaPasswordMensaje = document.getElementById("nuevaPasswordMensaje");

nuevaPasswordForm.addEventListener("submit", async function (event) {
    event.preventDefault();

    const token = new URLSearchParams(window.location.search).get("token");
    const password = document.getElementById("password").value;
    const confirmarPassword = document.getElementById("confirmarPassword").value;
    const boton = nuevaPasswordForm.querySelector('button[type="submit"]');

    nuevaPasswordMensaje.textContent = "";
    nuevaPasswordMensaje.style.color = "";

    if (!token) {
        nuevaPasswordMensaje.textContent =
            "El enlace no es válido. Solicitá uno nuevo.";
        nuevaPasswordMensaje.style.color = "#c0392b";
        return;
    }

    if (password !== confirmarPassword) {
        nuevaPasswordMensaje.textContent =
            "Las contraseñas no coinciden.";
        nuevaPasswordMensaje.style.color = "#c0392b";
        return;
    }

    boton.disabled = true;
    boton.textContent = "Actualizando...";

    try {
        const respuesta = await fetch("/api/auth/restablecer-password", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                token: token,
                nuevaPassword: password
            })
        });

        const datos = await respuesta.json();

        nuevaPasswordMensaje.textContent = datos.mensaje ||
            "No se pudo procesar la solicitud.";
        nuevaPasswordMensaje.style.color =
            respuesta.ok ? "#7654b8" : "#c0392b";

        if (respuesta.ok) {
            nuevaPasswordForm.reset();
        }
    } catch (error) {
        console.error("Error al restablecer la contraseña:", error);
        nuevaPasswordMensaje.textContent =
            "No se pudo conectar con el servidor. Intentá nuevamente.";
        nuevaPasswordMensaje.style.color = "#c0392b";
    } finally {
        boton.disabled = false;
        boton.textContent = "Restablecer contraseña";
    }
});
