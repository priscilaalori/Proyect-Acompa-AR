# Pruebas de pautas a lo largo de los dias

Desde la carpeta Android:

```powershell
dotnet run --project tools/medication-schedule/ScheduleChecks.csproj
.\gradlew.bat :app:testDebugUnitTest --rerun-tasks
```

El primer comando compila el archivo de produccion Services/AgendaMedicamento.cs
directamente y verifica 24 escenarios sin cambiar relojes ni escribir en MySQL.
Simula 45 dias y 44 ventanas consecutivas de hoy/manana, comparando cada ventana
con un calendario independiente. Incluye intervalos de 1, 5, 8, 12, 24, 36 y
168 horas, fin de tratamiento, inicio futuro, limites de ventanas, cruce de mes
y continuidad despues de 30 dias. Guarda app/build/medication-schedule/results.json.

ReminderTimingTest comprueba ademas 135 tomas cada ocho horas, su habilitacion
en el instante correcto y que no se vuelvan a habilitar como tomas de ayer.

Estas pruebas validan calculos reales con fechas simuladas. No ejercitan la
insercion SQL, la renovacion por JobScheduler ni la entrega de notificaciones
del sistema. Para validar esos tramos hay que ejecutar un recorrido integrado
con un backend y base de pruebas con reloj controlado y un emulador aislado,
o dejar un tratamiento ficticio activo durante varios dias. Cambiar solo el
reloj del emulador no adelanta CURDATE() de MySQL y no prueba el circuito completo.
