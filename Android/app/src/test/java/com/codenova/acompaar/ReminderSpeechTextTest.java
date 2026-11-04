package com.codenova.acompaar;

import org.junit.Test;
import static org.junit.Assert.*;

public class ReminderSpeechTextTest {
    @Test public void medicineKeepsNameDoseAndTimeFromNotification() {
        assertEquals("Es hora de tu medicamento: Medicamento de prueba. Horario: 20:00. Dosis de prueba.",
            ReminderSpeechText.format(true, "Medicamento de prueba", "20:00", "Dosis de prueba"));
    }

    @Test public void appointmentKeepsSpecialtyPlaceAndTimeFromNotification() {
        assertEquals("Hoy tenés un turno médico: Cardiología. Horario: 15:30. Consultorio de prueba.",
            ReminderSpeechText.format(false, "Cardiología", "15:30", "Consultorio de prueba"));
    }

    @Test public void absentFieldsDoNotInventMedicalInstructions() {
        assertEquals("Es hora de tu medicamento.", ReminderSpeechText.format(true, null, "", "  "));
    }
}
