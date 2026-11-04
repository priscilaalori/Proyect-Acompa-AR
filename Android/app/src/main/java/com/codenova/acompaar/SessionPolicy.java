package com.codenova.acompaar;

/** Expiration is confirmed by the server; reading a session never extends it. */
final class SessionPolicy {
    static final long INACTIVITY_MILLIS = 7L * 24 * 60 * 60 * 1000;
    private SessionPolicy() { }

    static boolean isValid(long expiresAt, long now) {
        long remaining = expiresAt - now;
        return remaining > 0 && remaining <= INACTIVITY_MILLIS + 5 * 60 * 1000;
    }
}
