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
    // Coincide con ExpireTimeSpan del backend; no se prolonga al abrir la app.
    private static final long SESSION_MILLIS = 8 * 60 * 60 * 1000L;

    AuthClient(android.content.Context context) { storage = new SessionStorage(context); }

    void clearSession() {
        cookies.getCookieStore().removeAll();
        storage.clear();
    }

    void rememberSession(JSONObject user) throws Exception {
        JSONArray savedCookies = new JSONArray();
        for (HttpCookie cookie : cookies.getCookieStore().getCookies()) {
            if (cookie.hasExpired()) continue;
            savedCookies.put(new JSONObject().put("name", cookie.getName()).put("value", cookie.getValue())
                .put("path", cookie.getPath()).put("secure", cookie.getSecure())
                .put("httpOnly", cookie.isHttpOnly()).put("maxAge", cookie.getMaxAge())
                .put("version", cookie.getVersion()));
        }
        if (savedCookies.length() == 0) throw new IllegalStateException("No session cookie");
        storage.save(new JSONObject().put("origin", BuildConfig.API_BASE_URL)
            .put("expiresAt", System.currentTimeMillis() + SESSION_MILLIS)
            .put("user", user).put("cookies", savedCookies));
    }

    JSONObject restoreSession() {
        JSONObject saved = storage.load();
        if (saved == null) return null;
        try {
            long remaining = saved.getLong("expiresAt") - System.currentTimeMillis();
            if (remaining <= 0 || remaining > SESSION_MILLIS
                || !BuildConfig.API_BASE_URL.equals(saved.getString("origin"))) throw new IllegalStateException();
            JSONObject user = saved.getJSONObject("user");
            if (!"MAYOR".equals(user.optString("tipoUsuario"))) throw new IllegalStateException();
            JSONArray savedCookies = saved.getJSONArray("cookies");
            if (savedCookies.length() == 0) throw new IllegalStateException();
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
            return user;
        } catch (Exception e) { clearSession(); return null; }
    }
    JSONObject post(String path, JSONObject body) throws Exception {
        return request(path, body);
    }
    JSONObject get(String path) throws Exception { return request(path, null); }
    private JSONObject request(String path, JSONObject body) throws Exception {
        URL url = new URL(BuildConfig.API_BASE_URL + path);
        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        try {
            connection.setRequestMethod(body == null ? "GET" : "POST");
            connection.setConnectTimeout(15000); connection.setReadTimeout(15000);
            connection.setInstanceFollowRedirects(false); connection.setDoOutput(body != null);
            connection.setRequestProperty("Content-Type", "application/json; charset=UTF-8");
            for (Map.Entry<String, List<String>> entry : cookies.get(url.toURI(), java.util.Collections.emptyMap()).entrySet())
                connection.setRequestProperty(entry.getKey(), String.join("; ", entry.getValue()));
            if (body != null) try (java.io.OutputStream output = connection.getOutputStream()) {
                output.write(body.toString().getBytes(StandardCharsets.UTF_8));
            }
            int status = connection.getResponseCode();
            if (status == 401) clearSession();
            cookies.put(url.toURI(), connection.getHeaderFields());
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
            catch (org.json.JSONException e) { throw new ApiException("No pudimos completar la solicitud. Intentá más tarde."); }
            if (status < 200 || status >= 300)
                throw new ApiException(result.optString("mensaje", result.optString("title", "No pudimos completar la solicitud.")));
            return result;
        } finally { connection.disconnect(); }
    }
    static final class ApiException extends Exception {
        ApiException(String message) { super(message); }
    }
}
