USE acompanAR_db;

-- El login espera SHA-256. La carga anterior guardaba estas claves en texto plano.
-- Convertimos únicamente las cuentas de ejemplo que aún conservan su clave original.
-- No modifica claves ya hasheadas ni contraseñas cambiadas por el usuario.
-- Se puede ejecutar más de una vez sin volver a hashear los valores.
UPDATE usuario
SET password = SHA2(password, 256)
WHERE (email = 'roberto@gmail.com' AND BINARY password = 'Roberto123')
   OR (email = 'laura@gmail.com' AND BINARY password = 'Laura123')
   OR (email = 'marta@gmail.com' AND BINARY password = 'Marta123')
   OR (email = 'nicolas@gmail.com' AND BINARY password = 'Nicolas123')
   OR (email = 'admin@codanova.com' AND BINARY password = 'Admin123');

-- Comprobación sin mostrar el contenido de las contraseñas.
SELECT email, estado, tipo_usuario,
       (BINARY LOWER(password) = BINARY SHA2('Roberto123', 256)) AS clave_roberto_correcta
FROM usuario
WHERE email = 'roberto@gmail.com';
