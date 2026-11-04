package com.codenova.acompaar;

import android.app.job.JobInfo;
import android.app.job.JobParameters;
import android.app.job.JobScheduler;
import android.app.job.JobService;
import android.content.ComponentName;
import android.content.Context;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Network constrained jobs survive process death and retry transient failures. */
public final class ReminderSyncService extends JobService {
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final java.util.Set<JobParameters> stopped = java.util.concurrent.ConcurrentHashMap.newKeySet();
    private volatile boolean destroyed;

    static void schedule(Context context, boolean periodic) {
        JobScheduler scheduler = context.getSystemService(JobScheduler.class);
        int id = periodic ? 8101 : 8102;
        if (scheduler.getPendingJob(id) != null) return;
        JobInfo.Builder job = new JobInfo.Builder(id, new ComponentName(context, ReminderSyncService.class))
            .setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY).setPersisted(true)
            .setBackoffCriteria(30000, JobInfo.BACKOFF_POLICY_EXPONENTIAL);
        if (periodic) job.setPeriodic(15 * 60 * 1000L);
        scheduler.schedule(job.build());
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
