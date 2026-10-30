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
}
