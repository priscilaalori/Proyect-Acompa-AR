package com.codenova.acompaar;

import java.time.LocalDate;
import java.time.format.DateTimeParseException;

/** Solo muestra hoy y mañana; las respuestas de hoy siguen disponibles hasta terminar el día. */
final class CareVisibility {
    private CareVisibility() { }

    static boolean isCurrent(String date, LocalDate today) {
        try {
            LocalDate scheduled = LocalDate.parse(date);
            return !scheduled.isBefore(today) && !scheduled.isAfter(today.plusDays(1));
        }
        catch (DateTimeParseException | NullPointerException e) { return false; }
    }

    static boolean isTreatmentCurrent(String start, String end, LocalDate today) {
        try {
            return !LocalDate.parse(start).isAfter(today.plusDays(1))
                && (end == null || !LocalDate.parse(end).isBefore(today));
        } catch (DateTimeParseException | NullPointerException e) { return false; }
    }
}
