package com.codenova.acompaar;

import android.Manifest;
import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;
import android.util.AtomicFile;
import org.json.JSONArray;
import org.json.JSONObject;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.time.ZoneId;
import java.util.Iterator;

/** Cached reminders and outbox are private, excluded from backups and scoped to one account. */
final class ReminderNotifications {
    private static final String CHANNEL = "care_reminders";
    private static final Object NETWORK_LOCK = new Object();
    private ReminderNotifications() { }

    private static AtomicFile file(Context c) {
        return new AtomicFile(new File(c.getNoBackupFilesDir(), "reminders.json"));
    }
    private static JSONObject read(Context c) {
        try { return new JSONObject(new String(file(c).readFully(), StandardCharsets.UTF_8)); }
        catch (Exception e) { return new JSONObject(); }
    }
    private static boolean write(Context c, JSONObject data) {
        AtomicFile target = file(c);
        FileOutputStream output = null;
        try {
            output = target.startWrite();
            output.write(data.toString().getBytes(StandardCharsets.UTF_8));
            target.finishWrite(output);
            return true;
        } catch (Exception e) {
            if (output != null) target.failWrite(output);
            return false;
        }
    }
    private static JSONObject object(JSONObject root, String key) {
        JSONObject result = root.optJSONObject(key);
        return result == null ? new JSONObject() : result;
    }
    private static void put(JSONObject o, String key, Object value) {
        try { o.put(key, value); } catch (org.json.JSONException e) { throw new IllegalStateException(e); }
    }
    private static NotificationManager manager(Context c) {
        NotificationManager manager = c.getSystemService(NotificationManager.class);
        manager.createNotificationChannel(new NotificationChannel(CHANNEL, "Medicamentos y turnos médicos",
            NotificationManager.IMPORTANCE_HIGH));
        return manager;
    }
    private static PendingIntent open(Context c, int id) {
        return PendingIntent.getActivity(c, id, new Intent(c, MainActivity.class)
            .setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP),
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
    private static PendingIntent action(Context c, int id, String action, boolean done) {
        Intent intent = new Intent(c, ReminderReceiver.class).setAction(action)
            .setData(android.net.Uri.parse("acompaar://reminder/" + id + "/" + action + "/" + done))
            .putExtra("id", id).putExtra("done", done)
            .putExtra("generation", read(c).optString("generation"));
        return PendingIntent.getBroadcast(c, 0, intent,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
    private static void notify(Context c, int id, String title, String text, boolean answers, boolean medicine) {
        NotificationManager nm = manager(c);
        if (Build.VERSION.SDK_INT >= 33 && c.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
            != PackageManager.PERMISSION_GRANTED) return;
        Notification.Builder builder = new Notification.Builder(c, CHANNEL)
            .setSmallIcon(R.drawable.ic_pill).setContentTitle(title).setContentText(text)
            .setStyle(new Notification.BigTextStyle().bigText(text)).setContentIntent(open(c, id))
            .setVisibility(Notification.VISIBILITY_PRIVATE).setOnlyAlertOnce(true).setAutoCancel(!answers);
        if (answers) {
            builder.addAction(new Notification.Action.Builder(null, medicine ? "Lo tomé" : "Asistí",
                action(c, id, "care.answer", true)).build());
            builder.addAction(new Notification.Action.Builder(null, medicine ? "No lo tomé" : "No asistí",
                action(c, id, "care.answer", false)).build());
            builder.addAction(new Notification.Action.Builder(null, "Escuchar",
                action(c, id, "care.speak", false)).build());
        }
        nm.notify("care", id, builder.build());
    }
    private static long time(JSONObject item) {
        return ReminderTiming.scheduledMillis(item.optString("fecha"), item.optString("hora"), ZoneId.systemDefault());
    }
    private static void cancel(Context c, int id) {
        c.getSystemService(AlarmManager.class).cancel(action(c, id, "care.alarm", false));
        manager(c).cancel("care", id);
    }
    private static void schedule(Context c, JSONObject item) {
        int id = item.optInt("idRecordatorio");
        long when = time(item);
        if (id <= 0 || when < 0) return;
        if (when <= System.currentTimeMillis()) { deliver(c, id); return; }
        AlarmManager alarms = c.getSystemService(AlarmManager.class);
        PendingIntent intent = action(c, id, "care.alarm", false);
        try {
            if (Build.VERSION.SDK_INT < 31 || alarms.canScheduleExactAlarms())
                alarms.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, when, intent);
            else alarms.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, when, intent);
        } catch (SecurityException e) { alarms.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, when, intent); }
    }

    static synchronized void activate(Context c, JSONObject user) {
        JSONObject data = read(c);
        int owner = user.optInt("idUsuario");
        if (owner <= 0) return;
        if (data.optInt("owner") != owner || !BuildConfig.API_BASE_URL.equals(data.optString("origin"))) {
            clear(c);
            data = new JSONObject();
        }
        put(data, "owner", owner);
        put(data, "origin", BuildConfig.API_BASE_URL);
        if (!data.has("generation")) put(data, "generation", java.util.UUID.randomUUID().toString());
        write(c, data);
        restore(c);
        ReminderSyncService.schedule(c, true);
        ReminderSyncService.schedule(c, false);
    }

    static synchronized void clear(Context c) {
        ReminderSpeechService.stop(c);
        JSONObject items = object(read(c), "items");
        Iterator<String> keys = items.keys();
        while (keys.hasNext()) cancel(c, Integer.parseInt(keys.next()));
        file(c).delete();
        c.getSystemService(android.app.job.JobScheduler.class).cancel(8101);
        c.getSystemService(android.app.job.JobScheduler.class).cancel(8102);
        manager(c).cancelAll();
    }

    static synchronized void update(Context c, JSONObject panel) {
        JSONObject data = read(c);
        if (data.optInt("owner") <= 0) return;
        JSONObject old = object(data, "items"), items = new JSONObject();
        JSONObject outbox = object(data, "outbox"), shown = object(data, "shown");
        JSONArray reminders = panel.optJSONArray("recordatorios");
        if (reminders == null) return;
        for (int i = 0; i < reminders.length(); i++) {
            JSONObject item = reminders.optJSONObject(i);
            if (item == null || item.optInt("idRecordatorio") <= 0) continue;
            String key = Integer.toString(item.optInt("idRecordatorio"));
            if (!item.isNull("cumplimiento") || "CANCELADO".equals(item.optString("estado"))) {
                outbox.remove(key); shown.remove(key); cancel(c, Integer.parseInt(key));
            } else if ("MEDICAMENTO".equals(item.optString("tipo")) || "TURNO".equals(item.optString("tipo"))) {
                if (time(item) != time(object(old, key))) shown.remove(key);
                put(items, key, item);
            }
        }
        Iterator<String> keys = old.keys();
        while (keys.hasNext()) {
            String key = keys.next();
            if (!items.has(key)) { cancel(c, Integer.parseInt(key)); outbox.remove(key); shown.remove(key); }
        }
        put(data, "items", items); put(data, "outbox", outbox); put(data, "shown", shown);
        if (write(c, data)) restore(c);
    }

    static synchronized void restore(Context c) {
        JSONObject items = object(read(c), "items");
        Iterator<String> keys = items.keys();
        while (keys.hasNext()) schedule(c, object(items, keys.next()));
    }

    static synchronized void deliver(Context c, int id) {
        JSONObject data = read(c);
        String key = Integer.toString(id);
        JSONObject item = object(object(data, "items"), key);
        JSONObject shown = object(data, "shown");
        if (!item.has("idRecordatorio") || shown.has(key) || object(data, "outbox").has(key)
            || time(item) > System.currentTimeMillis()) return;
        // Avoid replaying historic reminders after installation or a long absence.
        if (!ReminderTiming.dueToday(item.optString("fecha"), item.optString("hora"),
            System.currentTimeMillis(), ZoneId.systemDefault())) return;
        if (!manager(c).areNotificationsEnabled()) return;
        notify(c, id, item.optString("nombre"), item.optString("hora") + " · " + item.optString("detalle"),
            true, "MEDICAMENTO".equals(item.optString("tipo")));
        put(shown, key, true); put(data, "shown", shown); write(c, data);
        if (ReminderSpeechService.enabled(c)) readAloud(c, id);
    }

    static synchronized void readAloud(Context c, int id) {
        JSONObject data = read(c);
        String key = Integer.toString(id);
        JSONObject item = object(object(data, "items"), key);
        if (!item.has("idRecordatorio") || object(data, "outbox").has(key)
            || !ReminderTiming.dueToday(item.optString("fecha"), item.optString("hora"),
                System.currentTimeMillis(), ZoneId.systemDefault())) return;
        ReminderSpeechService.play(c, ReminderSpeechText.format("MEDICAMENTO".equals(item.optString("tipo")),
            item.optString("nombre"), item.optString("hora"), item.optString("detalle")));
    }

    static synchronized void answer(Context c, int id, boolean done) {
        JSONObject data = read(c);
        String key = Integer.toString(id);
        JSONObject item = object(object(data, "items"), key);
        JSONObject outbox = object(data, "outbox");
        if (!item.has("idRecordatorio") || outbox.has(key) || time(item) > System.currentTimeMillis()) return;
        ReminderSpeechService.stop(c);
        put(outbox, key, done ? "CUMPLIDO" : "NO_CUMPLIDO"); put(data, "outbox", outbox);
        put(data, "activityAt", System.currentTimeMillis());
        if (!write(c, data)) {
            notify(c, id, "No pudimos conservar la respuesta", "Abrí la app para volver a registrar tu respuesta.", false, false);
            return;
        }
        notify(c, id, "Respuesta pendiente de envío", "Se guardará cuando haya conexión y una sesión vigente.", false, false);
    }

    static synchronized void completed(Context c, int id) {
        JSONObject data = read(c);
        String key = Integer.toString(id);
        for (String field : new String[]{"items", "outbox", "shown"}) {
            JSONObject map = object(data, field); map.remove(key); put(data, field, map);
        }
        write(c, data); cancel(c, id);
    }

    static synchronized boolean isPending(Context c, int id) {
        return object(read(c), "outbox").has(Integer.toString(id));
    }

    static synchronized boolean accepts(Context c, Intent intent) {
        String generation = read(c).optString("generation");
        return !generation.isEmpty() && generation.equals(intent.getStringExtra("generation"));
    }

    private static synchronized boolean isCurrent(Context c, JSONObject snapshot) {
        return snapshot.has("generation")
            && snapshot.optString("generation").equals(read(c).optString("generation"));
    }

    static boolean synchronize(Context c) {
        synchronized (NETWORK_LOCK) {
            AuthClient api = new AuthClient(c);
            JSONObject user = api.restoreSession();
            JSONObject snapshot;
            synchronized (ReminderNotifications.class) { snapshot = read(c); }
            if (user == null) {
                JSONObject queue = object(snapshot, "outbox");
                Iterator<String> keys = queue.keys();
                while (keys.hasNext()) notify(c, Integer.parseInt(keys.next()), "Ingresá para enviar tu respuesta",
                    "La respuesta sigue pendiente. Abrí la app e iniciá sesión con tu cuenta.", false, false);
                return false;
            }
            if (snapshot.optInt("owner") != user.optInt("idUsuario")
                || !BuildConfig.API_BASE_URL.equals(snapshot.optString("origin"))) return false;
            JSONObject queue = object(snapshot, "outbox");
            Iterator<String> keys = queue.keys();
            while (keys.hasNext()) {
                String key = keys.next();
                if (!isCurrent(c, snapshot)) return false;
                try {
                    api.postWithActivity("/api/recordatorios/" + key + "/cumplimiento",
                        new JSONObject().put("estado", queue.getString(key)), snapshot.optLong("activityAt"));
                    synchronized (ReminderNotifications.class) {
                        if (!isCurrent(c, snapshot)) return false;
                        completed(c, Integer.parseInt(key));
                        notify(c, Integer.parseInt(key), "Respuesta guardada", "Tu respuesta quedó registrada.", false, false);
                    }
                } catch (AuthClient.ApiException e) {
                    if (!isCurrent(c, snapshot)) return false;
                    if (e.status == 401) {
                        notify(c, Integer.parseInt(key), "Ingresá para enviar tu respuesta",
                            "La sesión venció. Tu respuesta sigue pendiente.", false, false);
                        return false;
                    }
                    if (e.status == 409 || e.status == 404 || e.status == 403 || e.status == 400) {
                        synchronized (ReminderNotifications.class) {
                            if (!isCurrent(c, snapshot)) return false;
                            completed(c, Integer.parseInt(key));
                            notify(c, Integer.parseInt(key), "Revisá el recordatorio",
                                e.status == 409 ? "Ya se había registrado una respuesta. Abrí la app para consultar el resultado." : e.getMessage(),
                                false, false);
                        }
                    } else return true;
                } catch (Exception e) { return true; }
            }
            try {
                JSONObject panel = api.get("/api/mayor/panel");
                synchronized (ReminderNotifications.class) { if (isCurrent(c, snapshot)) update(c, panel); }
                return false;
            }
            catch (AuthClient.ApiException e) { return e.status != 401 && e.status != 403; }
            catch (Exception e) { return true; }
        }
    }
}
