package com.codenova.acompaar;

import org.junit.Test;
import static org.junit.Assert.*;

public class SessionPolicyTest {
    private static final long NOW = 1800000000000L;

    @Test public void acceptsAFullWeekConfirmedByServer() {
        assertEquals(604800000L, SessionPolicy.INACTIVITY_MILLIS);
        assertTrue(SessionPolicy.isValid(NOW + SessionPolicy.INACTIVITY_MILLIS, NOW));
    }
    @Test public void expiresExactlyAtDeadline() {
        long expires = NOW + SessionPolicy.INACTIVITY_MILLIS;
        assertTrue(SessionPolicy.isValid(expires, expires - 1));
        assertFalse(SessionPolicy.isValid(expires, expires));
        assertFalse(SessionPolicy.isValid(expires, expires + 1));
    }
    @Test public void passiveChecksNeverMoveTheDeadline() {
        long expires = NOW + SessionPolicy.INACTIVITY_MILLIS;
        for (int day = 0; day < 7; day++) assertTrue(SessionPolicy.isValid(expires, NOW + day * 86400000L));
        assertFalse(SessionPolicy.isValid(expires, NOW + 7 * 86400000L));
    }
    @Test public void rejectsAnUnexpectedlyLongServerLifetime() {
        assertFalse(SessionPolicy.isValid(NOW + 2 * SessionPolicy.INACTIVITY_MILLIS, NOW));
    }
}
