package com.codenova.acompaar;

import org.junit.Test;
import static org.junit.Assert.*;

public class FraudPreventionTest {
    @Test public void configurableOvernightRestRespectsMinutesAndBoundary() {
        java.time.ZoneId zone = java.time.ZoneId.of("America/Argentina/Buenos_Aires");
        long interval = 3 * 60 * 60_000L;
        assertTrue(FraudPrevention.canShow(0, at("2026-11-03T21:29:59"), zone, interval, 21 * 60 + 30, 9 * 60));
        assertFalse(FraudPrevention.canShow(0, at("2026-11-03T21:30:00"), zone, interval, 21 * 60 + 30, 9 * 60));
        assertFalse(FraudPrevention.canShow(0, at("2026-11-04T08:59:59"), zone, interval, 21 * 60 + 30, 9 * 60));
        assertTrue(FraudPrevention.canShow(0, at("2026-11-04T09:00:00"), zone, interval, 21 * 60 + 30, 9 * 60));
        assertEquals(at("2026-11-04T09:00:00"), FraudPrevention.nextDelivery(at("2026-11-03T20:00:00"),
            at("2026-11-03T20:00:00"), zone, interval, 21 * 60 + 30, 9 * 60));
    }
    @Test public void sameDayRestAndDelayedDeliveryRemainQuiet() {
        java.time.ZoneId zone = java.time.ZoneId.of("America/Argentina/Buenos_Aires");
        assertEquals(at("2026-11-03T15:30:00"), FraudPrevention.nextDelivery(0, at("2026-11-03T14:00:00"),
            zone, 60_000, 13 * 60, 15 * 60 + 30));
        assertFalse(FraudPrevention.canShow(at("2026-11-03T12:00:00"), at("2026-11-03T13:10:00"),
            zone, 60_000, 13 * 60, 15 * 60 + 30));
        assertTrue(FraudPrevention.canShow(0, at("2026-11-03T15:30:00"), zone, 60_000, 13 * 60, 15 * 60 + 30));
    }
    @Test public void backgroundDeliveryMovesNightDeadlineToMorning() {
        java.time.ZoneId zone = java.time.ZoneId.of("America/Argentina/Buenos_Aires");
        assertEquals(at("2026-11-04T08:00:00"), FraudPrevention.nextDelivery(
            at("2026-11-03T16:00:00"), at("2026-11-03T16:00:00"), zone, 8L * 60 * 60 * 1000));
        assertEquals(at("2026-11-04T08:00:00"), FraudPrevention.nextDelivery(
            0, at("2026-11-04T07:30:00"), zone, 60_000));
    }
    @Test public void backgroundTestSchedulesOneMinuteAndSkipsMissedPeriods() {
        java.time.ZoneId zone = java.time.ZoneId.of("America/Argentina/Buenos_Aires");
        long now = at("2026-11-03T10:00:00");
        assertEquals(now + 60_000, FraudPrevention.nextDelivery(now, now, zone, 60_000));
        assertEquals(now + 1000, FraudPrevention.nextDelivery(now - 10 * 60_000, now, zone, 60_000));
        assertEquals(now + 1000, FraudPrevention.nextDelivery(now + 60_000, now, zone, 60_000));
    }
    @Test public void developmentIntervalShowsAfterOneMinuteButStillRespectsSleep() {
        java.time.ZoneId zone = java.time.ZoneId.of("America/Argentina/Buenos_Aires");
        long shown = at("2026-11-03T10:00:00");
        assertFalse(FraudPrevention.canShow(shown, shown + 59_999, zone, 60_000));
        assertTrue(FraudPrevention.canShow(shown, shown + 60_000, zone, 60_000));
        assertFalse(FraudPrevention.canShow(shown, at("2026-11-03T22:00:00"), zone, 60_000));
    }
    @Test public void firstVisitShowsTip() {
        assertTrue(FraudPrevention.isDue(0, 1000));
    }
    @Test public void reopeningDoesNotShowTipBefore8Hours() {
        long shown = 1000;
        assertFalse(FraudPrevention.isDue(shown, shown + 8L * 60 * 60 * 1000 - 1));
        assertTrue(FraudPrevention.isDue(shown, shown + 8L * 60 * 60 * 1000));
    }
    private long at(String localTime) {
        return java.time.LocalDateTime.parse(localTime).atZone(java.time.ZoneId.of("America/Argentina/Buenos_Aires")).toInstant().toEpochMilli();
    }
    @Test public void quietHoursUseLocalTimeIncludingBoundaries() {
        java.time.ZoneId zone = java.time.ZoneId.of("America/Argentina/Buenos_Aires");
        assertFalse(FraudPrevention.canShow(0, at("2026-11-03T07:59:59"), zone));
        assertTrue(FraudPrevention.canShow(0, at("2026-11-03T08:00:00"), zone));
        assertTrue(FraudPrevention.canShow(0, at("2026-11-03T21:59:59"), zone));
        assertFalse(FraudPrevention.canShow(0, at("2026-11-03T22:00:00"), zone));
        assertFalse(FraudPrevention.canShow(0, at("2026-11-04T00:00:00"), zone));
    }
    @Test public void overdueNightTipWaitsUntilMorningWithoutResettingInterval() {
        java.time.ZoneId zone = java.time.ZoneId.of("America/Argentina/Buenos_Aires");
        long lastShown = at("2026-11-03T16:00:00");
        assertFalse(FraudPrevention.canShow(lastShown, at("2026-11-04T00:00:00"), zone));
        assertTrue(FraudPrevention.canShow(lastShown, at("2026-11-04T08:00:00"), zone));
        assertFalse(FraudPrevention.canShow(at("2026-11-04T08:00:00"), at("2026-11-04T15:59:59"), zone));
        assertTrue(FraudPrevention.canShow(at("2026-11-04T08:00:00"), at("2026-11-04T16:00:00"), zone));
    }
    @Test public void movingClockBackDoesNotBlockTipsIndefinitely() {
        assertTrue(FraudPrevention.isDue(2000, 1000));
        assertFalse(FraudPrevention.isDue(1000, 1001));
    }
    @Test public void rotationReturnsToFirstTipWithoutRepeatingEarly() {
        java.util.Set<String> titles = new java.util.HashSet<>();
        int index = 0;
        do {
            assertTrue(titles.add(FraudPrevention.title(index)));
            assertFalse(FraudPrevention.message(index).trim().isEmpty());
            index = FraudPrevention.nextIndex(index);
        } while (index != 0 && titles.size() < 100);
        assertEquals(0, index);
        assertEquals(8, titles.size());
    }
    @Test public void invalidSavedIndexIsSafe() {
        assertNotNull(FraudPrevention.title(Integer.MIN_VALUE));
        assertNotNull(FraudPrevention.message(Integer.MAX_VALUE));
    }
}
