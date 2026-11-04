package com.codenova.acompaar;

/** Spoken wording uses only the same fields displayed in the reminder. */
final class ReminderSpeechText {
    private ReminderSpeechText() { }

    static String format(boolean medicine, String name, String time, String detail) {
        StringBuilder text = new StringBuilder(medicine
            ? "Es hora de tu medicamento" : "Hoy tenés un turno médico");
        if (present(name)) text.append(": ").append(name.trim());
        text.append('.');
        if (present(time)) text.append(" Horario: ").append(time.trim()).append('.');
        if (present(detail)) text.append(' ').append(detail.trim()).append('.');
        return text.toString();
    }

    private static boolean present(String value) {
        return value != null && !value.trim().isEmpty();
    }
}
