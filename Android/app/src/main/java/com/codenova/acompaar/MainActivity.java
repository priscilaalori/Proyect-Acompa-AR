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
    private JSONObject currentUser;
    private LinearLayout careContent;
    private boolean loadingCare;
    private int careGeneration;
    private String careScreen = "HOME";
    private JSONObject carePanel;
    private final android.os.Handler careHandler = new android.os.Handler(android.os.Looper.getMainLooper());
    private final Runnable refreshCare = new Runnable() {
        @Override public void run() {
            if (currentUser != null) { loadCare(); careHandler.postDelayed(this, 30000); }
        }
    };

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
        getOnBackPressedDispatcher().addCallback(this, new androidx.activity.OnBackPressedCallback(true) {
            @Override public void handleOnBackPressed() {
                if (currentUser != null && !"HOME".equals(careScreen)) showWelcome(currentUser);
                else { setEnabled(false); getOnBackPressedDispatcher().onBackPressed(); setEnabled(true); }
            }
        });
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
        currentUser = null;
        carePanel = null;
        careGeneration++;
        careHandler.removeCallbacks(refreshCare);
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
        currentUser = user;
        careScreen = "HOME";
        reset("¡Hola, " + user.optString("nombre") + "!", "¿Qué querés consultar?");
        menuButton("Medicamentos", "Ver mis tomas de hoy y mañana", R.drawable.ic_pill, "MEDICAMENTO");
        menuButton("Turnos médicos", "Ver mis turnos de hoy y mañana", R.drawable.ic_clock, "TURNO");
        menuButton("Contactos de confianza", "Ver sus datos y teléfonos", R.drawable.ic_contacts, "CONTACTOS");
        menuButton("Solicitudes de acompañamiento", "Aceptar o rechazar a quien quiere acompañarme",
            R.drawable.ic_invitation, "SOLICITUDES");
        menuButton("Compartir mi código", "Vincularme con quien me acompaña", R.drawable.ic_share,
            () -> shareLinkingCode(user));
        button("Cerrar sesión", false, () -> { api.clearSession(); showLogin(); });
        careHandler.removeCallbacks(refreshCare);
    }
    private void menuButton(String title, String description, int icon, String screen) {
        menuButton(title, description, icon, () -> showCareScreen(screen));
    }
    private void menuButton(String title, String description, int icon, Runnable action) {
        MaterialButton entry = button(title + "\n" + description, false, action);
        entry.setTextSize(20);
        entry.setMinHeight(dp(168));
        entry.setGravity(android.view.Gravity.CENTER);
        entry.setPadding(dp(16), dp(20), dp(16), dp(20));
        entry.setIconGravity(MaterialButton.ICON_GRAVITY_TOP);
        entry.setIconResource(icon);
        entry.setIconSize(dp(48));
        entry.setIconPadding(dp(20));
        entry.setIconTint(android.content.res.ColorStateList.valueOf(android.graphics.Color.parseColor("#7654B8")));
        entry.setContentDescription(title + ". " + description);
        LinearLayout.LayoutParams params = (LinearLayout.LayoutParams) entry.getLayoutParams();
        params.bottomMargin = dp(20);
    }
    private void showCareScreen(String screen) {
        careScreen = screen;
        boolean medicine = "MEDICAMENTO".equals(screen);
        boolean contacts = "CONTACTOS".equals(screen);
        boolean invitations = "SOLICITUDES".equals(screen);
        reset(invitations ? "Solicitudes de acompañamiento"
                : contacts ? "Mis contactos de confianza" : medicine ? "Mis medicamentos" : "Mis turnos médicos",
            invitations ? "Vos elegís quién puede acompañarte."
                : contacts ? "Las personas con las que podés contar."
                : medicine ? "Tus tomas de hoy y mañana." : "Tus consultas y estudios de hoy y mañana.");
        MaterialButton back = button("Volver al inicio", false, () -> showWelcome(currentUser));
        back.setIconResource(R.drawable.ic_arrow_back);
        content.removeView(back);
        content.addView(back, 0);
        careContent = new LinearLayout(this);
        careContent.setOrientation(LinearLayout.VERTICAL);
        content.addView(careContent, new LinearLayout.LayoutParams(-1, -2));
        text("Cargando…", 18, false, careContent);
        if (carePanel != null) renderCare(carePanel);
        careHandler.removeCallbacks(refreshCare);
        careHandler.post(refreshCare);
    }
    private void loadCare() {
        if (loadingCare || currentUser == null || "HOME".equals(careScreen)) return;
        loadingCare = true;
        int generation = careGeneration;
        executor.execute(() -> {
            try {
                JSONObject panel = api.get("/api/mayor/panel");
                runOnUiThread(() -> {
                    loadingCare = false;
                    if (isDestroyed() || generation != careGeneration) return;
                    carePanel = panel;
                    if (!"HOME".equals(careScreen)) renderCare(panel);
                });
            } catch (Exception e) {
                runOnUiThread(() -> {
                    loadingCare = false;
                    if (isDestroyed() || generation != careGeneration || "HOME".equals(careScreen)) return;
                    careContent.removeAllViews();
                    text(e instanceof AuthClient.ApiException ? e.getMessage()
                        : "No pudimos cargar la información. Revisá tu conexión y volvé a intentar.", 18, false, careContent);
                    button("Reintentar", false, this::loadCare, careContent);
                });
            }
        });
    }
    private String dateLabel(String date) {
        try { return LocalDate.parse(date).format(DateTimeFormatter.ofPattern("dd/MM/yyyy")); }
        catch (Exception e) { return date; }
    }
    private void renderCare(JSONObject panel) {
        careContent.removeAllViews();
        if ("CONTACTOS".equals(careScreen)) { renderContacts(panel); return; }
        if ("SOLICITUDES".equals(careScreen)) { renderInvitations(panel); return; }
        boolean medicinesScreen = "MEDICAMENTO".equals(careScreen);
        LocalDate today = LocalDate.now();
        text(medicinesScreen ? "Mis tomas" : "Mis asistencias", 24, true, careContent);
        text(medicinesScreen ? "Desde el horario indicado, contanos si lo tomaste."
            : "Desde el horario del turno, contanos si asististe.", 18, false, careContent);
        org.json.JSONArray reminders = panel.optJSONArray("recordatorios");
        int visible = 0;
        if (reminders != null) for (int i = 0; i < reminders.length(); i++) {
            JSONObject item = reminders.optJSONObject(i);
            if (item == null || !careScreen.equals(item.optString("tipo"))
                || !CareVisibility.isCurrent(item.optString("fecha"), today)) continue;
            visible++;
            LinearLayout card = careCard();
            boolean medicine = "MEDICAMENTO".equals(item.optString("tipo"));
            text(item.optString("nombre"), 21, true, card);
            text(dateLabel(item.optString("fecha")) + " a las " + item.optString("hora")
                + "\n" + item.optString("detalle"), 18, false, card);
            if (!medicine) {
                org.json.JSONArray appointments = panel.optJSONArray("turnos");
                if (appointments != null) for (int j = 0; j < appointments.length(); j++) {
                    JSONObject appointment = appointments.optJSONObject(j);
                    if (appointment != null && matchesAppointment(item, appointment)) {
                        text(appointment.optString("medico")
                            + (appointment.isNull("motivo") ? "" : "\n" + appointment.optString("motivo")), 18, false, card);
                        break;
                    }
                }
            }
            if (!item.isNull("cumplimiento")) {
                boolean done = "CUMPLIDO".equals(item.optString("cumplimiento"));
                text(medicine ? (done ? "✓ Lo tomé" : "No lo tomé")
                    : (done ? "✓ Asistí" : "No asistí"), 20, true, card);
            } else if (item.optInt("disponible") == 1) {
                button(medicine ? "✓ Lo tomé" : "✓ Asistí", true,
                    () -> confirmCare(item, medicine, true), card);
                button(medicine ? "No lo tomé" : "No asistí", false,
                    () -> confirmCare(item, medicine, false), card);
            } else text("Podrás responder a partir del horario indicado.", 18, false, card);
        }
        if (visible == 0) text(medicinesScreen ? "No tenés tomas para hoy ni mañana."
            : "No tenés asistencias para registrar hoy ni mañana.", 18, false, careContent);
        if (medicinesScreen) {
        text("Mi tratamiento de hoy y mañana", 24, true, careContent);
        org.json.JSONArray medicines = panel.optJSONArray("medicamentos");
        int treatments = 0;
        if (medicines != null) for (int i = 0; i < medicines.length(); i++) {
            JSONObject item = medicines.optJSONObject(i);
            if (item == null || item.optInt("soloLectura") == 1 || item.optBoolean("soloLectura")
                || !CareVisibility.isTreatmentCurrent(item.optString("fechaInicio"),
                item.isNull("fechaFin") ? null : item.optString("fechaFin"), today)) continue;
            treatments++;
            LinearLayout card = careCard();
            text(item.optString("nombre") + " · " + item.optString("dosis"), 21, true, card);
            text(item.optString("frecuencia") + " · " + item.optString("horario")
                + "\nDesde " + dateLabel(item.optString("fechaInicio"))
                + (item.isNull("fechaFin") ? "" : " hasta " + dateLabel(item.optString("fechaFin"))), 18, false, card);
        }
        if (treatments == 0) text("No tenés tratamientos vigentes.", 18, false, careContent);
        }
        if (!medicinesScreen) {
        org.json.JSONArray appointments = panel.optJSONArray("turnos");
        if (appointments != null) for (int i = 0; i < appointments.length(); i++) {
            JSONObject item = appointments.optJSONObject(i);
            if (item == null || !CareVisibility.isCurrent(item.optString("fecha"), today)) continue;
            boolean alreadyShown = false;
            if (reminders != null) for (int j = 0; j < reminders.length(); j++) {
                JSONObject reminder = reminders.optJSONObject(j);
                if (reminder != null && matchesAppointment(reminder, item)) { alreadyShown = true; break; }
            }
            if (alreadyShown) continue;
            LinearLayout card = careCard();
            text(item.optString("especialidad"), 21, true, card);
            text(dateLabel(item.optString("fecha")) + " a las " + item.optString("hora")
                + "\n" + item.optString("medico") + "\n" + item.optString("direccion")
                + (item.isNull("motivo") ? "" : "\n" + item.optString("motivo"))
                + ("REALIZADO".equals(item.optString("estado")) ? "\n✓ Realizado" : ""), 18, false, card);
        }
        }
    }
    private boolean matchesAppointment(JSONObject reminder, JSONObject appointment) {
        return "TURNO".equals(reminder.optString("tipo"))
            && reminder.optString("nombre").equals(appointment.optString("especialidad"))
            && reminder.optString("fecha").equals(appointment.optString("fecha"))
            && reminder.optString("hora").equals(appointment.optString("hora"));
    }
    private LinearLayout careCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(16), dp(16), dp(16), dp(16));
        android.graphics.drawable.GradientDrawable background = new android.graphics.drawable.GradientDrawable();
        background.setColor(android.graphics.Color.parseColor("#F5EFFF"));
        background.setCornerRadius(dp(16));
        card.setBackground(background);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(-1, -2);
        params.bottomMargin = dp(16);
        careContent.addView(card, params);
        return card;
    }
    private void confirmCare(JSONObject item, boolean medicine, boolean done) {
        String answer = medicine ? (done ? "Lo tomé" : "No lo tomé") : (done ? "Asistí" : "No asistí");
        new com.google.android.material.dialog.MaterialAlertDialogBuilder(this)
            .setTitle("Confirmar registro")
            .setMessage(item.optString("nombre") + "\n" + answer + "\nUna vez confirmado no se puede cambiar.")
            .setNegativeButton("Volver", null)
            .setPositiveButton("Confirmar", (dialog, which) -> saveCare(item.optInt("idRecordatorio"), done))
            .show();
    }
    private void saveCare(int id, boolean done) {
        saveResponse("/api/recordatorios/" + id + "/cumplimiento", done ? "CUMPLIDO" : "NO_CUMPLIDO", "Respuesta guardada.");
    }
    private void saveResponse(String path, String state, String successMessage) {
        if (currentUser == null) return;
        if (loadingCare) {
            android.widget.Toast.makeText(this, "Estamos actualizando los datos. Volvé a confirmar en un momento.",
                android.widget.Toast.LENGTH_SHORT).show();
            return;
        }
        loadingCare = true;
        int generation = careGeneration;
        executor.execute(() -> {
            String error = null;
            try {
                api.post(path, new JSONObject().put("estado", state));
            } catch (Exception e) {
                error = e instanceof AuthClient.ApiException ? e.getMessage()
                    : "No pudimos guardar la respuesta. Revisá tu conexión y volvé a intentar.";
            }
            String result = error;
            runOnUiThread(() -> {
                loadingCare = false;
                if (isDestroyed() || generation != careGeneration) return;
                if (result == null) carePanel = null;
                android.widget.Toast.makeText(this, result == null ? successMessage : result,
                    android.widget.Toast.LENGTH_LONG).show();
                loadCare();
            });
        });
    }
    private void shareLinkingCode(JSONObject user) {
        String code = user.isNull("codigoMayor") ? "" : user.optString("codigoMayor").trim();
        if (code.isEmpty()) {
            new com.google.android.material.dialog.MaterialAlertDialogBuilder(this)
                .setTitle("Código no disponible")
                .setMessage("Cerrá sesión y volvé a ingresar para consultar tu código de vinculación.")
                .setPositiveButton("Entendido", null).show();
            return;
        }
        android.content.Intent share = new android.content.Intent(android.content.Intent.ACTION_SEND);
        share.setType("text/plain");
        share.putExtra(android.content.Intent.EXTRA_TEXT, "Mi código de vinculación de AcompañAR es " + code + ".");
        startActivity(android.content.Intent.createChooser(share, "Compartir mi código"));
    }
    private void renderContacts(JSONObject panel) {
        org.json.JSONArray contacts = panel.optJSONArray("contactos");
        if (contacts == null) {
            text("No pudimos cargar tus contactos. Volvé a intentar en unos momentos.", 18, false, careContent);
            button("Reintentar", false, this::loadCare, careContent);
            return;
        }
        if (contacts.length() == 0) text("Todavía no tenés contactos de confianza registrados.", 18, false, careContent);
        for (int i = 0; i < contacts.length(); i++) {
            JSONObject contact = contacts.optJSONObject(i);
            if (contact == null) continue;
            LinearLayout card = careCard();
            text((contact.optString("nombre") + " " + (contact.isNull("apellido") ? "" : contact.optString("apellido"))).trim(),
                22, true, card);
            text("Vínculo: " + contact.optString("tipoVinculo"), 18, false, card);
            String phone = contact.optString("telefono").trim();
            TextView phoneView = text("Teléfono: " + phone, 20, false, card);
            phoneView.setTextIsSelectable(true);
            if (!phone.isEmpty()) button("Llamar", true, () -> {
                android.content.Intent dial = new android.content.Intent(android.content.Intent.ACTION_DIAL,
                    android.net.Uri.fromParts("tel", phone, null));
                try { startActivity(dial); }
                catch (android.content.ActivityNotFoundException e) {
                    android.widget.Toast.makeText(this, "Este dispositivo no tiene una aplicación de teléfono.",
                        android.widget.Toast.LENGTH_LONG).show();
                }
            }, card);
        }
    }
    private void renderInvitations(JSONObject panel) {
        org.json.JSONArray invitations = panel.optJSONArray("solicitudes");
        if (invitations == null) {
            text("No pudimos consultar tus solicitudes. Volvé a intentar en unos momentos.", 18, false, careContent);
            button("Reintentar", false, this::loadCare, careContent);
            return;
        }
        text("Al aceptar, esa persona podrá acceder a tu información de cuidado y ayudarte a organizarla. Aceptá solo a quienes reconozcas.",
            18, false, careContent);
        if (invitations.length() == 0) {
            text("No tenés solicitudes pendientes.", 22, true, careContent);
            text("Cuando alguien solicite acompañarte, vas a verlo acá y podrás aceptar o rechazar.", 18, false, careContent);
            return;
        }
        for (int i = 0; i < invitations.length(); i++) {
            JSONObject invitation = invitations.optJSONObject(i);
            if (invitation == null) continue;
            LinearLayout card = careCard();
            String name = (invitation.optString("nombre") + " " + invitation.optString("apellido")).trim();
            text(name, 22, true, card);
            text("Quiere acompañarte", 18, false, card);
            text("Vínculo: " + invitation.optString("tipoVinculo")
                + "\nCorreo: " + invitation.optString("email")
                + "\nTeléfono: " + invitation.optString("telefono")
                + "\nSolicitud del " + dateLabel(invitation.optString("fecha")), 18, false, card);
            button("Aceptar acompañamiento", true, () -> confirmInvitation(invitation, name, true), card);
            button("Rechazar solicitud", false, () -> confirmInvitation(invitation, name, false), card);
        }
    }
    private void confirmInvitation(JSONObject invitation, String name, boolean accept) {
        new com.google.android.material.dialog.MaterialAlertDialogBuilder(this)
            .setTitle(accept ? "Aceptar acompañamiento" : "Rechazar solicitud")
            .setMessage(accept ? "¿Querés que " + name + " te acompañe? Podrá ver y organizar tu información de cuidado."
                : "¿Querés rechazar la solicitud de " + name + "? No tendrá acceso a tu información por esta solicitud.")
            .setNegativeButton("Volver", null)
            .setPositiveButton(accept ? "Sí, aceptar" : "Sí, rechazar", (dialog, which) ->
                saveResponse("/api/mayor/solicitudes/" + invitation.optInt("idVinculacion") + "/respuesta",
                    accept ? "ACEPTADA" : "RECHAZADA", accept ? "Solicitud aceptada." : "Solicitud rechazada."))
            .show();
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
    @Override protected void onStart() {
        super.onStart();
        careHandler.removeCallbacks(refreshCare);
        if (currentUser != null && !"HOME".equals(careScreen)) careHandler.post(refreshCare);
    }
    @Override protected void onStop() {
        careHandler.removeCallbacks(refreshCare);
        super.onStop();
    }
    @Override protected void onDestroy() { careHandler.removeCallbacks(refreshCare); executor.shutdownNow(); super.onDestroy(); }
}
