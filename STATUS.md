# Project Status

## SEGURIDAD — SEC-03: aislamiento entre empresas en configuración y retenciones — CORREGIDO (2026-10-26)

13 operaciones COMPANY no declaraban marcador de scope y `CompanyScopeBehavior` no validaba la
membership de la empresa de `X-Company-Id`: un usuario de A podía leer/modificar el SMTP, la política
fiscal de Consumidor Final, las preferencias operativas y los defaults de facturación de B, consultar
el estado de facturación electrónica de B y generar/registrar comprobantes de retención con el
contexto de B. Corregido protegiendo los 13 requests con el marcador existente
`IRequiresCompanyContext` (Communications ×3, Retenciones ×3, OrgConfig ×2, política fiscal ×2,
preferencias operativas ×2, estado FE ×1); `INV_E2` alineado a los namespaces reales (`Purchases`,
`Caja`) y ampliado a esas operaciones. Evidencia: `CompanySettingsIsolationHttpTests` RED 7/7 (fallos
esperados sin la corrección); focales PASS: API HTTP/PostgreSQL 15/15, Application 49/49,
`RetentionsControllerTests` 16/16, Arquitectura 17/17 y `architecture:check`; gate independiente del
propietario en PowerShell 32/32 PASS. Revisión técnica final PASS y alcance de la corrección aceptado
por el propietario (2026-10-26). **Estado: SEC-03 corregido y técnicamente validado.** Sin evidencia
de CI para este cambio hasta publicar su commit. Riesgos conocidos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## INVENTARIO V1 — D-1: aislamiento por empresa de motivos de ajuste — CORREGIDO (2026-10-26)

`InventoryAdjustmentReason` admitía `CompanyId` nulo y se filtraba solo por tenant: una empresa podía
listar, editar, desactivar y usar motivos de otra del mismo tenant. Corregido como dato de empresa
reutilizando el filtro global y `CompanyScopeBehavior` existentes; baseline única actualizada
(`company_id` NOT NULL, índice único Tenant + Company + código), sin migración incremental. Focales PASS:
Application 106/106, API HTTP/PostgreSQL 21/21 (incluye `InventoryAdjustmentReasonCompanyIsolationHttpTests`),
OpenAPI 3/3, Arquitectura backend 31/31, Infrastructure 59/59, frontend 10/10 y `architecture:check`.
Revisión técnica final PASS y aceptación del propietario (2026-10-26).
**Estado: D-1 corregido y técnicamente validado.** Las bases de desarrollo ya migradas conservan el
esquema anterior hasta reinstalarse.
[SSOT Inventario](docs/architecture/inventory.md#6-ajustes-de-inventario).

## INVENTARIO V1 — B4: Cuadre monetario Compras/Kardex/Contabilidad — CLOSED (2026-10-26)

12000 u × 0.1234 con 1.25 % de descuento e IVA 0: Compras y el asiento registraban 1462.29 y el Kardex
1462.30 (cantidad × `LandedUnitCost` redondeado); 3 × 100 + flete 10 prorrateado dejaba el Kardex en
309.99 frente a 310.00. Alcance: compras exclusivamente Product inventariable. El importe de
`TotalLineCost` se conserva entre Compras, Kardex y Contabilidad; la anulación revierte el importe
original persistido y un histórico ambiguo se rechaza en la transacción, sin efectos parciales. Sin
migración ni cambios en `PurchaseCostDistribution`. Compras mixtas Product + Service, ICE e IRBPNR
no forman parte de este cierre. Focales B4 en `PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs`:
6/6 PASS en CI #1001 (`infrastructure-01` 208/208 PASS, `application` SUCCESS). Revisión técnica final
PASS (2026-10-26), sin defectos reproducidos.
**Estado: B4 CLOSED; publicado en `8a2c3692d8fb5c870e2284e06832b60dc014cac5`.**
CI: [#999, #1000 y #1001](#inventario-v1--evidencia-ci-de-referencia).
[SSOT Inventario](docs/architecture/inventory.md#4-valoración-promedio-ponderado) · [SSOT Compras](docs/architecture/purchases.md#5-anulación-confirmed--cancelled).

## INVENTARIO V1 — B3: importe monetario canónico del Kardex — CLOSED (2026-10-26)

3 u por 10.00 y tres ventas dejaban cantidad 0 con valor 0.01 (y la compra siguiente lo heredaba);
una transferencia del saldo dejaba 0.01 en origen; A2 y el reproceso de NC usaban importes sin escala.
Corregidos los residuos y unificado el importe operativo; reglas exclusivamente en la SSOT enlazada.
Sin migración ni revalorización histórica. Implementación alineada con la SSOT. Focales PostgreSQL:
`InventoryValuationPostgreSqlTests` 6/6 y reproceso 2/2 PASS. Gates del propietario PASS (Domain 33/33,
Application 1080/1080, Infrastructure 150/150, API 98/98, Arquitectura backend 143/143, Arquitectura
frontend PASS). Revisión técnica final PASS (2026-10-26), sin defectos reproducidos.
**Estado: B3 CLOSED; publicado en `8dd2f91e2fc1de4cc36e5cc227e3f8c4f29f6ac5`.**
CI acumulativo #999 y #1000 SUCCESS ([referencia](#inventario-v1--evidencia-ci-de-referencia)); no sustituye los gates focales.
[SSOT Inventario](docs/architecture/inventory.md#4-valoración-promedio-ponderado).

## INVENTARIO V1 — Etapa 1: anulación con servicios y concurrencia de documentos — BUG REAL (2026-10-25)

B1: anular una compra Product + Service con bodega global fallaba ("El ítem no es un producto…");
corregida la selección de líneas inventariables. B2: ejecutar/anular ajuste y confirmar transferencia
concurrentes duplicaban Kardex y stock (5→10, 10→13, 6→2); corregida la concurrencia documental.
Sin migración. Gate del propietario aprobado: Application 137/137, Infrastructure 98/98,
API 71/71 y Arquitectura backend 143/143 PASS; revisión final del código PASS.
**Estado: B1/B2 CLOSED; publicados en `b16a412a0`, con gate y revisión final PASS registrados.**
CI acumulativo: [CI #999](#inventario-v1--evidencia-ci-de-referencia).
[SSOT Inventario](docs/architecture/inventory.md#6-ajustes-de-inventario) · [SSOT Compras](docs/architecture/purchases.md#5-anulación-confirmed--cancelled).

## INVENTARIO V1 — Kardex y Reporte de Stock UX/UI (2026-10-25)

Publicados en `50a1af3d9`, sobre la base histórica `8a2c3692d`. Kardex y selector: 32/32 PASS
focales registrados; Reporte de Stock: 17/17 PASS. La excepción obsoleta de tamaño de Kardex
se retiró en `6c5c8c111`. Sin cambios funcionales de inventario.
**Estado: PUBLICADO; Kardex y Reporte de Stock aceptados por el propietario (comunicado 2026-10-26).**
La aceptación se apoya en la evidencia ya registrada; no se ejecutó un nuevo smoke autenticado ni
se obtuvieron evidencias nuevas.
Limitaciones de fixtures, fuentes e hidratación en las evidencias de
[Kardex](docs/evidence/kardex-ux-01/README.md), [selector](docs/evidence/kardex-ux-01/selector/README.md)
y [Stock](docs/evidence/stock-report-ux-01/README.md).
CI: [CI #999](#inventario-v1--evidencia-ci-de-referencia). El workflow frontend no ejecuta las
pruebas Vitest focales; CI no sustituye esos resultados ni el smoke autenticado.

## INVENTARIO V1 — Evidencia CI de referencia

Referencia publicada: `6c5c8c1113f802a0f3ab2a9d795c261d0c8c05ab`.
CI #999, segundo intento: **SUCCESS, 19 PASS, 0 FAIL, deploy SKIPPED**, verificado en GitHub
por el propietario y aportado para esta corrección documental (2026-10-26). Es evidencia
acumulativa de la referencia, no una ejecución individual atribuida a cada commit anterior.
No acredita gates del propietario pendientes ni un despliegue.

Referencia publicada: `ba06799ac99b883bcca289d914c69f4cb261f672`.
[CI #1000](https://github.com/szhinin89/erp-saas/actions/runs/40322826093), intento 1:
**SUCCESS, 19 PASS, 0 FAIL, deploy Azure SKIPPED**. Resultado acumulativo del pipeline;
no sustituye los gates focales ni acredita un despliegue.

Referencia publicada: `fa7b2d842b457c7d2fbe2faef8d065cd01ecf4da`.
[CI #1001](https://github.com/szhinin89/erp-saas/actions/runs/40327986902): **SUCCESS**;
`backend / Backend (infrastructure-01)` 208/208 PASS, con las seis pruebas B4 de
`PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs` identificadas individualmente como PASS
en los logs; `backend / Backend (application)` SUCCESS. Acredita solo las pruebas que ejecutó el
pipeline; no acredita un despliegue.

Referencia publicada: `fce60ea1d52614fee5ce0f84c231df2643b18485`.
[CI #1002](https://github.com/szhinin89/erp-saas/actions/runs/40357020744), intento 1: **SUCCESS**.
Resultado acumulativo del pipeline; no acredita un smoke autenticado, una aceptación del
propietario ni un despliegue.

## Estado de entrega

Preproducción. Actualización documental: 2026-10-26. Este archivo conserva resultados
ya registrados; no acredita nuevas ejecuciones ni despliegues. Las aceptaciones del
propietario se registran solo donde se indican expresamente.
Reglas funcionales únicamente en las [SSOT](docs/architecture/README.md).

| Entrega | Evidencia registrada | Estado / límite pendiente |
|---|---|---|
| Compras: distribución canónica de costos A/B | Publicado en `29a69fcaa`; Backend Domain 34, Application 99, API 8, PostgreSQL 20 + 5 y arquitectura PASS; frontend implementado; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. `PurchaseCostDistribution` es la única autoridad de distribución ([SSOT Compras](docs/architecture/purchases.md)) |
| Compras: precisión de prorrateo/base cero | Publicado en `617253d38`; reproducción 6/6 FAIL; focal 9/9 y PostgreSQL 8/8 PASS en [evidencia](docs/evidence/purchases-cost-precision-01/README.md); revisión técnica final PASS; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. El frontend actual no calcula el reparto: usa el resultado del backend. El simulador frontend y su 178/178 pertenecen a una implementación sustituida en `29a69fcaa`; no son pruebas vigentes del cálculo |
| Compras XML: confirmación atómica y Service sin bodega | Publicado en `10afb8425`; PostgreSQL 7/7, Application 66/66, Domain 30/30; guardrails backend PASS; revisión técnica final PASS; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. No resuelve la integración operativa futura de ICE mixto ni IRBPNR |
| Frontend: timeouts de Compras/Contabilidad | Publicado en `746a1f1c6`; focales, TypeScript, ESLint, build y arquitectura PASS; suite frontend 3252/3252 PASS según [evidencia](docs/evidence/frontend-timeouts-01/README.md); el CI frontend no ejecuta Vitest | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. Limitación independiente vigente: en la suite, `ZHOverlayGeometry.test.tsx` falló en `afterAll` y pasó 20/20 aislado; no corregida |
| Frontend: cuatro pruebas desactualizadas de Caja/Contabilidad | 3248/3248 PASS, TypeScript, lint, build y arquitectura; [evidencia](docs/evidence/frontend-preexisting-fails-01/README.md) | Corrección verificada en la entrega previa; sin nueva ejecución |
| Ítems: formularios y catálogos asíncronos | Reproducción 4/4 FAIL; editor 32/32, frontend 313/313, Application 446/446 PASS; [evidencia](docs/evidence/items-forms-alignment-01/README.md) | Guardar/reabrir en ERP real pendiente |
| Compras: conflictos de códigos | Frontend 767/767 y gates técnicos PASS; [evidencia](docs/evidence/purchases-code-conflicts-sync-01/README.md) | Validación previa, sin acreditar otro gate |
| Compras: avisos compactos de duplicados | Frontend 763/763, focal 37/37 PASS; [evidencia](docs/evidence/purchases-compact-code-conflicts-01/README.md) | Validación previa; capturas con límites documentados |
| Ítems: diagnóstico unificado de códigos | Application 423/423, PostgreSQL 22/22, frontend 755 y arquitectura 143/143 PASS; [evidencia](docs/evidence/items-code-conflicts-01/README.md) | Implementado; sin nueva ejecución |
| Compras: Internos provisionales | Application 63/63, frontend 86/86, PostgreSQL 11/11 PASS; [evidencia](docs/evidence/purchases-internal-barcodes-01/README.md) | Implementado; sin decisiones pendientes en esa entrega |
| Compras: desglose XML | Commits 478f5cc0b / 77f80985f; Domain 59, Application 121, frontend 77 y objetos SQL 2/2 PASS | Implementado; sin acreditar despliegue |
| Compras: vinculación estricta por proveedor | dc643c455; PostgreSQL 6/6, Application 100/100, selección manual 6/6 PASS | Implementado |
| Compras: productos pendientes compactos | 77f80985f; frontend 589/589 y PR-7 PASS; [evidencia](docs/evidence/purchases-pending-products-compact-01/README.md) | Implementado; excepción del modal retirada |
| Compras: modal Distribuir | Focal 16/16 y gates PASS; [evidencia](docs/evidence/purchases-distribute-cost-01/README.md) | Implementado; no amplía cálculo |
| Compras: validaciones comerciales | Focal 368 y gates PASS; [evidencia](docs/evidence/purchases-commercial-risk-01/README.md) | Falta decisión sobre precisión del costo mostrado; no se resuelve en esta consolidación |
| Design System: incidencias de línea | [Evidencia](docs/evidence/zh-ds-line-issues-01/README.md) | Capturas con límites de fuentes/iconos; sin nueva aceptación visual |
| Unificación de NC de compra | PostgreSQL 21/21 + contabilización 6/6, API 24/24, Application 79/79, frontend 35/35; build/lint/TypeScript/arquitectura PASS | Publicada en `51678b165`. Guardrail general de aquella entrega limitado por prueba externa de Gastos 516 > 514; no se afirma fallo actual sin repetir gate |
| Anulación completa de compra | Validación focal PostgreSQL PASS | Implementada; calendario oficial y ventana interna Company siguen fuera del comportamiento operativo |

## Instalación inicial y GLOBAL

GREENFIELD-INSTALL: instalación única validada manualmente por el propietario en
`erp_zh_dev` (25/224/1048 INEC 2026 y SRI cargados). Oráculo PostgreSQL desechable:
212 tablas equivalentes salvo metadatos de procedencia/orden de columnas.
Infrastructure focal 256/256 y Architecture 2/2 PASS; suite previa 7371/7372,
snapshot OpenAPI corregido y focal 3/3 PASS. Regresión de arranque INEC 6/6 PASS.
No implica despliegue ni vuelve a autorizar recrear bases.
Instalación y pendientes normativos: [GLOBAL](docs/architecture/architecture.md#global-immutable-schema-global)
y [First Run](docs/architecture/first-run.md).
GLOBAL OFFICIAL-ALIGNMENT: Infrastructure 190/190, API 9/9, frontend 2/2 PASS;
ICE mixto representado en catálogo (65/65 PASS), sin integración de cálculo operativo.
Corrección Tabla 5: PostgreSQL 5/5, HTTP 1/1, frontend 46/46 PASS.

## Background Processing y despliegue

Implementación validada de catálogo/runner/locks/colas/host separado. Resultados de
los grupos originales solapados: 30/30, 19/19, 1/1, 20/20, 39/39 y 12/12 PASS;
no sumarlos como pruebas únicas. Aceptación posterior: API 20/20 e Infrastructure 4/4
con PostgreSQL 16 y SMTP simulado. La consulta de solo lectura del entorno anterior
observó siete recurring jobs, checkpoint durable y cero workers activos; no prueba
entrega SMTP/SRI ni un host local funcionando.
Fase D: Compose 4/4, focal 21/21 y PostgreSQL 4/4, imagen Linux y smoke Compose aislado PASS.
El arranque local sobre datos existentes no quedó acreditado; la aceptación aislada no lo sustituye.
Transición Coolify real pendiente de inspección, respaldo y autorización de operación.
No se instruye aplicar migraciones sustituidas. [Runbook](docs/deployment/README.md).
Collector del worker y ajuste de carga real pendientes de operación; no existe benchmark de producción.

## Contabilidad y Retenciones

Accounting: capacidades implementadas de continuidad 01–06, períodos 07, manuales 08,
reglas/reportes 11, ajustes 12A, caja 12C y cierre anual 12D documentadas en
[Accounting](docs/architecture/accounting.md). Son capacidades implementadas, no nuevos cierres.
Corrección de finalización SRI por job (2026-10-20): PostgreSQL focal 7 PASS registrados;
contabilización pendiente/fallida no equivale a anulación operativa fallida.
Retenciones: emisión, XML/RIDE, transmisión y anulación asistida implementados en el
alcance de [Backend](docs/architecture/backend.md); no quedan como tareas futuras genéricas.

## Cierres acreditados y límites

| Ámbito | Estado previamente registrado |
|---|---|
| A1 Ítems | CLOSED, 43cce57a9 |
| A2 costo negativo | CLOSED, 64e1b21ca |
| B1/B2 Inventario | CLOSED; evidencia en los bloques precedentes |
| First Run | Cerrado; SSOT en first-run.md |
| QA-INVENTORY-INFRA-SUITES-01 | CLOSED, 509ac6d75; PostgreSQL 39/39 PASS |
| Compras/Gastos/CxP/Pagos, cierre 2026-09-12 | LISTO / CERRADO para ese alcance; [matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). No cierra ampliaciones posteriores ni Accounting global |
| Pricing, BP, ElectronicDocuments e infraestructuras FROZEN | Conservan sus cierres y límites; [infraestructuras](docs/architecture/frozen-infrastructure.md) |
| A9 recovery comercial | Implementado y validado: PostgreSQL 19/19, guard 1/1, job 1/1, issuer 31/31, arquitectura 143/143 y gates técnicos PASS. No se acredita un cierre adicional |

El registro anterior de A9 afirmaba A1–A8 CLOSED, pero la cola antigua conservaba
A3 OPEN y A4–A8 sin abrir. No se usan esas entradas contradictorias para abrir trabajo
ni declarar nuevos cierres: falta acreditar documentalmente A3–A8 con el propietario.
AUTH sensitive logging: fix focal 21/21 PASS; sin modificar autenticación ni persistencia.
No usar resultados anteriores como evidencia de que el árbol actual pasó una suite.

## Riesgos y limitaciones vigentes

Registrados sin autorización de implementación; no constituyen trabajo abierto.

- **Aprobación documental:** elegir `SingleStep`/`MultiStep` en Documentos y flujos bloquea
  la confirmación de Gastos, porque no existe flujo de aprobación. Contrato:
  [security.md](docs/architecture/security.md#permisos-vs-política-de-flujo-documental-document-flow-policy-01).
  La aprobación es intención futura en [ROADMAP](docs/ROADMAP.md#etapa-1--completar-erp-core).
- **Retenciones electrónicas sin respuesta concluyente:** los comprobantes en `Signed`
  histórico, `Dispatching` o `Received` quedan en "Requiere conciliación" sin procedimiento
  del ERP para resolverlos. Pendientes sin decisión: procedimiento de conciliación
  (antes P-2/O-10), tratamiento de comprobantes de retención anteriores al ciclo actual cuyo
  origen ya fue anulado (antes O-1/O-9) y extensión de la regla de no reenvío a Ventas/NC
  (antes O-15). Contrato: [backend.md](docs/architecture/backend.md#límites-y-finalización).
- **Logs de autenticación anteriores a la corrección de AUTH sensitive logging:** pueden
  contener el campo `successorHash` del evento de rotación de refresh token. No se acredita
  exposición ni incidente; su revisión o depuración depende de la operación de cada entorno.
- **Aislamiento entre empresas — límites conocidos tras SEC-03** (contrato:
  [security.md](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant)):
  `UpdateCustomerRoleConfigValidator` (request TENANT) comprueba códigos de catálogo contra la
  empresa del contexto; `CreateCompanyUserPreferences` es un consumidor interno sin marcador de
  empresa; la resolución de permisos usa la única membership del usuario cuando la empresa del
  header no tiene membership, por lo que una operación COMPANY sin marcador ni guard no quedaría
  protegida; `INV_E2` no cubre todos los módulos ni detecta un marcador TENANT puesto por error.
  Impacto acreditado: como máximo, el validador revela si un código existe en el catálogo de otra
  empresa; no se acredita lectura ni escritura de sus datos.

## Consolidación documental

2026-10-26: edición autorizada de documentos e índice ADR, sin lógica, pruebas funcionales,
base de datos, migraciones, capturas o logs. Publicada en `ba06799ac99b883bcca289d914c69f4cb261f672`;
[CI #1000](#inventario-v1--evidencia-ci-de-referencia) SUCCESS.
Trabajo abierto: [WORK_QUEUE](docs/agent/WORK_QUEUE.md). Futuro: [ROADMAP](docs/ROADMAP.md).
