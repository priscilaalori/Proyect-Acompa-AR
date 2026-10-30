package com.codenova.acompaar;

import org.junit.Test;
import java.time.Instant;
import java.time.ZoneId;
import static org.junit.Assert.*;

public class ReminderTimingTest {
    private final ZoneId argentina = ZoneId.of("America/Argentina/Buenos_Aires");
    private final long atNine = Instant.parse("2026-10-30T12:00:00Z").toEpochMilli();

    @Test public void convertsLocalTimeWithoutTreatingItAsUtc() {
        assertEquals(atNine, ReminderTiming.scheduledMillis("2026-10-30", "09:00", argentina));
        assertEquals(atNine, ReminderTiming.scheduledMillis("2026-10-30", "09:00:00", argentina));
    }
    @Test public void becomesAvailableAtScheduledTime() {
        assertFalse(ReminderTiming.dueToday("2026-10-30", "09:00", atNine - 1, argentina));
        assertTrue(ReminderTiming.dueToday("2026-10-30", "09:00", atNine, argentina));
    }
    @Test public void doesNotReplayHistoricalOrFutureReminders() {
        assertFalse(ReminderTiming.dueToday("2026-10-29", "09:00", atNine, argentina));
        assertFalse(ReminderTiming.dueToday("2026-10-31", "09:00", atNine, argentina));
    }
    @Test public void usesLocalDayAroundMidnight() {
        long beforeMidnight = Instant.parse("2026-10-31T02:59:00Z").toEpochMilli();
        assertTrue(ReminderTiming.dueToday("2026-10-30", "23:58", beforeMidnight, argentina));
        assertFalse(ReminderTiming.dueToday("2026-10-30", "23:58", beforeMidnight + 60000, argentina));
    }
    @Test public void rejectsMalformedSchedule() {
        assertEquals(-1, ReminderTiming.scheduledMillis("2026-02-30", "09:00", argentina));
        assertEquals(-1, ReminderTiming.scheduledMillis("2026-10-30", "25:00", argentina));
        assertFalse(ReminderTiming.dueToday("", "", atNine, argentina));
    }

    @Test public void futureEightHourlyDosesBecomeDueOnTheirOwnDayOverFortyFiveDays() {
        java.time.ZonedDateTime first = java.time.ZonedDateTime.of(
            2026, 10, 30, 20, 0, 0, 0, argentina);
        for (int occurrence = 0; occurrence < 45 * 3; occurrence++) {
            java.time.ZonedDateTime dose = first.plusHours(occurrence * 8L);
            String date = dose.toLocalDate().toString();
            String time = dose.toLocalTime().toString();
            long at = dose.toInstant().toEpochMilli();
            assertEquals(at, ReminderTiming.scheduledMillis(date, time, argentina));
            assertFalse(ReminderTiming.dueToday(date, time, at - 1, argentina));
            assertTrue(ReminderTiming.dueToday(date, time, at, argentina));
            assertFalse(ReminderTiming.dueToday(date, time,
                dose.toLocalDate().plusDays(1).atStartOfDay(argentina).toInstant().toEpochMilli(), argentina));
        }
    }

    @Test public void tomorrowDoseBecomesDueAfterMidnightWithoutReplayingYesterday() {
        long midnight = Instant.parse("2026-10-31T03:00:00Z").toEpochMilli();
        long atFour = Instant.parse("2026-10-31T07:00:00Z").toEpochMilli();
        assertFalse(ReminderTiming.dueToday("2026-10-31", "04:00", midnight - 1, argentina));
        assertFalse(ReminderTiming.dueToday("2026-10-31", "04:00", midnight, argentina));
        assertTrue(ReminderTiming.dueToday("2026-10-31", "04:00", atFour, argentina));
        assertFalse(ReminderTiming.dueToday("2026-10-30", "20:00", atFour, argentina));
    }
}
