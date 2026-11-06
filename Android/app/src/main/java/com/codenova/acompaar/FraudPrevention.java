package com.codenova.acompaar;

final class FraudPrevention {
    private static final long INTERVAL_MS = 8L * 60 * 60 * 1000;
    private static final String[][] TIPS = {
        {"Tus claves son privadas", "No compartas contraseñas, PIN ni códigos que recibas por mensaje, aunque alguien diga que trabaja en tu banco. Cortá la comunicación y consultá al banco por un canal oficial que ya conozcas."},
        {"Verificá antes de transferir", "Si alguien te pide dinero por teléfono o WhatsApp, hacé una pausa. Llamá a esa persona al número que ya tenías guardado y consultá con un contacto de confianza antes de transferir."},
        {"Cuidado con los falsos llamados del banco", "Si te llaman diciendo que son del banco y te piden claves o transferir dinero para proteger tus ahorros, cortá. Contactá vos al banco desde su app oficial o el número que figura en tu tarjeta."},
        {"No entregues efectivo por un llamado", "Si te llaman para que entregues dinero en tu casa, aunque digan que es por un familiar o para cambiar billetes, no lo entregues. Cortá y hablá con un contacto de confianza al número que ya tenías guardado."},
        {"Desconfiá de los enlaces inesperados", "Si un mensaje te apura para abrir un enlace y poner tus claves, no lo hagas. Abrí vos la app oficial o escribí la dirección que ya conocés. Consultá con alguien de confianza si tenés dudas."},
        {"Una urgencia también puede ser un engaño", "Si alguien dice ser un familiar y necesita dinero urgente, cortá y llamalo al número que ya tenías guardado. No te guíes solo por su voz, su foto o su nombre en WhatsApp."},
        {"No pagues para cobrar un premio", "Si te ofrecen un premio o beneficio y te piden transferir dinero para recibirlo, frená. Verificá la oferta por un canal oficial antes de pagar o compartir tus datos."},
        {"No sigas instrucciones en el cajero por teléfono", "Si alguien te llama y te guía para usar el cajero o la app del banco, cortá la comunicación. No hagas operaciones mientras seguís indicaciones de un llamado inesperado."}
    };

    static boolean isDue(long lastShown, long now) {
        return isDue(lastShown, now, INTERVAL_MS);
    }

    static boolean isDue(long lastShown, long now, long intervalMs) {
        return lastShown <= 0 || now < lastShown || now - lastShown >= intervalMs;
    }

    static boolean canShow(long lastShown, long now, java.time.ZoneId zone) {
        return canShow(lastShown, now, zone, INTERVAL_MS);
    }

    static boolean canShow(long lastShown, long now, java.time.ZoneId zone, long intervalMs) {
        return canShow(lastShown, now, zone, intervalMs, 22 * 60, 8 * 60);
    }

    static boolean isQuiet(java.time.ZonedDateTime local, int start, int end) {
        int minute = local.getHour() * 60 + local.getMinute();
        if (start == end) return true;
        return start > end ? minute >= start || minute < end : minute >= start && minute < end;
    }

    static boolean canShow(long lastShown, long now, java.time.ZoneId zone, long intervalMs, int start, int end) {
        return !isQuiet(java.time.Instant.ofEpochMilli(now).atZone(zone), start, end)
            && isDue(lastShown, now, intervalMs);
    }

    static int normalizeIndex(int index) { return Math.floorMod(index, TIPS.length); }
    static long nextDelivery(long lastShown, long now, java.time.ZoneId zone, long intervalMs) {
        return nextDelivery(lastShown, now, zone, intervalMs, 22 * 60, 8 * 60);
    }
    static long nextDelivery(long lastShown, long now, java.time.ZoneId zone, long intervalMs, int start, int end) {
        long candidate = Math.max(now + 1000,
            lastShown > 0 && lastShown <= now ? lastShown + intervalMs : now + 1000);
        java.time.ZonedDateTime local = java.time.Instant.ofEpochMilli(candidate).atZone(zone);
        if (isQuiet(local, start, end)) {
            java.time.LocalDate endDate = local.toLocalDate();
            if (start >= end && local.getHour() * 60 + local.getMinute() >= start) endDate = endDate.plusDays(1);
            local = endDate.atTime(end / 60, end % 60).atZone(zone);
            if (local.toInstant().toEpochMilli() <= candidate) local = endDate.plusDays(1).atTime(end / 60, end % 60).atZone(zone);
        }
        return local.toInstant().toEpochMilli();
    }
    static int nextIndex(int index) { return (normalizeIndex(index) + 1) % TIPS.length; }
    static String title(int index) { return TIPS[normalizeIndex(index)][0]; }
    static String message(int index) { return TIPS[normalizeIndex(index)][1]; }
}
