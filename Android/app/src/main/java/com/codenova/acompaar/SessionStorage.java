package com.codenova.acompaar;

import android.content.Context;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.AtomicFile;
import org.json.JSONObject;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Sesión cifrada con una clave del dispositivo, excluida de las copias de seguridad. */
final class SessionStorage {
    private static final String KEY_ALIAS = "acompanar.session";
    private final AtomicFile file;

    SessionStorage(Context context) {
        file = new AtomicFile(new File(context.getNoBackupFilesDir(), "session.enc"));
    }

    private SecretKey key() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        if (store.containsAlias(KEY_ALIAS)) return (SecretKey) store.getKey(KEY_ALIAS, null);
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generator.init(new KeyGenParameterSpec.Builder(KEY_ALIAS,
            KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
        return generator.generateKey();
    }

    void save(JSONObject session) throws Exception {
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.ENCRYPT_MODE, key());
        byte[] encrypted = cipher.doFinal(session.toString().getBytes(StandardCharsets.UTF_8));
        FileOutputStream output = null;
        try {
            output = file.startWrite();
            output.write(cipher.getIV().length);
            output.write(cipher.getIV());
            output.write(encrypted);
            file.finishWrite(output);
        } catch (Exception e) {
            if (output != null) file.failWrite(output);
            throw e;
        }
    }

    JSONObject load() {
        try {
            byte[] bytes = file.readFully();
            int ivLength = bytes[0] & 0xff;
            if (ivLength != 12 || bytes.length <= ivLength + 1) throw new IllegalStateException();
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, key(), new GCMParameterSpec(128, bytes, 1, ivLength));
            return new JSONObject(new String(cipher.doFinal(bytes, ivLength + 1,
                bytes.length - ivLength - 1), StandardCharsets.UTF_8));
        } catch (Exception e) {
            clear();
            return null;
        }
    }

    void clear() { file.delete(); }
}
