# Project Status

## Cuentas por cobrar — E2E de factura con varias cuotas — CLOSED (2026-10-27)

Resultado E2E **58/58 PASS** y aislamiento **7/7 PASS**, aprobado expresamente por el propietario;
CLOSED solo para el alcance descrito a continuación.
Sin BUG REAL ni cambios de código de producción; se agregó un test focal de mora con varias cuotas.
Evidencia: [receivables-e2e-02](docs/evidence/receivables-e2e-02/README.md).
Alcance: factura a crédito por 300.00 en 3 cuotas con vencimientos distintos; cobro de 30.00 a la
cuota #2 (tras rechazar un monto mayor al saldo de la cuota), 120.00 sin cuota aplicado por FIFO y
liquidación de 150.00 sin cuota, con recarga y reapertura tras cada paso, «Vence» siguiendo a la
primera cuota no saldada, asientos de venta y cobro balanceados y aislamiento entre empresas. La
mora no se reprodujo en E2E (las fechas de los datos no lo permiten) y solo se acredita por test
focal. No acredita cobro a caja o sin destino, reversa de cobros, devolución con CxC cobrada, saldo
inicial IL-5, suite completa ni despliegue. Sin commit ni push.

## Compras/Gastos → CxP — E2E de reversa de pagos y anulación — CLOSED (2026-10-27)

Resultado E2E **46/46 PASS** (17 comprobaciones en navegador, 20 por API y 9 en PostgreSQL),
aprobado expresamente por el propietario. No se encontró BUG REAL ni hubo cambios de código. Evidencia:
[payables-reversal-cancel-e2e-01](docs/evidence/payables-reversal-cancel-e2e-01/README.md).
Se probaron una compra (115.00) y un gasto (80.00) a crédito, cada uno con un pago parcial por
transferencia. Con pagos activos, la anulación se bloqueó y no hubo cambios en la base. Luego se
reversaron los pagos desde «Reversar pago» del detalle del pago: las CxP recuperaron su saldo y se
generaron asientos inversos. Después se anularon compra y gasto: CxP y cuotas anuladas, kardex
revertido y asientos originales reversados. También se comprobaron las restricciones posteriores
(doble reversa, doble anulación y pago sobre una CxP anulada) y el aislamiento entre empresas. No
acredita pagos en efectivo o cheque, varias cuotas, retenciones, saldo a favor aplicado, suite
completa ni despliegue. Quedan dos DEBT no bloqueantes, documentados en la evidencia: «Reversado
por» muestra el UUID del usuario, y el bloqueo del gasto devuelve un código y un mensaje distintos
de los de la compra. No abren trabajos ni reabren módulos CLOSED. Sin commit ni push.

## Compras → CxP → Pago — E2E de compra a crédito — CLOSED (2026-10-27)

Resultado E2E **40/40 PASS** (flujo, asientos y aislamiento), aprobado expresamente por el
propietario junto con las dos correcciones, tras corregir dos **BUG REAL** de
UI en Compras: la cuota regenerada mostraba y re-confirmaba un monto obsoleto (0.00), lo que dejaba
«Las cuotas no cuadran» y bloqueaba «Confirmar compra»; y la compra confirmada sin retención mostraba
la clave i18n cruda `purchases.retention.noneIssued`. Corrección focal en `PaymentScheduleSection`
(`PurchasesPage.tsx`) y en `es.json`/`en.json`, con tests focales. Evidencia:
[purchases-payables-e2e-01](docs/evidence/purchases-payables-e2e-01/README.md).
Alcance: compra a crédito de una cuota por 115.00, pago parcial 50.00 y del saldo 65.00 por
transferencia, reapertura de compra, CxP y pagos, asientos de compra y pago balanceados y
aislamiento entre empresas. No acredita compras de varias cuotas, retención, reversa de pagos,
bloqueo de anulación, suite completa ni despliegue. Los DEBT observados en la evidencia no abren
trabajos ni reabren módulos CLOSED. Sin commit ni push.

## Cuentas por cobrar — E2E de cobros — CLOSED (2026-10-27)

Resultado E2E **21/21 PASS** y aislamiento **7/7 PASS**, aprobado expresamente por el
propietario, tras corregir un **BUG REAL**: el cobro no se acumulaba en la cuota de la CxC (cuota
`pending` con saldo cobrado). Corrección focal en `SalesReceivable`, `SalesReceivableInstallment`
y `RegisterCollectionCommandHandler`, con tests focales. Comportamiento: [SSOT de
CxC](docs/architecture/receivables.md); cobertura: [matriz QA](docs/QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md);
evidencia: [receivables-e2e-01](docs/evidence/receivables-e2e-01/README.md).
Alcance acreditado: factura a crédito de una cuota por 100.00, cobro parcial 40.00 y del saldo
60.00 con destino banco, reapertura de factura y CxC, asientos de venta y cobro balanceados y
aislamiento entre empresas. No acredita facturas de varias cuotas, cobro a caja o sin destino,
reversa de cobros (sin endpoint ni UI), devolución con CxC cobrada, saldo inicial IL-5, listado de
cobros (no existe), suite completa ni despliegue. No se reparan cuotas cobradas antes de la
corrección. Sin commit ni push.

## Pagos a proveedores — cierre E2E de gastos — CLOSED (2026-10-27)

Resultado E2E **17/17 PASS**, aprobado expresamente por el propietario. Clasificación:
**KEEP**, sin BUG REAL. Evidencia existente incorporada en
[supplier-payments-e2e-01](docs/evidence/supplier-payments-e2e-01/README.md)
([resultados](docs/evidence/supplier-payments-e2e-01/e2e-results.json) y seis capturas).
Alcance: gasto confirmado por 100.00, pago parcial de 40.00, reapertura del pago y de la CxP
con saldo 60.00, pago total del saldo y cartera sin pendientes. En la
[matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md) se acredita únicamente
el pago parcial/total de gastos.
No acredita pagos de compras, reversas, bloqueo de anulación, aislamiento entre empresas,
suite completa ni despliegue. Se conservan las observaciones y límites de la evidencia,
sin abrir pendientes ni reabrir módulos CLOSED. Cierre documental: sin cambios de código,
sin repetir pruebas y sin tocar el archivo RED histórico. Staging autorizado expresamente
para esta entrega; sin commit ni push.

## GOV-002 — CLOSED

Cierre documental del seguimiento de gobernanza aprobado expresamente por el propietario
(2026-10-27), sobre el contrato central implementado y las validaciones previamente acreditadas.
No certifica cumplimiento exhaustivo de todos los módulos ni acredita nuevas pruebas o despliegues.
Las dos deudas conocidas (códigos de módulo sin catalogar y formateador frontend alternativo)
se conservan como deuda no bloqueante en [error-handling.md](docs/architecture/error-handling.md#deudas-conocidas-y-límite-del-cierre).
No autoriza reauditar ni refactorizar módulos CLOSED. Esta fase solo modifica documentación;
staging autorizado por el propietario, commit y push no realizados.

## SEGURIDAD — SEC-06: revocación de permisos con caché — CLOSED — revisión técnica final PASS (2026-10-27)

La caché de permisos efectivos (TTL 300 s) podía seguir autorizando permisos ya revocados: la
entrada se publicaba con la versión vigente al escribir (una carga anterior a la revocación
quedaba válida), el cambio de perfil solo borraba la entrada (una carga concurrente del perfil
anterior la recreaba), el bump de versión era GET+1+SET (incrementos perdidos), los fallos de
invalidación se ignoraban y `UpsertCompanyUserMembership` podía guardar rol/perfil y salir sin
invalidar. Corregido en el mecanismo existente, sin cachés ni autorizadores nuevos: sello de
versiones (tenant, empresa, usuario) observado antes de cargar la BD, perfil dentro de la entrada,
versiones y marcas atómicas (Lua sobre el `IConnectionMultiplexer` del host; contadores del
proceso sin Redis), protocolo de invalidación en dos fases fail-closed en todos los handlers que
cambian permisos (503 `PERMISSIONS_INVALIDATION_UNAVAILABLE` sin persistir / 503
`PERMISSIONS_INVALIDATION_INCOMPLETE` con el alcance protegido), membership validada antes de
persistir y guardada en transacción, y regla de despliegue verificable `Redis:Required=true` para
más de una instancia de API. TTL sin cambios; sin cambios de base de datos ni migraciones.
Ajustes finales (opción A aprobada): una restauración o failover de Redis podía revivir versiones y
entradas anteriores a una revocación; ahora el Redis de autorización debe ser `noeviction`, sin
RDB/AOF y primario sin réplicas, y API/worker no arrancan si no cumple o no es verificable
(`CONFIG GET` + `INFO`); el Redis local de compose quedó declarado así (el contenedor existente
conserva la configuración anterior hasta recrearlo). Frontend: los dos 503 se presentan en las
pantallas IAM (perfiles, permisos, usuarios, configuración de usuario) sin éxito falso ni reintento
automático; el estado incierto recarga desde el servidor y deja la advertencia. Focales de los
ajustes PASS: Infrastructure 39/39 (worktree aislado; incluye Redis conforme/incompatible/sin
CONFIG/inaccesible, presión de memoria con `noeviction` y reinicio), API 21/21 (registro del
validador, contrato DevCache con Redis conforme e inaccesible), Vitest access 75/75, lint (0
errores), `tsc -b`, build, Arquitectura 143/143 y `architecture:check` PASS. Focales previos PASS: Application 214/214 (Access) + 161/161 (sesión, alta de usuario, scope),
Infrastructure (worktree aislado, sin el archivo RED) `PermissionsCacheRevocationTests` +
`PermissionsCacheRevocationRedisTests` 21/21 con Redis efímero, API 41/41 (regla de despliegue,
host, `CompanyAccessProfilesIsolationHttpTests`, `CrossCompanyAdminPrivilegeHttpTests`, contrato de
errores, sesión), Arquitectura 143/143, `architecture:check` PASS, tamaño de handlers PASS.
Gate independiente del propietario (PowerShell): Infrastructure `Security.PermissionsCache` 39/39
(worktree temporal sin el archivo RED), API Redis/hosts 14/14, Frontend Access 75/75, Architecture
frontend 140/140, `git diff --check` PASS. Revisión técnica final PASS. **SEC-06 CLOSED: commit `bbaa708cd`, CI #1011 SUCCESS.**
Sin despliegue: el Redis de producción (Coolify) no fue verificado contra el contrato. Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-05: perfiles de acceso por empresa — CORREGIDO — revisión técnica final PASS (2026-10-26)

Los perfiles de acceso eran TENANT: un Admin de A podía consultar, editar, desactivar o cambiar
permisos de un perfil usado (o usado solo) por B, con efecto inmediato en B; un usuario Admin en A
y User en B podía autoescalarse en B editando el perfil de su membership de B; y un no Admin con
`access.company_user_memberships.view` podía asignarse rol Admin o un perfil más amplio que el suyo.
Decisión del propietario: perfiles independientes por `TenantId + CompanyId`. Corregido en el
mecanismo existente: `AccessProfile` es `ICompanyOperationalEntity` (unicidad por empresa, FK
compuesta membership → perfil de la misma empresa, esquema inicial y snapshot adaptados, sin
migración nueva ni cambios en bases existentes); los casos de uso de perfiles exigen empresa activa
(`IRequiresCompanyContext`) y resuelven el perfil por tenant + empresa; un perfil inactivo no
concede permisos; `UpsertCompanyUserMembership` y la revocación aplican anti-escalamiento con el
mismo bypass de SEC-04; el perfil por defecto "DataEntry" se siembra por empresa. Sin cambios de
contrato en frontend. Focales PASS: Application 213/213 (Access, sesión, `CompanyScopeBehavior`),
Arquitectura 143/143, API HTTP/PostgreSQL `CompanyAccessProfilesIsolationHttpTests` 12/12 +
`CrossCompanyAdminPrivilegeHttpTests` 8/8 y clases con fixtures de perfiles 103/103 + unitarias/
snapshot API 102/102, `architecture:check` PASS, `git diff --check` PASS. Infrastructure.Tests no
compilado en este árbol (archivo RED histórico). Gate independiente del propietario (PowerShell):
Application 22/22, API HTTP 20/20, compilación de ERP.API y coherencia EF/baseline PASS,
`git diff --check` PASS. Revisión técnica final PASS: aislamiento empresarial de perfiles aceptado
técnicamente. **CI del commit SEC-05: aún no ejecutado.** La revocación de permisos frente a la caché se
corrige en SEC-06. Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-04: privilegios administrativos entre empresas — CORREGIDO — revisión técnica final PASS (2026-10-26)

`RuntimePermissionAuthorizer` concedía el bypass Admin (`*`) por el rol del JWT, emitido para la
empresa de apertura de sesión: un usuario Admin en A y User limitado en B podía, con el JWT de A y
`X-Company-Id: B`, ejecutar en B cualquier operación `perm:*` (y `GetMyPermissions` devolvía `*`).
Además, `CompanyContextProvider` sustituía en silencio un `X-Company-Id` sin membership por la única
membership del usuario. Corregido en el mecanismo existente: el bypass sale de la membership de la
empresa operativa u operador global (`IOperatorCompanyAccessPolicy`), alineando `GetMyPermissions`,
sesión, navegación y anti-escalamiento de `UpsertProfilePermissions`; un header sin membership activa
del tenant se rechaza con 403 `COMPANY_SCOPE_FORBIDDEN`, sin fallback. Sin cambios en tokens, first-run
ni base de datos. Focales PASS: Application 97/97 (incluye `CompanyScopedAdminAuthorizationTests`),
API HTTP/PostgreSQL 290/290 en las clases con host real (incluye `CrossCompanyAdminPrivilegeHttpTests`
8/8; fixtures de Caja, Ventas y BusinessPartner alineados: su JWT usaba un `sub` sin membership).
Regla funcional aprobada por el propietario: únicamente el Administrador Global de instalación
(modo operador) atraviesa tenants y empresas; los Administradores de Empresa quedan limitados a las
empresas donde su membership tiene rol Admin. Gate independiente del propietario (PowerShell):
Application 37/37, API HTTP 15/15, `architecture:check` 140/140, `git diff --check` PASS; revisión
técnica final PASS sin defectos bloqueantes. **CI del commit SEC-04: aún no ejecutado.** Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

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
| Ítems: E2E visual Product/Service | Chromium 48/48 PASS sobre `erp_zh_test`; 2 BUG REAL corregidos (precio base perdido al reabrir; 13 claves `items.validation.*` sin traducir); `PricingTab` 17/17, lint, `tsc -b`, build y arquitectura PASS; [evidencia](docs/evidence/items-e2e-visual-01/README.md) | Implementado y verificado 2026-10-27; sin commit; suite completa no ejecutada |
| Ítems: formularios y catálogos asíncronos | Reproducción 4/4 FAIL; editor 32/32, frontend 313/313, Application 446/446 PASS; [evidencia](docs/evidence/items-forms-alignment-01/README.md) | Guardar/reabrir en ERP real verificado en [E2E visual](docs/evidence/items-e2e-visual-01/README.md) |
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
| Unificación de NC de compra | PostgreSQL 21/21 con SRI simulado + contabilización 6/6, API 24/24, Application 79/79, frontend 35/35; build/lint/TypeScript/arquitectura PASS | **CLOSED (2026-10-27)**; aceptación funcional expresa del propietario para el alcance implementado de [Compras §§6–7](docs/architecture/purchases.md#6-nota-de-crédito-del-proveedor--entrada-única), publicado en `51678b165`. Recuento frontend conciliado en [evidencia SSOT](docs/architecture/purchases.md#9-evidencia). El límite histórico externo de Gastos (516 > 514) ya no se observa: conteo puntual actual 514, techo 514; no acredita un nuevo gate completo. Sin nuevas pruebas, validación SRI real ni despliegue. |
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

## A3–A8 — CLOSED (2026-10-27)

Cierre documental y funcional aprobado expresamente por el propietario, con gate
independiente A5 **10/10 PASS en PostgreSQL real** comunicado en esta fase. Se conservan
los PASS ya acreditados de los demás casos, sin repetir pruebas.

| Caso | Evidencia focal acreditada | Estado |
|---|---|---|
| A3 — actualización atómica del borrador de venta | Application 24/24 + PostgreSQL 1/1 PASS | CLOSED |
| A4 — ICE/IRBPNR en devoluciones de venta | Application 7/7 PASS | CLOSED |
| A5 — búsqueda y selección por barcode de presentación | RED 2/2 FAIL esperados → GREEN PostgreSQL 10/10 PASS; gate independiente del propietario 10/10 PASS | CLOSED |
| A6 — guardas de bodega y carreras UI | Application 10/10 + PostgreSQL 6/6 PASS; carreras frontend PASS en el grupo indicado debajo | CLOSED |
| A7 — concurrencia de autorización de ventas | PostgreSQL 6/6 PASS | CLOSED |
| A8 — retry/idempotencia de autorización | PostgreSQL 13/13 + HTTP/PostgreSQL 3/3 PASS | CLOSED |

Frontend focal agrupado previo: 113/113 PASS (incluye autoselección de presentación y
carreras de búsqueda/bodega); no sumarlo como ejecuciones independientes por caso.
A5 conserva el fix de una línea y sus cuatro casos de regresión en
`InvoiceItemSearchRepositoryTests`; evidencias [RED](docs/evidence/a5-barcode-search-01/red.trx)
y [GREEN](docs/evidence/a5-barcode-search-01/green.trx). Contrato funcional únicamente en
[Backend](docs/architecture/backend.md#búsqueda-por-barcode-de-presentación-a5).
Validación del agente en copia temporal sin leer/copiar/compilar el archivo RED histórico;
revisión focal del diff y `git diff --check` PASS. Esta consolidación no ejecuta pruebas,
no acredita despliegue y no modifica otros cierres. Commit y push no realizados;
el propietario conserva su ejecución. CLOSED: no reauditar salvo BUG REAL relacionado.

## Cierres acreditados y límites

| Ámbito | Estado previamente registrado |
|---|---|
| A1 Ítems | CLOSED, 43cce57a9 |
| A2 costo negativo | CLOSED, 64e1b21ca |
| B1/B2 Inventario | CLOSED; evidencia en los bloques precedentes |
| First Run | Cerrado; SSOT en first-run.md |
| QA-INVENTORY-INFRA-SUITES-01 | CLOSED, 509ac6d75; PostgreSQL 39/39 PASS |
| Compras/Gastos/CxP/Pagos, cierre 2026-09-12 | LISTO / CERRADO para ese alcance; [matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). No cierra ampliaciones posteriores ni Accounting global |
| CxC y cobros, cierre 2026-10-27 | **CLOSED** para el alcance de la sección precedente; [SSOT](docs/architecture/receivables.md) y [matriz QA](docs/QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md) |
| Pricing, BP, ElectronicDocuments e infraestructuras FROZEN | Conservan sus cierres y límites; [infraestructuras](docs/architecture/frozen-infrastructure.md) |
| A9 recovery comercial | **CLOSED (2026-10-27)**; cierre funcional aprobado expresamente por el propietario, exclusivamente para el alcance create-only de [Sales recovery](docs/architecture/backend.md#lotes-fairness-y-ownership). Evidencia PASS existente: PostgreSQL 19/19, guard 1/1, job 1/1, issuer 31/31, arquitectura 143/143 y gates técnicos. No incluye envío, reenvío ni nuevas funcionalidades; no acredita nuevas ejecuciones ni despliegue. |

A3–A8 quedan acreditados por el cierre aprobado en su sección precedente; la discrepancia
documental anterior queda resuelta, sin ampliar el alcance de A9.
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
  empresa; `INV_E2` no cubre todos los módulos ni detecta un marcador TENANT puesto por error.
  Tras SEC-04 la resolución de permisos ya no usa la única membership cuando
  el header no tiene membership; la firma `IRuntimePermissionAuthorizer.IsAuthorizedAsync`
  conserva el parámetro `role`, sin efecto. Los perfiles de acceso son por empresa desde SEC-05.
  Impacto acreditado: como máximo, el validador revela si un código existe en el catálogo de otra
  empresa; no se acredita lectura ni escritura de sus datos.
- **Seguridad — revocación de permisos con caché (SEC-06):** garantías y
  contrato en [security.md](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).
  Límites: (1) cumplir "Redis obligatorio con varias instancias" depende de que el despliegue declare
  `Redis:Required=true`; el repositorio no conoce la cantidad real de instancias. (2) Si Redis
  desaloja una marca de invalidación en curso (política de memoria `volatile-*`/`allkeys-*`) mientras
  Complete falló, la protección de ese alcance termina antes de los 900 s — excluido por el
  contrato `noeviction` verificado al arrancar. (3) En el alta de usuario
  (transacción ambiente) la invalidación se completa antes del commit externo; el usuario es nuevo y
  no tiene entradas previas. (4) La caché de navegación se invalida solo en el proceso local (no
  autoriza). (5) Escrituras de caché con más de 60 s entre la observación del sello y la escritura se
  descartan (más lecturas a BD, nunca más permisos). (6) Garantías solo operativas: no cambiar la
  configuración de Redis en caliente, no restaurar respaldos ni promover réplicas, monitoreo de
  memoria y alertas de disponibilidad; con `noeviction` y memoria llena los cambios de permisos
  responden 503 sin guardar. (7) Un Redis gestionado que bloquee `CONFIG` impide el arranque hasta
  aprobar una verificación equivalente. (8) El Redis de producción (Coolify) no está inspeccionado:
  si no cumple el contrato, la API no arrancará tras desplegar este cambio.

## Consolidación documental

2026-10-26: edición autorizada de documentos e índice ADR, sin lógica, pruebas funcionales,
base de datos, migraciones, capturas o logs. Publicada en `ba06799ac99b883bcca289d914c69f4cb261f672`;
[CI #1000](#inventario-v1--evidencia-ci-de-referencia) SUCCESS.
Trabajo abierto: [WORK_QUEUE](docs/agent/WORK_QUEUE.md). Futuro: [ROADMAP](docs/ROADMAP.md).
