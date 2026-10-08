const formulario = document.getElementById("loginForm");
const mensaje = document.getElementById("loginMensaje");

formulario.addEventListener("submit", async event => {
    event.preventDefault();

    const boton = formulario.querySelector('button[type="submit"]');
    boton.disabled = true;
    mensaje.textContent = "Verificando tus datos…";

    try {
        const respuesta = await fetch("/api/auth/login", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                email: document.getElementById("email").value.trim(),
                password: document.getElementById("password").value
            })
        });

        const datos = await respuesta.json().catch(() => ({}));

        if (!respuesta.ok) {
            mensaje.textContent =
                datos.mensaje || datos.title || "No se pudo validar el ingreso.";
            return;
        }

        document.getElementById("password").value = "";

        mensaje.textContent =
            `¡Hola, ${datos.usuario.nombre}! Tus datos son correctos.`;
    } catch {
        mensaje.textContent = "No pudimos conectar con el servidor.";
    } finally {
        boton.disabled = false;
    }
});