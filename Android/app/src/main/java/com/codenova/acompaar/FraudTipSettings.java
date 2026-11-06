package com.codenova.acompaar;

import android.content.Context;
import android.content.SharedPreferences;

final class FraudTipSettings {
    private static SharedPreferences preferences(Context c) {
        return c.getSharedPreferences("accessibility", Context.MODE_PRIVATE);
    }
    static int intervalMinutes(Context c) {
        return Math.max(1, Math.min(10080, preferences(c).getInt("fraudIntervalMinutes", 480)));
    }
    static long intervalMillis(Context c) { return intervalMinutes(c) * 60_000L; }
    static int quietStart(Context c) { return Math.floorMod(preferences(c).getInt("fraudQuietStart", 22 * 60), 1440); }
    static int quietEnd(Context c) { return Math.floorMod(preferences(c).getInt("fraudQuietEnd", 8 * 60), 1440); }
}
