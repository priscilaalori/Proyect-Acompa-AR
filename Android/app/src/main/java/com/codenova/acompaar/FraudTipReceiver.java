package com.codenova.acompaar;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public final class FraudTipReceiver extends BroadcastReceiver {
    @Override public void onReceive(Context context, Intent intent) {
        if ("fraud.deliver".equals(intent.getAction())) FraudTipNotifications.deliver(context);
        else FraudTipNotifications.restore(context);
    }
}
