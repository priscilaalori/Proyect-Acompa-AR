usuarioDROP DATABASE IF EXISTS acompanAR_db;

CREATE DATABASE acompanAR_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE acompanAR_db;


-- ============================================================
-- 1. USUARIO
-- ============================================================

CREATE TABLE usuario (
    id_usuario INT AUTO_INCREMENT,
    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NOT NULL,
    dni VARCHAR(20) NOT NULL,
    fecha_nacimiento DATE NOT NULL,
    email VARCHAR(150) NOT NULL,
    password VARCHAR(255) NOT NULL,
    telefono VARCHAR(30) NOT NULL,

    tipo_usuario ENUM(
        'MAYOR',
        'ACOMPANANTE',
        'ADMIN'
    ) NOT NULL,

    estado BOOLEAN NOT NULL DEFAULT TRUE,

    codigo_mayor VARCHAR(30) NULL,

    CONSTRAINT pk_usuario
        PRIMARY KEY (id_usuario),

    CONSTRAINT uq_usuario_dni
        UNIQUE (dni),

    CONSTRAINT uq_usuario_email
        UNIQUE (email),

    CONSTRAINT uq_usuario_codigo_mayor
        UNIQUE (codigo_mayor)
);


-- ============================================================
-- 2. VINCULACION
-- Relaciona una persona mayor con un acompañante registrado.
-- ============================================================

CREATE TABLE vinculacion (
    id_vinculacion INT AUTO_INCREMENT,
    id_mayor INT NOT NULL,
    id_acompanante INT NOT NULL,

    tipo_vinculo VARCHAR(50) NOT NULL,

    estado ENUM(
        'PENDIENTE',
        'ACEPTADA',
        'RECHAZADA'
    ) NOT NULL DEFAULT 'PENDIENTE',

    fecha_vinculacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_vinculacion
        PRIMARY KEY (id_vinculacion),

    CONSTRAINT fk_vinculacion_mayor
        FOREIGN KEY (id_mayor)
        REFERENCES usuario(id_usuario),

    CONSTRAINT fk_vinculacion_acompanante
        FOREIGN KEY (id_acompanante)
        REFERENCES usuario(id_usuario),

    CONSTRAINT uq_vinculacion
        UNIQUE (id_mayor, id_acompanante)
);


-- ============================================================
-- 3. CONTACTO DE CONFIANZA
-- Como dijimos no necesariamente tiene que ser un usuario de la app
-- ============================================================

CREATE TABLE contacto_confianza (
    id_contacto INT AUTO_INCREMENT,
    id_mayor INT NOT NULL,

    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NULL,
    telefono VARCHAR(30) NOT NULL,
    tipo_vinculo VARCHAR(50) NOT NULL,

    estado BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT pk_contacto_confianza
        PRIMARY KEY (id_contacto),

    CONSTRAINT fk_contacto_mayor
        FOREIGN KEY (id_mayor)
        REFERENCES usuario(id_usuario)
);


-- ============================================================
-- 4. MEDICAMENTO
-- ============================================================

CREATE TABLE medicamento (
    id_medicamento INT AUTO_INCREMENT,
    id_mayor INT NOT NULL,

    nombre VARCHAR(100) NOT NULL,
    dosis VARCHAR(50) NOT NULL,
    frecuencia VARCHAR(100) NOT NULL,

    horario TIME NOT NULL,

    fecha_inicio DATE NOT NULL,
    fecha_fin DATE NULL,

    estado BOOLEAN NOT NULL DEFAULT TRUE,

    CONSTRAINT pk_medicamento
        PRIMARY KEY (id_medicamento),

    CONSTRAINT fk_medicamento_mayor
        FOREIGN KEY (id_mayor)
        REFERENCES usuario(id_usuario),

    CONSTRAINT chk_medicamento_fechas
        CHECK (
            fecha_fin IS NULL
            OR fecha_fin >= fecha_inicio
        )
);


-- ============================================================
-- 5. TURNO MEDICO
-- ============================================================

CREATE TABLE turno_medico (
    id_turno INT AUTO_INCREMENT,
    id_mayor INT NOT NULL,

    especialidad VARCHAR(100) NOT NULL,
    nombre_medico VARCHAR(150) NOT NULL,

    fecha DATE NOT NULL,
    hora TIME NOT NULL,

    direccion VARCHAR(200) NOT NULL,
    motivo VARCHAR(255) NULL,

    estado ENUM(
        'PROGRAMADO',
        'CANCELADO',
        'REALIZADO'
    ) NOT NULL DEFAULT 'PROGRAMADO',

    CONSTRAINT pk_turno_medico
        PRIMARY KEY (id_turno),

    CONSTRAINT fk_turno_mayor
        FOREIGN KEY (id_mayor)
        REFERENCES usuario(id_usuario)
);


-- ============================================================
-- 6. RECORDATORIO
-- Puede corresponder a un medicamento O a un turno.
-- ============================================================

CREATE TABLE recordatorio (
    id_recordatorio INT AUTO_INCREMENT,

    id_medicamento INT NULL,
    id_turno INT NULL,

    fecha_hora DATETIME NOT NULL,

    estado ENUM(
        'PENDIENTE',
        'ENVIADO',
        'CANCELADO'
    ) NOT NULL DEFAULT 'PENDIENTE',

    CONSTRAINT pk_recordatorio
        PRIMARY KEY (id_recordatorio),

    CONSTRAINT fk_recordatorio_medicamento
        FOREIGN KEY (id_medicamento)
        REFERENCES medicamento(id_medicamento),

    CONSTRAINT fk_recordatorio_turno
        FOREIGN KEY (id_turno)
        REFERENCES turno_medico(id_turno),

    CONSTRAINT chk_recordatorio_origen
        CHECK (
            (id_medicamento IS NOT NULL AND id_turno IS NULL)
            OR
            (id_medicamento IS NULL AND id_turno IS NOT NULL)
        )
);


-- ============================================================
-- 7. CUMPLIMIENTO
-- ============================================================

CREATE TABLE cumplimiento (
    id_cumplimiento INT AUTO_INCREMENT,
    id_recordatorio INT NOT NULL,

    fecha_hora_registro DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    estado ENUM(
        'CUMPLIDO',
        'NO_CUMPLIDO'
    ) NOT NULL,

    CONSTRAINT pk_cumplimiento
        PRIMARY KEY (id_cumplimiento),

    CONSTRAINT fk_cumplimiento_recordatorio
        FOREIGN KEY (id_recordatorio)
        REFERENCES recordatorio(id_recordatorio),

    CONSTRAINT uq_cumplimiento_recordatorio
        UNIQUE (id_recordatorio)
);