package com.codenova.acompaar;

import org.json.JSONObject;
import java.net.CookieManager;
import java.net.CookiePolicy;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.HttpCookie;
import org.json.JSONArray;
import java.io.InputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;

/** Conserva la cookie de sesión sin almacenar la contraseña. */
final class AuthClient {
    private final CookieManager cookies = new CookieManager(null, CookiePolicy.ACCEPT_ORIGINAL_SERVER);
    private final SessionStorage storage;
    private static final Object SESSION_LOCK = new Object();
    private static final Object REQUEST_LOCK = new Object();
    private String sessionId;
    private long expiresAt;

    AuthClient(android.content.Context context) { storage = new SessionStorage(context); }

    void clearSession() {
        cookies.getCookieStore().removeAll();
        synchronized (SESSION_LOCK) { storage.clear(); }
        sessionId = null;
        expiresAt = 0;
    }

    void rememberSession(JSONObject user) throws Exception {
        if (!SessionPolicy.isValid(expiresAt, System.currentTimeMillis()))
            throw new IllegalStateException("The server must confirm the session expiration");
        synchronized (SESSION_LOCK) {
            sessionId = java.util.UUID.randomUUID().toString();
            try { storage.save(snapshot(user)); }
            catch (Exception e) { sessionId = null; throw e; }
        }
    }

    private JSONObject snapshot(JSONObject user) throws Exception {
        JSONArray savedCookies = new JSONArray();
        for (HttpCookie cookie : cookies.getCookieStore().getCookies()) {
            if (cookie.hasExpired()) continue;
            savedCookies.put(new JSONObject().put("name", cookie.getName()).put("value", cookie.getValue())
                .put("path", cookie.getPath()).put("secure", cookie.getSecure())
                .put("httpOnly", cookie.isHttpOnly()).put("maxAge", cookie.getMaxAge())
                .put("version", cookie.getVersion()));
        }
        if (savedCookies.length() == 0) throw new IllegalStateException("No session cookie");
        return new JSONObject().put("origin", BuildConfig.API_BASE_URL)
            .put("expiresAt", expiresAt).put("sessionId", sessionId)
            .put("user", user).put("cookies", savedCookies);
    }

    JSONObject restoreSession() {
        synchronized (SESSION_LOCK) {
        JSONObject saved = storage.load();
        if (saved == null) return null;
        try {
            long remaining = saved.getLong("expiresAt") - System.currentTimeMillis();
            if (!SessionPolicy.isValid(saved.getLong("expiresAt"), System.currentTimeMillis())
                || !BuildConfig.API_BASE_URL.equals(saved.getString("origin"))) throw new IllegalStateException();
            JSONObject user = saved.getJSONObject("user");
            if (!"MAYOR".equals(user.optString("tipoUsuario"))) throw new IllegalStateException();
            JSONArray savedCookies = saved.getJSONArray("cookies");
            if (savedCookies.length() == 0) throw new IllegalStateException();
            cookies.getCookieStore().removeAll();
            for (int i = 0; i < savedCookies.length(); i++) {
                JSONObject item = savedCookies.getJSONObject(i);
                HttpCookie cookie = new HttpCookie(item.getString("name"), item.getString("value"));
                cookie.setVersion(item.optInt("version", 0));
                cookie.setPath(item.optString("path", "/"));
                cookie.setSecure(item.getBoolean("secure"));
                cookie.setHttpOnly(item.getBoolean("httpOnly"));
                long maxAge = item.getLong("maxAge");
                cookie.setMaxAge(maxAge < 0 ? remaining / 1000 : Math.min(maxAge, remaining / 1000));
                cookies.getCookieStore().add(new URL(BuildConfig.API_BASE_URL).toURI(), cookie);
            }
            expiresAt = saved.getLong("expiresAt");
            sessionId = saved.optString("sessionId", "");
            if (sessionId.isEmpty()) {
                sessionId = java.util.UUID.randomUUID().toString();
                saved.put("sessionId", sessionId);
                storage.save(saved);
            }
            return user;
        } catch (Exception e) { clearSession(); return null; }
        }
    }
    JSONObject post(String path, JSONObject body) throws Exception {
        return request(path, body);
    }
    JSONObject get(String path) throws Exception { return request(path, null); }
    JSONObject getWithActivity(String path, long activityAt) throws Exception { return request(path, null, activityAt); }
    JSONObject postWithActivity(String path, JSONObject body, long activityAt) throws Exception {
        return request(path, body, activityAt);
    }
    private JSONObject request(String path, JSONObject body) throws Exception {
        return request(path, body, 0);
    }
    private JSONObject request(String path, JSONObject body, long activityAt) throws Exception {
        synchronized (REQUEST_LOCK) { return send(path, body, activityAt); }
    }
    private JSONObject send(String path, JSONObject body, long activityAt) throws Exception {
        // A notification may have renewed the persisted cookie while the Activity was stopped.
        synchronized (SESSION_LOCK) {
            if (sessionId != null) {
                JSONObject latest = storage.load();
                if (latest == null || !sessionId.equals(latest.optString("sessionId"))) {
                    cookies.getCookieStore().removeAll();
                    throw new ApiException(401, "Ingresá nuevamente para continuar.");
                }
                if (restoreSession() == null) throw new ApiException(401, "La sesión venció. Ingresá nuevamente.");
            }
        }
        URL url = new URL(BuildConfig.API_BASE_URL + path);
        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        try {
            connection.setRequestMethod(body == null ? "GET" : "POST");
            connection.setConnectTimeout(15000); connection.setReadTimeout(15000);
            connection.setInstanceFollowRedirects(false); connection.setDoOutput(body != null);
            connection.setRequestProperty("Content-Type", "application/json; charset=UTF-8");
            connection.setRequestProperty("X-Acompaar-Client", "android");
            if ("/api/auth/login".equals(path)) activityAt = System.currentTimeMillis();
            if (activityAt > 0) connection.setRequestProperty("X-Acompaar-Activity", Long.toString(activityAt));
            for (Map.Entry<String, List<String>> entry : cookies.get(url.toURI(), java.util.Collections.emptyMap()).entrySet())
                connection.setRequestProperty(entry.getKey(), String.join("; ", entry.getValue()));
            if (body != null) try (java.io.OutputStream output = connection.getOutputStream()) {
                output.write(body.toString().getBytes(StandardCharsets.UTF_8));
            }
            int status = connection.getResponseCode();
            if (status == 401) {
                synchronized (SESSION_LOCK) {
                    JSONObject latest = storage.load();
                    if (sessionId != null && latest != null && sessionId.equals(latest.optString("sessionId"))) storage.clear();
                }
                cookies.getCookieStore().removeAll();
            } else {
                cookies.put(url.toURI(), connection.getHeaderFields());
                String expiration = connection.getHeaderField("X-Acompaar-Session-Expires");
                if (expiration != null) {
                    long confirmed = Long.parseLong(expiration);
                    if (!SessionPolicy.isValid(confirmed, System.currentTimeMillis()))
                        throw new ApiException("El servidor devolvió un vencimiento de sesión inválido.");
                    expiresAt = confirmed;
                    synchronized (SESSION_LOCK) {
                        JSONObject latest = storage.load();
                        if (sessionId != null && latest != null && sessionId.equals(latest.optString("sessionId")))
                            storage.save(snapshot(latest.getJSONObject("user")));
                    }
                }
            }
            InputStream stream = status < 400 ? connection.getInputStream() : connection.getErrorStream();
            String raw = "";
            if (stream != null) {
                try (InputStream input = stream; ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                    byte[] buffer = new byte[4096]; int count;
                    while ((count = input.read(buffer)) != -1) output.write(buffer, 0, count);
                    raw = new String(output.toByteArray(), StandardCharsets.UTF_8);
                }
            }
            JSONObject result;
            try { result = new JSONObject(raw); }
            catch (org.json.JSONException e) { throw new ApiException(status, status == 401
                ? "La sesión venció. Ingresá nuevamente." : "No pudimos completar la solicitud. Intentá más tarde."); }
            if (status < 200 || status >= 300)
                throw new ApiException(status, result.optString("mensaje", result.optString("title", "No pudimos completar la solicitud.")));
            return result;
        } finally { connection.disconnect(); }
    }
    static final class ApiException extends Exception {
        final int status;
        ApiException(String message) { this(0, message); }
        ApiException(int status, String message) { super(message); this.status = status; }
    }
}
