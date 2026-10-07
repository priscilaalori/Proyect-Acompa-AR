SELECT
    CONCAT(mayor.nombre, ' ', mayor.apellido) AS Persona_Mayor,
    mayor.codigo_mayor AS Codigo,

    CONCAT(acompanante.nombre, ' ', acompanante.apellido) AS Acompanante,
    v.tipo_vinculo AS Vinculo,

    med.nombre AS Medicamento,
    med.dosis AS Dosis,
    med.frecuencia AS Frecuencia,

    t.especialidad AS Especialidad,
    t.nombre_medico AS Medico,
    t.fecha AS Fecha_Turno,
    t.hora AS Hora_Turno

FROM usuario mayor

INNER JOIN vinculacion v
    ON mayor.id_usuario = v.id_mayor
    AND v.estado = 'ACEPTADA'

INNER JOIN usuario acompanante
    ON v.id_acompanante = acompanante.id_usuario

LEFT JOIN medicamento med
    ON mayor.id_usuario = med.id_mayor
    AND med.estado = TRUE

LEFT JOIN turno_medico t
    ON mayor.id_usuario = t.id_mayor
    AND t.estado = 'PROGRAMADO'

WHERE mayor.id_usuario = 1

ORDER BY t.fecha, t.hora;