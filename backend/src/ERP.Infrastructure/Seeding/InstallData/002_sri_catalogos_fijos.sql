-- ============================================================
-- CATÁLOGOS FIJOS SRI - COMPROBANTES ELECTRÓNICOS
-- Basado en Ficha Técnica Versión 2.32 (Octubre 2025)
-- Aplicable a: Ecuador - Sistema de Facturación Electrónica
-- ============================================================
-- Estas tablas contienen valores definidos por el SRI.
-- Son IGUALES para todos los contribuyentes.
-- NO deben ser modificadas por el usuario final.
-- ============================================================

-- ============================================================
-- TABLA: sri_tipo_emision (Tabla 2)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tipo_emision (
    codigo      VARCHAR(1)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(60)  NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tipo_emision (codigo, descripcion) VALUES
    ('1', 'Emisión Normal')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_tipo_comprobante (Tabla 3)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tipo_comprobante (
    codigo      VARCHAR(2)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(100) NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tipo_comprobante (codigo, descripcion) VALUES
    ('01', 'FACTURA'),
    ('03', 'LIQUIDACIÓN DE COMPRA DE BIENES Y PRESTACIÓN DE SERVICIOS'),
    ('04', 'NOTA DE CRÉDITO'),
    ('05', 'NOTA DE DÉBITO'),
    ('06', 'GUÍA DE REMISIÓN'),
    ('07', 'COMPROBANTE DE RETENCIÓN')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_tipo_ambiente (Tabla 4)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tipo_ambiente (
    codigo      VARCHAR(1)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(50)  NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tipo_ambiente (codigo, descripcion) VALUES
    ('1', 'PRUEBAS'),
    ('2', 'PRODUCCIÓN')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_tipo_identificacion (Tabla 6)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tipo_identificacion (
    codigo      VARCHAR(2)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(80)  NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tipo_identificacion (codigo, descripcion) VALUES
    ('04', 'RUC'),
    ('05', 'CÉDULA'),
    ('06', 'PASAPORTE'),
    ('07', 'VENTA A CONSUMIDOR FINAL'),
    ('08', 'IDENTIFICACIÓN DEL EXTERIOR')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_codigo_impuesto (Tabla 16)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_codigo_impuesto (
    codigo      VARCHAR(2)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(60)  NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_codigo_impuesto (codigo, descripcion) VALUES
    ('2', 'IVA'),
    ('3', 'ICE'),
    ('5', 'IRBPNR')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_tarifa_iva (Tabla 17)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tarifa_iva (
    codigo      VARCHAR(2)     NOT NULL PRIMARY KEY,
    descripcion VARCHAR(80)    NOT NULL,
    porcentaje  NUMERIC(5,2)   NOT NULL,
    activo      BOOLEAN        NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tarifa_iva (codigo, descripcion, porcentaje) VALUES
    ('0',  '0% - Tarifa cero',                          0.00),
    ('2',  '12% - IVA doce por ciento',                12.00),
    ('3',  '14% - IVA catorce por ciento',             14.00),
    ('4',  '15% - IVA quince por ciento',              15.00),
    ('5',  '5%  - IVA cinco por ciento',                5.00),
    ('6',  'No objeto de Impuesto',                     0.00),
    ('7',  'Exento de IVA',                             0.00),
    ('8',  '8%  - IVA diferenciado (sector turismo)',   8.00),
    ('10', '13% - IVA trece por ciento',               13.00)
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_tarifa_ice (Tabla 18 - datos actualizados a oct 2025)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tarifa_ice (
    codigo              VARCHAR(10)    NOT NULL PRIMARY KEY,
    descripcion         VARCHAR(200)   NOT NULL,
    tarifa_ad_valorem   NUMERIC(6,2),
    tarifa_especifica   NUMERIC(10,4),
    unidad_especifica   VARCHAR(30),
    activo              BOOLEAN        NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tarifa_ice (codigo, descripcion, tarifa_ad_valorem, tarifa_especifica, unidad_especifica) VALUES
    ('3011', 'ICE Cigarrillos Rubios',                                         NULL,  0.16,  'unidad'),
    ('3021', 'ICE Cigarrillos Negros',                                         NULL,  0.16,  'unidad'),
    ('3023', 'ICE Tabacos y Productos del Tabaco excl. Cigarrillos y Sucedáneos', 150.00, NULL, NULL),
    ('3031', 'ICE Bebidas Alcohólicas',                                         75.00, 10.00, 'litro'),
    ('3033', 'ICE Alcohol',                                                     75.00, 10.00, 'litro'),
    ('3041', 'ICE Cerveza Industrial',                                          75.00, NULL,  NULL),
    ('3043', 'ICE Cerveza Artesanal',                                           NULL,  1.50,  'litro'),
    ('3053', 'ICE Bebidas Gaseosas con Alto Contenido de Azúcar',              NULL,  0.18,  '100g azúcar'),
    ('3054', 'ICE Bebidas Gaseosas con Bajo Contenido de Azúcar',              10.00, NULL,  NULL),
    ('3073', 'ICE Vehículos Motorizados PVP hasta USD 20.000',                   5.00, NULL,  NULL),
    ('3075', 'ICE Vehículos Motorizados PVP USD 30.000–40.000',                 15.00, NULL,  NULL),
    ('3077', 'ICE Vehículos Motorizados PVP USD 40.000–50.000',                 20.00, NULL,  NULL),
    ('3078', 'ICE Vehículos Motorizados PVP USD 50.000–60.000',                 25.00, NULL,  NULL),
    ('3079', 'ICE Vehículos Motorizados PVP USD 60.000–70.000',                 30.00, NULL,  NULL),
    ('3080', 'ICE Vehículos Motorizados PVP superior a USD 70.000',             35.00, NULL,  NULL),
    ('3081', 'ICE Aviones, Tricares, Yates, Barcos de Recreo',                  15.00, NULL,  NULL),
    ('3092', 'ICE Servicios de Televisión Prepagada',                            0.00, NULL,  NULL),
    ('3093', 'ICE Servicios de Telefonía - Sociedades',                          15.00, NULL,  NULL),
    ('3101', 'ICE Bebidas Energizantes',                                         10.00, NULL,  NULL),
    ('3111', 'ICE Bebidas No Alcohólicas con Azúcar',                           NULL,  0.18,  '100g azúcar'),
    ('3532', 'ICE Importación Alcohol SENAE',                                   75.00, 10.00, 'litro'),
    ('3533', 'ICE Importación Bebidas Alcohólicas',                             75.00, NULL,  NULL),
    ('3541', 'ICE Cerveza Gran Escala CAE',                                     75.00, NULL,  NULL),
    ('3542', 'ICE Cigarrillos Rubios CAE',                                      NULL,  0.16,  'unidad'),
    ('3543', 'ICE Cigarrillos Negros CAE',                                      NULL,  0.16,  'unidad'),
    ('3544', 'ICE Tabaco y Sucedáneos excl. Cigarrillos CAE',                  150.00, NULL,  NULL),
    ('3545', 'ICE Cerveza Artesanal SENAE',                                     75.00, 1.50,  'litro'),
    ('3552', 'ICE Bebidas Gaseosas Alto Azúcar SENAE',                          NULL,  0.18,  '100g azúcar'),
    ('3553', 'ICE Bebidas Gaseosas Bajo Azúcar SENAE',                          10.00, NULL,  NULL),
    ('3581', 'ICE Aeronaves CAE',                                               15.00, NULL,  NULL),
    ('3601', 'ICE Bebidas Energizantes SENAE',                                   10.00, NULL,  NULL),
    ('3602', 'ICE Bebidas No Alcohólicas con Azúcar SENAE',                    NULL,  0.18,  '100g azúcar'),
    ('3610', 'ICE Perfumes y Aguas de Tocador',                                  20.00, NULL,  NULL),
    ('3620', 'ICE Videojuegos',                                                   0.00, NULL,  NULL),
    ('3630', 'ICE Municiones, Armas Deportivas y de Fuego',                    300.00, NULL,  NULL),
    ('3640', 'ICE Focos Incandescentes',                                        100.00, NULL,  NULL),
    ('3660', 'ICE Acciones, Cuotas, Membresías, Afiliaciones',                  35.00, NULL,  NULL),
    ('3671', 'ICE Calefones y Sistemas de Calentamiento a Gas',                100.00, NULL,  NULL),
    ('3680', 'ICE Fundas Plásticas',                                             NULL,  0.08,  'unidad'),
    ('3681', 'ICE Servicios de Telefonía Móvil Personas Naturales',              0.00, NULL,  NULL),
    ('3682', 'ICE Líquidos consumibles nicotina / Tabaco calentado',           150.00, NULL,  NULL),
    ('3683', 'ICE Líquidos consumibles nicotina / Tabaco calentado SENAE',      50.00, NULL,  NULL),
    ('3684', 'ICE Camionetas y Vehículos de Rescate PVP hasta USD 30.000',       5.00, NULL,  NULL),
    ('3685', 'ICE Camionetas y Vehículos de Rescate PVP hasta USD 30.000 SENAE', 5.00, NULL,  NULL),
    ('3686', 'ICE Vehículos excl. camionetas PVP USD 20.000–30.000',            10.00, NULL,  NULL),
    ('3687', 'ICE Vehículos excl. camionetas PVP USD 20.000–30.000 SENAE',      10.00, NULL,  NULL),
    ('3688', 'ICE Vehículos Híbridos PVP hasta USD 35.000',                      0.00, NULL,  NULL),
    ('3689', 'ICE Vehículos Híbridos PVP hasta USD 35.000 SENAE',                0.00, NULL,  NULL),
    ('3690', 'ICE Vehículos Híbridos PVP USD 35.000–40.000',                     8.00, NULL,  NULL),
    ('3691', 'ICE Vehículos Híbridos PVP USD 35.000–40.000 SENAE',               8.00, NULL,  NULL),
    ('3692', 'ICE Vehículos Híbridos PVP USD 40.000–50.000',                    14.00, NULL,  NULL),
    ('3693', 'ICE Vehículos Híbridos PVP USD 40.000–50.000 SENAE',              14.00, NULL,  NULL),
    ('3694', 'ICE Vehículos Híbridos PVP USD 50.000–60.000 SENAE',              20.00, NULL,  NULL),
    ('3695', 'ICE Vehículos Híbridos PVP USD 50.000–60.000',                    20.00, NULL,  NULL),
    ('3696', 'ICE Vehículos Híbridos PVP USD 60.000–70.000',                    26.00, NULL,  NULL),
    ('3697', 'ICE Vehículos Híbridos PVP USD 60.000–70.000 SENAE',              26.00, NULL,  NULL),
    ('3698', 'ICE Vehículos Híbridos PVP superior a USD 70.000',                32.00, NULL,  NULL),
    ('3699', 'ICE Vehículos Híbridos PVP superior a USD 70.000 SENAE',          32.00, NULL,  NULL),
    ('3710', 'ICE Perfumes y Aguas de Tocador CAE',                              20.00, NULL,  NULL),
    ('3720', 'ICE Videojuegos CAE',                                              35.00, NULL,  NULL),
    ('3730', 'ICE Importación Armas de Fuego y Municiones CAE',                300.00, NULL,  NULL),
    ('3740', 'ICE Focos Incandescentes CAE',                                    100.00, NULL,  NULL),
    ('3771', 'ICE Calefones y Sistemas de Calentamiento SENAE',                100.00, NULL,  NULL),
    ('3871', 'ICE Vehículos Motorizados SENAE PVP hasta USD 20.000',             5.00, NULL,  NULL),
    ('3873', 'ICE Vehículos Motorizados SENAE PVP USD 30.000–40.000',           15.00, NULL,  NULL),
    ('3874', 'ICE Vehículos Motorizados SENAE PVP USD 40.000–50.000',           20.00, NULL,  NULL),
    ('3875', 'ICE Vehículos Motorizados SENAE PVP USD 50.000–60.000',           25.00, NULL,  NULL),
    ('3876', 'ICE Vehículos Motorizados SENAE PVP USD 60.000–70.000',           30.00, NULL,  NULL),
    ('3877', 'ICE Vehículos Motorizados SENAE PVP superior a USD 70.000',       35.00, NULL,  NULL),
    ('3878', 'ICE Aviones, Tricares, Yates, Barcos de Recreo SENAE',            15.00, NULL,  NULL)
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_impuesto_retener (Tabla 19)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_impuesto_retener (
    codigo      VARCHAR(2)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(60)  NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_impuesto_retener (codigo, descripcion) VALUES
    ('1', 'RENTA'),
    ('2', 'IVA'),
    ('6', 'ISD')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_porcentaje_retencion_iva (Tabla 20 - IVA)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_porcentaje_retencion_iva (
    codigo      VARCHAR(5)     NOT NULL PRIMARY KEY,
    descripcion VARCHAR(80)    NOT NULL,
    porcentaje  NUMERIC(5,2)   NOT NULL,
    activo      BOOLEAN        NOT NULL DEFAULT TRUE
);

INSERT INTO sri_porcentaje_retencion_iva (codigo, descripcion, porcentaje) VALUES
    ('1',  'Retención IVA 30%',                                           30.00),
    ('2',  'Retención IVA 70%',                                           70.00),
    ('3',  'Retención IVA 100%',                                         100.00),
    ('7',  'Retención IVA 0% (Res. NAC-DGERCGC15-00000284)',               0.00),
    ('8',  'No procede retención de IVA',                                  0.00),
    ('9',  'Retención IVA 10%',                                           10.00),
    ('10', 'Retención IVA 20%',                                           20.00),
    ('11', 'Retención IVA 50%',                                           50.00)
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_porcentaje_retencion_isd (Tabla 20 - ISD histórico)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_porcentaje_retencion_isd (
    codigo          VARCHAR(10)    NOT NULL PRIMARY KEY,
    descripcion     VARCHAR(100)   NOT NULL,
    porcentaje      NUMERIC(5,4)   NOT NULL,
    vigencia_desde  DATE,
    vigencia_hasta  DATE,
    activo          BOOLEAN        NOT NULL DEFAULT TRUE
);

INSERT INTO sri_porcentaje_retencion_isd (codigo, descripcion, porcentaje, vigencia_desde, vigencia_hasta, activo) VALUES
    ('4580_v1', 'ISD 5.00%  (hasta 31-dic-2021)',        5.0000, '2013-01-01', '2021-12-31', FALSE),
    ('4580_v2', 'ISD 4.75%  (ene 2022)',                 4.7500, '2022-01-01', '2022-03-31', FALSE),
    ('4580_v3', 'ISD 4.50%  (abr–jun 2022)',             4.5000, '2022-04-01', '2022-06-30', FALSE),
    ('4580_v4', 'ISD 4.25%  (jul–sep 2022)',             4.2500, '2022-07-01', '2022-09-30', FALSE),
    ('4580_v5', 'ISD 4.00%  (oct–dic 2022)',             4.0000, '2022-10-01', '2022-12-31', FALSE),
    ('4580_v6', 'ISD 3.75%  (feb–jun 2023)',             3.7500, '2023-02-01', '2023-06-30', FALSE),
    ('4580_v7', 'ISD 3.50%  (jul 2023–mar 2024)',        3.5000, '2023-07-01', '2024-03-31', FALSE),
    ('4580_v8', 'ISD 5.00%  (desde abr-2024)',           5.0000, '2024-04-01', NULL,         TRUE),
    ('4586',    'ISD 2.50%  (desde may-2025 según ley)', 2.5000, '2025-05-01', NULL,         TRUE)
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_porcentaje_retencion_iva_presuntivo (Tabla 23)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_porcentaje_retencion_iva_presuntivo (
    codigo      VARCHAR(5)     NOT NULL PRIMARY KEY,
    descripcion VARCHAR(150)   NOT NULL,
    porcentaje  NUMERIC(6,2)   NOT NULL,
    activo      BOOLEAN        NOT NULL DEFAULT TRUE
);

INSERT INTO sri_porcentaje_retencion_iva_presuntivo (codigo, descripcion, porcentaje) VALUES
    ('3', 'Retención IVA 100% (IVA según Res. NAC-DGERCGC21-00000063)',                100.00),
    ('4', 'Retención IVA 12% (Presuntiva editores a voceadores)',                       12.00),
    ('5', 'Retención IVA 100% (Venta periódicos/revistas a distribuidores)',           100.00),
    ('6', 'Retención IVA 100% (Venta periódicos/revistas a voceadores)',               100.00)
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_retencion_renta_derivados (Tabla 23 - renta)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_retencion_renta_derivados (
    codigo      VARCHAR(5)     NOT NULL PRIMARY KEY,
    descripcion VARCHAR(100)   NOT NULL,
    porcentaje  NUMERIC(5,3)   NOT NULL,
    activo      BOOLEAN        NOT NULL DEFAULT TRUE
);

INSERT INTO sri_retencion_renta_derivados (codigo, descripcion, porcentaje) VALUES
    ('327', 'Retención Renta 0.2% (2 por mil) - Derivados de petróleo', 0.200),
    ('328', 'Retención Renta 0.3% (3 por mil) - Derivados de petróleo', 0.300)
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_forma_pago (Tabla 24)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_forma_pago (
    codigo          VARCHAR(3)   NOT NULL PRIMARY KEY,
    descripcion     VARCHAR(80)  NOT NULL,
    vigencia_desde  DATE,
    activo          BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_forma_pago (codigo, descripcion, vigencia_desde) VALUES
    ('01', 'SIN UTILIZACIÓN DEL SISTEMA FINANCIERO',       '2013-01-01'),
    ('15', 'COMPENSACIÓN DE DEUDAS',                       '2013-01-01'),
    ('16', 'TARJETA DE DÉBITO',                            '2016-06-01'),
    ('17', 'DINERO ELECTRÓNICO',                           '2016-06-01'),
    ('18', 'TARJETA PREPAGO',                              '2016-06-01'),
    ('19', 'TARJETA DE CRÉDITO',                           '2016-06-01'),
    ('20', 'OTROS CON UTILIZACIÓN DEL SISTEMA FINANCIERO', '2016-06-01'),
    ('21', 'ENDOSO DE TÍTULOS',                            '2016-06-01')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_estado_comprobante
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_estado_comprobante (
    siglas      VARCHAR(5)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(60)  NOT NULL
);

INSERT INTO sri_estado_comprobante (siglas, descripcion) VALUES
    ('PPR', 'EN PROCESAMIENTO'),
    ('AUT', 'AUTORIZADO'),
    ('NAT', 'NO AUTORIZADO')
ON CONFLICT (siglas) DO NOTHING;

-- ============================================================
-- TABLA: sri_pais (Tabla 25)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_pais (
    codigo      VARCHAR(5)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(80)  NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_pais (codigo, descripcion) VALUES
    ('016', 'AMERICAN SAMOA'), ('032', 'ARGENTINA'), ('068', 'BOLIVIA'), ('076', 'BRASIL'),
    ('100', 'BULGARIA'), ('116', 'CAMBOYA'), ('124', 'CANADÁ'), ('152', 'CHILE'),
    ('156', 'CHINA'), ('170', 'COLOMBIA'), ('188', 'COSTA RICA'), ('192', 'CUBA'),
    ('214', 'REPÚBLICA DOMINICANA'), ('218', 'ECUADOR'), ('222', 'EL SALVADOR'),
    ('246', 'FINLANDIA'), ('250', 'FRANCIA'), ('276', 'ALEMANIA'), ('300', 'GRECIA'),
    ('320', 'GUATEMALA'), ('332', 'HAITÍ'), ('340', 'HONDURAS'), ('356', 'INDIA'),
    ('364', 'IRÁN'), ('376', 'ISRAEL'), ('380', 'ITALIA'), ('388', 'JAMAICA'),
    ('392', 'JAPÓN'), ('484', 'MÉXICO'), ('558', 'NICARAGUA'), ('591', 'PANAMÁ'),
    ('598', 'PAPUA NUEVA GUINEA'), ('600', 'PARAGUAY'), ('604', 'PERÚ'), ('616', 'POLONIA'),
    ('620', 'PORTUGAL'), ('630', 'PUERTO RICO'), ('642', 'RUMANIA'), ('643', 'RUSIA'),
    ('682', 'ARABIA SAUDITA'), ('703', 'ESLOVAQUIA'), ('724', 'ESPAÑA'), ('752', 'SUECIA'),
    ('756', 'SUIZA'), ('764', 'TAILANDIA'), ('792', 'TURQUÍA'), ('826', 'REINO UNIDO'),
    ('840', 'ESTADOS UNIDOS DE AMERICA'), ('858', 'URUGUAY'), ('862', 'VENEZUELA'),
    ('074', 'BOUVET ISLAND'), ('334', 'QATAR'), ('335', 'MALDIVAS'), ('336', 'NEPAL'),
    ('337', 'OMAN'), ('338', 'SINGAPUR'), ('339', 'SRI LANKA (CEILAN)'), ('593', 'ECUADOR')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_documento_sustento (ATS)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_documento_sustento (
    codigo      VARCHAR(3)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(120) NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_documento_sustento (codigo, descripcion) VALUES
    ('01', 'Factura'),
    ('04', 'Nota de Crédito'),
    ('05', 'Nota de Débito'),
    ('06', 'Guía de Remisión'),
    ('07', 'Comprobante de Retención'),
    ('41', 'Comprobante de Reembolso'),
    ('42', 'Documento de retención presuntiva o autoretención')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_tipo_proveedor_reembolso (Tabla 26)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tipo_proveedor_reembolso (
    codigo      VARCHAR(2)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(50)  NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tipo_proveedor_reembolso (codigo, descripcion) VALUES
    ('01', 'Persona Natural'),
    ('02', 'Sociedad')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_codigo_error (Numeral 11)
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_codigo_error (
    codigo              INTEGER      NOT NULL PRIMARY KEY,
    tipo                VARCHAR(20)  NOT NULL CHECK (tipo IN ('ERROR', 'ADVERTENCIA', 'VALIDACION')),
    descripcion         VARCHAR(255) NOT NULL,
    posible_solucion    TEXT,
    activo              BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_codigo_error (codigo, tipo, descripcion, posible_solucion) VALUES
    (2,    'ERROR',       'RUC del emisor se encuentra NO ACTIVO',          'Verificar que el número de RUC esté en estado ACTIVO en el SRI'),
    (10,   'ERROR',       'Establecimiento del emisor se encuentra Clausurado', 'No se autorizará comprobantes hasta que concluya la clausura'),
    (26,   'ERROR',       'Tamaño máximo superado',                         'El archivo supera el tamaño permitido (individual 320KB, lote 500KB)'),
    (35,   'ERROR',       'Documento inválido',                             'El XML no pasa la validación de esquema XSD. Revisar estructura y etiquetas.'),
    (39,   'ERROR',       'Firma inválida',                                 'La firma electrónica no es válida (XAdES_BES). Verificar certificado y librería.'),
    (43,   'ERROR',       'Clave acceso registrada',                        'La clave de acceso ya existe en la base de datos del SRI. No se puede duplicar.'),
    (45,   'ERROR',       'Secuencial registrado',                          'El secuencial ya fue usado para ese tipo de comprobante, establecimiento y punto de emisión.'),
    (52,   'ERROR',       'Error en diferencias',                           'Los cálculos del comprobante son incorrectos (suma de impuestos, totales). Revisar.'),
    (65,   'ERROR',       'Fecha de emisión extemporánea',                  'El comprobante fue enviado fuera del plazo permitido (emisión vs envío).'),
    (70,   'ERROR',       'Clave de acceso en procesamiento',               'El comprobante ya fue enviado y está en proceso. No reenviar hasta respuesta.'),
    (59,   'ADVERTENCIA', 'Identificación no existe',                       'El número de identificación del adquirente no existe en el RUC.'),
    (60,   'ADVERTENCIA', 'Ambiente ejecución',                             'Comprobante emitido en ambiente de pruebas (solo informativo).'),
    (62,   'ADVERTENCIA', 'Identificación incorrecta',                      'El número de identificación no pasa el dígito verificador (cédula mal ingresada).'),
    (2000, 'VALIDACION',  'Éxito',                                          'Proceso exitoso de devolución automática de IVA'),
    (3000, 'VALIDACION',  'ERROR_FORMATO',                                  'Los campos registrados no cumplen con el formato establecido')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_tipo_regimen
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_tipo_regimen (
    codigo      VARCHAR(5)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(100) NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_tipo_regimen (codigo, descripcion) VALUES
    ('01', 'RÉGIMEN GENERAL'),
    ('02', 'RIMPE - EMPRENDEDOR'),
    ('03', 'RIMPE - NEGOCIO POPULAR'),
    ('04', 'MICROEMPRESA')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- TABLA: sri_sustento_tributario
-- ============================================================
CREATE TABLE IF NOT EXISTS sri_sustento_tributario (
    codigo      VARCHAR(3)   NOT NULL PRIMARY KEY,
    descripcion VARCHAR(200) NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE
);

INSERT INTO sri_sustento_tributario (codigo, descripcion) VALUES
    ('01', 'Crédito Tributario para declaración de IVA (servicios y bienes distintos de inventarios y activos fijos)'),
    ('02', 'Costo o Gasto para declaración de IR (otros costos y gastos)'),
    ('03', 'Activo Fijo - Crédito Tributario para declaración de IVA'),
    ('04', 'Activo Fijo - Costo o Gasto para declaración de IR'),
    ('05', 'Liquidación Crédito Tributario para declaración de IVA (No objeto de IVA)'),
    ('06', 'Liquidación Costo o Gasto para declaración de IR (No objeto de IVA)'),
    ('07', 'Compras con sustento tributario para declaración IR e IVA (inventario)'),
    ('08', 'Valor pagado por Anticipos'),
    ('09', 'Pagos de Dividendos Anticipados'),
    ('10', 'Pagos de Participación a Trabajadores'),
    ('20', 'Sustento de Importaciones (liquidación de compra de bienes y prestación de servicios)')
ON CONFLICT (codigo) DO NOTHING;

-- ============================================================
-- FIN DEL SCRIPT DE CATÁLOGOS FIJOS SRI
-- ============================================================
