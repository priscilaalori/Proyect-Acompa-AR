package com.codenova.acompaar;

import android.app.job.JobInfo;
import android.app.job.JobParameters;
import android.app.job.JobScheduler;
import android.app.job.JobService;
import android.content.ComponentName;
import android.content.Context;
import android.os.Build;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class ReminderSyncService extends JobService {
    private static final int PERIODIC_JOB = 8101;
    private static final int SYNC_JOB = 8102;
    private static final int ANSWER_JOB = 8103;
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final java.util.Set<JobParameters> stopped = java.util.concurrent.ConcurrentHashMap.newKeySet();
    private volatile boolean destroyed;

    static void schedule(Context context, boolean periodic) {
        schedule(context, periodic ? PERIODIC_JOB : SYNC_JOB);
    }

    static void scheduleAnswer(Context context) {
        if (Build.VERSION.SDK_INT < 31 || !schedule(context, ANSWER_JOB)) {
            schedule(context, false);
        }
    }

    static void cancel(Context context) {
        JobScheduler scheduler = context.getSystemService(JobScheduler.class);
        scheduler.cancel(PERIODIC_JOB);
        scheduler.cancel(SYNC_JOB);
        scheduler.cancel(ANSWER_JOB);
    }

    private static boolean schedule(Context context, int id) {
        JobScheduler scheduler = context.getSystemService(JobScheduler.class);
        // A running answer job may have already taken its snapshot of the queue.
        if (scheduler.getPendingJob(id) != null) return id != ANSWER_JOB;
        JobInfo.Builder job = new JobInfo.Builder(id, new ComponentName(context, ReminderSyncService.class))
            .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY).setPersisted(true)
            .setBackoffCriteria(30000, JobInfo.BACKOFF_POLICY_EXPONENTIAL);
        if (id == PERIODIC_JOB) job.setPeriodic(15 * 60 * 1000L);
        if (id == ANSWER_JOB && Build.VERSION.SDK_INT >= 31) job.setExpedited(true);
        return scheduler.schedule(job.build()) == JobScheduler.RESULT_SUCCESS;
    }

    @Override public boolean onStartJob(JobParameters params) {
        executor.execute(() -> {
            boolean retry = ReminderNotifications.synchronize(this);
            new android.os.Handler(android.os.Looper.getMainLooper()).post(() -> {
                if (!destroyed && !stopped.remove(params)) jobFinished(params, retry);
            });
        });
        return true;
    }

    @Override public boolean onStopJob(JobParameters params) { stopped.add(params); return true; }
    @Override public void onDestroy() { destroyed = true; executor.shutdownNow(); super.onDestroy(); }
}
