USE acompanAR_db;

-- ============================================================
-- DATOS DE PRUEBA - ACOMPAÑAR
-- codaNova
-- ============================================================


-- ============================================================
-- 1. USUARIOS
-- ============================================================

INSERT INTO usuario
(
    nombre,
    apellido,
    dni,
    fecha_nacimiento,
    email,
    password,
    telefono,
    tipo_usuario,
    estado,
    codigo_mayor
)
VALUES
(
    'Roberto',
    'Fernandez',
    '20123456',
    '1955-04-12',
    'roberto@gmail.com',
    'Roberto123',
    '1155551001',
    'MAYOR',
    TRUE,
    'MAY-000001'
),
(
    'Laura',
    'Fernandez',
    '32123456',
    '1985-08-21',
    'laura@gmail.com',
    'Laura123',
    '1155551002',
    'ACOMPANANTE',
    TRUE,
    NULL
),
(
    'Marta',
    'Gomez',
    '18555777',
    '1948-11-03',
    'marta@gmail.com',
    'Marta123',
    '1155551003',
    'MAYOR',
    TRUE,
    'MAY-000002'
),
(
    'Nicolas',
    'Gomez',
    '40111222',
    '1997-06-15',
    'nicolas@gmail.com',
    'Nicolas123',
    '1155551004',
    'ACOMPANANTE',
    TRUE,
    NULL
),
(
    'Administrador',
    'codaNova',
    '99999999',
    '1990-01-01',
    'admin@codanova.com',
    'Admin123',
    '1155559999',
    'ADMIN',
    TRUE,
    NULL
);


-- ============================================================
-- 2. VINCULACIONES
-- Roberto <-> Laura
-- Marta <-> Nicolas
-- ============================================================

INSERT INTO vinculacion
(
    id_mayor,
    id_acompanante,
    tipo_vinculo,
    estado,
    fecha_vinculacion
)
VALUES
(
    1,
    2,
    'Hija',
    'ACEPTADA',
    '2026-09-01 10:30:00'
),
(
    3,
    4,
    'Nieto',
    'ACEPTADA',
    '2026-09-10 16:15:00'
);


-- ============================================================
-- 3. CONTACTOS DE CONFIANZA
-- ============================================================

INSERT INTO contacto_confianza
(
    id_mayor,
    nombre,
    apellido,
    telefono,
    tipo_vinculo,
    estado
)
VALUES
(
    1,
    'Carlos',
    'Fernandez',
    '1155552001',
    'Hermano',
    TRUE
),
(
    1,
    'Laura',
    'Fernandez',
    '1155551002',
    'Hija',
    TRUE
),
(
    1,
    'Juan',
    'Perez',
    '1155552003',
    'Vecino',
    TRUE
),
(
    3,
    'Sofia',
    'Gomez',
    '1155553001',
    'Hija',
    TRUE
),
(
    3,
    'Nicolas',
    'Gomez',
    '1155551004',
    'Nieto',
    TRUE
);


-- ============================================================
-- 4. MEDICAMENTOS
-- ============================================================

INSERT INTO medicamento
(
    id_mayor,
    nombre,
    dosis,
    frecuencia,
    horario,
    fecha_inicio,
    fecha_fin,
    estado
)
VALUES
(
    1,
    'Losartan',
    '50 mg',
    'Cada 12 horas',
    '08:00:00',
    '2026-10-01',
    NULL,
    TRUE
),
(
    1,
    'Metformina',
    '850 mg',
    'Una vez por dia',
    '20:00:00',
    '2026-10-01',
    NULL,
    TRUE
),
(
    3,
    'Enalapril',
    '10 mg',
    'Una vez por dia',
    '09:00:00',
    '2026-09-15',
    NULL,
    TRUE
);


-- ============================================================
-- 5. TURNOS MEDICOS
-- ============================================================

INSERT INTO turno_medico
(
    id_mayor,
    especialidad,
    nombre_medico,
    fecha,
    hora,
    direccion,
    motivo,
    estado
)
VALUES
(
    1,
    'Cardiologia',
    'Dr. Martin Perez',
    '2026-10-15',
    '10:30:00',
    'Av. Corrientes 1250, CABA',
    'Control anual',
    'PROGRAMADO'
),
(
    1,
    'Clinica Medica',
    'Dra. Carolina Lopez',
    '2026-10-22',
    '15:00:00',
    'Av. Santa Fe 2200, CABA',
    'Control general',
    'PROGRAMADO'
),
(
    3,
    'Traumatologia',
    'Dr. Pablo Martinez',
    '2026-10-20',
    '11:15:00',
    'Av. Rivadavia 5400, CABA',
    'Dolor de rodilla',
    'PROGRAMADO'
);


-- ============================================================
-- 6. RECORDATORIOS DE MEDICAMENTOS
-- ============================================================

-- Losartan de Roberto
INSERT INTO recordatorio
(
    id_medicamento,
    id_turno,
    fecha_hora,
    estado
)
VALUES
(
    1,
    NULL,
    '2026-10-07 08:00:00',
    'ENVIADO'
),
(
    1,
    NULL,
    '2026-10-07 20:00:00',
    'ENVIADO'
),
(
    1,
    NULL,
    '2026-10-08 08:00:00',
    'PENDIENTE'
),
(
    1,
    NULL,
    '2026-10-08 20:00:00',
    'PENDIENTE'
);


-- Metformina de Roberto
INSERT INTO recordatorio
(
    id_medicamento,
    id_turno,
    fecha_hora,
    estado
)
VALUES
(
    2,
    NULL,
    '2026-10-07 20:00:00',
    'ENVIADO'
),
(
    2,
    NULL,
    '2026-10-08 20:00:00',
    'PENDIENTE'
);


-- Enalapril de Marta
INSERT INTO recordatorio
(
    id_medicamento,
    id_turno,
    fecha_hora,
    estado
)
VALUES
(
    3,
    NULL,
    '2026-10-07 09:00:00',
    'ENVIADO'
),
(
    3,
    NULL,
    '2026-10-08 09:00:00',
    'PENDIENTE'
);


-- ============================================================
-- 7. RECORDATORIOS DE TURNOS MEDICOS
-- ============================================================

-- Turno de cardiologia de Roberto
INSERT INTO recordatorio
(
    id_medicamento,
    id_turno,
    fecha_hora,
    estado
)
VALUES
(
    NULL,
    1,
    '2026-10-14 10:30:00',
    'PENDIENTE'
);


-- Turno de clinica medica de Roberto
INSERT INTO recordatorio
(
    id_medicamento,
    id_turno,
    fecha_hora,
    estado
)
VALUES
(
    NULL,
    2,
    '2026-10-21 15:00:00',
    'PENDIENTE'
);


-- Turno de traumatologia de Marta
INSERT INTO recordatorio
(
    id_medicamento,
    id_turno,
    fecha_hora,
    estado
)
VALUES
(
    NULL,
    3,
    '2026-10-19 11:15:00',
    'PENDIENTE'
);


-- ============================================================
-- 8. CUMPLIMIENTOS
-- ============================================================

-- Roberto cumplio la toma de Losartan de las 08:00
INSERT INTO cumplimiento
(
    id_recordatorio,
    fecha_hora_registro,
    estado
)
VALUES
(
    1,
    '2026-10-07 08:06:00',
    'CUMPLIDO'
);


-- Roberto NO cumplio la toma de Losartan de las 20:00
INSERT INTO cumplimiento
(
    id_recordatorio,
    fecha_hora_registro,
    estado
)
VALUES
(
    2,
    '2026-10-07 21:00:00',
    'NO_CUMPLIDO'
);


-- Roberto cumplio la toma de Metformina
INSERT INTO cumplimiento
(
    id_recordatorio,
    fecha_hora_registro,
    estado
)
VALUES
(
    5,
    '2026-10-07 20:04:00',
    'CUMPLIDO'
);


-- Marta cumplio la toma de Enalapril
INSERT INTO cumplimiento
(
    id_recordatorio,
    fecha_hora_registro,
    estado
)
VALUES
(
    7,
    '2026-10-07 09:03:00',
    'CUMPLIDO'
);


-- ============================================================
-- CONSULTAS PARA COMPROBAR QUE SE CARGO CORRECTAMENTE
-- ============================================================

SELECT * FROM usuario;
SELECT * FROM vinculacion;
SELECT * FROM contacto_confianza;
SELECT * FROM medicamento;
SELECT * FROM turno_medico;
SELECT * FROM recordatorio;
SELECT * FROM cumplimiento;