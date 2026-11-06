package com.codenova.acompaar;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import java.time.ZoneId;

// Uso esto para que el mismo consejo no aparezca dos veces, dentro y fuera de la app.
final class FraudTipNotifications {
    private static final String CHANNEL = "fraud_prevention";
    static final String TIP_EXTRA = "fraud_tip";
    private static SharedPreferences preferences(Context c) {
        return c.getSharedPreferences("fraud_prevention", Context.MODE_PRIVATE);
    }
    private static PendingIntent alarm(Context c) {
        return PendingIntent.getBroadcast(c, 8401,
            new Intent(c, FraudTipReceiver.class).setAction("fraud.deliver"),
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
    static synchronized void activate(Context c) {
        preferences(c).edit().putBoolean("enabled", true).apply();
        restore(c);
    }
    static synchronized void disable(Context c) {
        preferences(c).edit().putBoolean("enabled", false).apply();
        c.getSystemService(AlarmManager.class).cancel(alarm(c));
        c.getSystemService(NotificationManager.class).cancel("fraud", 8401);
    }
    static synchronized void restore(Context c) {
        schedule(c, anchor(c));
    }
    private static long anchor(Context c) {
        return Math.max(preferences(c).getLong("lastShown", 0), preferences(c).getLong("configuredAt", 0));
    }
    static synchronized void settingsChanged(Context c) {
        preferences(c).edit().putLong("configuredAt", System.currentTimeMillis()).apply();
        c.getSystemService(AlarmManager.class).cancel(alarm(c));
        c.getSystemService(NotificationManager.class).cancel("fraud", 8401);
        restore(c);
    }
    private static void schedule(Context c, long lastShown) {
        if (!preferences(c).getBoolean("enabled", false)) return;
        long when = FraudPrevention.nextDelivery(lastShown, System.currentTimeMillis(),
            ZoneId.systemDefault(), FraudTipSettings.intervalMillis(c), FraudTipSettings.quietStart(c), FraudTipSettings.quietEnd(c));
        AlarmManager manager = c.getSystemService(AlarmManager.class);
        try {
            if (BuildConfig.DEBUG && (Build.VERSION.SDK_INT < 31 || manager.canScheduleExactAlarms())) {
                manager.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, when, alarm(c));
                return;
            }
        } catch (SecurityException ignored) { }
        manager.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, when, alarm(c));
    }
    static synchronized int claimTip(Context c) {
        SharedPreferences prefs = preferences(c);
        long now = System.currentTimeMillis();
        if (!prefs.getBoolean("enabled", false) || !FraudPrevention.canShow(anchor(c), now, ZoneId.systemDefault(),
            FraudTipSettings.intervalMillis(c), FraudTipSettings.quietStart(c), FraudTipSettings.quietEnd(c))) return -1;
        int index = FraudPrevention.normalizeIndex(prefs.getInt("nextTip", 0));
        if (!prefs.edit().putLong("lastShown", now).remove("configuredAt")
            .putInt("nextTip", FraudPrevention.nextIndex(index)).commit()) return -1;
        restore(c);
        return index;
    }
    static void createChannel(Context c) {
        NotificationManager manager = c.getSystemService(NotificationManager.class);
        NotificationChannel channel = new NotificationChannel(CHANNEL, "Prevención de estafas", NotificationManager.IMPORTANCE_HIGH);
        channel.setDescription("Consejos para cuidar tus claves y tu dinero. Frecuencia y descanso configurables en Accesibilidad.");
        manager.createNotificationChannel(channel);
    }
    static synchronized void deliver(Context c) {
        createChannel(c);
        NotificationManager manager = c.getSystemService(NotificationManager.class);
        if ((Build.VERSION.SDK_INT >= 33 && c.checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS)
            != PackageManager.PERMISSION_GRANTED) || !manager.areNotificationsEnabled()
            || manager.getNotificationChannel(CHANNEL).getImportance() == NotificationManager.IMPORTANCE_NONE) {
            schedule(c, System.currentTimeMillis());
            return;
        }
        int index = claimTip(c);
        if (index < 0) { restore(c); return; }
        Intent intent = new Intent(c, MainActivity.class).putExtra(TIP_EXTRA, index)
            .setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        PendingIntent open = PendingIntent.getActivity(c, 8401, intent,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        manager.notify("fraud", 8401, new Notification.Builder(c, CHANNEL)
            .setSmallIcon(R.drawable.ic_shield).setContentTitle(FraudPrevention.title(index))
            .setContentText(FraudPrevention.message(index))
            .setStyle(new Notification.BigTextStyle().bigText(FraudPrevention.message(index)))
            .setCategory(Notification.CATEGORY_REMINDER).setContentIntent(open)
            .setVisibility(Notification.VISIBILITY_PUBLIC).setAutoCancel(true).build());
    }
}
