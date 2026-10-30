# Recorrido visible de web y Android

El controlador usa Playwright con Microsoft Edge visible y un contexto separado.
Android se maneja con toques, escritura y desplazamientos sobre los controles de
su jerarquía de accesibilidad mediante ADB. Las cuatro respuestas se ejecutaron
pulsando los botones de las notificaciones de Android; no se simularon llamadas
de cumplimiento para sustituir esos toques.

La variante `e2e` instala `com.codenova.acompaar.e2e`, separada de la aplicación
habitual. `debug` y `e2e` permiten HTTP para el backend local de desarrollo.

## Herramientas

```powershell
.\gradlew.bat :app:assembleE2e
dotnet run --project tools/visible-e2e/VisibleE2e.csproj
```

El navegador acepta comandos JSON por línea con un arreglo `steps`:

```json
{"steps":[{"action":"goto","url":"http://localhost:5232/registro.html"},{"action":"type","selector":"#nombre","value":"Acompanante"}]}
```

Las acciones disponibles son `goto`, `click`, `fill`, `type`, `check`, `select`,
`reload`, `text`, `controls`, `responses`, `screenshot`, `front` y `wait`.
Los formularios se completan mediante sus controles reales. `responses` sirve
para observar las respuestas HTTP que generaron esas interacciones.

```powershell
& tools/visible-e2e/Android-Ui.ps1 -Action dump
& tools/visible-e2e/Android-Ui.ps1 -Action tap -Text 'Lo tomé'
& tools/visible-e2e/Android-Ui.ps1 -Action screenshot -Name 'resultado'
```

El script requiere un emulador `emulator-5554` por defecto; admite `-Device`.
`back` representa la tecla de retroceso de Android y puede cerrar una pantalla
si no hay teclado o diálogo abierto.

Las capturas, la identificación de las cuentas ficticias y la comprobación SQL
quedan en `app/build/visible-e2e`, fuera del control de versiones. La contraseña
no forma parte del informe. Scrcpy se usó como espejo visible del emulador, con
una copia portátil en la misma carpeta de artefactos.

La verificación SQL es de solo lectura, exige las cuentas `@example.invalid`
identificadas en `run.json` y usa la configuración de MySQL del backend sin
imprimir sus credenciales:

```powershell
dotnet run --project tools/e2e-db/VerifyE2e.csproj
```

Este conjunto permite conducir y observar un recorrido integrado; no constituye
todavía una suite de regresión completa que ejecute todos los escenarios con un
único comando. La expiración semanal se comprueba por separado con las pruebas
HTTP de `tools/session-backend/tests` y su reloj simulado.
