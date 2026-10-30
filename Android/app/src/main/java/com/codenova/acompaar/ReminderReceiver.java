package com.codenova.acompaar;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** Receives only explicit private actions; system broadcasts restore cached alarms. */
public final class ReminderReceiver extends BroadcastReceiver {
    @Override public void onReceive(Context context, Intent intent) {
        String action = intent.getAction();
        if (("care.answer".equals(action) || "care.alarm".equals(action))
            && !ReminderNotifications.accepts(context, intent)) return;
        if ("care.answer".equals(action)) {
            ReminderNotifications.answer(context, intent.getIntExtra("id", 0),
                intent.getBooleanExtra("done", false));
        } else if ("care.alarm".equals(action)) {
            ReminderNotifications.deliver(context, intent.getIntExtra("id", 0));
        } else {
            ReminderNotifications.restore(context);
        }
        ReminderSyncService.schedule(context, false);
    }
}
