package com.codenova.acompaar;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.media.AudioAttributes;
import android.media.AudioFocusRequest;
import android.media.AudioManager;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;
import android.util.Log;
import android.widget.Toast;
import java.util.ArrayDeque;

/** Short-lived playback service: queued speech, explicit stop, no microphone. */
public final class ReminderSpeechService extends Service {
    private static final String CHANNEL = "care_voice";
    private static final int NOTIFICATION_ID = 8201;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final ArrayDeque<String> queue = new ArrayDeque<>();
    private final Runnable timeout = () -> fail("No se pudo terminar la lectura. Revisá la voz del teléfono.");
    private TextToSpeech speech;
    private AudioManager audio;
    private AudioFocusRequest focus;
    private boolean ready, speaking, closing;
    private String utterance;
    private int sequence;

    static boolean enabled(Context context) {
        return context.getSharedPreferences("reminder_voice", MODE_PRIVATE).getBoolean("enabled", false);
    }

    static void setEnabled(Context context, boolean enabled) {
        context.getSharedPreferences("reminder_voice", MODE_PRIVATE).edit().putBoolean("enabled", enabled).apply();
        if (!enabled) stop(context);
    }

    static void stop(Context context) {
        context.stopService(new Intent(context, ReminderSpeechService.class));
    }

    static boolean play(Context context, String text) {
        try {
            context.startForegroundService(new Intent(context, ReminderSpeechService.class).putExtra("text", text));
            return true;
        } catch (RuntimeException error) {
            // Background-start restrictions must never prevent the normal notification.
            Log.w("ReminderSpeech", "Speech unavailable; reminder remains visible", error);
            return false;
        }
    }

    @Override public void onCreate() {
        super.onCreate();
        audio = getSystemService(AudioManager.class);
    }

    @Override public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent == null || "stop".equals(intent.getAction())) {
            finish();
            return START_NOT_STICKY;
        }
        String text = intent.getStringExtra("text");
        if (closing || text == null || text.trim().isEmpty()) { finish(); return START_NOT_STICKY; }
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.createNotificationChannel(new NotificationChannel(CHANNEL, "Lectura de recordatorios", NotificationManager.IMPORTANCE_LOW));
        PendingIntent stop = PendingIntent.getService(this, 0,
            new Intent(this, ReminderSpeechService.class).setAction("stop"),
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        Notification notification = new Notification.Builder(this, CHANNEL)
            .setSmallIcon(R.drawable.ic_pill).setContentTitle("Leyendo tu recordatorio")
            .setContentText("Podés detener la lectura en cualquier momento.")
            .setVisibility(Notification.VISIBILITY_PRIVATE).setOngoing(true).setOnlyAlertOnce(true)
            .addAction(new Notification.Action.Builder(null, "Detener lectura", stop).build()).build();
        try { startForeground(NOTIFICATION_ID, notification); }
        catch (RuntimeException error) { Log.w("ReminderSpeech", "Playback not allowed", error); finish(); return START_NOT_STICKY; }
        if (audio.getRingerMode() != AudioManager.RINGER_MODE_NORMAL
            || manager.getCurrentInterruptionFilter() != NotificationManager.INTERRUPTION_FILTER_ALL
            || audio.getStreamVolume(AudioManager.STREAM_ALARM) == 0) {
            fail("La lectura está silenciada. Revisá el sonido y el volumen de alarmas del teléfono.");
            return START_NOT_STICKY;
        }
        // Bound both queue and utterance length. Each item is spoken in order, never flushed by another reminder.
        if (queue.size() < 20) queue.add(text.substring(0, Math.min(text.length(), TextToSpeech.getMaxSpeechInputLength() - 1)));
        if (speech == null) {
            handler.postDelayed(timeout, 20000);
            speech = new TextToSpeech(getApplicationContext(), status -> handler.post(() -> initialized(status)));
        } else if (ready) next();
        return START_NOT_STICKY;
    }

    private void initialized(int status) {
        if (closing) return;
        if (status != TextToSpeech.SUCCESS) { fail("No se encontró una voz disponible en el teléfono."); return; }
        // Prefer an installed Spanish voice so medication details need not leave the device.
        Voice selected = null;
        if (speech.getVoices() != null) for (Voice voice : speech.getVoices()) {
            if (!"es".equals(voice.getLocale().getLanguage()) || voice.isNetworkConnectionRequired()) continue;
            if (selected == null || "AR".equals(voice.getLocale().getCountry())) selected = voice;
        }
        if (selected == null || speech.setVoice(selected) != TextToSpeech.SUCCESS) {
            fail("Instalá una voz en español sin conexión en los ajustes de texto a voz del teléfono.");
            return;
        }
        AudioAttributes attributes = new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ALARM)
            .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH).build();
        speech.setAudioAttributes(attributes);
        speech.setSpeechRate(0.9f);
        focus = new AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN_TRANSIENT)
            .setAudioAttributes(attributes).setOnAudioFocusChangeListener(change -> {
                if (change < 0) finish();
            }, handler).build();
        if (audio.requestAudioFocus(focus) != AudioManager.AUDIOFOCUS_REQUEST_GRANTED) { finish(); return; }
        speech.setOnUtteranceProgressListener(new UtteranceProgressListener() {
            @Override public void onStart(String id) {
                if (BuildConfig.DEBUG) Log.d("ReminderSpeech", "Playback started: " + id);
            }
            @Override public void onDone(String id) { handler.post(() -> completed(id, false)); }
            @Override public void onError(String id) { handler.post(() -> completed(id, true)); }
        });
        ready = true;
        next();
    }

    private void next() {
        if (closing || speaking) return;
        handler.removeCallbacks(timeout);
        String text = queue.poll();
        if (text == null) { finish(); return; }
        utterance = "care-" + (++sequence);
        speaking = true;
        handler.postDelayed(timeout, 120000);
        if (speech.speak(text, TextToSpeech.QUEUE_ADD, null, utterance) == TextToSpeech.ERROR)
            fail("No se pudo leer el recordatorio. Revisá la voz del teléfono.");
    }

    private void completed(String id, boolean error) {
        if (closing || !id.equals(utterance)) return;
        if (error) { fail("No se pudo leer el recordatorio. Revisá la voz del teléfono."); return; }
        if (BuildConfig.DEBUG) Log.d("ReminderSpeech", "Playback completed: " + id);
        speaking = false;
        next();
    }

    private void fail(String message) {
        Toast.makeText(this, message, Toast.LENGTH_LONG).show();
        finish();
    }

    private void finish() {
        if (closing) return;
        closing = true;
        handler.removeCallbacksAndMessages(null);
        queue.clear();
        if (speech != null) speech.stop();
        stopForeground(STOP_FOREGROUND_REMOVE);
        stopSelf();
    }

    @Override public void onDestroy() {
        closing = true;
        handler.removeCallbacksAndMessages(null);
        if (speech != null) { speech.stop(); speech.shutdown(); }
        if (focus != null) audio.abandonAudioFocusRequest(focus);
        super.onDestroy();
    }

    @Override public IBinder onBind(Intent intent) { return null; }
}
