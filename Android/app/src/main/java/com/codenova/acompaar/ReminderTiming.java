package com.codenova.acompaar;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.ZoneId;

final class ReminderTiming {
    private ReminderTiming() { }

    static long scheduledMillis(String date, String time, ZoneId zone) {
        try {
            return LocalDateTime.of(LocalDate.parse(date), LocalTime.parse(time))
                .atZone(zone).toInstant().toEpochMilli();
        } catch (java.time.DateTimeException | NullPointerException e) { return -1; }
    }

    static boolean dueToday(String date, String time, long now, ZoneId zone) {
        long scheduled = scheduledMillis(date, time, zone);
        return scheduled >= 0 && scheduled <= now
            && java.time.Instant.ofEpochMilli(now).atZone(zone).toLocalDate().toString().equals(date);
    }
}
