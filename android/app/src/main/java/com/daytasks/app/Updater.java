package com.daytasks.app;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.net.Uri;
import android.provider.Settings;

import androidx.core.content.FileProvider;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.function.Consumer;

/**
 * Installs a new version of the app from GitHub Releases.
 * The page finds the release and asks for it (see "update" in wwwroot/index.html); here DayTasks.apk is
 * downloaded to the cache, checked against its .sha256 and handed to the system installer, which asks
 * the user to confirm. The APK is signed with the same key, so it installs over this one and keeps the data.
 */
final class Updater {

    private static final String DOWNLOAD_PREFIX = "https://github.com/" + BuildConfig.UPDATE_REPO + "/releases/download/";

    private final Activity activity;
    private final Consumer<JSONObject> report; // → window.taskWidgetUpdate(...) in the page
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private volatile boolean busy;

    Updater(Activity activity, Consumer<JSONObject> report) {
        this.activity = activity;
        this.report = report;
    }

    /** What the page needs to look for updates. */
    static String info() {
        try {
            return new JSONObject()
                    .put("version", BuildConfig.VERSION_NAME)
                    .put("repo", BuildConfig.UPDATE_REPO)
                    .put("asset", "DayTasks.apk")
                    .toString();
        } catch (JSONException e) {
            return "null";
        }
    }

    void install(String apkUrl, String shaUrl) {
        if (busy) return;
        if (!apkUrl.startsWith(DOWNLOAD_PREFIX) || !shaUrl.startsWith(DOWNLOAD_PREFIX)) {
            send("error", "code", "source");
            return;
        }
        // Android 8+: the user allows installing apps from this app once, in system settings.
        if (!activity.getPackageManager().canRequestPackageInstalls()) {
            send("permission", null, null);
            try {
                activity.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                        Uri.parse("package:" + activity.getPackageName())));
            } catch (ActivityNotFoundException ignored) {
            }
            return;
        }
        busy = true;
        worker.execute(() -> {
            try {
                File apk = download(apkUrl, shaUrl);
                send("installing", null, null);
                Uri uri = FileProvider.getUriForFile(activity, activity.getPackageName() + ".updates", apk);
                Intent intent = new Intent(Intent.ACTION_VIEW)
                        .setDataAndType(uri, "application/vnd.android.package-archive")
                        .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
                activity.runOnUiThread(() -> activity.startActivity(intent));
            } catch (UpdateError e) {
                try {
                    report.accept(new JSONObject().put("state", "error").put("code", e.code).put("status", e.status));
                } catch (JSONException ignored) {
                }
            } catch (Exception e) {
                send("error", "message", e.toString()); // e.g. no connection
            } finally {
                busy = false;
            }
        });
    }

    private File download(String apkUrl, String shaUrl) throws IOException, NoSuchAlgorithmException {
        String expected = readText(shaUrl).trim().split("\\s+")[0];

        File dir = new File(activity.getCacheDir(), "updates");
        if (!dir.isDirectory() && !dir.mkdirs()) throw new UpdateError("space", 0);
        File apk = new File(dir, "DayTasks.apk");

        HttpURLConnection conn = open(apkUrl);
        MessageDigest sha = MessageDigest.getInstance("SHA-256");
        try (InputStream in = conn.getInputStream(); OutputStream out = new FileOutputStream(apk)) {
            long total = conn.getContentLengthLong(), done = 0;
            byte[] buffer = new byte[64 * 1024];
            int read, lastPct = -1;
            while ((read = in.read(buffer)) > 0) {
                out.write(buffer, 0, read);
                sha.update(buffer, 0, read);
                done += read;
                int pct = total > 0 ? (int) (done * 100 / total) : 0;
                if (pct != lastPct) send("progress", "pct", lastPct = pct);
            }
        } finally {
            conn.disconnect();
        }

        StringBuilder actual = new StringBuilder();
        for (byte b : sha.digest()) actual.append(String.format("%02x", b));
        if (!actual.toString().equalsIgnoreCase(expected)) {
            //noinspection ResultOfMethodCallIgnored
            apk.delete();
            throw new UpdateError("checksum", 0);
        }
        return apk;
    }

    private static String readText(String url) throws IOException {
        HttpURLConnection conn = open(url);
        try (InputStream in = conn.getInputStream()) {
            byte[] bytes = new byte[4096];
            int n = in.read(bytes);
            return n > 0 ? new String(bytes, 0, n, "US-ASCII") : "";
        } finally {
            conn.disconnect();
        }
    }

    // GitHub answers release downloads with a redirect to its file storage (https → https, followed automatically)
    private static HttpURLConnection open(String url) throws IOException {
        HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
        conn.setConnectTimeout(20_000);
        conn.setReadTimeout(60_000);
        conn.setRequestProperty("User-Agent", "DayTasks/" + BuildConfig.VERSION_NAME);
        if (conn.getResponseCode() != HttpURLConnection.HTTP_OK) {
            int code = conn.getResponseCode();
            conn.disconnect();
            throw new UpdateError("http", code);
        }
        return conn;
    }

    /** A known failure; the page shows the code in its own language ("source", "space", "checksum", "http" + status). */
    private static final class UpdateError extends IOException {
        final String code;
        final int status;

        UpdateError(String code, int status) {
            super(code);
            this.code = code;
            this.status = status;
        }
    }

    private void send(String state, String key, Object value) {
        try {
            JSONObject status = new JSONObject().put("state", state);
            if (key != null) status.put(key, value);
            report.accept(status);
        } catch (JSONException ignored) {
        }
    }
}
