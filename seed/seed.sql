-- Datos iniciales de Arrendia: catálogos, conceptos, parámetros, plantilla de condiciones, feriados y usuarios base.
-- Se ejecuta manualmente sobre una base que ya tiene su estructura creada. Se puede ejecutar varias veces:
-- solo inserta lo que falta. La estructura (tablas, procedimientos, vistas) NO vive en este repositorio.
--
-- El repositorio es público: aquí no van contraseñas. El administrador se crea sin contraseña
-- (PASSWORD_HASH nulo) y la activa con una invitación que genera la aplicación.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- obligatorio: hay índices filtrados y columnas calculadas
BEGIN TRANSACTION;

/* Usuario del sistema: registra los procesos automáticos. Es el ID 1 y se crea a sí mismo. */
IF NOT EXISTS (SELECT 1 FROM dbo.USUARIO WHERE ROL = 'Sistema')
BEGIN
    SET IDENTITY_INSERT dbo.USUARIO ON;
    INSERT INTO dbo.USUARIO (ID, NOMBRE, EMAIL, PASSWORD_HASH, ROL, ACTIVO, CREADO_POR)
    VALUES (1, N'Sistema', N'sistema@arrendia.local', NULL, 'Sistema', 0, 1);
    SET IDENTITY_INSERT dbo.USUARIO OFF;
END;
DECLARE @SISTEMA INT = (SELECT ID FROM dbo.USUARIO WHERE ROL = 'Sistema');

/* Primer administrador. Cambia el correo antes de ejecutar en producción. */
IF NOT EXISTS (SELECT 1 FROM dbo.USUARIO WHERE ROL = 'Administrador')
    INSERT INTO dbo.USUARIO (NOMBRE, EMAIL, PASSWORD_HASH, ROL, ACTIVO, CREADO_POR)
    VALUES (N'Administrador', N'admin@arrendia.site', NULL, 'Administrador', 0, @SISTEMA);

INSERT INTO dbo.TIPO_UNIDAD (NOMBRE)
SELECT v.NOMBRE FROM (VALUES (N'Local comercial'), (N'Apartamento'), (N'Oficina'), (N'Bodega'), (N'Casa')) v (NOMBRE)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TIPO_UNIDAD t WHERE t.NOMBRE = v.NOMBRE);

INSERT INTO dbo.ESPECIALIDAD (NOMBRE)
SELECT v.NOMBRE FROM (VALUES (N'Fontanería'), (N'Electricidad'), (N'Aire acondicionado'), (N'Cerrajería'), (N'Pintura'), (N'Albañilería')) v (NOMBRE)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ESPECIALIDAD e WHERE e.NOMBRE = v.NOMBRE);

INSERT INTO dbo.CATEGORIA_REPORTE (NOMBRE, ESPECIALIDAD_ID, HORAS_ATENCION)
SELECT v.NOMBRE, e.ID, v.HORAS
FROM (VALUES (N'Fuga o problema de agua', N'Fontanería', 24),
             (N'Falla eléctrica', N'Electricidad', 24),
             (N'Aire acondicionado', N'Aire acondicionado', 72),
             (N'Cerradura o portón', N'Cerrajería', 48),
             (N'Pintura y acabados', N'Pintura', 120),
             (N'Obra civil o área común', N'Albañilería', 72)) v (NOMBRE, ESPECIALIDAD, HORAS)
JOIN dbo.ESPECIALIDAD e ON e.NOMBRE = v.ESPECIALIDAD
WHERE NOT EXISTS (SELECT 1 FROM dbo.CATEGORIA_REPORTE c WHERE c.NOMBRE = v.NOMBRE);

/* RENTA y MORA son del sistema: los procedimientos los buscan por código y no se editan. */
INSERT INTO dbo.CONCEPTO_CARGO (CODIGO, NOMBRE, ES_SISTEMA, APLICA_IVA, GENERA_MORA_POR_DEFECTO, CREADO_POR)
SELECT v.CODIGO, v.NOMBRE, v.SIS, v.IVA, v.MORA, @SISTEMA
FROM (VALUES ('RENTA', N'Renta mensual', 1, 'SegunUnidad', 1),
             ('MORA', N'Mora por atraso', 1, 'No', 0),
             ('REPARACION', N'Reparación', 0, 'Si', 0),
             ('AGUA', N'Servicio de agua', 0, 'No', 0),
             ('ENERGIA', N'Servicio de energía eléctrica', 0, 'No', 0),
             ('OTRO', N'Otro cargo', 0, 'Si', 0)) v (CODIGO, NOMBRE, SIS, IVA, MORA)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CONCEPTO_CARGO c WHERE c.CODIGO = v.CODIGO);

INSERT INTO dbo.PARAMETRO (CLAVE, VALOR, TIPO, DESCRIPCION, CREADO_POR)
SELECT v.CLAVE, v.VALOR, v.TIPO, v.DESCRIPCION, @SISTEMA
FROM (VALUES ('Empresa.Nombre', N'Nombre de la empresa, S.A. de C.V.', 'Texto', N'Aparece en avisos, comprobantes y contratos'),
             ('Empresa.NIT', N'0000-000000-000-0', 'Texto', N'NIT del arrendante'),
             ('IVA.Porcentaje', N'13', 'Numero', N'IVA de locales y oficinas'),
             ('Correo.Remitente', N'cobros@arrendia.site', 'Texto', N'Remitente de los avisos de cobro'),
             ('Invitacion.DiasVigencia', N'7', 'Numero', N'Días que dura un enlace de invitación'),
             ('Recuperacion.MinutosVigencia', N'60', 'Numero', N'Minutos que dura el enlace para recuperar la contraseña'),
             ('Tecnico.HorasEnlace', N'48', 'Numero', N'Horas que dura el enlace del técnico'),
             ('Tecnico.MaxIntentosPin', N'5', 'Numero', N'Intentos de PIN antes de bloquear el enlace'),
             ('Pago.MinutosIntencion', N'30', 'Numero', N'Minutos para completar un pago en línea'),
             ('Mora.TopeDias', N'180', 'Numero', N'Días máximos de mora por cargo')) v (CLAVE, VALOR, TIPO, DESCRIPCION)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PARAMETRO p WHERE p.CLAVE = v.CLAVE);

/* Plantillas de condiciones: se copian a cada contrato nuevo. Solo una es la predeterminada. */
INSERT INTO dbo.PLANTILLA_CONDICIONES (NOMBRE, ES_PREDETERMINADA, DIA_PAGO, DIAS_EMISION_AVISO, APLICA_MORA, DIAS_GRACIA,
       PORCENTAJE_MORA_DIARIO, TIPO_TOPE_MORA, VALOR_TOPE_MORA, PERMITE_PAGOS_PARCIALES, CORRER_VENCIMIENTO_FERIADO, CREADO_POR)
SELECT v.NOMBRE, v.PRED, 5, 5, 1, v.GRACIA, v.MORA, 'Ninguno', NULL, 1, 1, @SISTEMA
FROM (VALUES (N'Comercial estándar', 1, 5, 0.1000),
             (N'Residencial estándar', 0, 5, 0.0500)) v (NOMBRE, PRED, GRACIA, MORA)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PLANTILLA_CONDICIONES p WHERE p.NOMBRE = v.NOMBRE);

/* Feriados nacionales de El Salvador (2026 y 2027). */
INSERT INTO dbo.FERIADO (FECHA, NOMBRE)
SELECT v.FECHA, v.NOMBRE
FROM (VALUES ('2026-01-01', N'Año Nuevo'), ('2026-04-02', N'Jueves Santo'), ('2026-04-03', N'Viernes Santo'), ('2026-04-04', N'Sábado Santo'),
             ('2026-05-01', N'Día del Trabajo'), ('2026-05-10', N'Día de la Madre'), ('2026-06-17', N'Día del Padre'),
             ('2026-08-06', N'Fiestas Agostinas'), ('2026-09-15', N'Día de la Independencia'), ('2026-11-02', N'Día de los Difuntos'), ('2026-12-25', N'Navidad'),
             ('2027-01-01', N'Año Nuevo'), ('2027-03-25', N'Jueves Santo'), ('2027-03-26', N'Viernes Santo'), ('2027-03-27', N'Sábado Santo'),
             ('2027-05-01', N'Día del Trabajo'), ('2027-05-10', N'Día de la Madre'), ('2027-06-17', N'Día del Padre'),
             ('2027-08-06', N'Fiestas Agostinas'), ('2027-09-15', N'Día de la Independencia'), ('2027-11-02', N'Día de los Difuntos'), ('2027-12-25', N'Navidad')
     ) v (FECHA, NOMBRE)
WHERE NOT EXISTS (SELECT 1 FROM dbo.FERIADO f WHERE f.FECHA = v.FECHA);

COMMIT TRANSACTION;
PRINT 'Seed aplicado.';
