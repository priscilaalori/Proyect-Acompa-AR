package com.codenova.acompaar;

import java.time.LocalDate;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.regex.Pattern;

final class RegistrationValidator {
    private static final Pattern NAME = Pattern.compile("[\\p{L}\\p{M}]+(?:[ '’\\-][\\p{L}\\p{M}]+)*");
    private static final Pattern EMAIL = Pattern.compile(
        "[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+");

    static String normalizeName(String value) { return value == null ? "" : value.trim().replaceAll("\\s+", " "); }
    private static String trimmed(Map<String, String> values, String key) {
        String value = values.get(key);
        return value == null ? "" : value.trim();
    }

    static Map<String, String> validate(Map<String, String> values, LocalDate birthDate, LocalDate today) {
        Map<String, String> errors = new LinkedHashMap<>();
        for (String key : new String[]{"nombre", "apellido"}) {
            String name = normalizeName(values.get(key));
            if (name.isEmpty()) errors.put(key, "Completá este dato.");
            else if (name.length() > 100 || !NAME.matcher(name).matches())
                errors.put(key, "Usá letras; podés incluir espacios, guiones o apóstrofes (hasta 100 caracteres).");
        }
        String dni = trimmed(values, "dni");
        if (dni.isEmpty()) errors.put("dni", "Ingresá tu DNI.");
        else if (!dni.matches("[0-9]{7,9}") || dni.matches("0+"))
            errors.put("dni", "Ingresá entre 7 y 9 números, sin puntos ni letras.");
        if (birthDate == null) errors.put("fecha", "Elegí tu fecha de nacimiento.");
        else if (birthDate.getYear() < 1000 || !birthDate.isBefore(today))
            errors.put("fecha", "La fecha debe ser anterior a hoy.");
        String email = trimmed(values, "email");
        String localPart = email.contains("@") ? email.substring(0, email.indexOf('@')) : "";
        if (email.isEmpty()) errors.put("email", "Ingresá tu correo electrónico.");
        else if (email.length() > 150 || localPart.length() > 64 || localPart.startsWith(".")
            || localPart.endsWith(".") || localPart.contains("..") || !EMAIL.matcher(email).matches())
            errors.put("email", "Ingresá un correo válido, por ejemplo nombre@gmail.com.");
        String phone = trimmed(values, "telefono");
        if (phone.isEmpty()) errors.put("telefono", "Ingresá tu teléfono.");
        else if (phone.length() > 12 || !phone.matches("\\+?[0-9]{8,12}") || phone.replace("+", "").matches("0+"))
            errors.put("telefono", "Usá entre 8 y 12 números, sin espacios ni guiones. Podés comenzar con + (máximo 12 caracteres).");
        String password = values.getOrDefault("password", "");
        if (password == null || password.isEmpty()) errors.put("password", "Creá una contraseña.");
        else if (password.length() < 8 || password.length() > 16 || password.codePoints().anyMatch(
            c -> Character.isWhitespace(c) || Character.isSpaceChar(c)))
            errors.put("password", "Usá entre 8 y 16 caracteres, sin espacios.");
        String confirmation = values.getOrDefault("confirmar", "");
        if (confirmation == null || confirmation.isEmpty()) errors.put("confirmar", "Repetí la contraseña.");
        else if (!confirmation.equals(password)) errors.put("confirmar", "Las contraseñas deben ser iguales.");
        return errors;
    }
}
