package com.codenova.acompaar;

import android.app.DatePickerDialog;
import android.os.Bundle;
import android.text.InputType;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;
import androidx.activity.EdgeToEdge;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;
import org.json.JSONObject;
import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class MainActivity extends AppCompatActivity {
    private LinearLayout content;
    private final Map<String, TextInputLayout> fields = new LinkedHashMap<>();
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private AuthClient api;
    private com.google.android.material.checkbox.MaterialCheckBox rememberSession;
    private TextView message;
    private MaterialButton submit;
    private LocalDate birthDate;
    private boolean busy;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        EdgeToEdge.enable(this);
        setContentView(R.layout.activity_main);
        ViewCompat.setOnApplyWindowInsetsListener(findViewById(R.id.main), (v, insets) -> {
            Insets bars = insets.getInsets(WindowInsetsCompat.Type.systemBars() | WindowInsetsCompat.Type.ime());
            v.setPadding(bars.left, bars.top, bars.right, bars.bottom);
            return insets;
        });
        content = findViewById(R.id.content);
        api = new AuthClient(this);
        JSONObject savedUser = api.restoreSession();
        if (savedUser == null) showLogin();
        else if (!savedUser.has("codigoMayor")) {
            api.clearSession();
            showLogin();
            message.setText("Ingresá una vez más para ver tu código de vinculación. Después podremos recordar tu sesión.");
        }
        else showWelcome(savedUser);
    }
    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
    private TextView text(String value, int size, boolean heading) {
        return text(value, size, heading, content);
    }
    private TextView text(String value, int size, boolean heading, LinearLayout parent) {
        TextView view = new TextView(this);
        view.setText(value);
        view.setTextSize(size);
        view.setTextColor(android.graphics.Color.parseColor("#3D3650"));
        ViewCompat.setAccessibilityHeading(view, heading);
        if (heading) view.setTypeface(null, android.graphics.Typeface.BOLD);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(-1, -2);
        params.bottomMargin = dp(16);
        parent.addView(view, params);
        return view;
    }
    private void reset(String title, String description) {
        content.removeAllViews(); fields.clear(); birthDate = null;
        text(title, 26, true); text(description, 18, false);
        findViewById(R.id.main).post(() -> ((android.widget.ScrollView) findViewById(R.id.main)).scrollTo(0, 0));
    }
    private void addBackButton() {
        MaterialButton back = button("Volver", false, this::showLogin);
        back.setIconResource(R.drawable.ic_arrow_back);
        back.setIconTint(android.content.res.ColorStateList.valueOf(android.graphics.Color.parseColor("#7654B8")));
        back.setContentDescription("Volver al inicio de sesión");
        back.setBackgroundTintList(android.content.res.ColorStateList.valueOf(android.graphics.Color.TRANSPARENT));
        back.setElevation(0);
        content.removeView(back);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(-2, -2);
        params.gravity = android.view.Gravity.START;
        params.bottomMargin = dp(12);
        content.addView(back, 0, params);
    }
    private TextInputEditText field(String key, String label, int type, int maxLength) {
        TextInputLayout box = new TextInputLayout(this);
        box.setHint(label);
        box.setBoxBackgroundMode(TextInputLayout.BOX_BACKGROUND_OUTLINE);
        box.setBoxCornerRadii(dp(12), dp(12), dp(12), dp(12));
        TextInputEditText input = new TextInputEditText(box.getContext());
        input.setTextSize(18); input.setMinHeight(dp(60)); input.setInputType(type); input.setSingleLine(true);
        input.setTag(key);
        input.setFilters(new android.text.InputFilter[]{new android.text.InputFilter.LengthFilter(maxLength)});
        box.addView(input);
        if (type == (InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD))
            box.setEndIconMode(TextInputLayout.END_ICON_PASSWORD_TOGGLE);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(-1, -2);
        params.bottomMargin = dp(16); content.addView(box, params); fields.put(key, box);
        return input;
    }
    private MaterialButton button(String label, boolean primary, Runnable action) {
        return button(label, primary, action, content);
    }
    private MaterialButton button(String label, boolean primary, Runnable action, LinearLayout parent) {
        MaterialButton button = new MaterialButton(this);
        if (!primary) {
            button.setBackgroundTintList(android.content.res.ColorStateList.valueOf(android.graphics.Color.parseColor("#EEE5FF")));
            button.setTextColor(android.graphics.Color.parseColor("#7654B8"));
        }
        button.setText(label); button.setTextSize(18); button.setAllCaps(false);
        button.setMinHeight(dp(56)); button.setCornerRadius(dp(12));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(-1, -2);
        params.topMargin = dp(8); parent.addView(button, params);
        button.setOnClickListener(v -> { if (!busy) action.run(); });
        return button;
    }
    private void status() {
        message = text("", 16, false);
        message.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
    }
    private void emailField() {
        field("email", "Correo electrónico", InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_EMAIL_ADDRESS, 150)
            .setAutofillHints(View.AUTOFILL_HINT_EMAIL_ADDRESS);
    }
    private void passwordField(String key, String label, int limit) {
        field(key, label, InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD, limit)
            .setAutofillHints(View.AUTOFILL_HINT_PASSWORD);
    }
    private void showLogin() {
        reset("Qué bueno verte", "Ingresá para continuar con tu cuidado.");
        emailField(); passwordField("password", "Contraseña", 1024);
        rememberSession = new com.google.android.material.checkbox.MaterialCheckBox(this);
        rememberSession.setText("Mantener mi sesión iniciada");
        rememberSession.setTextSize(18);
        rememberSession.setMinHeight(dp(56));
        content.addView(rememberSession, new LinearLayout.LayoutParams(-1, -2));
        status();
        submit = button("Ingresar", true, this::login);
        button("Olvidé mi contraseña", false, this::showRecovery);
        text("¿Es tu primera vez?", 18, false);
        button("Crear mi cuenta", false, this::showRegister);
    }
    private void showRegister() {
        reset("Creá tu cuenta", "Completá tus datos. Vamos paso a paso.");
        addBackButton();
        field("nombre", "Nombre", InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_WORDS, 100);
        field("apellido", "Apellido", InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_WORDS, 100);
        field("dni", "DNI (sin puntos)", InputType.TYPE_CLASS_NUMBER, 9);
        fields.get("dni").setHelperText("Entre 7 y 9 números.");
        TextInputEditText date = field("fecha", "Fecha de nacimiento", InputType.TYPE_CLASS_DATETIME, 10);
        date.setFocusable(false); date.setClickable(true);
        date.setOnClickListener(v -> {
            LocalDate initial = birthDate != null ? birthDate : LocalDate.now().minusYears(65);
            DatePickerDialog picker = new DatePickerDialog(this, (view, y, m, d) -> {
                birthDate = LocalDate.of(y, m + 1, d);
                date.setText(birthDate.format(DateTimeFormatter.ofPattern("dd/MM/yyyy")));
                fields.get("fecha").setError(null);
            }, initial.getYear(), initial.getMonthValue() - 1, initial.getDayOfMonth());
            picker.getDatePicker().setMaxDate(System.currentTimeMillis() - 86400000L);
            picker.show();
        });
        emailField(); field("telefono", "Teléfono", InputType.TYPE_CLASS_PHONE, 12);
        fields.get("telefono").setHelperText("Ej.: 1155551001. Sin espacios ni guiones.");
        passwordField("password", "Creá una contraseña", 16);
        fields.get("password").setHelperText("Entre 8 y 16 caracteres, sin espacios.");
        passwordField("confirmar", "Repetí la contraseña", 16);
        status(); submit = button("Crear mi cuenta", true, this::register);
        button("Ya tengo cuenta. Ingresar", false, this::showLogin);
    }
    private String value(String key) { return fields.get(key).getEditText().getText().toString(); }
    private void error(String key, String value) { fields.get(key).setError(value); }
    private boolean validate(boolean registration) {
        fields.values().forEach(f -> f.setError(null));
        if (registration) {
            Map<String, String> values = new LinkedHashMap<>();
            fields.keySet().forEach(key -> values.put(key, value(key)));
            RegistrationValidator.validate(values, birthDate, LocalDate.now()).forEach(this::error);
        } else {
        if (!android.util.Patterns.EMAIL_ADDRESS.matcher(value("email").trim()).matches())
            error("email", "Ingresá un correo electrónico válido.");
        String password = value("password");
        if (password.isEmpty()) error("password", "Ingresá tu contraseña.");
        }
        for (TextInputLayout box : fields.values()) {
            if (box.getError() != null) {
                box.getEditText().requestFocus();
                box.post(() -> box.requestRectangleOnScreen(new android.graphics.Rect(0, 0, box.getWidth(), box.getHeight()), false));
                return false;
            }
        }
        return true;
    }
    private void login() {
        if (busy || !validate(false)) return;
        boolean remember = rememberSession.isChecked();
        api.clearSession();
        JSONObject body = new JSONObject();
        try { body.put("email", value("email").trim()); body.put("password", value("password")); }
        catch (org.json.JSONException e) { throw new IllegalStateException(e); }
        request("/api/auth/login", body, result -> {
            JSONObject user = result.optJSONObject("usuario");
            if (user == null || !"MAYOR".equals(user.optString("tipoUsuario"))) {
                api.clearSession();
                message.setText("Esta app es para personas mayores. Si sos acompañante, ingresá desde la web."); return;
            }
            String notice = null;
            if (remember) {
                try { api.rememberSession(user); }
                catch (Exception e) { notice = "Ingresaste correctamente, pero no pudimos recordar la sesión en este dispositivo."; }
            }
            showWelcome(user);
            if (notice != null) text(notice, 16, false);
        });
    }
    private void showWelcome(JSONObject user) {
        reset("¡Hola, " + user.optString("nombre") + "!", "Ingresaste correctamente a AcompañAR.");
        showLinkingCode(user);
        text("Tu cuenta está lista. Las funciones de cuidado se incorporarán aquí próximamente.", 18, false);
        button("Cerrar sesión", true, () -> { api.clearSession(); showLogin(); });
    }
    private void showLinkingCode(JSONObject user) {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(20), dp(20), dp(20), dp(20));
        android.graphics.drawable.GradientDrawable background = new android.graphics.drawable.GradientDrawable();
        background.setColor(android.graphics.Color.parseColor("#F5EFFF"));
        background.setCornerRadius(dp(16));
        background.setStroke(dp(1), android.graphics.Color.parseColor("#DFD0FF"));
        card.setBackground(background);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(-1, -2);
        params.bottomMargin = dp(24);
        content.addView(card, params);
        text("Mi código de vinculación", 20, true, card);
        String code = user.isNull("codigoMayor") ? "" : user.optString("codigoMayor").trim();
        if (code.isEmpty()) {
            text("Tu código todavía no está disponible.", 18, false, card);
            text("Si acabás de actualizar la app, cerrá sesión y volvé a ingresar.", 16, false, card);
            return;
        }
        TextView codeView = text(code, 30, true, card);
        codeView.setTextColor(android.graphics.Color.parseColor("#7654B8"));
        codeView.setTextIsSelectable(true);
        codeView.setTextDirection(View.TEXT_DIRECTION_LTR);
        text("Compartilo con la persona que te acompaña para que pueda vincularse con vos.", 18, false, card);
        button("Compartir mi código", true, () -> {
            android.content.Intent share = new android.content.Intent(android.content.Intent.ACTION_SEND);
            share.setType("text/plain");
            share.putExtra(android.content.Intent.EXTRA_TEXT, "Mi código de vinculación de AcompañAR es " + code + ".");
            startActivity(android.content.Intent.createChooser(share, "Compartir mi código"));
        }, card);
    }
    private void register() {
        if (busy || !validate(true)) return;
        JSONObject body = new JSONObject();
        try {
            for (String key : new String[]{"nombre", "apellido", "dni", "email", "telefono"})
                body.put(key, key.equals("nombre") || key.equals("apellido")
                    ? RegistrationValidator.normalizeName(value(key)) : value(key).trim());
            body.put("password", value("password")); body.put("fechaNacimiento", birthDate.toString());
            body.put("tipoUsuario", "MAYOR");
        } catch (org.json.JSONException e) { throw new IllegalStateException(e); }
        request("/api/auth/registro", body, result -> {
            reset("¡Tu cuenta está lista!", "Ya sos parte de AcompañAR.");
            String code = result.optString("codigoMayor");
            if (!code.isEmpty()) {
                text("Tu código de vinculación", 18, false);
                text(code, 26, true).setTextIsSelectable(true);
                text("Compartilo con la persona que te acompaña para que pueda vincularse con vos.", 18, false);
            }
            button("Ir a ingresar", true, this::showLogin);
        });
    }
    private void showRecovery() {
        reset("Recuperá tu contraseña", "Te enviaremos un enlace a tu correo para crear una nueva contraseña.");
        addBackButton();
        emailField(); status();
        submit = button("Enviar instrucciones", true, () -> {
            if (!android.util.Patterns.EMAIL_ADDRESS.matcher(value("email").trim()).matches()) {
                error("email", "Ingresá un correo electrónico válido."); return;
            }
            error("email", null);
            JSONObject body = new JSONObject();
            try { body.put("email", value("email").trim()); } catch (org.json.JSONException e) { throw new IllegalStateException(e); }
            request("/api/auth/solicitar-recuperacion", body, result -> message.setText(result.optString("mensaje")));
        });
        button("Volver a ingresar", false, this::showLogin);
    }
    private void request(String path, JSONObject body, java.util.function.Consumer<JSONObject> success) {
        if (busy) return;
        busy = true; setInputsEnabled(false); message.setText("Un momento, por favor…");
        executor.execute(() -> {
            try {
                JSONObject result = api.post(path, body);
                runOnUiThread(() -> {
                    if (isFinishing() || isDestroyed()) return;
                    busy = false; setInputsEnabled(true); message.setText(""); success.accept(result);
                });
            } catch (Exception e) {
                runOnUiThread(() -> {
                    if (isFinishing() || isDestroyed()) return;
                    busy = false; setInputsEnabled(true);
                    message.setText(e instanceof AuthClient.ApiException ? e.getMessage()
                        : "No pudimos conectar. Revisá tu conexión e intentá nuevamente.");
                });
            }
        });
    }
    private void setInputsEnabled(boolean enabled) {
        submit.setEnabled(enabled);
        if (rememberSession != null) rememberSession.setEnabled(enabled);
        fields.values().forEach(f -> f.setEnabled(enabled));
    }
    @Override protected void onDestroy() { executor.shutdownNow(); super.onDestroy(); }
}
