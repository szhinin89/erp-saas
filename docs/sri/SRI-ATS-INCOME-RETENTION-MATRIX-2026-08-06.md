# Matriz Renta — Catálogo ATS oficial vs ERP (ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01)

> Auditoría y registro de decisiones (no normativo). Decisión vinculante: [ADR-037](../decisions/ADR-037-sri-global-catalog-governance.md).

## Fuente oficial

| Campo | Valor |
|---|---|
| Archivo | `Catalogo_ATS.xls` |
| URL | https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/e6a826af-b22c-40bb-8752-d711f293b8f9/Catalogo_ATS.xls |
| Página de origen | https://www.sri.gob.ec/formularios-e-instructivos1 (descargado el 2026-10-02) |
| SHA-256 | `bd3f7834f2cd31187af39cd2f4c685a646d7e49316e92ee493da5f2dd9776e3e` (3 823 616 bytes) |
| Hoja / tabla / bloque | `TABLAS RETENCIONES` · Tabla 3.10 Conceptos de retención en la fuente de IR (AIR) · bloque **DESDE 06/AGOSTO/2026** (columnas B–E) |
| Actualización | 06/08/2026 (inicio del bloque) |
| Diferencia con el bloque anterior (01/08/2026) | Solo la descripción de 3440; ninguna tasa cambia |

Columna **ERP antes** = seed vigente hasta el commit `92096e38`. "Fija" = una sola tarifa numérica; "Condicional/variable" = la fuente no fija una tarifa única (no se guarda porcentaje; el ERP no puede resolverla).

## Matriz completa (127 códigos vigentes + 3 retirados del ERP)

| Código | Grupo | Descripción oficial | Tarifa/regla oficial | Tipo | ERP antes | Diferencia / decisión |
|---|---|---|---|---|---|---|
| 303 | Residente | Honorarios profesionales y demás pagos por servicios relacionados con el título profesional | 10 % | Fija | Honorarios profesionales y demás servicios — 10 % | Mismo significado y tasa. Nombre oficial; versión ATS desde 06/08/2026. Defaults intactos. |
| 303A | Residente | Servicios profesionales prestados por sociedades residentes | 5 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 304 | Residente | Servicios predomina el intelecto no relacionados con el título profesional | 10 % | Fija | Servicios – predomina mano de obra — 2 % | **Significado distinto.** Nombre y tasa oficiales (10 %); versión ATS. Defaults deshabilitados. |
| 304A | Residente | Comisiones y demás pagos por servicios predomina intelecto no relacionados con el título profesional | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 304B | Residente | Pagos a notarios y registradores de la propiedad y mercantil por sus actividades ejercidas como tales | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 304C | Residente | Pagos a deportistas, entrenadores, árbitros, miembros del cuerpo técnico por sus actividades ejercidas como tales | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 304D | Residente | Pagos a artistas por sus actividades ejercidas como tales | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 304E | Residente | Honorarios y demás pagos por servicios de docencia | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 307 | Residente | Servicios predomina la mano de obra | 3 % | Fija | Publicidad y comunicación — 1.75 % | **Significado distinto.** Nombre y tasa oficiales (3 %); versión ATS. Defaults deshabilitados. |
| 308 | Residente | Utilización o aprovechamiento de la imagen o renombre (personas naturales, sociedades," influencers") | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 309 | Residente | Servicios prestados por medios de comunicación y agencias de publicidad | 3 % | Fija | Arrendamiento bienes inmuebles (persona natural) — 8 % | **Significado distinto.** Nombre y tasa oficiales (3 %); versión ATS. Defaults deshabilitados. |
| 310 | Residente | Servicio de transporte privado de pasajeros o transporte público o privado de carga | 1 /0 según resolución NAC-DGERCGC26-00000028 | Condicional/variable | Seguros y reaseguros (10% de primas) — 1 % | **Significado distinto + tasa condicional.** Nombre oficial; versión Conditional (sin %); concepto no habilitado; resolución falla cerrado. Defaults deshabilitados. |
| 311 | Residente | Pagos a través de liquidación de compra (nivel cultural o rusticidad) | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 312 | Residente | Transferencia de bienes muebles de naturaleza corporal | 2 % | Fija | Transf. bienes muebles de naturaleza corporal — 1 % | Mismo significado; **tasa 1 % → 2 %**. Versión ATS; tasa operativa 2 %. Defaults intactos. |
| 312A | Residente | COMPRAS AL PRODUCTOR: de bienes de origen bioacuático, forestal y los descritos el art.27.1 de LRTI | 1 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 312C | Residente | COMPRAS AL COMERCIALIZADOR: de bienes de origen bioacuático, forestal y los descritos el art.27.1 de LRTI | 1.75 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 314A | Residente | Regalías por concepto de franquicias de acuerdo al Código INGENIOS (COESCCI) - pago a personas naturales | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 314B | Residente | Cánones, derechos de autor, marcas, patentes y similares de acuerdo al Código INGENIOS (COESCCI) – pago a personas naturales | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 314C | Residente | Regalías por concepto de franquicias de acuerdo al Código INGENIOS (COESCCI) - pago a sociades | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 314D | Residente | Cánones, derechos de autor, marcas, patentes y similares de acuerdo al Código INGENIOS (COESCCI) | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 319 | Residente | Cuotas de arrendamiento mercantil (prestado por sociedades), inclusive la de opción de compra | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 320 | Residente | Arrendamiento bienes inmuebles | 10 % | Fija | Servicios entre sociedades — 2.75 % | **Significado distinto.** Nombre y tasa oficiales (10 %); versión ATS. Defaults deshabilitados. |
| 322 | Residente | Seguros y reaseguros (primas y cesiones) | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323 | Residente | Rendimientos financieros pagados a naturales y sociedades (No a IFIs) | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323A | Residente | Rendimientos financieros depósitos Cta. Corriente | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323B1 | Residente | Rendimientos financieros depósitos Cta. Ahorros Sociedades | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323E | Residente | Rendimientos financieros depósito a plazo fijo gravados | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323E2 | Residente | Rendimientos financieros depósito a plazo fijo exentos | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323F | Residente | Rendimientos financieros operaciones de reporto - repos | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323G | Residente | Inversiones (captaciones) rendimientos distintos de aquellos pagados a IFIs | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323H | Residente | Rendimientos financieros obligaciones | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323I | Residente | Rendimientos financieros bonos convertible en acciones | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323 M | Residente | Rendimientos financieros : Inversiones en títulos valores en renta fija gravados | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323 N | Residente | Rendimientos financieros Inversiones en títulos valores en renta fija exentos | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323 O | Residente | Intereses y demás rendimientos financieros pagados a bancos y otras entidades sometidas al control de la Superintendencia de Bancos y de la Economía Popular y Solidaria | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323 P | Residente | Intereses pagados por entidades del sector público a favor de sujetos pasivos | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323Q | Residente | Otros intereses y rendimientos financieros gravados | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323R | Residente | Otros intereses y rendimientos financieros exentos | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323S | Residente | Pagos y créditos en cuenta efectuados por el BCE y los depósitos centralizados de valores, en calidad de intermediarios, a instituciones del sistema financiero por cuenta de otras personas naturales y sociedades | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323T | Residente | Rendimientos financieros originados en la deuda pública ecuatoriana | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 323U | Residente | Rendimientos financieros originados en títulos valores de obligaciones de 360 días o más para el financiamiento de proyectos públicos en asociación público-privada | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 324A | Residente | Intereses en operaciones de crédito entre instituciones del sistema financiero y entidades economía popular y solidaria. | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 324B | Residente | Inversiones entre instituciones del sistema financiero y entidades economía popular y solidaria | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 324C | Residente | Pagos y créditos en cuenta efectuados por el BCE y los depósitos centralizados de valores, en calidad de intermediarios, a instituciones del sistema financiero por cuenta de otras instituciones del sistema financiero | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 325 | Residente | Anticipo dividendos | 25 % | Fija | Compra bienes corporales muebles — 1.75 % | **Significado distinto.** Nombre y tasa oficiales (25 %); versión ATS. Defaults deshabilitados. |
| 325A | Residente | Préstamos accionistas, beneficiarios o partícipes residentes o establecidos en el Ecuador | 25 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 3250 | Residente | Dividendos exentos (por no llegar a franja exenta o beneficio de otras leyes) | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 326 | Residente | Dividendos distribuidos que correspondan al impuesto a la renta único establecido en el art. 27 de la LRTI | 12 o 14 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 327 | Residente | Dividendos distribuidos a personas naturales residentes | 12 o 14 | Condicional/variable | Actividades de construcción (contrato) — 1.75 % | **Significado distinto + tasa condicional.** Nombre oficial; versión Conditional (sin %); concepto no habilitado; resolución falla cerrado. Defaults deshabilitados. |
| 328 | Residente | Dividendos distribuidos a sociedades residentes | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 329 | Residente | Dividendos distribuidos a fideicomisos residentes | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 331 | Residente | Dividendos en acciones (capitalización de utilidades) | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332 | Residente | Otras compras de bienes y servicios no sujetas a retención (incluye régimen RIMPE - Negocios Populares, para este caso aplica con cualquier forma de pago inclusive los pagos que deban realizar las tarjetas de crédito/débito) | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332B | Residente | Compra de bienes inmuebles | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332C | Residente | Transporte público de pasajeros | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332D | Residente | Pagos en el país por transporte de pasajeros o transporte internacional de carga, a compañías nacionales o extranjeras de aviación o marítimas | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332E | Residente | Valores entregados por las cooperativas de transporte a sus socios | 0 /1 según resolución NAC-DGERCGC26-00000028 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 332F | Residente | Compraventa de divisas distintas al dólar de los Estados Unidos de América | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332G | Residente | Pagos con tarjeta de crédito | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332H | Residente | Pago al exterior tarjeta de crédito reportada por la Emisora de tarjeta de crédito, solo recap | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 332I | Residente | Pago a través de convenio de debito (Clientes IFI`s) | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 333 | Residente | Ganancia en la enajenación de derechos representativos de capital u otros derechos que permitan la exploración, explotación, concesión o similares de sociedades, que se coticen en bolsa de valores del Ecuador | 10 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 334 | Residente | Contraprestación producida por la enajenación de derechos representativos de capital u otros derechos que permitan la exploración, explotación, concesión o similares de sociedades, no cotizados en bolsa de valores del Ecuador | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 335 | Residente | Loterías, rifas, pronósticos deportivos, apuestas y similares | 15 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 336 | Residente | Venta de combustibles a comercializadoras | 2/mil | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 337 | Residente | Venta de combustibles a distribuidores | 3/mil | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 338 | Residente | Producción y venta local de banano producido o no por el mismo sujeto pasivo | 1 a 2 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 340 | Residente | Impuesto único a la exportación de banano | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 343 | Residente | Otras retenciones aplicables el 1% (incluye régimen RIMPE - Emprendedores, para este caso aplica con cualquier forma de pago inclusive los pagos que deban realizar las tarjetas de crédito/débito) | 1 % | Fija | Otras retenciones aplicables al 1.75% — 1.75 % | **Descripción/tasa distinta** (ERP 'otras 1,75 %' vs oficial 'otras 1 %'). Nombre y tasa oficiales (1 %); versión ATS. Defaults deshabilitados. |
| 343A | Residente | Energía eléctrica | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 343B | Residente | Actividades de construcción de obra material inmueble, urbanización, lotización o actividades similares | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 343C | Residente | Recepción de botellas plásticas no retornables de PET | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 3440 | Residente | Otras retenciones aplicables el 3% (incluye pago utilidades a extrabajadores) | 3 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 344A | Residente | Pago local tarjeta de crédito /débito reportada por la Emisora de tarjeta de crédito / entidades del sistema financiero/sistemas auxiliares de pago | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 344B | Residente | Adquisición de sustancias minerales dentro del territorio nacional | 2 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 346 | Residente | Otras retenciones aplicables a otros porcentajes | varios porcentajes | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 346A | Residente | Otras ganancias de capital distintas de enajenación de derechos representativos de capital | varios porcentajes | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 346B | Residente | Donaciones en dinero -Impuesto a las donaciones | Conforme Art 36 LRTI literal d) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 346C | Residente | Retención a cargo del propio sujeto pasivo por la producción y/o comercialización de minerales y otros bienes | 0 a 10 (NAC-DGERCGC16-00000217 y sus reformas) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 346D | Residente | Retención a cargo del propio sujeto pasivo por la comercialización de productos forestales | 0 o 10 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 350 | Residente | Otras autorretenciones (inciso 1 y 2 Art.92.1 RLRTI) | 1,50 o 1,75 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 3480 | Residente | Impuesto a la renta único sobre los ingresos percibidos por los operadores de pronósticos deportivos | 15 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 3481 | Residente | Autorretenciones Sociedades Grandes Contribuyentes | varios porcentajes (Conforme RESOLUCIÓN No. NAC-DGERCGC24-00000024 y sus reformas) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 3482 | Residente | Comisiones a sociedades, nacionales o extranjeras residentes y establecimientos permanentes domiciliados en el país | 5 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 500 | No residente | Pago a no residentes - Rentas Inmobiliarias | 25 o 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 501 | No residente | Pago a no residentes - Beneficios/Servicios Empresariales | 25 o 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 501A | No residente | Pago a no residentes - Servicios técnicos, administrativos o de consultoría y regalías | 3, 5, 8, 10, 15, 25, 37 (Las tarifas 3 y 8 solo en pagos de Regalías bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 503 | No residente | Pago a no residentes- Navegación Marítima y/o aérea | 0 o 25 o 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 504 | No residente | Pago a no residentes- Dividendos distribuidos a personas naturales (domiciliados o no en paraíso fiscal) o a sociedades sin beneficiario efectivo persona natural residente en Ecuador | 3, 5, 8, 10 (5 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 504A | No residente | Dividendos a sociedades con beneficiario efectivo persona natural residente en el Ecuador | 3, 5, 8, 10, 12 (5 y 10 es por la tarifa de CDI. Las tarifas 3 y 8 aplica únicamente bajo el CDI con China | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 504B | No residente | Dividendos a no residentes incumpliendo el deber de informar la composición societaria | 3, 5, 8, 10, 14 (5 y 10 es por la tarifa de CDI. Las tarifas 3 y 8 aplica únicamente bajo el CDI con China | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 504C | No residente | Dividendos a residentes o establecidos en paraísos fiscales o regímenes de menor imposición (con beneficiario Persona Natural residente en Ecuador) | 3, 5, 8, 10, 14 (5 y 10 es por la tarifa de CDI. Las tarifas 3 y 8 aplica únicamente bajo el CDI con China | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 504D | No residente | Dividendos a fideicomisos o establecidos en paraísos fiscales o regímenes de menor imposición (con beneficiario Persona Natural residente en Ecuador) | 3, 5, 8, 10, 14 (5 y 10 es por la tarifa de CDI. Las tarifas 3 y 8 aplica únicamente bajo el CDI con China | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 504E | No residente | Pago a no residentes - Anticipo dividendos (no domiciliada en paraísos fiscales o regímenes de menor imposición) | 25 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 504F | No residente | Pago a no residentes - Anticipo dividendos (domiciliadas en paraísos fiscales o regímenes de menor imposición) | 25 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 504G | No residente | Pago a no residentes - Préstamos accionistas, beneficiarios o partícipes (no domiciliadas en paraísos fiscales o regímenes de menor imposición) | 25 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 504H | No residente | Pago a no residentes - Préstamos accionistas, beneficiarios o partícipes (domiciliadas en paraísos fiscales o regímenes de menor imposición) | 25 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 504I | No residente | Pago a no residentes - Préstamos no comerciales a partes relacionadas (no domiciliadas en paraísos fiscales o regímenes de menor imposición) | 25 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 504J | No residente | Pago a no residentes - Préstamos no comerciales a partes relacionadas (domiciliadas en paraísos fiscales o regímenes de menor imposición) | 25 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 505 | No residente | Pago a no residentes - Rendimientos financieros | 3,5,8,10,15,25 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 505A | No residente | Pago a no residentes – Intereses de créditos de Instituciones Financieras del exterior | 0, 3,5,8,10,15,25 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China; 0 o 25 art.13 LRTI) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 505B | No residente | Pago a no residentes – Intereses de créditos de gobierno a gobierno | 0, 3,5,8,10,15,25 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China; 0 o 25 art.13 LRTI) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 505C | No residente | Pago a no residentes – Intereses de créditos de organismos multilaterales | 0, 3,5,8,10,15,25 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China; 0 o 25 art.13 LRTI) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 505D | No residente | Pago a no residentes - Intereses por financiamiento de proveedores externos | 3,5,8,10,15,25 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 505E | No residente | Pago a no residentes - Intereses de otros créditos externos | 3,5,8,10,15,25 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 505F | No residente | Pago a no residentes - Otros Intereses y Rendimientos Financieros | 3,5,8,10,15,25 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 509 | No residente | Pago a no residentes- Cánones, derechos de autor, marcas, patentes y similares | 3,5,8,10,15,25,37 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 509A | No residente | Pago a no residentes - Regalías por concepto de franquicias | 3,5,8,10,15,25,37 (5, 10 y 15 es por la tarifa de CDI Las tarifas 3 y 8 aplica únicamente bajo el CDI con China) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 510 | No residente | Pago a no residentes - Otras ganancias de capital distintas de enajenación de derechos representativos de capital | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 511 | No residente | Pago a no residentes - Servicios profesionales independientes | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 512 | No residente | Pago a no residentes - Servicios profesionales dependientes | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 513A | No residente | Pago a no residentes - Deportistas | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 514 | No residente | Pago a no residentes - Participación de consejeros | 25 o 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 515 | No residente | Pago a no residentes- Artistas y relacionados a organización, producción y espectáculos artísticos y culturales en Ecuador | 15, 25, 37; según Art. 39.3 LRTI y la Resolución NAC-DGERCGC24-00000008 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 516 | No residente | Pago a no residentes - Pensiones | 0, 25, 37 (0 por casuística de CDI) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 517 | No residente | Pago a no residentes- Reembolso de Gastos | 0 al 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 518 | No residente | Pago a no residentes- Funciones Públicas | 0, 25, 37 (0 por casuística de CDI) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 519 | No residente | Pago a no residentes - Estudiantes | 25 o 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 520A | No residente | Pago a no residentes - Pago a proveedores de servicios hoteleros y turísticos en el exterior | 25 o 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 520B | No residente | Pago a no residentes - Arrendamientos mercantil internacional | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 520D | No residente | Pago a no residentes - Comisiones por exportaciones y por promoción de turismo receptivo | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 520E | No residente | Pago a no residentes - Por las empresas de transporte marítimo o aéreo y por empresas pesqueras de alta mar, por su actividad. | 0 % | Fija | — | No existe en el ERP (sin alta en este ticket). |
| 520F | No residente | Pago a no residentes - Por las agencias internacionales de prensa | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 520G | No residente | Pago a no residentes - Contratos de fletamento de naves para empresas de transporte aéreo o marítimo internacional | 0, 25, 37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 521 | No residente | Pago a no residentes - Enajenación de derechos representativos de capital u otros derechos que permitan la exploración, explotación, concesión o similares de sociedades | 0, 10 (0 por casuística de CDI) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 523A | No residente | Pago a no residentes - Seguros y reaseguros (primas y cesiones) | 0 ,25 ,37 | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 525 | No residente | Pago a no residentes- Donaciones en dinero -Impuesto a las donaciones | Según art 36 LRTI literal d) | Condicional/variable | — | No existe en el ERP (sin alta en este ticket). |
| 341 | — | No existe en el bloque vigente (histórico: 'Otras 2 %' 2009–2014; 'IU exportación banano' 2015–2019) | — | — | Otras retenciones aplicables al 2% — 2 % | **Retirado.** Versión heredada cerrada 2026-08-05; sin versión nueva; no habilitado; defaults deshabilitados. |
| 342 | — | No existe en el bloque vigente (histórico: 'Otras 8 %' 2009–2014; 'IU exportación banano terceros' 2015–2019) | — | — | Otras retenciones aplicables al 1% — 1 % | **Retirado.** Igual que 341. |
| 344 | — | No existe en el bloque vigente (histórico: 'Otros porcentajes'/2 % hasta 03/2020; el concepto 'otras' pasó a 3440: 2,75 % → 3 % desde 01/03/2026) | — | — | Otras retenciones aplicables al 2.75% — 2,75 % | **Retirado.** Igual que 341. No se remapea a 3440 (decisión del propietario). |

## Antes de desplegar: defaults de proveedor afectados (solo lectura)

La migración `SriRetentionIncomeCatalogAts20260806` deshabilita los defaults de proveedor que apuntan a conceptos cuyo significado cambió o que se retiraron. Ejecutar en el piloto **antes** de desplegar, para saber qué proveedores habrá que revisar:

```sql
BEGIN TRANSACTION READ ONLY;
SELECT d.tenant_id, d.company_id, d.business_partner_id, c.code, c.name AS nombre_erp_anterior, d.is_active
FROM master_supplier_retention_defaults d
JOIN global.sri_retention_code c ON c.id = d.sri_retention_code_id
WHERE c.tax_type = 'RENTA' AND c.code IN ('304','307','309','310','320','325','327','341','342','343','344')
ORDER BY d.tenant_id, d.company_id, c.code;
ROLLBACK;
```
