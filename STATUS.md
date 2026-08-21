# Project Status

**Single source of truth** for delivery state. Updated: **2026-08-21** · Kernel refactor: **2026-06-05**.

---

## ERP-CORE-CLOSEOUT-10-FINALIZE — Cierre final sin pendientes técnicos accionables (2026-08-21)

### Veredicto final

**ERP Core queda listo para piloto técnico.** Pendientes técnicos accionables en el repositorio: **ninguno**. Bloques cerrados: **01 a 10**. Solo quedan pendientes externos inevitables, listados abajo con procedimiento ya preparado para cuando estén disponibles.

- **Tirilla (Print Agent)**: preparada — cola persistente, reintentos, `Driver: "windows-raw"`, instalación como servicio Windows, 21 tests verdes. Pendiente prueba física real por falta de impresora térmica disponible.
- **Correo (SMTP)**: preparado — outbox desacoplado (nunca bloquea una venta), resolución en dos capas (`OrgSettings` → fallback `Communications:Email:*`/env vars) ya implementada y probada. Pendiente credencial SMTP real (Zoho u otro) para el smoke de envío end-to-end.
- **SRI proveedor de sistema (XML)**: configuración dinámica lista (`SystemProviderSettings`, singleton de instancia). Pendiente el texto de la Resolución NAC-DGERCGC26-00000027 o ficha técnica SRI que confirme el campo/elemento exacto antes de tocar los XML builders — normativa, no técnica.

### Corrección a un hallazgo del cierre anterior (ERP-CORE-CLOSEOUT-10)

El cierre anterior afirmó que el backup de PostgreSQL/FileStorage era "procedimiento manual, no programado" — **eso era impreciso**: `scripts/backup-localprod.ps1` (dump + FileStorage + checksums + manifest) y `scripts/restore-check-localprod.ps1` (drill de restore completo en un entorno descartable, sin tocar el stack real) ya existían y están documentados en detalle en `docs/BACKUP_RESTORE_LOCALPROD.md` — no se habían revisado en el cierre anterior. Lo único que realmente falta es agendar la ejecución periódica (no existe cron/scheduler todavía) — eso sí queda clasificado como externo/operativo, no como código faltante.

### Qué se revisó y su clasificación

| Punto | Clasificación | Detalle |
|---|---|---|
| Health checks API/Postgres/Redis, `depends_on: service_healthy` | **Cerrado** | Corregido en el cierre anterior (`docker-compose.localprod.yml`), reverificado con `docker compose config`. |
| Volúmenes persistentes: Postgres, FileStorage, logs API | **Cerrado** | `erp_saas_pgdata`, `erp-api-files`, `erp-api-logs` (este último agregado en el cierre anterior) — documentados en `docs/DOCKER_LOCAL_PROD.md`. |
| Secretos en `docker-compose*.yml`/`.env*.example` | **Cerrado** | Sin secretos reales; `POSTGRES_PASSWORD`/`JWT_SECRET_KEY` sin default en `compose.base.yml` (falla si no se exportan) — ningún compose de prod puede heredar un password débil. |
| Migraciones EF aplicables desde cero | **Cerrado** | Re-verificado en este cierre: 27 migraciones aplicadas sin error contra un Postgres 16 real (contenedor temporal, no Testcontainers). `has-pending-model-changes` → sin cambios. Comando documentado en `docs/DOCKER_LOCAL_PROD.md` §5 y `docs/DEVELOPMENT.md`. |
| Backup/restore/rollback PostgreSQL + FileStorage | **Cerrado** | Scripts ya existentes (`backup-localprod.ps1`, `restore-check-localprod.ps1`) + `docs/DOCKER_LOCAL_PROD.md` § Rollback de la aplicación (nuevo en este cierre: rollback de contenedores por commit + advertencia sobre downgrade de esquema). `docs/deployment/README.md` corregido para referenciar los scripts reales en vez de comandos genéricos inventados. |
| Backend config (appsettings, guardas de arranque, CORS, Swagger, Hangfire, JWT) | **Cerrado** | Reverificado directamente en `Program.cs`: guard fail-fast en Production para `Jwt:SecretKey`/`ConnectionStrings:DefaultConnection`/`Cors:AllowedOrigins` con placeholder o vacíos; CORS sin `AllowAnyOrigin`; Swagger solo Development/Testing; Hangfire deshabilitado por defecto sin bloquear ventas ni Communications. |
| Frontend config (`.env` examples, `VITE_API_URL`, `VITE_PRINT_AGENT_*`) | **Cerrado** | `VITE_API_URL` vacío = proxy relativo `/api` (funciona en dev y en Docker vía nginx); las 4 variables `VITE_PRINT_AGENT_*` documentadas en `.env.development.example`, comentadas, sin clave real; Vite no embebe ningún secreto por defecto en el bundle. |
| SMTP documentado (OrgSettings + fallback env vars) | **Cerrado** | Nueva sección en `docs/deployment/README.md`: tabla de variables `Communications__Email__*` (ejemplo Zoho), confirmación de que no bloquea ventas, y nota honesta de que el endpoint de administración de `communications.email.*` vía OrgSettings **no existe todavía** (se usa el fallback por variables de entorno para el piloto) — no se inventó ni se implementó esa pantalla en este cierre (sería alcance funcional nuevo). |
| Print Agent — versionado, README, prueba física | **Cerrado** | `print-agent/` ya está versionado (43 archivos trackeados, no `?? print-agent/`) — no hacía falta un commit separado. Build (`ZH.PrintAgent.sln`) y tests (21/21) verdes. README ya cubría instalación/ApiKey/DataDirectory/`windows-raw`/nombre de cola; se agregó la advertencia explícita de prueba física pendiente. |
| SRI/certificado — documentación de dependencia física vs. electrónica | **Cerrado** | Nueva sección en `docs/deployment/README.md`: factura física sin dependencia del certificado (garantía estructural), venta electrónica con error claro (nunca 500) si falta certificado/settings, endpoint de readiness, y el punto normativo pendiente del proveedor de sistema. |
| `Deployment:SuperAdminPanelEnabled` | **No aplicable por arquitectura** | Esa clave de configuración no existe en `backend/src` — ERP Core no tiene panel SuperAdmin/Platform por diseño (`ERP_CORE_FREEZE.md`). Discrepancia de alcance de la tarea, no un defecto de este repo. |
| Dominio `.com.ec` + SSL real | **Externo inevitable** | Requiere dominio registrado y decisión de proveedor de certificado — documentado en `docs/deployment/README.md`, sin inventar configuración TLS sin el dominio real. |
| Credenciales SMTP reales (Zoho) | **Externo inevitable** | El código/config ya está listo (ver tabla de variables); falta la cuenta real. |
| Impresora térmica física | **Externo inevitable** | El agente y su README ya están listos; falta el hardware para la prueba end-to-end. |
| Certificado `.p12` SRI real por empresa piloto | **Externo inevitable** | El flujo de subida/validación ya está implementado (ERP-CORE-CLOSEOUT-06/07); falta el certificado real de la empresa piloto. |
| Texto/ficha técnica de la Resolución NAC-DGERCGC26-00000027 | **Externo inevitable** | Ver ERP-CORE-CLOSEOUT-09 — no se puede confirmar la estructura XML sin la fuente normativa oficial; no se inventó. |
| Backups productivos con periodicidad automatizada | **Externo inevitable (operativo)** | Los scripts de backup/restore ya funcionan (ver arriba); falta solo agendarlos (cron/Task Scheduler) en el entorno real del piloto — decisión operativa, no de código. |

### Validado en este cierre

- `dotnet build backend/src/ERP.slnx --no-restore` → 0 errores.
- `npm run build` (frontend) → build correcto.
- Migraciones EF aplicadas **desde cero** contra Postgres 16 real (segunda verificación independiente, contenedor temporal nuevo) → sin errores. `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.
- `dotnet build print-agent/ZH.PrintAgent.sln --no-restore` → 0 errores. `dotnet test print-agent/ZH.PrintAgent.sln --no-build` → 21/21 verdes.
- `git diff --check` limpio. `git status` revisado antes de cualquier commit propuesto — sin `bin/`, `obj/`, `data/`, `TestResults/` en los cambios.
- Efecto colateral detectado y revertido dos veces en este cierre: `npm run build`/`dotnet build` regeneran automáticamente `docs/ci/PLATFORM_GUARD_REPORT.md` y `docs/future-platform/API_USAGE_GRAPH.json` con timestamp nuevo (contenido idéntico, `PASS`/0 violaciones) — revertidos por no ser cambios semánticos reales.

### Archivos modificados en este cierre (pendientes de commit — no se commiteó nada todavía)

`STATUS.md`, `docker-compose.localprod.yml` (ya modificado en el cierre anterior, sin cambios adicionales en este), `docs/DOCKER_LOCAL_PROD.md`, `docs/deployment/README.md`, `print-agent/README.md`. Ninguno mezcla código de backend/frontend con print-agent en el mismo cambio — todo es documentación/configuración Docker.

---

## ERP-CORE-CLOSEOUT-10 — Preparación despliegue piloto (2026-08-21)

**Estado: COMPLETADO.** Auditoría de entorno/Docker/variables/migraciones/seeds/health/logs/seguridad mínima para el piloto. Se validó de punta a punta (build backend/frontend, migraciones EF aplicadas desde cero contra Postgres real, `docker compose config`) y se corrigieron **2 gaps reales de infraestructura**; el resto del entorno ya estaba listo.

**Corregido**:
- `docker-compose.localprod.yml`: `erp-frontend` dependía de `erp-api` con `condition: service_started` (arranque del contenedor), no `service_healthy` — nginx podía empezar a proxyear `/api/*` mientras la API todavía aplicaba migraciones/bootstrap. Corregido a `service_healthy`.
- `docker-compose.localprod.yml`: los logs de Serilog (`logs/erp-.txt`, resuelve a `/app/logs` por el `WORKDIR` del Dockerfile) no tenían volumen — se perdían en cada recreación del contenedor. Se agregó el volumen nombrado `erp-api-logs`.
- `docs/deployment/README.md` (antes un placeholder de una línea): se agregaron procedimientos concretos de **backup** (`pg_dump`/`pg_restore` contra el contenedor `postgreszh`, con nota explícita de que hoy es manual, no programado), **rollback** (rebuild desde commit anterior — no hay registry de imágenes versionado todavía; downgrade de EF requiere revisar el `Down()` de la migración) y **dominio/SSL** (documentado honestamente como pendiente externo no resuelto, sin inventar una configuración TLS sin dominio real).

**Validado end-to-end (no solo revisado)**:
- `dotnet build backend/src/ERP.slnx --no-restore` → 0 errores.
- `npm run build` (frontend) → build correcto (solo warnings preexistentes de tamaño de chunk).
- Migraciones EF aplicadas **desde cero** contra un Postgres 16 real (contenedor temporal, no Testcontainers) — las 27 migraciones corrieron sin error hasta `AddSystemProviderSettings`. `dotnet ef migrations has-pending-model-changes` → sin cambios pendientes.
- `docker compose -f docker-compose.yml -f docker-compose.localprod.yml config` → renderiza correctamente (validación estática, sin levantar contenedores).
- `git diff --check` limpio.

**Confirmado sin defectos** (auditado, no corregido): guard de arranque que falla rápido en Production si `Jwt:SecretKey`/`ConnectionStrings:DefaultConnection`/`Cors:AllowedOrigins` quedan con el placeholder o vacíos (`Program.cs`) — ningún secreto real en el repo, solo placeholders `CHANGE_ME_*`. CORS sin `AllowAnyOrigin`, con fallback a `localhost` inalcanzable en Production por el guard anterior. Swagger habilitado solo en Development/Testing. Hangfire deshabilitado por defecto (`Hangfire:Enabled=false`) sin romper el arranque ni las colas de Communications — los jobs simplemente no se programan; una venta/factura nunca depende de que Hangfire esté activo. Migraciones y bootstrap global se auto-aplican en cada arranque de la API (`db.Database.MigrateAsync()` + `GlobalBootstrapOrchestrator`); una empresa piloto llega a estado operativo solo con `POST /api/v1/setup/admin` (sin intervención manual en BD, coherente con ERP-CORE-CLOSEOUT-06). Volumen `erp-api-files` ya persistía certificados P12/XML/RIDE correctamente. Dockerfile backend ya en Alpine/musl con el fix de SkiaSharp Linux confirmado (ERP-CORE-CLOSEOUT-07); Dockerfile frontend sirve build estático vía nginx, no dev server. Variables `VITE_*` no embeben ningún secreto por defecto en el bundle. `.env.docker.local.example`/`compose.base.yml` fuerzan `POSTGRES_PASSWORD`/`JWT_SECRET_KEY` sin default real — un compose de prod no puede heredar silenciosamente una contraseña débil.
- Nota menor no bloqueante: `.env.example` (solo para dev local, nunca alcanzable por prod por el guard de arranque) trae un password de conveniencia no vacío — documentado en el propio archivo como dev-only, no accionado.

**Discrepancia de alcance detectada**: el punto "SuperAdmin panel controlado por `Deployment:SuperAdminPanelEnabled`" no aplica — esa clave de configuración no existe en ningún lugar de `backend/src`, consistente con hallazgos de auditorías previas (ERP-CORE-CLOSEOUT-05): este repo de ERP Core no contiene ningún panel de SuperAdmin/Platform por diseño arquitectónico (`ERP_CORE_FREEZE.md`, "ERP never depends on Platform"). No es un defecto de este repo — probablemente una referencia cruzada a un flag de otro producto (ZH Platform).

### Checklist operativo del piloto

1. **Crear empresa**: `POST /api/v1/setup/admin` con el token de instalación impreso en consola al primer arranque → crea Tenant + Company + admin + `CompanyUserMembership` + `CompanyUserBranch` a la sucursal principal (fix de ERP-CORE-CLOSEOUT-06). Bootstrap automático crea sucursal, bodega, establecimiento, punto de emisión, caja, secuencias, métodos de pago, cliente "Consumidor Final" y lista de precios por defecto — sin pasos manuales adicionales.
2. **Sucursal/bodega/caja adicionales** (si el piloto necesita más de una sucursal): crear vía `/settings/branches`, `/inventory/warehouses`, `/settings/cash-registers` — cada uno valida pertenencia a la empresa activa (ERP-CORE-CLOSEOUT-05-FIX01).
3. **Abrir caja**: requiere `CashRegister` con `EmissionPointId` asignado — sin caja abierta, Ventas bloquea con mensaje claro ("No existe una caja abierta para realizar ventas.").
4. **Compra**: requiere bodega válida en la sucursal activa — bloquea con mensaje claro si falta.
5. **Venta física**: funciona sin ningún dato de facturación electrónica configurado (aislamiento estructural confirmado en ERP-CORE-CLOSEOUT-07).
6. **Venta electrónica**: requiere `SriSettings` (ambiente + WSDL) y certificado `.p12` subido vía `/settings/electronic-invoicing` — sin eso, bloquea con mensaje claro, nunca un 500. El endpoint `GET /api/companies/operational-readiness` muestra exactamente qué falta antes de intentar vender.
7. **RIDE**: disponible solo tras factura Authorized con XML autorizado persistido — `GET /api/v1/ride/content`. Funciona en Docker/Linux (QuestPDF Community + SkiaSharp Linux, confirmado).
8. **Correo SMTP pendiente**: la venta/factura electrónica nunca se bloquea por falta de SMTP — el correo simplemente no se envía hasta que se configure SMTP real por empresa vía OrgSettings.
9. **Print Agent pendiente de impresora física**: `SalesIssueModal` ofrece imprimir tirilla vía el agente local; si no hay impresora física conectada, el agente reporta el error de forma aislada sin afectar la venta ya emitida (ver `print-agent/README.md`).

### Pendientes externos (no resolubles en este repo)

SMTP real (Zoho u otro) · impresora térmica física + Print Agent instalado por caja · dominio `.com.ec` + SSL · certificado `.p12` SRI real por empresa piloto · confirmación normativa de la Resolución NAC-DGERCGC26-00000027 (ver ERP-CORE-CLOSEOUT-09) · backups productivos automatizados (hoy manual, ver `docs/deployment/README.md`).

---

## ERP-CORE-CLOSEOUT-09 — Cumplimiento SRI proveedor de sistema (2026-08-21)

**Estado: PARCIAL — infraestructura de configuración dinámica lista; integración XML queda como precondición normativa explícita.** Preparación del ERP para obligaciones de proveedor de sistema de facturación electrónica (Resolución NAC-DGERCGC26-00000027).

**Restricción reconocida al iniciar este cierre**: no es posible verificar de forma confiable, desde el conocimiento de este agente, el contenido técnico exacto de la resolución (qué campo/elemento del XML —si alguno— debe llevar el dato del proveedor de sistema). Inventar esa estructura habría violado la propia instrucción del cierre ("No modificar XML SRI sin confirmar estructura/campo aplicable"). Se confirmó el alcance con el usuario antes de implementar: preparar la infraestructura de configuración dinámica sin tocar XML, dejando la integración documentada como precondición.

- **Sin hardcodes previos**: se auditó el código de runtime (Application/Domain/Infrastructure/API, excluyendo tests y `E2ESeedService.cs` ya gateado como no-producción) buscando RUC/razón social/CIIU/"ZH Technologies" — no se encontró ningún hardcode fuera de tests y de un string descriptivo de Swagger. Este punto ya estaba limpio.
- **Configuración dinámica implementada**: nueva entidad singleton `SystemProviderSettings` (RUC, razón social, CIIU, habilitado, fecha de vigencia) — **a nivel de instancia del ERP, no por tenant/empresa** (decisión confirmada con el usuario: el proveedor de sistema es quien construyó el software, un hecho fijo del despliegue, no algo que cada empresa cliente configura). Deliberadamente separada de `Company`/`SriSettings` (el emisor de cada comprobante) — mismo patrón singleton que `SystemSetupState` (Id=1, sin TenantId/CompanyId). Fail-closed: no puede quedar `Enabled=true` con RUC/razón social/CIIU incompletos (validado en el dominio y en el validador de FluentValidation).
- **API**: `GET`/`PUT /api/v1/system/provider-settings`, controlador nuevo y separado (`SystemProviderSettingsController`) — acceso solo Admin del tenant (`[Authorize(Roles = SecurityRoles.Admin)]`, mismo patrón que `SecurityController`), sin requerir contexto de empresa, para no mezclar con la configuración del emisor. Sin pantalla de frontend nueva (no había una existente que lo requiriera, fuera del alcance de este cierre).
- **PRECONDICIÓN NORMATIVA PENDIENTE (bloqueante para cerrar el punto 3 del alcance)**: el dato del proveedor de sistema **todavía no se inyecta en ningún XML de comprobante electrónico**. Antes de tocar `InvoiceXmlBuilder`/`CreditNoteXmlBuilder` o el `infoTributaria`/`infoAdicional` del XML, se necesita el texto de la Resolución NAC-DGERCGC26-00000027 o la ficha técnica SRI correspondiente que confirme el campo/elemento exacto. Documentado también como comentario en `SystemProviderSettings.cs`.
- **Checklist de facturación electrónica**: deliberadamente NO se agregó un ítem de readiness para "proveedor de sistema" en `CompanyOperationalReadinessResolver` en este cierre — el `Code` de cada ítem requiere una traducción i18n correspondiente en frontend (fuera de alcance: "frontend solo si existe pantalla de configuración necesaria", y no hay pantalla de proveedor de sistema todavía). Queda como seguimiento explícito para cuando se implemente esa pantalla.
- **Precondición legal/administrativa externa (no es un bug técnico)**: si ZH Technologies comercializa este ERP como proveedor de sistema, debe revisar/actualizar su propio RUC ante el SRI con el código CIIU J62021002 (actividad de desarrollo de software) antes de operar bajo esa obligación regulatoria — trámite administrativo externo al código, no una tarea de este repositorio.
- Sin cambios en `SalesPage`/POS, Print Agent, ni XML SRI existente — la emisión electrónica actual no se modificó ni se rompió.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests nuevos de dominio y de los handlers Get/Upsert (10 tests, todos verdes), tests filtrados ElectronicDocument/ElectronicInvoicing/Sri/Configuration/CompanyProfile/Security en Application/Infrastructure/API.Tests (119+90+4, todo verde), guardrails de `ERP.Architecture.Tests` (101/101 verde), migración EF nueva (`AddSystemProviderSettings`) sin cambios de modelo pendientes, y `git diff --check`.

**Resultado real vs. esperado**: la configuración dinámica queda lista y sin hardcodes (cumple items 1, 2, 5 —como precondición documentada—, 6 y 7 del alcance). El item 3 (integración XML) y el item 4 (checklist UI) quedan explícitamente abiertos, no cerrados por decisión deliberada ante la falta de confirmación normativa/de alcance de frontend — no se debe interpretar este cierre como "cumplimiento SRI completo".

---

## ERP-CORE-CLOSEOUT-08 — Reportes mínimos finales (2026-08-21)

**Estado: COMPLETADO.** Auditoría de los 8 reportes mínimos (Ventas, Compras, Inventario/stock, Kardex, Caja, Cuentas por Cobrar, Cuentas por Pagar, Monitor de documentos electrónicos). Se encontraron y corrigieron **2 defectos reales**; el resto de los reportes ya tenía aislamiento y cálculos correctos.

- **Totales de Ventas/Compras inflados por Draft/Cancelled corregido**: `GetDailySalesReportQueryHandler` y `GetPurchasesBySupplierReportQueryHandler` sumaban `Totals` sobre **todas** las facturas/compras del rango sin filtrar por estado — una factura Draft (aún no emitida) o Cancelled (anulada), o una compra Draft (aún no confirmada), inflaba el "ingreso"/"gasto" del período. Corregido: `Totals` ahora se calcula solo sobre facturas `Authorized` (Ventas) / compras `Confirmed` (Compras); las filas individuales del reporte siguen mostrando **todos** los documentos del rango con su estado real, para auditoría/trazabilidad — no se ocultó nada, solo se corrigió qué entra en el agregado. 4 tests nuevos.
- **Filtro "Pagadas" de Cuentas por Pagar corregido (bug real, no semántica documentada)**: `PurchasePayableRepository.GetPagedAsync` filtraba `Status == "paid"` literalmente, pero `PurchasePayable.Status` nunca transiciona a `"paid"` (`RegisterPayment` solo acumula `PaidAmount`) — el filtro "Pagadas" del listado de CxP siempre devolvía cero filas, incluso con cuentas completamente saldadas. Corregido con el mismo patrón que ya existía (y funcionaba) en `SalesReceivableRepository.GetPagedAsync` desde `FINANCE-RECEIVABLES-LIST-ENTERPRISE-01`: `"pending"`/`"paid"` se traducen a la condición real de saldo (`BalanceDue`), `"cancelled"` sigue siendo comparación literal. El caso equivalente de Cuentas por Cobrar (saldo cero con `Status` persistido en `"pending"`) ya estaba correctamente resuelto — documentado como semántica intencional (el saldo es la única señal real de "pagada"; `StatusLabel` deriva el estado visible correcto) — y no es un bug. 2 tests nuevos (Postgres real vía Testcontainers, necesario porque el bug era de traducción de la query EF, no verificable con un mock).
- **Confirmado sin defectos** (auditado, no corregido): aislamiento por empresa correcto en los 8 reportes (`ForOperationalScope`/`TenantId`+`CompanyId`, sin excepciones); alcance company-wide (no filtrado por sucursal) es una decisión de negocio ya documentada y consistente en Ventas/Compras/Inventario/Kardex/Caja, no un defecto; fix P0 de `StockRepository` (ERP-CORE-CLOSEOUT-05-FIX01) sigue intacto y correctamente heredado por los reportes de stock/Kardex; fix crítico de RIDE/XML cross-empresa (ERP-CORE-CLOSEOUT-07) sigue intacto y sin reversión; dashboard/estadísticas del monitor de documentos electrónicos correctamente scopeado por empresa, con conteos `Authorized`/`Failed`/reintentables coherentes con los estados reales del documento; fechas comparadas en `DateOnly`/UTC sin ambigüedad de zona horaria; validación de rango de fechas (`DateFrom <= DateTo`) presente en Ventas/Compras. Compras importadas vía recepción XML aparecen en el reporte igual que las manuales (mismo tipo de entidad, sin exclusión especial).
- **Notas no bloqueantes (no corregidas, reportadas)**: `PendingRetries` del dashboard de documentos electrónicos cuenta `RetryCount > 0` (histórico) en vez de estados actualmente reintentables — semánticamente impreciso, no una fuga; `GetPreviousMovementAsync`/`GetNextMovementAsync` en `StockRepository` filtran manualmente por `CompanyId` en vez de usar `ForOperationalScope` — funcionalmente seguro (filtro explícito + filtro global EF de respaldo) pero inconsistente con el resto del archivo; faltan tests directos de company-scoping para `GetForReportAsync`/`GetPreviousMovementAsync`/`GetNextMovementAsync` y para el dashboard/list de documentos electrónicos (cubiertos indirectamente por el filtro global EF, no por un test que lo pruebe explícitamente).
- Sin cambios en `frontend/`, `SalesPage`/POS, Print Agent, ni reglas de negocio cerradas.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados Reports/Sales/Purchases/Inventory/Kardex/Cash/Receivables/Payables/ElectronicDocument en Application/Infrastructure/API.Tests (513+87+108, todo verde tras descartar un fallo transitorio de Testcontainers/Docker no relacionado), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-07 — Documentos electrónicos, monitor y reintentos (2026-08-21)

**Estado: COMPLETADO.** Auditoría de los 8 flujos de documentos electrónicos (configuración incompleta, emisión/firma/SRI, documento autorizado, documento fallido/rechazado, reintentos, monitor, RIDE/XML, integración con Communications). Se encontró y corrigió **1 fuga crítica cross-empresa**; el resto del pipeline (estados, firma, reintentos, idempotencia, RIDE en Docker/Linux) ya estaba sólido.

- **Fuga crítica corregida (cross-empresa, mismo tenant)**: `GetElectronicDocumentQueryHandler` y `GetElectronicDocumentXmlQueryHandler` resolvían el documento vía `GetBySourceAsync`, que solo filtra por `TenantId` — sin comparar `document.CompanyId` contra la empresa activa. Cualquier usuario autenticado del tenant podía leer el XML comercial completo (borrador/firmado/autorizado: cliente, ítems, totales, RUC) de otra empresa, y esa misma fuga se propagaba al RIDE (`GET /api/v1/ride/content` devolvía el PDF de la factura de otra empresa) porque `ElectronicDocumentRideSourceXmlProvider` consume exactamente esas dos queries. Contradecía además el propio comentario de `RideController` que afirmaba que un documento de otra empresa "nunca es distinguible de 'no aplica'" — en la práctica sí se distinguía, devolviendo datos reales. Ambos handlers ya no existían como huecos aislados: `GetElectronicDocumentDetailQueryHandler`/`Timeline`/`RetryElectronicDocument` ya tenían el chequeo correcto (`document.CompanyId != _currentCompany.CompanyId → NotFound`); se aplicó el mismo patrón exacto a los dos handlers que quedaban sin él. 4 tests nuevos (`GetElectronicDocumentQueryHandlerTests`, `GetElectronicDocumentXmlQueryHandlerTests`).
- **Confirmado sin defectos** (auditado, no corregido): modelo de estados (`Draft/XmlGenerated/Signed/Sent/Received/Authorized/Rejected/DeadLetter/Cancelled/Failed`) con transiciones estrictamente guardadas, sin retroceso posible desde estados avanzados. Pipeline de emisión con try/catch en cada etapa, nunca un 500 sin manejar (dos capas: `ElectronicDocumentIssuer` y `ElectronicSalesInvoiceEmissionStrategy`). Clave de acceso persistida antes del envío a SRI; XML firmado/autorizado escrito antes de la transición de estado que lo reclama. Índices únicos `(TenantId, SourceModule, SourceEntityId)` y `(TenantId, AccessKey)` impiden doble sometimiento a SRI incluso bajo carrera. Ambiente/WSDL SRI 100% dinámico por empresa, sin URLs hardcodeadas. Nota de crédito reutiliza el mismo pipeline con las mismas garantías. Rechazo SRI persiste el mensaje real y queda auditado (`ElectronicDocumentSriMessage`); logs con contexto completo (documentId, clave de acceso, texto real del error SRI). La transacción comercial (venta/kardex/CxC) se commitea **antes** de intentar la emisión electrónica — un fallo SRI nunca revierte la venta. Reintentos: Draft/Failed regeneran el XML pero con clave de acceso determinística (hash de RUC+establecimiento+PE+secuencial+tipo — mismo documento, misma clave siempre); Signed/Received solo reenvían el XML ya firmado sin volver a capturar secuencia; documentos Authorized estructuralmente excluidos de la cola de reintento; concurrencia optimista (`xmin`) más `[DisableConcurrentExecution]` evitan doble procesamiento; reintento manual usa el mismo servicio que el job automático, mismas garantías; error en un documento no detiene el batch de los demás. Monitor (lista/detalle/timeline) ya scopeaba correctamente por empresa. QuestPDF con licencia Community configurada y SkiaSharp con paquete Linux/musl explícito para Alpine — RIDE funciona en Docker. Communications: sin email no falla, sin SMTP no bloquea la venta, índice único de `IdempotencyKey` en BD impide duplicados reales (no solo un check de aplicación).
- Sin cambios en `frontend/`, `SalesPage`/POS, Print Agent, ni reglas de negocio.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados ElectronicDocument/Sales/Ride/Communications en Application/Infrastructure/API.Tests (285+94+63, todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-06 — Configuración inicial obligatoria para empresa piloto (2026-08-21)

**Estado: COMPLETADO.** Auditoría de los 11 flujos de configuración inicial (Empresa, Sucursal, Establecimiento, Punto de Emisión, Bodega, Caja, Secuencias, Facturación electrónica, Communications/correo, Usuarios/permisos, smoke de empresa recién configurada). Se encontró y corrigió **1 bloqueante crítico**; el resto de los flujos ya funcionaba end-to-end vía la app sin intervención manual en base de datos.

- **Bloqueante crítico corregido**: `POST /api/v1/setup/admin` (`CreateInitialAdminHandler`) creaba el `CompanyUserMembership` del admin inicial pero **ninguna `CompanyUserBranch`**. Resultado real: el admin podía iniciar sesión, pero `BranchAccessGuard` rechazaba toda operación branch-scoped (venta, compra, caja) con "No tiene autorización para operar en esta sucursal.", y el modal de selección de sucursal del frontend (`BranchSelectorModal`, no descartable) quedaba bloqueado en "No tiene sucursales asignadas. Contacte a un administrador." — sin otro admin a quien contactar, la empresa piloto quedaba atrapada sin salida posible desde la propia app. Corregido: `CreateInitialAdminHandler` ahora ubica la sucursal principal ya creada por el bootstrap (`EnsureDefaultCompanyAsync`/`CompanyBootstrapOrchestrator`) y autoriza al admin en ella dentro de la misma transacción — mismo patrón ya usado por `E2ESeedService` para su admin de pruebas. 5 tests en `CreateInitialAdminHandlerTests` (1 nuevo).
- **Confirmado sin defectos** (auditado, no corregido — no hacía falta): bootstrap automático de Sucursal/Bodega/Establecimiento/Punto de Emisión/Caja/Secuencias documentales/Métodos de pago/Cliente "Consumidor Final"/Lista de precios por defecto al crear la empresa (`CompanyBootstrapOrchestrator`, 7 steps) — un admin no necesita crear nada de eso manualmente. `DocumentSequence.CaptureNextAsync` es find-or-create con advisory lock, sin duplicación posible (índice único `(TenantId, CompanyId, EmissionPointId, DocTypeCode)`). Facturación electrónica: certificado .p12 se sube vía endpoint real (`POST /api/v1/electronic-invoicing/sri-configuration/certificate`), ambiente SRI es dinámico por empresa (nunca hardcodeado), falta de certificado al emitir devuelve `ValidationFailure` claro (nunca 500), factura física nunca toca certificado/ElectronicDocument (garantía estructural — estrategias separadas). Existe un endpoint de "readiness" (`GET /api/companies/operational-readiness`) que le dice al admin exactamente qué falta antes de vender/facturar/usar inventario/caja. SMTP nunca bloquea una venta — el encolado de correo está desacoplado (domain event handler post-commit con try/catch) y el outbox processor aísla fallas por fila sin detener el batch ni otras empresas.
- **Sin hardcodes nuevos**: se buscó "ZH Tech(nologies)"/"Sumak"/RUC de ejemplo fuera de tests/seeders explícitamente gateados — ningún hit en código de runtime real.
- **Nota no bloqueante (no corregida, reportada)**: en el endpoint de readiness, la ausencia de caja/bodega principal es `Warning` para `CanSell`, pero el bloqueo real en runtime (`HasOpenSession`) sigue funcionando correctamente — es solo una posible discrepancia de UX entre "listo para vender" y "puede abrir caja", no un defecto de seguridad ni de datos.
- Sin cambios en `frontend/`, `print-agent/`, `SalesPage`/POS, ni reglas de negocio cerradas.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados Company/Branch/Establishment/EmissionPoint/Warehouse/Cash/DocumentSequence/Electronic/Auth/Setup en Application/Infrastructure/API.Tests (449+156+88, todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-05-FIX03 — Cierre de gobernanza IgnoreQueryFilters (2026-08-21)

**Estado: COMPLETADO.** Cierra el último pendiente de gobernanza dejado abierto por FIX02: `ConfigurationChangeLogQueryRepository.cs` usaba `.IgnoreQueryFilters()` fuera del allowlist permitido, sin necesitarlo.

- **Causa raíz**: `query.TenantId`/`query.CompanyId` (los únicos filtros aplicados) siempre provienen de `ICurrentTenant`/`ICurrentCompany` — documentado explícitamente en `ConfigurationChangeLogQuery` ("nunca del query string"). Eso es exactamente lo mismo que ya exige el filtro global de EF para `ConfigurationChangeLog` (`ITenantScopedEntity` + `ICompanyScopedEntity`), así que el bypass no tenía ninguna razón real — no era un caso de "necesita cruzar tenant/empresa" como el resto del allowlist (login, bootstrap, seeding).
- **Fix**: se eliminó el bypass del filtro global; se mantiene el `Where` explícito por `TenantId`/`CompanyId` como defensa en profundidad, sin ningún bypass. No se usó `PlatformQueryAccessor` aquí porque no aplicaba — el requisito era "si no necesita ignorar filtros, eliminarlo", no envolverlo.
- **`IgnoreQueryFiltersAuditTests` queda en verde** — no quedan usos de `.IgnoreQueryFilters()` fuera del allowlist documentado en todo `backend/src`. ERP-CORE-CLOSEOUT-05 cierra sin pendientes de gobernanza multi-tenant.
- Sin cambios en Sales, Purchases, Inventory, Cash, Communications, Print Agent ni lógica funcional de auditoría/configuración — solo se retiró un bypass innecesario.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), `dotnet test backend/src/ERP.Infrastructure.Tests --filter IgnoreQueryFiltersAuditTests` (verde), `dotnet test backend/src/ERP.Application.Tests --filter Configuration|Settings|Branch|Tenant` (139 tests verdes), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-05-FIX02 — P1 de aislamiento y gobernanza (2026-08-21)

**Estado: COMPLETADO.** Se corrigieron los 6 hallazgos P1 de ERP-CORE-CLOSEOUT-05 sin reabrir los P0 de FIX01 ni tocar reglas de negocio de venta/compra/inventario/caja.

- **Compras por id (P1-1)**: `GetPurchaseByIdHandler` ahora valida `inv.BranchId == ICurrentBranch.BranchId` (mismo patrón que `GetSalesInvoiceByIdHandler`, FIX01). `GetPurchaseListQuery` queda sin cambios — su alcance company-wide es una decisión de negocio ya documentada en el propio código (mismo criterio que `GetSalesInvoiceListQuery`), no un defecto.
- **CashMovement (P1-2)**: decisión documentada — se mantiene `IMustHaveTenant` (sin Company/Branch) porque nunca se consulta directamente, solo como hijo de `CashSession` (ya scopeado). Se agregó `CashMovementDirectQueryAuditTests` (guardrail de gobernanza) para que una futura consulta directa no pueda introducirse sin scope explícito.
- **SwitchBranchHandler / UserSession.BranchId (P1-3)**: `UserSession.BranchId` quedaba congelado en la sucursal del login tras un switch, pudiendo ser consultado como fallback por `GetSessionContextHandler` cuando el cliente aún no envía `X-Branch-Id`. Se agregó `UserSession.UpdateBranch()` y `SwitchBranchHandler` ahora actualiza la sesión activa tras un switch exitoso — best-effort, nunca fuente de autorización (eso sigue siendo `ICurrentBranch` + `BranchScopeBehavior` por request).
- **IgnoreQueryFilters en StockAdjustmentRepository (P1-4)**: reemplazado por el wrapper sancionado `PlatformQueryAccessor.AsPlatformQuery()` (ya pre-registrado en el allowlist de `IgnoreQueryFiltersAuditTests`), manteniendo el filtro explícito por TenantId.
- **StockAdjustment sin guard de sucursal (P1-5 — hallazgo real, no duplicado)**: se confirmó que `CreateStockAdjustmentCommandHandler`/`ExecuteStockAdjustmentCommandHandler` **nunca tuvieron** validación de bodega/sucursal en el código commiteado (el reporte de auditoría previo que decía "ya protegido" citaba líneas que no correspondían a código real). Se agregaron los guards (`warehouse.BranchId == ICurrentBranch.BranchId`) con 5 tests nuevos.
- **CommunicationOutboxProcessor (P1-6/gobernanza)**: `IgnoreQueryFiltersAuditTests` fallaba en el código commiteado (previo a este fix, no causado por esta sesión) porque este archivo usaba `.IgnoreQueryFilters()` crudo, fuera del allowlist. Cambio de una línea a `.AsPlatformQuery()`, sin tocar SMTP, outbox ni lógica de envío — confirmado con el usuario antes de tocar Communications.
- **Hallazgo nuevo fuera de alcance, reportado sin corregir**: `ConfigurationChangeLogQueryRepository.cs` (módulo Configuration/Settings) también usa `.IgnoreQueryFilters()` fuera del allowlist — no relacionado a Communications/Sales/Purchases/Inventory/Caja y fuera de la lista de P1 de este cierre. `IgnoreQueryFiltersAuditTests` sigue en rojo por este motivo (no cubierto por los filtros de test exigidos en este fix). Candidato a un FIX03 futuro.
- Tests nuevos: `GetPurchaseByIdHandlerTests`, `StockAdjustmentBranchOwnershipTests` (5 casos), `CashMovementDirectQueryAuditTests`, 2 tests nuevos en `SwitchBranchHandlerTests`.
- Sin cambios en `frontend/`, `print-agent/`, `SalesPage`, reglas de negocio, ni infraestructura FROZEN.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), tests filtrados Purchases/Cash/Inventory/Branch/StockAdjustment en Application/Infrastructure/API.Tests (todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.

---

## ERP-CORE-CLOSEOUT-05-FIX01 — Corrección de P0 de aislamiento multiempresa/multisucursal (2026-08-21)

**Estado: COMPLETADO.** Se corrigieron los 4 P0 detectados en la auditoría ERP-CORE-CLOSEOUT-05, sin tocar reglas de negocio, `print-agent/` ni Communications.

- **Caja**: `CloseCashSessionHandler` y `RecordCashMovementHandler` ahora validan que la `CashSession` cargada por id pertenezca a la sucursal activa (`ICurrentBranch`) antes de cerrarla o registrar un movimiento — antes solo filtraban por Tenant+Company, permitiendo cerrar/mutar la caja de otra sucursal por GUID.
- **Ventas/Caja (lectura)**: `GetSalesInvoiceByIdHandler` y `GetCashSessionByIdHandler` agregan el mismo chequeo de `BranchId` (mismo patrón ya usado por el endpoint `receipt-print-payload`) antes de devolver el detalle — antes exponían facturas/cajas de otra sucursal de la misma empresa por GUID.
- **Inventario**: `StockRepository.GetStockAsync/GetStockByWarehouseAsync/GetStockByProductAsync/GetMovementsAsync/GetMovementsByProductAsync/GetMovementByIdAsync/GetMovementsByDocumentAsync` ahora scopean explícitamente por `CompanyId` vía `ForOperationalScope` (defensa en profundidad — `CurrentStock`/`StockMovement` ya tenían filtro global EF por `CompanyId`, pero los métodos del repositorio no lo reforzaban explícitamente).
- **Warehouse/CashRegister**: `CreateWarehouseCommandHandler`, `UpdateWarehouseCommandHandler` y `CreateCashRegisterHandler` ahora resuelven la sucursal recibida en el body vía `IBranchRepository` y rechazan el comando si no existe o `branch.CompanyId` no coincide con la empresa activa — antes confiaban en el `BranchId` del cliente sin validar pertenencia a la empresa.
- Tests nuevos: `CashSessionBranchScopeTests`, `CreateCashRegisterBranchOwnershipTests`, `WarehouseBranchOwnershipTests`, `GetSalesInvoiceByIdHandlerTests` (Application.Tests) y `StockRepositoryCompanyScopeIntegrationTests` (Infrastructure.Tests, Postgres real vía Testcontainers, dos empresas del mismo tenant).
- Sin cambios en `frontend/`, `print-agent/`, Communications, ni en la infraestructura FROZEN (Secuencias Documentales, Entity Tracking, Configuración Tributaria).
- Validado con `dotnet build backend/src/ERP.slnx --no-restore` (0 errores), `dotnet test` filtrado por Sales/Cash/Inventory/Warehouse en Application/Infrastructure/API.Tests (todo verde), `dotnet ef migrations has-pending-model-changes` (sin cambios pendientes) y `git diff --check`.
- Nota: durante la auditoría previa, un subagente de investigación introdujo cambios no solicitados fuera de alcance (guard de sucursal en StockAdjustment, refactor de `CommunicationOutboxProcessor`) pese a instrucciones explícitas de solo lectura; se detectaron vía `git status` antes de commitear y se revirtieron. Quedan pendientes como posible FIX02 si el usuario decide retomarlos formalmente.

---

## ZH-PRINT-AGENT-02B — SalesIssueModal integrado con Print Agent local (2026-08-21)

**Estado: COMPLETADO.** Se integró el modal post-facturación de Ventas/POS con el ZH Print Agent local usando el payload oficial del backend, sin imprimir desde backend y sin recalcular datos fiscales en frontend.

- Frontend: `SalesIssueModal` ahora ofrece `Imprimir tirilla` / `Reimprimir tirilla` en el estado de éxito de emisión, manteniendo `Nueva venta` como salida normal para omitir impresión.
- Datos: antes de imprimir consulta `GET /api/v1/sales/invoices/{invoiceId}/receipt-print-payload`; el request al agente se arma solo con esos snapshots oficiales, sin recalcular totales, IVA, pagos ni vuelto.
- Print Agent: cliente local configurable con `VITE_PRINT_AGENT_BASE_URL`, `VITE_PRINT_AGENT_RECEIPT_ENDPOINT`, `VITE_PRINT_AGENT_API_KEY`, `VITE_PRINT_AGENT_PRINTER_NAME` y overrides por `localStorage` (`zh.printAgent.*`). El endpoint real del agente actual es `/print-jobs`.
- Idempotencia: `jobId = invoice-{invoiceId}-receipt`; reenviar el mismo job no duplica una tirilla ya `Printed` según semántica del agente. Si el job queda `Failed`/`NeedsReview`, el reintento usa `POST /print-jobs/{jobId}/retry`.
- UX: mensajes visibles para `Imprimiendo...`, `Tirilla enviada a impresión.`, agente apagado, API key inválida/no configurada, impresora no disponible y error de impresión reintentable.
- Sin cambios en `SalesPage`, reglas de venta, caja, kardex, stock, pagos, SRI, RIDE, Communications ni `print-agent/`.
- Validado con `npx vitest run src/modules/sales/api/printAgentClient.test.ts`, eslint específico de archivos tocados, `npm run build` y guardas de plataforma OK.

---

## ZH-PRINT-AGENT-02A — Backend payload oficial de tirilla POS (2026-08-21)

**Estado: COMPLETADO.** Se agregó un contrato backend oficial, estable y solo lectura para que el POS pueda obtener el payload de tirilla de una factura ya emitida sin acoplar el ERP al Print Agent ni ejecutar impresión física desde backend.

- Application/API: agregado `GetSalesReceiptPrintPayloadQuery` y endpoint `GET /api/v1/sales/invoices/{invoiceId}/receipt-print-payload`, expuesto desde `SalesController` con controller delgado y permiso `Sales.View`.
- Payload: devuelve tenant, empresa, sucursal, RUC, nombre comercial, caja/sesión, número de factura, cliente, estado electrónico/SRI cuando aplica, líneas, totales, pagos y pie de documento resuelto por `ICompanyBrandingResolver`.
- Scope: la consulta falla cerrado con `NotFound` si la factura no existe, no está autorizada/emitida o pertenece a otra sucursal activa del contexto. No crea ni modifica factura, pagos, caja, stock, kardex, RIDE, Communications ni outbox.
- Fallbacks documentados: `cashReceived` y `cashChange` se devuelven `null` porque hoy no están persistidos en `SalesInvoicePayment` ni `CashMovement`; `establishmentCode`/`emissionPointCode` usan configuración actual si existe y fallback histórico desde `InvoiceNumber`/snapshot de `CashSession`.
- Sin cambios en `frontend/`, `print-agent/`, `SalesPage`, `SalesIssueModal`, reglas de venta, SRI, RIDE ni Communications.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore`, tests nuevos de payload, tests Application relevantes de Sales/Caja/ElectronicDocuments, y `dotnet ef migrations has-pending-model-changes` sin cambios pendientes.

---

## ERP-CORE-CLOSEOUT-02B — Correo automático de factura autorizada SRI (2026-08-21)

**Estado: COMPLETADO.** Se conectó la autorización electrónica SRI con el módulo transversal Communications para encolar automáticamente el correo de factura autorizada al cliente, sin acoplar Ventas/SRI/POS a SMTP.

- Integración: agregado `SalesInvoiceAuthorizedCommunicationHandler`, suscrito a `ElectronicDocumentAuthorizedEvent`. Solo actúa cuando el documento electrónico está `Authorized`, es `Invoice` y su origen es `Sales`.
- Communications: agregado propósito canónico `SALES_INVOICE_AUTHORIZED`; `ICommunicationQueue` ahora permite pasar `BranchId` explícito y diferir `SaveChanges` para integrarse correctamente con handlers de domain events.
- Email: si el snapshot del cliente tiene email válido, se encola `CommunicationOutbox` con asunto/cuerpo que incluyen número de factura, clave de acceso, cliente, total y empresa emisora. Si el cliente no tiene email válido, no se encola y la factura no falla.
- Adjuntos: se referencia el XML autorizado (`AuthorizedXmlPath`) y se solicita RIDE por el caso de uso público `GetOrGenerateRideQuery`; si RIDE no está disponible o falla, se registra y el correo se encola con los adjuntos disponibles sin revertir la autorización.
- Idempotencia: la comunicación usa una `IdempotencyKey` determinística por tenant, empresa, factura, propósito y destinatario para evitar duplicados ante reprocesos.
- Sin cambios de UI: no se modificó `SalesPage`, POS ni se agregó botón manual de correo. No hubo migración nueva en 02B; se reutiliza la migración `AddCommunicationsOutbox` de 02A.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore`, tests Application relevantes de Communications/ElectronicDocuments/Sales y tests Domain relevantes de Communications/ElectronicDocuments/Sales.

---

## ERP-CORE-CLOSEOUT-02A — Communications transversal reutilizable (2026-08-21)

**Estado: COMPLETADO.** Se implementó la arquitectura base de Communications como módulo transversal desacoplado de Ventas/SRI/POS y reutilizable por otros módulos.

- Domain: agregado `Communications` con `CommunicationOutbox`, `CommunicationOutboxAttachment`, `CommunicationTemplate`, enums de canal/estado/prioridad/tipo de adjunto e interfaces de repositorio. Domain no depende de SMTP, Hangfire, EF ni ASP.NET.
- Application: agregado contrato reutilizable `ICommunicationQueue`, `QueueEmailCommand` CQRS/MediatR con FluentValidation, DTOs de encolado y contratos técnicos `IEmailSender`, `ICommunicationSettingsResolver`, `ICommunicationOutboxProcessor`.
- Infrastructure/API: agregado mapeo EF y repositorios, resolvedor SMTP desde `OrgSettings` (`communications.email.*`) con fallback a `Communications:Email:*`, `SmtpEmailSender`, processor de outbox multi-tenant con `JobExecutionContext`, y job Hangfire `process-communications` cada minuto.
- Persistencia: migración `20260821020826_AddCommunicationsOutbox` crea `communication_outbox`, `communication_outbox_attachments` y `communication_templates`, con índices para pendientes, correlación e idempotencia por `(tenant_id, company_id, idempotency_key)`.
- No se modificó `SalesPage` y no se conectó SRI/POS a SMTP; el disparo post-autorización de factura se implementó después en `ERP-CORE-CLOSEOUT-02B`.
- Validado con `dotnet build backend/src/ERP.slnx --no-restore`, tests puntuales de Domain/Application para Communications/configuración, y generación de SQL idempotente de la migración EF.

---

## ERP-CORE-CLOSEOUT-01 — Cierre POS Retail / SalesPage para piloto (2026-08-20)

**Estado: COMPLETADO.** Auditoría de `SalesPage` contra el checklist funcional de cierre para piloto retail/POS — la implementación existente ya cumplía la mayoría de los requisitos; se corrigió el único vacío real encontrado.

- Confirmado ya implementado (sin cambios): búsqueda por SKU/nombre/código de barras (`InvoiceItemSearchRepository`, rankeo barcode exacto → SKU exacto → parcial → nombre), tarjeta de resultado con stock/precio sin IVA/IVA/precio final sin costo, fusión de línea al reescanear/duplicar producto (`findMergeableLineIndex`) con actualización visual de cantidad/subtotal/IVA/total, diseño de línea aprobado (`ZHLineCard` con rail numerado + basurero), bloqueo de emisión sin caja abierta (`canEmit`/`hasCashSession`) y sin cobro completo (`paymentOk`), y modal post-facturación sin envío de correo manual.
- Corregido: el modal de éxito de emisión (`SalesIssueModal`) no mostraba **dinero entregado** ni **vuelto** en ventas con cobro en efectivo — se agregaron ambos campos (visibles solo cuando `cashDue > 0`), reutilizando el estado ya existente en `useSalesPage` (`cashReceived`/`cashChange`), sin nuevos componentes ni cálculos duplicados.
- Sin cambios de backend, sin componentes nuevos del Design System, sin estilos inline.
- Validado con `npx eslint` (sin errores nuevos), `npx tsc --noEmit` (sin errores), `npm run build` y `npx vitest run` sobre los tests existentes de `SalesPage` (bottombar/emitButton/paymentMethod, 20/20 passed).

---

## FLOW-READY-02F.11-FIX01 — Compras: proveedor inactivo y reactivación visible (2026-08-13)

**Estado: COMPLETADO.** Corrección acotada del bloqueo de compras con proveedor inactivo y de la visibilidad operativa en Administración de proveedores.

- Compras muestra el mensaje específico de `data.errors` antes que el genérico de `VALIDATION_ERROR`, con resumen visible de errores en `ZHPageNotice`.
- Backend mantiene el bloqueo fail-closed de proveedor inactivo y ahora devuelve el nombre legal del proveedor en el mensaje específico cuando está disponible.
- Administración de proveedores expone filtro explícito `Todos / Activos / Inactivos`, muestra el estado real `BusinessPartner.IsActive` y reutiliza `PATCH /activate` / `DELETE` soft-disable existentes.
- No se tocaron PricingResolver, Kardex, IStockRepository, posting/accounting, PurchaseCreditNote, PurchaseReturn ni SupplierCredit.
- Validado con `npx tsc --noEmit`, `npm run lint`, `npm run build`, `npx vitest run src/modules/purchases src/modules/masterData src/modules/items`, `dotnet build backend/src/ERP.slnx`, `dotnet test backend/src/ERP.slnx --filter Purchase`, `dotnet test backend/src/ERP.slnx --filter BusinessPartner`, `dotnet test backend/src/ERP.slnx --filter Items` y `git diff --check`.

---

## FLOW-READY-02F.10-CLEAN01 — Items Admin SSOT cleanup (2026-08-12)

**Estado: COMPLETADO.** Auditoría y limpieza acotada del Admin de Ítems sin cambios de backend.

- Códigos de barras y códigos proveedor quedan gestionados solo en Principal; se eliminó la sección duplicada no consumida de códigos proveedor en detalle.
- Presentaciones/empaques quedan en Inventario y presentaciones; se conserva `ItemPackagingLevel` como SSOT y no se infieren factores desde nombres.
- Precio/costo/rentabilidad queda en una sola sección de Principal; se eliminó el componente antiguo de simulación “Nuevo PVP / Simular” y su cliente frontend.
- Textos visibles tocados en catálogo/listado/árbol se pasaron a i18n `es/en/qu`; se removió CSS huérfano del simulador viejo y se mantuvo cero `style=`.
- Validado con `npx vitest run src/modules/items`, `npx tsc --noEmit`, `npm run lint`, `npm run build` y `git diff --check`.

---

## FLOW-READY-02F.7 — Controles preventivos empaques / XML (2026-08-11)

**Estado: COMPLETADO.** Controles fail-closed para evitar configuraciones peligrosas de presentación, código proveedor y compra XML.

- Ítems inventariables requieren exactamente una presentación base con `BaseQuantity = 1`; servicios/no inventariables no quedan bloqueados por ausencia de base.
- Empaques validan cantidad positiva, no duplican `UOM + BaseQuantity`, no permiten base con factor distinto de 1 y advierten nombres tipo `PACA x12` con factor 1 sin inferir automáticamente.
- Códigos proveedor muestran estado “sin presentación” y permiten guardar la presentación asociada.
- Confirmación de compra XML muestra checklist de ítems, presentaciones, líneas sin presentación, impuestos y diferencia total; backend bloquea líneas XML inventariables sin presentación.
- Empaques usados por códigos proveedor o documentos confirmados no pueden eliminarse ni cambiar su factor; se debe crear una nueva presentación.
- Alerta de costo base extremo contra último costo/promedio sugiere revisar presentación/factor.
- Validado con `dotnet build backend/src/ERP.slnx`, `dotnet test backend/src/ERP.slnx --filter Items`, `dotnet test backend/src/ERP.slnx --filter Purchase`, `dotnet test backend/src/ERP.slnx --filter PurchaseReception`, `npx tsc --noEmit`, `npm run lint`, `npm run build` y `npx vitest run src/modules/items src/modules/purchases`.

---

## Purchases — Recepción XML empaques FIX03 (2026-08-11)

**Estado: COMPLETADO.** Corrección de rehidratación de presentación al abrir una compra desde Recepción Electrónica con `fromReceptionId`.

- `CreatePurchaseReceptionDraftHandler` re-resuelve cada línea vinculada usando `SupplierId + SupplierCode` contra `ItemSupplierCode` y toma `PackagingLevelId`, UOM y factor desde `ItemPackagingLevel`.
- El DTO de draft de recepción expone `packagingLevelId`, `uomCode`, `baseUomCode`, `conversionFactor` y `quantityInBaseUom`, evitando que el frontend vuelva a factor 1.
- `/purchases?fromReceptionId=...` hidrata el formulario con la instantánea de presentación y el VM muestra `Ítem + PACA x12` aun si el contexto de bodega todavía no cargó.
- Guardar presentación del proveedor actualiza la línea local con UOM, factor y cantidad base sin requerir recarga manual.
- Validado con `dotnet build backend/src/ERP.slnx`, `dotnet test backend/src/ERP.slnx --filter PurchaseReception`, `dotnet test backend/src/ERP.slnx --filter Purchase`, `dotnet test backend/src/ERP.slnx --filter Items`, `npx tsc --noEmit`, `npm run lint`, `npm run build` y `npx vitest run src/modules/purchases src/modules/items`.

---

## Items — Empaques FIX02 (2026-08-11)

**Estado: COMPLETADO.** Corrección del flujo de edición de niveles de empaque en maestro de ítems.

- El guardado de empaques muestra errores reales de validación backend y conserva la fila en edición si falla.
- La UI impide guardar conjuntos sin exactamente una presentación base y facilita crear `UNIDAD X1`.
- `replacePackagingLevels` preserva IDs existentes al editar, evitando romper asociaciones de códigos de proveedor.
- El selector de presentación de códigos proveedor usa los empaques refrescados y muestra el factor contra la unidad base del ítem.
- Validado con `npx tsc --noEmit`, `npm run lint`, `npm run build`, `npx vitest run src/modules/items`, `dotnet build backend/src/ERP.slnx` y `dotnet test backend/src/ERP.slnx --filter Items`.

---

## Design System Form Controls SSOT — fase cerrada (2026-08-07)

**Estado: CERRADO CON DEUDA DOCUMENTADA.** Migración de controles HTML crudos (`<input>`/`<select>`/`<textarea>`) hacia los componentes ZH oficiales (`ZhTextInput`, `ZhNumberInput`, `ZhDecimalInput`, `ZhDateInput`, `ZhPhoneInput`, `ZhSelect`, `ZhTextarea`), ejecutada en bloques 14B-4 a 14B-12.

**Resultado:**
- Controles HTML crudos reducidos de 314 a 149 (`frontend/src/modules/**/*.tsx`).
- 165 controles migrados a componentes ZH oficiales, sin cambios en schemas, handlers, payloads ni servicios.
- No quedan clusters grandes de Categoría A (pendiente real simple); el mayor residuo es de 3 controles en un mismo archivo.
- 12 controles A dispersos en 7 archivos quedan documentados como deuda menor (candidatos a cierre puntual futuro, cada uno ≤3 controles/mismo archivo).
- Residuos restantes (137 controles) están justificados por tipo HTML (email/password/checkbox/radio/file/color) o por dominio especializado: SRI crítico, IAM/permisos, pickers con teclado, tablas editables, stock/logística crítica, ItemTypes FROZEN, min/max nativo crítico.

**Validado:** `npx tsc -b`, `npm run build`, `git diff --check` en verde en cada bloque de migración.

---

## Piloto operativo Sumak — uso supervisado (2026-08-03)

**Estado: READY_FOR_PILOT / uso supervisado.** No implica producción estable ni cierre de módulo — es habilitación para operar con supervisión directa mientras se completan las limitaciones aceptadas abajo.

`SUMAK_E2E_01_STATUS: PASSED`. Commits relacionados: `da1a2381` (reporte de stock actual por bodega), `cef699d6` (reporte de compras por proveedor), `c49da503` (reportes mínimos en el menú).

**Capacidades validadas (E2E manual):**
- Compra manual y creación de Item desde línea de compra
- IVA compra/venta + precio de venta resuelto correctamente
- Confirmación de compra
- Stock actual y Kardex
- Venta POS con cobro en efectivo y cálculo de vuelto
- Factura electrónica autorizada
- Caja actualizada tras la venta
- Reportes de Ventas, Compras y Stock funcionando
- Devolución de compra bloqueada correctamente por stock insuficiente
- 0 errores HTTP 5xx y 0 errores de consola durante la prueba E2E

**Limitaciones aceptadas (no bloquean el piloto, sí producción):**
- SRI producción no validado (solo ambiente de pruebas)
- Recepción física sin factura previa: pendiente
- Reportes sin exportación a Excel/PDF
- Reportes de ventas/compras alcance company-scoped (no consolidado multi-sucursal)
- Caja consolidada diaria: pendiente
- CxP/CxC avanzado: pendiente
- Limpieza global de lint/architecture/e2e: fuera de este cierre

---

## Backlog futuro UX

### MEJORA_FUTURA_UX_01 — Command Palette / Buscador rápido de navegación

- **Estado:** BACKLOG / FUTURE
- **Prioridad:** P2
- **Tipo:** UX / Navegación / Productividad
- **Dependencia:** App Drawer estabilizado y `navigation.config.ts` como SSOT.
- **Objetivo:** Permitir buscar y abrir formularios con `Ctrl+K` / `Cmd+K` usando la misma fuente de verdad del menú.
- **Fuera de alcance actual:** No implementar código, no tocar backend, no cambiar rutas, no cambiar permisos ni modificar el App Drawer.
- **Motivo:** Mejora no bloqueante para usuarios avanzados cuando existan más pantallas.

---

## Estado actual (2026-06-24)

**Completado**
- Arquitectura base terminada (Clean Architecture + CQRS)
- Autenticación JWT + Refresh Token
- Multi-tenant por `tenant_id` + `company_id`
- Cambio de empresa (multi-company)
- Dashboard unificado
- ERP Core congelado
- Items Module FROZEN v1.0 (2026-06-17)
- **Items Module — Rediseño flujo de creación: FROZEN v2.0 (2026-07-02)** — reemplaza v1.0: código de barras obligatorio (mínimo 1, exactamente 1 principal), códigos de proveedor opcionales (`ItemSupplierCode`, 0..N, FK a `BusinessPartner`), categoría y marca obligatorias en creación, eliminación de flags booleanos de impuesto (`AppliesVatOnSale/Purchase/ExciseTax` — el código tributario es la única fuente de verdad, alineado con la Infraestructura Tributaria CLOSED), precio inicial creado atómicamente junto con el ítem (`ItemPrice` contra lista DEFAULT/PVP)
- **Items Module — Auditoría por fases, Fase 1 (Información Base del Item): COMPLETADA (2026-07-02)** — SKU editable y único por tenant (índice BD), marca/categoría con FK real e integridad activa validada, breadcrumb de categoría, profundidad máxima del árbol de categorías configurable por empresa (`OrgSettings`, default 3). Detalle completo: [`docs/items/PHASE1-ITEM-IDENTITY.md`](items/PHASE1-ITEM-IDENTITY.md)
- **Items Module — Auditoría por fases, Fase 2 (Identificación del Item): COMPLETADA (2026-07-02)** — código de barras único globalmente por tenant (antes solo por ítem), código de proveedor único por `(tenant_id, supplier_id, code)`, proveedor obligatorio por cada entrada de código de proveedor. Detalle completo: [`docs/items/PHASE2-ITEM-IDENTIFICATION.md`](items/PHASE2-ITEM-IDENTIFICATION.md)
- **Items Module — Auditoría por fases, Fase 3 (Tributación del Item): COMPLETADA (2026-07-02)** — códigos SRI (`SaleVatCode`/`PurchaseVatCode`/`ExciseTaxCode`) confirmados como única fuente de verdad, sin cambios; campos de cuenta contable (`VatAccountId`/`PurchaseVatAccountId`/`ExciseAccountId`) retirados del contrato público del módulo Items por no tener módulo de Contabilidad que los respalde (quedan reservados internamente); `SriServiceCode` retirado del formulario por no tener catálogo SRI de respaldo. Sin impacto en Ventas/Compras/Facturación (siguen resolviendo impuestos vía `ISriTaxResolver`, Infraestructura Tributaria CLOSED intacta). Detalle completo: [`docs/items/PHASE3-ITEM-TAXATION.md`](items/PHASE3-ITEM-TAXATION.md)
- **Items Module — Auditoría por fases, Fase 4 (Comercial del Item): COMPLETADA (2026-07-02)** — confirmado: precio inicial siempre a la lista de precios predeterminada, sin selector en el formulario; corregido símbolo de moneda hardcodeado (`$`) en `PricingTab.tsx`, ahora refleja `PriceList.CurrencyCode` real. Sin cambios de backend. Detalle completo: [`docs/items/PHASE4-ITEM-COMMERCIAL.md`](items/PHASE4-ITEM-COMMERCIAL.md)
- **Items Module — Auditoría por fases, Fase 5 (Inventario y Venta del Item): COMPLETADA (2026-07-02)** — confirmado: la configuración de Inventario/Venta (`TracksStock`, lotes, series, decimales, disponibilidad POS/Web/Mobile) es intencionalmente independiente del `ItemType`, sin restricciones ni defaults condicionados por tipo. Sin cambios de código. Detalle completo: [`docs/items/PHASE5-ITEM-INVENTORY-SALE.md`](items/PHASE5-ITEM-INVENTORY-SALE.md)
- **Items Module — Auditoría por fases, Fase 6 (Variantes del Item): COMPLETADA (2026-07-02)** — SKU de variante único globalmente por tenant (antes solo por ítem), consistente con SKU de ítem (Fase 1) y barcode/código de proveedor (Fase 2). Detalle completo: [`docs/items/PHASE6-ITEM-VARIANTS.md`](items/PHASE6-ITEM-VARIANTS.md)
- **Items Module — Auditoría por fases, Fase 7 (Pricing del Item): COMPLETADA (2026-07-02)** — corregida violación de la regla "no eliminar registros": `RemoveItemPriceCommand` ahora deshabilita el precio en vez de hacer `DELETE` físico; historial de cambios de precio registrado en `UserActivity` (auditoría existente, append-only), no en tabla propia. Detalle completo: [`docs/items/PHASE7-ITEM-PRICING.md`](items/PHASE7-ITEM-PRICING.md)
- **Motor de Pricing v2 — Dominio Items+Pricing: CLOSED (2026-07-05)** — reemplaza el modelo de Fase 7: `Item.BaseSalePrice` es el SSOT del precio base; `ItemPrice` fue eliminado y reemplazado por `PricingRule` (regla de ajuste, no precio absoluto, sin quiebres de cantidad — eso pertenece al futuro módulo Promotions); `PriceList` gana una regla general opcional; `IPricingResolver` centraliza la resolución de precio (antes duplicada en 4 lugares) como única API pública que el resto del ERP debe consumir. Reabre parcialmente el freeze de Items v1.0 y de Fase 7 solo en lo referente a precios — ambos quedan reemplazados por este ADR en ese punto. Integración con Ventas/Compras/POS/Facturación (consumo real de `IPricingResolver`) queda pendiente como trabajo de esos módulos, sin reabrir este dominio. Detalle completo: [`docs/decisions/ADR-021-pricing-engine-ssot.md`](adr/ADR-021-pricing-engine-ssot.md)
- **Items Module — Auditoría por fases, Fase 8 (Compras): COMPLETADA (2026-07-02)** — Compras migrado para resolver el código de proveedor vía `ItemSupplierCode` (Fase 2) según el proveedor real de la factura, con fallback al campo legacy `Item.Code.PurchaseCode`; corregido defecto preexistente que impedía cargar `Item.SupplierCodes` en cualquier lectura del agregado (`.Include()` faltante). Detalle completo: [`docs/items/PHASE8-ITEM-PURCHASES.md`](items/PHASE8-ITEM-PURCHASES.md)
- **Items Module — Auditoría por fases, Fase 9 (Arquitectura — revisión transversal): COMPLETADA (2026-07-02)** — revisión de duplicación/acoplamientos/cumplimiento de infraestructuras FROZEN en las Fases 1-8; único hallazgo (duplicación menor de resolución de código de proveedor introducida en Fase 8) corregido con un helper compartido en `PurchaseDraftUseCases.cs`. **Cierra la auditoría completa del módulo Items (Fases 1-9).** Detalle completo: [`docs/items/PHASE9-ARCHITECTURE.md`](items/PHASE9-ARCHITECTURE.md)
- Customer Module FROZEN (2026-06-17)
- Compras: auditoría UX + SSOT completada (2026-06-24)
- Sales Invoice + Detail: módulo cerrado (2026-06-24)
- Payment Methods + Formas de Cobro Multi-Pago: CERRADO (2026-06-24)
- Sales Receivable (CxC deuda, sin cobros): CERRADO (2026-06-25)
- Estándar de Precisión Numérica: CERRADO (2026-06-25) — ver tabla Módulos FROZEN
- Estándar de Fechas y Horas: CERRADO (2026-06-25) — ver tabla Módulos FROZEN
- Infraestructura de Mensajes Visuales: CLOSED (2026-06-29) — ADR-018
- Infraestructura de Secuencias Documentales: CLOSED (2026-06-29) — ADR-019
- **Infraestructura de Entity Tracking (EF Core Change Tracking): CLOSED (2026-06-30) — ADR-020**
- **Infraestructura Tributaria (Tax Infrastructure): CLOSED (2026-07-01)**
- **Infraestructura de Valores por Defecto de Facturación: CLOSED (2026-07-01) — migrado a org_settings (Phase 8, 2026-07-01)**
- **Infraestructura Org Config Jerárquica (OrgSetting / 5 scopes): CLOSED (2026-07-01)** — `org_settings`, `IOrgSettingsRepository`, `OrgSettingKeys`; 10 endpoints GET/PUT por scope; UI en Company Settings Hub
- **Infraestructura Master Configuration UI: CLOSED (2026-07-02)** — Patrón oficial de tabs para módulos de configuración; `ConfigTabsLayout` + `items-catalog.css`; implementado en Branches, Establishments, Emission Points, Warehouses; prohibido crear variantes sin decisión arquitectónica global
- **Infraestructura de Auditoría por Dominio (Entity Audit): CLOSED (2026-07-07) — ADR-022** — contratos comunes (`AuditRecordBase`/`IAuditWriter`/`IAuditReader`/`IAuditService`/`IAuditContext`) reutilizables por todo dominio futuro; pilotos `PricingRuleAudit`/`PriceListItemAudit`; Process Audit (procesos sin `EntityId` único) queda diseñado en `docs/architecture/audit-infrastructure.md`, sin implementar
- **Contexto Operativo del Usuario (UserSession): implementado y estabilizado (2026-07-17)** — registro de sesión operativa (empresa/sucursal/terminal) integrado a Login/SwitchCompany, expiración automática vía Hangfire, dashboard administrativo en `/admin/access/sessions` (`AdminUserSessionController`, única API pública del dominio). Detalle: [`docs/IDENTITY.md#usersession-contexto-operativo-del-usuario`](IDENTITY.md#usersession-contexto-operativo-del-usuario). Hardening Fase 12: eliminado `UserSessionController` self-service (IDOR + cero consumidores reales) en vez de endurecerlo
- **CompanyUserPreferences (preferencias de login: sucursal por defecto + modo de ingreso): ciclo cerrado (2026-07-17)** — única fuente de verdad de `DefaultBranchId`/`LoginMode`; escritura vía `UpsertCompanyUserMembershipHandler` (alta/edición de membresía) y `PUT /api/v1/admin/iam/company-users/{companyUserId}/preferences`; lectura centralizada en `CompanyUserPreferencesLoginResolver` (Login/SwitchCompany) y `GET` del mismo endpoint; `CompanyUserBranch` sigue siendo la única fuente de sucursales autorizadas (nunca se le agregó comportamiento). Auditoría de cierre (Fase H) corrigió que una sucursal desactivada podía aceptarse como `DefaultBranchId`. UI en `SecuritySettingsPage` (`/admin/security`), sin CRUD propio. Sin cambios de JWT en todo el ciclo. Detalle: [`docs/IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login`](IDENTITY.md#companyuserpreferences-preferencias-operativas-de-login)
- **Access/IAM — Fase I-A (wiring administrativo de CompanyUserMembership): backend completado (2026-07-17)** — expone `POST /api/v1/admin/iam/memberships` (alta/edición de rol, perfil y sucursales autorizadas) y `POST /api/v1/admin/iam/memberships/revoke` (`CompanyUserMembershipsController`), que hasta esta fase no existían pese a que `UpsertCompanyUserMembershipHandler`/`RevokeCompanyUserMembershipHandler` (Fase D) estaban implementados y probados sin ningún consumidor de producción. TenantId/CompanyId nunca viajan en el request — cada Admin command (`UpsertCompanyUserMembershipAdminCommand`/`RevokeCompanyUserMembershipAdminCommand`) los resuelve del contexto autenticado (`ICurrentTenant`/`ICurrentCompany`) y delega íntegramente vía MediatR en los handlers de Fase D, sin reimplementar su lógica. `CompanyUserMembership` sigue siendo la única fuente de verdad de la relación usuario-empresa, `Role`, `ProfileId` e `IsActive` de membresía; `CompanyUserBranch` sigue siendo la única fuente de autorización de sucursal; `CompanyUserPreferences` no se modificó. Reutiliza el permiso `access.company_user_memberships.view` (mismo criterio que `AccessProfilesController`/`CompanyUserPreferencesController`) — no se introdujo un permiso `.manage` nuevo en esta fase. Sin frontend, sin invitaciones, sin cambios a `IdentityUser` ni a su `IsActive` global.
- **Access/IAM — Fase I-B (administración de CompanyUserBranch): backend completado (2026-07-17)** — expone `GET`/`PUT /api/v1/admin/iam/memberships/{membershipId}/branches` (`CompanyUserBranchesController`). `GetCompanyUserBranchesAdminQuery` proyecta las sucursales activas de la empresa de la membresía marcando cuáles están autorizadas (`{branchId, branchName, authorized}`), pensado para que un futuro selector de frontend lo consuma directamente. `UpdateCompanyUserBranchesAdminCommand` reemplaza la autorización completa todo-o-nada (ninguna escritura ocurre si cualquier `BranchId` es inválido): reactiva/crea las solicitadas, desactiva el resto — `CompanyUserBranch` sigue siendo la única fuente de verdad de sucursales autorizadas, nunca se copia a `Membership`/`Preferences`/`IdentityUser`. Hallazgo de auditoría: `IBranchRepository.GetAsync`/`GetByIdAsync` solo filtran por `TenantId` (no por `CompanyId`, a diferencia de entidades con `ForOperationalScope`) — ambos handlers filtran/comparan `Branch.CompanyId` manualmente contra la empresa de la membresía antes de aceptar cualquier sucursal, y usan el mismo mensaje para "no existe" y "pertenece a otra empresa" (mismo criterio anti-enumeración que `GetCompanyUserPreferencesAdminHandler`). Decisión documentada: lista vacía es un valor válido (revoca todas las sucursales sin desactivar la membresía) — es seguro porque `CompanyUserPreferencesLoginResolver` (Fase E) ya revalida `DefaultBranchId` en cada login y falla con `ValidationFailure` controlado si dejó de estar autorizado, nunca asumió que hubiera siempre al menos una sucursal activa. Reutiliza `access.company_user_memberships.view` — sin permiso nuevo. Sin frontend, sin cambios a `CompanyUserPreferences`/`IdentityUser`/JWT.
- **Access/IAM — Fase I-C (pantalla administrativa de usuarios empresariales): completado (2026-07-17)** — reemplaza el placeholder `/admin/users` (antes un `<Navigate>` a `/admin/roles`) por `UsersPage` (`frontend/src/modules/access/users/`), que administra `CompanyUserMembership` end-to-end: tabla principal (Usuario/Email/Perfil/Role/Estado/Sucursales autorizadas/Modo de ingreso/Acciones), modal de alta/edición de membership (`membershipService.upsertMembership`, nunca crea `IdentityUser`), modal de sucursales autorizadas (`branchAssignmentService`, Fase I-B — el frontend nunca valida pertenencia/activa/autorización previa, solo envía los `BranchId` marcados), modal de preferencias de login (reutiliza 100% el schema/servicio de Fase G, sin extraer un componente compartido con `SecuritySettingsPage` para no tocar ese ciclo ya cerrado) y revocación con confirmación vía `message.confirm` (`lib/messages`, API pública oficial). Bloqueo real detectado y resuelto: no existía ningún endpoint que listara `CompanyUserMembership` con inactivas + `ProfileName` (`GET /api/v1/security/admin-matrix`, Fase B, solo devuelve `IdentityUser` activos sin perfil) — se agregó `GET /api/v1/admin/iam/memberships` (`GetCompanyUserMembershipsAdminQuery`, solo lectura, junta `CompanyUserMembership`+`IdentityUser`+`AccessProfile`, todos ya expuestos individualmente) reutilizando `access.company_user_memberships.view`, sin permiso nuevo. Limitación conocida y documentada en código: "Sucursales autorizadas"/"Modo de ingreso" por fila se resuelven con `Promise.allSettled` por membership (sin endpoint de resumen agregado) — aceptable al volumen típico de usuarios por empresa, candidato a un endpoint agregado en una fase futura si escala. Sin invitaciones, sin cambios a `IdentityUser`/JWT/`CompanyUserPreferences`.
- **Access/IAM — Fase S1 (Security Hardening): completado (2026-07-17)** — corrige los 3 hallazgos críticos/altos de la auditoría de cierre de Access/IAM, sin agregar funcionalidad ni tocar JWT/frontend/otros módulos:
  - **5A** — `POST /api/v1/auth/register` **eliminado**. Permitía crear un usuario (con `Role` arbitrario, incl. `Admin`) en cualquier tenant existente indicando `TenantId` en el body, sin ningún control de identidad. El alta del primer usuario/tenant ya tenía un flujo seguro y dedicado (`SetupController` → `CreateInitialAdminCommand`, token de instalación de un solo uso generado por consola, nunca acepta `TenantId`/`Role` del cliente) — confirmado sin consumidor alguno en frontend antes de eliminar. `RegisterCommand`/`RegisterHandler`/`RegisterCommandValidator`/`RegisterDto` eliminados.
  - **5B** — `POST /api/v1/auth/password-reset` **eliminado**. Cambiaba la contraseña de cualquier usuario solo con `TenantId`+`Email`, sin contraseña actual, token ni OTP. El flujo oficial (`ForgotPassword` + `ResetPasswordWithToken`, token de un solo uso por email) queda como único camino. `DirectPasswordResetCommand`/`Handler`/`Validator` eliminados. Su único consumidor frontend (`PasswordResetPage.tsx`, página pública en `/password-reset`) se eliminó en el cierre final del módulo (ver entrada siguiente) — no quedan referencias vivas al flujo eliminado.
  - **5C** — `GetCompanyUserMembershipsAdminQuery`, `GetCompanyUserPreferencesAdminQuery`, `UpdateCompanyUserPreferencesAdminCommand`, `GetCompanyUserBranchesAdminQuery`, `UpdateCompanyUserBranchesAdminCommand` ahora implementan `IRequiresCompanyContext` — mismo marker que `UpsertCompanyUserMembershipAdminCommand`/`RevokeCompanyUserMembershipAdminCommand` (Fase I-A), sin inventar un mecanismo nuevo. Antes, su única defensa era comparar manualmente contra `ICurrentCompany.CompanyId` (header `X-Company-Id`, no un claim firmado), sin pasar por `ICompanyAccessGuard` — un caller con rol Admin de su propio tenant podía leer/escribir memberships, sucursales y preferencias de una empresa ajena manipulando el header, porque el bypass de rol Admin (`RuntimePermissionAuthorizer`) nunca revalidaba tenant/membership real. El marker fuerza `CompanyScopeBehavior` → `ICompanyAccessGuard.RequireCurrentCompanyAsync` antes del handler; el chequeo manual original se mantiene como defensa adicional.
  - Tests nuevos: `ERP.Architecture.Tests/AuthAttackSurfaceGuardTests.cs` (CI-bloqueante, impide reintroducir 5A/5B), `ERP.API.Tests/Auth/AuthControllerTests.cs`, `ERP.Application.Tests/Setup/CreateInitialAdminHandlerTests.cs` (prueba que el flujo alternativo seguro sigue funcionando), `ERP.Application.Tests/Behaviors/CompanyScopeBehaviorTests.cs` + `ERP.Application.Tests/Access/CompanyScopeMarkerConsistencyTests.cs` (prueban el mecanismo de 5C y que los 5 handlers corregidos usan el mismo patrón que Fase I-A).
  - **Módulo Access/IAM: apto para producción** en lo referente a estos 3 hallazgos. Deuda no crítica restante documentada en la auditoría de cierre (naming, duplicación de UI en modal de preferencias, etc.) — ver entrada de cierre final más abajo para lo que sí se resolvió en la limpieza posterior.
- **Access/IAM — Cierre final del módulo (limpieza de deuda técnica menor): completado (2026-07-17)** — módulo declarado terminado y cerrado a mantenimiento únicamente. Sin funcionalidad nueva, sin endpoints nuevos, sin cambios de comportamiento ni de contrato HTTP/BD. Alcance:
  - **Código muerto eliminado**: `PasswordResetPage.tsx`/`.css` y `passwordResetSchema.ts` (frontend, único consumidor de `POST /auth/password-reset`, eliminado en Fase S1 — la página había quedado sin backend detrás); ruta `/password-reset` retirada de `publicRoutes.tsx`; entradas `/api/v1/auth/register` y `/api/v1/auth/password-reset` retiradas de `PUBLIC_AUTH_PATHS` (`authRefreshPolicy.ts`, rutas ya inexistentes); 7 claves i18n huérfanas (`reset.title`, `reset.subtitle`, `reset.directSubtitle`, `reset.error.disabled`, `reset.error.mismatch`, `reset.subscriberCheck.enabled/unavailable`) retiradas de `es/en/qu.json`; `RegisterDto` (backend, ya sin uso desde antes de Fase S1) eliminado.
  - **Naming corregido (solo archivos, sin tocar clases/namespaces/contratos)**: `Entities/Membership.cs` → `CompanyUserMembership.cs` (la clase ya se llamaba así); carpetas `UseCases/UpsertMembership`/`RevokeMembership` → `UpsertCompanyUserMembership`/`RevokeCompanyUserMembership` (ya coincidían con el namespace, no con el nombre de carpeta); los 6 archivos `Upsert/RevokeMembership{Command,CommandValidator,Handler}.cs` dentro renombrados a `Upsert/RevokeCompanyUserMembership{Command,CommandValidator,Handler}.cs` (las clases ya tenían el nombre completo).
  - **No se encontró** ningún Command/Query/DTO/validator/servicio registrado sin consumidor en Access/IAM más allá de lo ya listado — confirmado por auditoría previa y revalidado en esta fase.
- **ADR-026 (Accounting Core Architecture): ACCEPTED (2026-07-24)** — diseño arquitectónico aprobado por Architecture Review Board (`docs/decisions/ADR-026-accounting-core.md`): bounded context (`Account`/`AccountingPeriod`/`JournalEntry`/`PostingRule`), `CompanyId`-scoped obligatorio en los 4 aggregates, integración exclusivamente vía Domain Events (sin dependencias directas hacia Sales/Purchases), `JournalEntrySequence` independiente de `IDocumentSequenceRepository` (ADR-019), alcance v1 limitado a Sales/Purchases/Caja/Inventory.
  - **Fase 0 (housekeeping, 2026-07-24)**: eliminado `ERP.Application/Common/Interfaces/IAccountingService.cs` (dead code confirmado — cero implementaciones, cero consumidores).
  - **Fase 1 — Fundamentos de dominio (2026-07-24)**: `Account`/`AccountingPeriod`/`PostingRule` con comportamiento completo (`Create`, `Rename`, `Activate`/`Disable`/`Enable`, `Close`, `Lock`, `UpdateMapping`); `JournalEntry` como esqueleto de identidad únicamente (sin líneas, sin `Post()`/`Reverse()` — explícitamente fuera de esta fase). VO `AccountCode`, enums `AccountType`/`AccountNature`/`PeriodStatus`/`JournalEntryStatus`. 7 domain events (`AccountCreatedEvent`/`AccountActivatedEvent`/`AccountDisabledEvent`/`AccountingPeriodCreatedEvent`/`AccountingPeriodClosedEvent`/`AccountingPeriodLockedEvent`/`PostingRuleCreatedEvent`).
  - **Fase 1.2/1.3/1.4 — Persistencia (2026-07-25)**: 4 configuraciones EF Core, 4 tablas (`accounts`, `accounting_periods`, `journal_entries`, `posting_rules`), 3 índices únicos (`uq_accounts_company_code`, `uq_accounting_periods_company_year_period`, `uq_posting_rules_company_source_fact`) + 1 FK (`journal_entries.accounting_period_id → accounting_periods.id`, `RESTRICT`). Migración `20260725000917_AddAccountingCoreFoundations` **aplicada** en desarrollo, auditada por Database Migration Review Board — `ACCEPTED`.
  - **Fase 2.0/2.1/2.2 — Application + API (2026-07-25)**: 4 repositorios (`IAccountRepository`/`IAccountingPeriodRepository`/`IJournalEntryRepository`/`IPostingRuleRepository`) con filtrado `TenantId`+`CompanyId` en toda consulta; 11 Commands + 6 Queries + 11 Validators FluentValidation + 17 Handlers (patrón CQRS/MediatR, sin ningún `AccountingService`/`AccountService`/`PostingRuleService`); concurrencia con patrón pre-check → `SaveChanges` → `IDatabaseExceptionTranslator` en los 3 Commands de creación; permisos `accounting.view/create/update/delete`; `AccountingController` (`api/v1/accounting`) con 14 endpoints REST (6 GET, 3 POST, 5 PATCH, sin `DELETE` — baja lógica vía `PATCH .../disable`). Auditado por Architecture Review Board (Auditoría Final de Implementación) — `APPROVED WITH MINOR CHANGES` (hallazgo de documentación ya resuelto con esta entrada; longitudes de validación duplicadas entre `Validator`/EF `Configuration` sin constante compartida queda como deuda menor no bloqueante).
  - **Explícitamente NO implementado hasta Fase 2.2**: Posting Engine (ADR-026 §8), `JournalEntryLine`/partida doble, `Post()`/`Reverse()`, numeración `JournalEntrySequence` (ADR-026 §7), integración vía eventos con Sales/Purchases/Caja/Inventory, reportes financieros. `JournalEntry` no tenía ningún endpoint ni caso de uso — solo existía como tabla y aggregate de identidad.
  - **Fase 3.1 — Posting Engine inicial (2026-07-25)**: `ERP.Application/Modules/Accounting/Posting/` — `IPostingEngine.PostAsync(PostingFact, ct)` como único contrato público (`PostingFact`: `TenantId`/`CompanyId`/`SourceModule`/`FactType`/`SourceEventId`/`EntryDate`, sin Currency/Amount/Lines/impuestos — fuera de esta fase). Pipeline interno fijo (Idempotency → PostingRuleResolver → PostingPeriodResolver → PostingPeriodGuard → JournalFactory → JournalValidator → Persistencia), componentes `internal` sin registro propio en DI — solo `IPostingEngine → PostingEngine` se registra. `PostingOutcomeDto`/`PostingOutcomeStatus` (`Created`/`AlreadyProcessed` — reintento del mismo hecho **es éxito**, nunca `Conflict`). Códigos de error: `RULE_NOT_FOUND`, `PERIOD_NOT_OPEN`, `VALIDATION_FAILED`. `JournalFactory` construye vía `JournalEntry.Create()` (sin DTO intermedio) con `SystemActor = Guid.Empty` (mismo patrón que `ExpireUserSessionsHandler`) y descripción determinística `"{SourceModule} — {FactType} — {SourceEventId}"`. `JournalValidator` es NO-OP documentado (partida doble aún no existe). Idempotencia real: `IJournalEntryRepository.FindByKeyAsync` + índice único `uq_journal_entries_company_source_event_fact` (`company_id`, `source_module`, `source_event_id`, `source_event_type`) — reemplaza el índice no-único anterior (migración `20260725013347_AddJournalEntryIdempotencyKey`); en carrera, `IDatabaseExceptionTranslator` traduce la violación UNIQUE y la segunda ejecución re-consulta y retorna `AlreadyProcessed`. `IAccountingPeriodRepository.FindContainingDateAsync` agregado para resolución de período por fecha. Tests: 4 unitarios (`ERP.Application.Tests/Accounting/PostingEngineTests.cs` — RuleNotFound/PeriodNotOpen/Created/AlreadyProcessed, mocks) + 2 de integración PostgreSQL real vía Testcontainers (`ERP.Infrastructure.Tests/Accounting/PostingEngineIntegrationTests.cs` — doble ejecución secuencial idempotente, concurrencia real con dos tareas paralelas verificando un único `JournalEntry`). **Pendiente al cierre de Fase 3.1**: `PostingRule.IsActive == false` no se validaba — resuelto en Fase 3.3 (ver abajo). `JournalEntryLine`/partida doble, `Post()`/`Reverse()`, numeración `JournalEntrySequence`, endpoints HTTP del Posting Engine y reportes financieros siguen sin implementar.
  - **Fase 3.3 — Primer consumidor real: SalesInvoiceAuthorizedPostingTranslator (2026-07-25)**: `ERP.Application/Modules/Accounting/Posting/Translators/SalesInvoiceAuthorizedPostingTranslator.cs` — `INotificationHandler<SalesInvoiceAuthorizedEvent>`, dependencias únicamente `IPostingEngine`+`ILogger<T>` (sin `DbContext`, sin repositorios de Sales), construye `PostingFact{ SourceModule="Sales", FactType="InvoiceIssued" }` y llama `PostAsync`; si falla, `LogWarning` con `InvoiceId`/`InvoiceNumber`/`Code`/`Error` y **no lanza excepción** — la autorización de la venta nunca se revierte por un problema de configuración contable. `SalesInvoiceAuthorizedEvent` enriquecido con `CompanyId`/`IssueDate` y `TenantId` ahora fijado en el constructor (antes quedaba siempre `null` — defecto real detectado y corregido, no solo teórico); los 3 datos se toman del propio agregado `SalesInvoice` en `Authorize()`, sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany` ambiente. `PostingRuleResolver` ahora trata `PostingRule` inactiva igual que regla inexistente (`RULE_NOT_FOUND`, sin código nuevo) — el filtro vive en el Resolver (Application), no en `IPostingRuleRepository.FindByKeyAsync` (compartido con `CreatePostingRuleHandler`, que sigue necesitando ver reglas inactivas para su pre-check de duplicados). Tests: 4 unitarios (`ERP.Application.Tests/Accounting/SalesInvoiceAuthorizedPostingTranslatorTests.cs`, mocks) + 3 de integración PostgreSQL real vía Testcontainers + contenedor DI real con `AddMediatR`/escaneo de ensamblado (`ERP.Infrastructure.Tests/Accounting/SalesInvoiceAuthorizedPostingIntegrationTests.cs`).
  - **✅ Hallazgo crítico de Fase 3.3 — RESUELTO (Fase 3.3.5, 2026-07-25)**: la re-entrancia de `SaveChangesAsync` detectada al conectar el primer Translator (`PostingPipeline` llamaba a `IJournalEntryRepository.SaveChangesAsync()` desde dentro de `ErpDbContext.SaveChangesAsync`, produciendo `DbUpdateConcurrencyException` real cuando coexistía con el handler de Caja sobre el mismo evento) quedó corregida con dos cambios: (1) `PostingPipeline` ya no comitea — solo hace `AddAsync` (staging) y retorna; la persistencia física pertenece exclusivamente al ciclo externo de `ErpDbContext.SaveChangesAsync`, misma convención que ya seguían `SalesInvoiceAuthorizedHandler` (Caja) y los `*AuditHandler`. (2) `IJournalEntryRepository.AcquireIdempotencyLockAsync(companyId, sourceModule, sourceEventId, factType, ct)` — nuevo método, implementado en `JournalEntryRepository` con `pg_advisory_xact_lock(int4, int4)` (mismo mecanismo que `DocumentSequenceRepository`/ADR-019, `StableHash` duplicado deliberadamente sin helper compartido), invocado por `PostingIdempotencyGuard` **antes** de `FindByKeyAsync`, sobre la transacción ambiente (nunca abre ni comitea transacción propia). Con el lock, dos ejecuciones concurrentes para la misma clave se serializan antes de competir por el mismo `INSERT` — la violación UNIQUE deja de ocurrir en el camino normal (el índice `uq_journal_entries_company_source_event_fact` queda como protección final, no como mecanismo primario). El stub de `ICashSessionRepository` en los tests de integración fue retirado — la suite corre con el repositorio real. Se agregó además un test de doble publicación concurrente del mismo `SalesInvoiceAuthorizedEvent` (Caja + Accounting reaccionando simultáneamente en dos transacciones distintas) que confirma ausencia de excepción y un único `JournalEntry`. Detalle completo del proceso de diseño: revisiones ARB Fase 3.3.1 (SaveChanges ownership) a 3.3.4 (readiness review). Habilitado conectar un segundo Translator (Purchases) con este mismo patrón.
  - **Fase 3.4 — Segundo consumidor real: PurchaseInvoiceConfirmedPostingTranslator (2026-07-25)**: replica exactamente el patrón de Fase 3.3 sobre `PurchaseInvoice.Confirm()`. `PurchaseInvoiceConfirmedEvent` enriquecido de forma aditiva con `CompanyId`/`IssueDate` (tomados del propio agregado en `Confirm()`, sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany` ambiente) — único consumidor preexistente del evento (`PurchaseInvoiceAuditHandler`, Entity Audit ADR-022) no requirió cambios, es aditivo. `ERP.Application/Modules/Accounting/Posting/Translators/PurchaseInvoiceConfirmedPostingTranslator.cs` — `INotificationHandler<PurchaseInvoiceConfirmedEvent>`, dependencias únicamente `IPostingEngine`+`ILogger<T>`, construye `PostingFact{ SourceModule="Purchases", FactType="InvoiceReceived" }` y llama `PostAsync`; si falla, `LogWarning` y **no lanza excepción** — la confirmación de la compra nunca se revierte por un problema de configuración contable. `PostingPipeline`/`PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalFactory`/`JournalValidator`/`JournalEntryRepository` — sin ningún cambio (mismo Posting Engine, ningún `SaveChangesAsync`/transacción/lock nuevo). Tests: 4 unitarios (`ERP.Application.Tests/Accounting/PurchaseInvoiceConfirmedPostingTranslatorTests.cs`, mocks) + 4 de integración PostgreSQL real vía Testcontainers + contenedor DI real con `AddMediatR`/escaneo de ensamblado (`ERP.Infrastructure.Tests/Accounting/PurchaseInvoiceConfirmedPostingIntegrationTests.cs` — JournalEntry Draft, fallo sin revertir, idempotencia, concurrencia con advisory lock). Retenciones (`IssuedWithholding`) quedan explícitamente fuera de alcance — hecho contable distinto, Translator futuro si se requiere.
  - **Fase 3.5.2 — PostingFact Enrichment, cierre de ADR-026 §4 (2026-07-25)**: prerrequisito para el futuro motor de partida doble (`JournalEntryLine`, diseñado en Fase 3.5.1, aún no implementado). `SalesInvoiceAuthorizedEvent` y `PurchaseInvoiceConfirmedEvent` enriquecidos de forma aditiva con `Subtotal`/`TotalVat`/`TotalIce`/`TotalDiscount` — tomados de las propiedades ya computadas del propio agregado (`SalesInvoice.Subtotal/TotalVat/TotalIce/TotalDiscount` en `Authorize()`, `PurchaseInvoice.Subtotal/TotalVat/TotalIce/TotalDiscount` en `Confirm()`), sin releer por repositorio ni depender de `ICurrentTenant`/`ICurrentCompany`. `PostingFact` extendido con los mismos 4 campos más `GrandTotal` — deliberadamente **sin** `Currency`/`ExchangeRate`/`Branch`/`CostCenter`/`Metadata` (fuera de alcance v1 por ADR-026 §10 y por ausencia de módulo `CostCenter`, ver Fase 3.5.1). `SalesInvoiceAuthorizedPostingTranslator`/`PurchaseInvoiceConfirmedPostingTranslator` actualizados únicamente en la construcción de `PostingFact` (una línea cada uno) — sin cambio de patrón, dependencias ni manejo de errores. Posting Engine (`PostingPipeline`/`PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalFactory`/`JournalValidator`/`JournalEntryRepository`) sin ningún cambio — los montos nuevos viajan en `PostingFact` pero `JournalFactory` todavía no los consume (eso pertenece a la fase de `JournalEntryLine`). Compatibilidad: 10 call sites de construcción de `SalesInvoiceAuthorizedEvent`/`PurchaseInvoiceConfirmedEvent`/`PostingFact` en código productivo y tests actualizados; regresión completa en verde (452 `ERP.Application.Tests`, 254 `ERP.Domain.Tests`, 97 `ERP.Architecture.Tests`, 10 de integración PostgreSQL real en `ERP.Infrastructure.Tests/Accounting`). ADR-026 §4 queda implementado en su parte de montos (`Subtotal`/`TotalVat`/`TotalIce`/`TotalDiscount`, alcance exacto de esta fase); **pendiente** el otro requisito original de §4 para `SalesInvoiceAuthorizedEvent` — *"información de pago necesaria para la contabilización (forma de pago / referencia de cobro)"* — no incluido en el alcance aprobado de Fase 3.5.2, queda para una fase posterior o para reevaluación explícita si el motor de partida doble no lo necesita.
  - **Fase 3.5.3 — Modelo de dominio de partida doble (2026-07-25)**: implementa únicamente el modelo de dominio aprobado en Fase 3.5.1 — sin persistencia EF Core, sin migración, sin cambios en `JournalFactory`/`JournalValidator`/`PostingPipeline`/`PostingEngine`. `JournalEntryLine` (nueva entidad hija de `JournalEntry`, `ERP.Domain/Modules/Accounting/Entities/`) con invariante propio: exactamente uno de `Debit`/`Credit` mayor a cero, nunca ambos con valor ni ambos en cero (`JournalEntryLine.Create`, `IMustHaveTenant`, sin `CompanyId` propio — igual patrón que `PurchaseInvoiceDetail`/`SalesInvoiceDetail`). `JournalEntry` incorpora `Lines` (`IReadOnlyCollection<JournalEntryLine>`), `AddLine(accountId, description, debit, credit)` (construye la línea internamente, asigna `SortOrder` incremental) y `EnsureBalanced()` (Σ Debit == Σ Credit) — ninguno con consumidor todavía: `JournalFactory` sigue construyendo solo el encabezado (0 líneas), por lo que `EnsureBalanced()` se cumple trivialmente (0 == 0) sin invocarse desde ningún flujo real. `PostingRuleLine` (nueva entidad hija de `PostingRule`) con `AccountId`/`Nature` (`AccountNature`, reutilizado)/`AmountKind` (`PostingAmountKind`, enum nuevo)/`SortOrder`. `PostingRule` incorpora `Lines` + `AddLine(...)` — coexiste con `DebitAccountId`/`CreditAccountId` planos sin retirarlos (transición, ningún consumidor migra todavía). `PostingAmountKind` (`Subtotal`/`TaxVat`/`TaxIce`/`Discount`/`Retention`/`GrandTotal`) — únicos 6 valores aprobados en Fase 3.5.1, ninguno adicional. Hallazgo de compatibilidad EF Core resuelto: `JournalEntry.Lines`/`PostingRule.Lines` son navegaciones nuevas que `RelationshipDiscoveryConvention` detecta y registra como entidades independientes con tabla propia aunque se las ignore a nivel de propiedad (`builder.Ignore(x => x.Lines)` en cada `IEntityTypeConfiguration` no basta) — requiere además `modelBuilder.Ignore<JournalEntryLine>()`/`Ignore<PostingRuleLine>()` a nivel de `ErpDbContext.OnModelCreating()` para que el modelo runtime siga coincidiendo exactamente con la migración ya aplicada (`dotnet ef migrations has-pending-model-changes` verificado en `No changes`). Tests: 24 nuevos en `ERP.Domain.Tests/Accounting/` (`JournalEntryLineTests`, `JournalEntryTests`, `PostingRuleLineTests`) — Debit/Credit válidos, ambos con valor, ambos en cero, montos negativos, cuenta vacía, creación con líneas, `SortOrder` incremental, colección de solo lectura, `EnsureBalanced()` con/sin líneas balanceadas y desbalanceadas, naturaleza y `AmountKind` correctos. Regresión completa en verde: 278 `ERP.Domain.Tests` (254+24), 452 `ERP.Application.Tests`, 97 `ERP.Architecture.Tests`, 219 `ERP.Infrastructure.Tests` (incluye las 10 suites de integración PostgreSQL de Accounting ya existentes, sin cambios de comportamiento).
  - **Fase 3.5.4 — Persistencia de JournalEntryLine y PostingRuleLine (2026-07-25)**: única y exclusivamente la capa de persistencia del modelo aprobado en Fase 3.5.3 — sin cambios en `JournalFactory`/`JournalValidator`/`PostingPipeline`/`PostingEngine`, sin generación automática de líneas, sin consumo de `PostingAmountKind`. `JournalEntryLineConfiguration`/`PostingRuleLineConfiguration` (`ERP.Infrastructure/Accounting/Persistence/Configurations/`) nuevas — `journal_entry_lines`/`posting_rule_lines`, `Debit`/`Credit` en `numeric(18,2)` (Estándar de Precisión Numérica INMUTABLE, CLAUDE.md). `JournalEntryLine.AccountId` con FK real a `accounts` (`Restrict`) — a diferencia de `PostingRuleLine.AccountId`, columna plana sin FK (mismo criterio ya vigente para `PostingRule.DebitAccountId`/`CreditAccountId`: configuración de datos, existencia se valida en Application al resolver, no en la base de datos). `JournalEntryConfiguration`/`PostingRuleConfiguration`: `Ignore(x => x.Lines)` reemplazado por `HasMany(x => x.Lines).WithOne().HasForeignKey(...).OnDelete(Cascade)` (mismo patrón que `PurchaseInvoice`→`PurchaseInvoiceDetail`) — cascade porque ninguna línea tiene sentido de existir sin su encabezado. `ErpDbContext`: retirados los dos `modelBuilder.Ignore<T>()` de Fase 3.5.3 (ya no aplican, las líneas ahora se mapean), agregados `DbSet<JournalEntryLine>`/`DbSet<PostingRuleLine>`. Migración `20260725165737_AddJournalEntryLineAndPostingRuleLine` — crea ambas tablas, 2 FKs (`journal_entry_lines→accounts` Restrict, `journal_entry_lines→journal_entries` Cascade, `posting_rule_lines→posting_rules` Cascade), 4 índices; no toca ninguna columna existente de `posting_rules` (`DebitAccountId`/`CreditAccountId` intactos, coexistencia deliberada durante la transición). Verificado `dotnet ef migrations has-pending-model-changes` → `No changes`. Tests: 8 nuevos de persistencia PostgreSQL real vía Testcontainers (`ERP.Infrastructure.Tests/Accounting/JournalEntryLinePersistenceTests.cs`, `PostingRuleLinePersistenceTests.cs`) — guardar con líneas, recuperar navegación (`Include(x => x.Lines)`), integridad referencial (FK real en `JournalEntryLine` vs. ausencia deliberada de FK en `PostingRuleLine`), cascade delete de líneas al eliminar el encabezado. Regresión completa en verde: 278 `ERP.Domain.Tests`, 452 `ERP.Application.Tests`, 97 `ERP.Architecture.Tests`, 227 `ERP.Infrastructure.Tests` (18 en `Accounting/`, incluye las 10 suites de Sales/Purchases/PostingEngine ya existentes sin cambio de comportamiento).
  - **Fase 3.5.5 — JournalFactory & JournalValidator: motor de partida doble real (2026-07-25)**: `JournalFactory` deja de construir solo el encabezado — ahora itera `PostingRule.Lines` (`PostingRuleLine`, persistido en Fase 3.5.4), resuelve el monto de cada línea exclusivamente por `PostingAmountKind` (`Subtotal→fact.Subtotal`, `TaxVat→fact.TotalVat`, `TaxIce→fact.TotalIce`, `Discount→fact.TotalDiscount`, `GrandTotal→fact.GrandTotal`, `Retention→0m` — no disponible en `PostingFact` todavía, fuera de alcance de esta fase) y llama `JournalEntry.AddLine(...)` por cada línea con monto distinto de cero (líneas en cero se omiten, nunca se contabilizan). `JournalValidator` deja de ser NO-OP: valida mínimo 2 líneas, `AccountId` requerido, exactamente un monto (Débito o Crédito) por línea, ninguna cuenta simultáneamente en Débito y Crédito del mismo asiento, totales distintos de cero, y balance (`entry.EnsureBalanced()`, código `VALIDATION_FAILED` en cualquier fallo). **2 excepciones mínimas y necesarias, declaradas explícitamente**: (1) `PostingPipeline.ExecuteAsync` — una línea agrega el parámetro `PostingRule` ya resuelto a la llamada de `JournalFactory.Create(...)` (el orden de las 7 etapas no cambia, solo se propaga un dato ya calculado); (2) `PostingRuleRepository.FindByKeyAsync` — agrega `.Include(x => x.Lines)`, sin el cual `PostingRule.Lines` llegaría siempre vacío a `PostingRuleResolver` (`PostingRule` es `sealed` sin navegación `virtual`, no hay lazy loading posible). `PostingEngine`/`PostingIdempotencyGuard`/`PostingRuleResolver`/`PostingPeriodResolver`/`PostingPeriodGuard`/`JournalEntryRepository`/`Translators`/`PostingFact`/Domain Events sin ningún otro cambio. Compatibilidad: las 3 suites de integración PostgreSQL ya existentes (`PostingEngineIntegrationTests`, `SalesInvoiceAuthorizedPostingIntegrationTests`, `PurchaseInvoiceConfirmedPostingIntegrationTests`) actualizaron su `SeedRuleAndPeriodAsync` para sembrar `Account`s reales + `PostingRuleLine`s balanceadas (antes sembraban solo `DebitAccountId`/`CreditAccountId` legacy, sin `Lines` — habrían producido asientos de 0 líneas, rechazados por el nuevo `JournalValidator`). Tests: 12 unitarios nuevos (`ERP.Application.Tests/Accounting/JournalFactoryTests.cs`, `JournalValidatorTests.cs` — ejercidos indirectamente vía `PostingEngine.PostAsync` con repositorios mockeados, ya que `JournalFactory`/`JournalValidator` son `internal` sin `InternalsVisibleTo`, sin precedente de ese patrón en el proyecto) + 2 de integración PostgreSQL real nuevos en `PostingEngineIntegrationTests.cs` (persistencia de `JournalEntry` con `JournalEntryLine`, recuperación completa del agregado con balance verificado). Riesgo documentado: "cuentas existentes"/"cuentas activas" no se validan en `JournalValidator` (fuera del alcance aprobado para esta fase) — hoy solo protegidas por la FK real de `JournalEntryLine.AccountId` a nivel de base de datos, que falla como `DbUpdateException` no como `Result` limpio. Regresión completa en verde: 278 `ERP.Domain.Tests`, 464 `ERP.Application.Tests` (452+12), 97 `ERP.Architecture.Tests`, 229 `ERP.Infrastructure.Tests` (20 en `Accounting/`).
- **P0-01 — Devolución de Venta (SalesReturn) + Nota de Crédito SRI: COMPLETED / CLOSED (2026-07-31)** — módulo cerrado formalmente de punta a punta, sin código productivo pendiente. Diseño: [`P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md`](docs/archive/designs/P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md). Plan de ejecución por fases (1-15, todas cerradas) y backlog técnico no bloqueante: [`P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`](docs/archive/plans/P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md). Activación de Nota de Crédito v1.1.0: [`docs/decisions/ADR-031-credit-note-v1-activation.md`](docs/decisions/ADR-031-credit-note-v1-activation.md) (Accepted).
  - **Capacidades entregadas:** `SalesReturn`/`SalesReturnDetail`/`SalesReturnRefundAllocation` (Domain); devolución parcial y total sobre una `SalesInvoice` `Authorized`; ciclo Draft → Update → Cancel → Authorize; control de remanente devolvible bajo concurrencia real (advisory lock por factura + revalidación bajo lock, cierre de la ventana de condición de carrera que el chequeo preventivo del Draft no podía cerrar por sí solo); reversión de inventario (Kardex, `StockMovementType.SaleReturn`) al autorizar; reembolso explícito sin prorrateo automático — Efectivo / Crédito CxC / mixto (`SalesReturnRefundAllocation`, `Σ Amount == GrandTotal` como invariante de dominio); asiento contable automático vía `SalesReturnAuthorizedPostingTranslator` (mismo Posting Engine que Factura/Compra, ADR-026); Entity Audit (`SalesReturnAudit`, ADR-022); Nota de Crédito electrónica SRI V1.1.0 (XML, validación XSD, firma XAdES-BES, secuencial "04" vía `IDocumentSequenceRepository`, envío/autorización) activada por ADR-031; RIDE de Nota de Crédito; API REST documentada (`SalesReturnController`, `api/v1/sales/returns`); frontend completo (listado, formulario Draft/Authorize, sección de Nota de Crédito Electrónica); suite E2E de 23/23 escenarios contra PostgreSQL real (`SalesReturnEndToEndTests`).
  - **Mejora de infraestructura registrada junto con el cierre:** `DocumentSequenceRepository.CaptureNextAsync` corregido para participar de una transacción ambiente ya abierta por el caller (defecto real detectado durante el cierre de P0-01) — sin cambio de API pública ni de estrategia de locking de la infraestructura FROZEN de Secuencias Documentales (ADR-019).
  - **Pendiente operativo (no bloqueante para el cierre técnico):** prueba real de emisión de Nota de Crédito contra el ambiente de Pruebas del SRI (`celcer.sri.gob.ec`) con certificado `.p12` configurado — no ejecutada en esta fase por no existir certificado de prueba disponible en este entorno (ver ADR-031, sección "Validación de la activación"). Mismo protocolo ya usado para cerrar ADR-023 con Factura (comprobantes reales, rechazo real confirmado) queda pendiente de repetirse para Nota de Crédito cuando haya certificado disponible.
  - **Backlog técnico no bloqueante** (detalle completo en la sección homónima de `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`): wiring de React Hook Form + Zod en el formulario Draft de `SalesReturnFormPage`; unificación de `formatApiError`/`formatApiRequestError` en `SalesReturnCreditNoteSection`; evaluación de la ubicación REST de `GET .../returnable-lines`; consolidación de fixtures de test repetidas en `ERP.Application.Tests/Sales`; constante propia (no heredada de `SalesInvoice`) para la longitud de `CreditNoteDocumentNumber`. Ninguno bloquea el cierre — todos fueron evaluados y descartados de corrección inmediata en la auditoría de hardening previa por implicar refactor o riesgo de cambio de comportamiento fuera de ese alcance.

**Futuro (no implementado, fuera del ERP actual)**
- Plataforma externa — ver [`docs/future-platform/`](./future-platform/)

---

## FASE 1 — ERP Kernel Cleanup — COMPLETE 2026-06-05

> Branch `feat/platform-kernel-refactor`. Todos los componentes SaaS eliminados. Build: **0 errores**.
> Eliminado: Billing domain, Subscriptions domain, Platform entities, Commercial plans, Entitlements,
> SaaS controllers/middleware/jobs/services/behaviors. Tests SaaS eliminados. ERP puro compila limpio.
>
> **FASE 2 — Subscriber → Tenant rename: COMPLETADA (2026-07-23).** JWT claim (`tenant_id`), columna BD (`tenant_id`), DbContext (`ITenantScopedEntity`), frontend (componentes, i18n, navegación) y documentación normativa (`docs/architecture/`) consolidados en `Tenant`.
>
> Deuda cosmética conocida y no bloqueante:
> - nombres de variable/parámetro `subscriber` en código backend.
> - nombres históricos de índices SQL con `_subscriber_`.
>
> La columna física y el aislamiento real usan `tenant_id`. Esta deuda queda pendiente para una limpieza mecánica futura.

---

## ERP CORE FREEZE — GOVERNANCE LOCK ACTIVE (2026-06-08)

> **ERP Core está oficialmente congelado como producto independiente.** Acta completa, módulos incluidos/excluidos, frontera de integración (`/api/integration/v1/*`, [ADR-ERP-002](adr/ADR-ERP-002-platform-separation.md)) y reglas obligatorias (*ERP never depends on Platform* / *Platform may consume ERP APIs only*) en [`ERP_CORE_FREEZE.md`](../ERP_CORE_FREEZE.md).

## ERP CORE BASELINE v1.0 — FROZEN 2026-06-05

> Architecture frozen. Changes to any module below require an Architecture Review before implementation.

| Module | Closed | Evidence |
|--------|:------:|----------|
| BusinessPartner V2 (Customer + Supplier roles) | ✅ | `docs/decisions/ADR-017-business-partner-scope.md` |
| Customer Module | ✅ | BP V2 Customer closed 2026-06-04 |
| Supplier Module | ✅ | BP V2 Supplier closed 2026-06-04 |
| Company Isolation (ICompanyOperationalEntity + EF filters) | ✅ | `docs/security/MULTI-TENANT-HARDENING.md` |
| Security Hardening (CompanyScopeBehavior, namespaced fallback removed) | ✅ | Migration `20260605113654_AddCompanyIdToOperationalEntities` |
| Multi-Tenant Boundaries (all scopes explicit, fail-closed dual filter) | ✅ | `FINAL HARDENING REPORT 2026-06-05` — 0 CRITICAL/HIGH/MEDIUM/LOW issues |

**Test baseline at freeze:** ERP.Application.Tests 190/190 · ERP.API.Tests SecurityTests 33/33 · Build 0 errors.

---

## Documentation map (canonical — `docs/architecture/` + `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md` + docs/ + índices)

| Topic | File |
|-------|------|
| **Implementation rules (canonical)** | `docs/architecture/README.md` |
| Index | `CONTEXT.md` |
| Repo structure (2026-05) | `README.md`, `infrastructure/`, `scripts/`, `tools/` |
| Product summary | `README.md` |
| Agent adapters | `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md`, `.cursor/rules/` → `docs/architecture/*` |
| Delivery state | `STATUS.md` (this file) |
| Priorities | `docs/ROADMAP.md` |
| Architecture | `docs/ARCHITECTURE.md` |
| Architecture rules (PR blocking) | `docs/architecture/pr-rules-catalog.md` (entry: `docs/ARCHITECTURE-RULES.md`) |
| ADRs (architectural rationale) | `docs/decisions/README.md` |
| Development + stack | `docs/DEVELOPMENT.md` |
| Identity + security | `docs/IDENTITY.md` |
| SaaS plans + billing (histórico) | `docs/archive/SAAS-COMMERCIAL.md` |
| Database | `docs/DATABASE.md` |

Consolidated 2026-05-21: former `MULTITENANCY`, `SCOPES`, `SECURITY`, `BILLING`, `DATABASE/*`, etc. merged into the files above. **2026-05-21:** `AI-RULES/` centralized implementation rules for Cursor, Claude and future agents. **2026-08-07 (Bloque 16B):** `AI-RULES/` reorganizado a `docs/architecture/` (SSOT único) + `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md`; contenido original archivado en `docs/decisions/archive-ai-rules/`.

## Módulos FROZEN (arquitectura cerrada)

Los siguientes módulos tienen su arquitectura y modelo de datos cerrados definitivamente.
No se aceptan cambios estructurales sin una ADR aprobada.

| Módulo | Fecha cierre | ADR | Notas |
|--------|:------------:|-----|-------|
| **Business Partners V2** (Clientes / Proveedores) | 2026-06-05 | `docs/decisions/ADR-017-business-partner-scope.md` | subscriber-scoped, Roles (Customer/Supplier), CompanySettings, LegalRepresentativeName, unique index DB |
| **Customer Module** | 2026-06-05 | BP V2 ADR | FROZEN + FREEZE GATE PASS (2026-06-17); 5 ARs, 31+ endpoints, 20 domain events, 38 [Authorize]; UI completa: listado + wizard + detalle + ubicaciones CRUD + contactos CRUD + roles + trading settings; RUC/CI SRI; consumidores: Sales, Quotations, Orders, E-Invoicing, CRM, AR |
| **Supplier Module** | 2026-06-05 | BP V2 ADR | Fiscal + classification, full FROZEN |
| **Company Isolation** | 2026-06-05 | Security Hardening Report | ICompanyOperationalEntity, fail-closed EF filters, PaymentApplication, ArAp/AccountingPeriod scopes |
| **Security Hardening** | 2026-06-05 | Security Hardening Report | CompanyScopeBehavior explicit only, 0 namespace fallback, all APIs fail-closed |
| **Multi-Tenant Boundaries** | 2026-06-05 | Security Hardening Report | 223/223 tests, migration 20260605120243_FinalHardening |
| **SaaS Commercial Flow** | 2026-05-28 | `docs/archive/historical-decisions/SAAS-FREEZE.md` | Plans, Entitlements, Subscription lifecycle |
| **Sucursales** | 2026-06-16 | — | Entidad organizativa (no fiscal); CRUD + soft-disable; ruta `/settings/branches` |
| **Establecimientos SRI** | 2026-06-16 | — | Código SRI único por empresa; BranchId opcional; disable bloqueado si tiene PEs activos; ruta `/settings/establishments` |
| **Puntos de Emisión** | 2026-06-16 | — | Código único por Establecimiento; DocumentSequence automático; ruta `/settings/emission-points` |
| **Items / Catálogo v1.0** | 2026-06-17 | — | 14 entidades, 56 endpoints, 20 validators; tenant-scoped catalog compartido entre companies; 6 catálogos CRUD (Brand, Family, Category, Subcategory, AttributeGroup, AttributeDefinition); Detail page con Variants, Images, Conversions, Substitutes, Packaging; SRI lookups (UOM, VAT, ICE); listo para Inventario, Compras, Ventas, Facturación Electrónica |
| **Sales Invoice + Detail** | 2026-06-24 | — | Aggregate root SalesInvoice + SalesInvoiceDetail; lifecycle Draft→Authorized→Cancelled; freeze contract irreversible (IsFrozen + EnsureDraft); snapshot fiscal (VAT/ICE rates + amounts + names); computed totals no persistidos (LineSubtotal, TaxableBase, TaxInclusiveTotal); AuthorizedSubtotal/GrandTotal congelados al autorizar; ReplaceLines único mutator; DocumentSequence SRI; facturación electrónica (AccessKey, AuthorizationNumber); frontend preview-only (salesCalc.ts); 4 use cases (Draft CRUD, Authorize, Discount, Cancel); FluentValidation; company-scoped + tenant-scoped |
| **Payment Methods + Formas de Cobro** | 2026-06-24 | — | PaymentMethod catálogo dinámico (CRUD+Toggle, multi-tenant, seed 5 métodos). SalesInvoicePayment entidad hija (N pagos por factura, snapshot Code+Name, Amount>0, Reference condicional). Authorize() valida ≥1 pago + Sum==GrandTotal. Sin enums, sin JSONB, sin auto-default. Base definitiva para CxC/Cobros/Caja/Contabilidad |
| **Sales Receivable (CxC deuda)** | 2026-06-25 | — | SalesReceivable + SalesReceivableInstallment. Solo crédito (CreditTermDays>0 o Installments>1). PaidAmount=0 (sin cobros). Cancel cascada desde factura. 2 tablas, 6 índices, 2 endpoints GET. Módulo pasivo: registra deuda, no cobra |
| **Estándar de Precisión Numérica** | 2026-06-25 | — | CLOSED. 73/73 columnas auditadas, 100% compliance. Reglas: [`docs/architecture/data-standards.md`](architecture/data-standards.md) |
| **Estándar de Fechas y Horas** | 2026-06-25 | — | CLOSED. Reglas: [`docs/architecture/data-standards.md`](architecture/data-standards.md) |
| **Infraestructura de Mensajes Visuales** | 2026-06-29 | `docs/decisions/ADR-018-message-infrastructure.md` | API pública `message.*` congelada. Store interno encapsulado. Cola FIFO + deduplicación. 22 tests. ESLint gate activo. |
| **Infraestructura de Secuencias Documentales** | 2026-06-29 | `docs/decisions/ADR-019-document-sequence-infrastructure.md` | CLOSED. 4 gates CI-bloqueantes, suite concurrente 8/8 passing (PostgreSQL 16 real, 500 req simultáneas, 0 duplicados). Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Infraestructura de Entity Tracking (EF Core Change Tracking)** | 2026-06-30 | `docs/decisions/ADR-020-entity-tracking-infrastructure.md` | CLOSED. `ATT-GATE-01` gate CI-bloqueante, 6/6 tests de integración passing (PostgreSQL 16 real, Testcontainers). Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Infraestructura de Valores por Defecto de Facturación** | 2026-07-01 | — | CLOSED. Migrado a `org_settings` (Phase 8, 2026-07-01) — ya no `SriSettings`. Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Infraestructura Tributaria (Tax Infrastructure)** | 2026-07-01 | — | CLOSED. Motor único `ISriTaxResolver`/`sriLookupService.*Rates()`. Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Tipos de Ítem (Item Types)** | 2026-07-04 | — | CLOSED. `ItemTypeDefinition` catálogo tenant-editable, reemplaza el enum fijo `Physical/Service/Digital/Kit/Bundle`. Reglas: [`docs/architecture/frozen-infrastructure.md`](architecture/frozen-infrastructure.md) |
| **Items Administration** | 2026-07-07 | — | Item CRUD (14 entidades hijas: variantes, códigos de proveedor, barcodes, imágenes, conversiones, sustitutos, packaging), pricing base (`Item.BaseSalePrice` SSOT), catálogo de Tipos de Ítem tenant-editable, `ItemAudit` (Entity Audit) sobre `ItemCreatedEvent`/`ItemUpdatedEvent`/`ItemPriceChangedEvent`/`ItemEnabledEvent`/`ItemDisabledEvent`. Deuda técnica documentada (no bloqueante): `ItemVariantAddedEvent`/`ItemVariantDisabledEvent` no implementan `IAuditEvent` — cubrirlos requiere modificar las clases de evento, decisión explícita futura |
| **Pricing Administration** | 2026-07-07 | — | `PriceList` (contenedor + regla general opcional), `PriceListItem` (asignación administrativa ítem↔lista, sin reglas ni precios), `PricingRule` (excepción por ítem, override de la regla general). `PricingResolver`/`PricingCalculation` como única API de resolución de precio neto. Auditoría de dominio completa vía Domain Events: `PriceListAudit` (creación/actualización/activación/desactivación), `PriceListItemAudit` (asignación/activación/desactivación), `PricingRuleAudit` (creación/actualización/activación/desactivación, con old/new tipados). Invariante `PricingRule` requiere `PriceListItem` activa (validado en `SetPricingRuleHandler`/`EnablePricingRuleHandler`) — no existen reglas huérfanas. Pricing no calcula impuestos (frontera con `ISriTaxResolver`/`sriLookupService`). Pricing no soporta `ItemVariantId` (retirado deliberadamente 2026-07-07, ver `PricingRule.cs`). Endpoint legacy `/api/v1/pricing/item-prices` queda explícitamente fuera de este freeze — pendiente del cierre de Compras |
| **Infraestructura de Auditoría por Dominio (Entity Audit)** | 2026-07-07 | `docs/decisions/ADR-022-audit-infrastructure-entity-vs-process.md` | Contratos comunes `AuditRecordBase`/`AuditActor`/`AuditSource`/`IAuditEvent` (Domain) + `IAuditWriter<T>`/`IAuditReader<T>`/`IAuditContext`/`IAuditService` (Application) + `EfAuditWriter<T>`/`EfAuditReader<T>`/`HttpAuditContext`/`AuditService` genéricos (Infrastructure, open-generic en DI). Dispatcher reutiliza domain events + Outbox ya FROZEN (ADR-007/008). Pilotos: `PricingRuleAudit`, `PriceListItemAudit`, `PriceListAudit` (tablas `pricing_rule_audit`, `price_list_item_audit`, `price_list_audit`). Cada dominio nuevo agrega solo su entidad + eventos + handler, sin tocar la infraestructura común. `UserActivity` queda reservada al feed liviano, no a auditoría de negocio tipada. Process Audit (auditoría de procesos sin `EntityId` único — recálculos masivos, cierres, ETL, jobs) queda diseñado y documentado en `docs/architecture/audit-infrastructure.md`, sin implementar: reutilizará el `EntityId` como `ProcessRunId` sintético, sin modificar ningún contrato FROZEN. `UserName` resuelto 2026-07-07: snapshot histórico obligatorio en `AuditActor` (no-nullable, fallback `"Unknown"`), poblado desde claims JWT (`ClaimTypes.Email`/`ClaimTypes.Name`) embebidas al emitir el token en `AccessTokenService` — no de una consulta en vivo. Corregido el mismo día un error de claim (`GivenName` representa solo el nombre, no el nombre completo; se corrigió a `ClaimTypes.Name`, con fallback transitorio de compatibilidad en `CurrentUserService`). `AuditActor` confirmado como único modelo oficial del actor (ampliado additive con `FullName`/`Email`/`RoleName` opcionales) — regla Open/Closed nueva: prohibido agregar columnas de identidad del usuario en las entidades de auditoría de cada dominio. Columna `user_name` migrada a `NOT NULL` (`MakeAuditUserNameRequired`). Deuda técnica restante (no bloquea el freeze del contrato): `Source` hardcodeado a `UserAction` en `HttpAuditContext` (falta contexto para jobs/sistema), `CorrelationId`/`RequestId` sin truncado antes de persistir en `varchar(100)`. |
| **ElectronicDocuments v1.0 (Facturación Electrónica SRI)** — **CIERRE OFICIAL** | 2026-07-11 | `docs/decisions/ADR-023-electronic-documents-v1-closure.md` | Núcleo FROZEN: generación XML, validación XSD, firma XAdES-BES, recepción/autorización SRI (esquema offline), reintentos con backoff (`ElectronicDocumentRetryPolicy`, 5 intentos), Monitor de consulta. Cerrado tras 3 rondas: auditoría de robustez (2 críticos + 3 altos corregidos con evidencia/reproducción — TIMEOUT deadletering prematuro, pipeline sin try/catch, Hangfire sin guard de concurrencia, IDOR Company Scope en retry, 503→409 en carrera de registro), cumplimiento del Anexo Técnico SRI verificado texto por texto contra el PDF oficial (clave de acceso módulo 11 reproducido bit a bit, catálogo `sri_error_code` reescrito con 33 códigos reales), y pruebas reales contra `celcer.sri.gob.ec` (8 comprobantes reales, incluido un rechazo real confirmado con código `[65]`). **Addendum RESP-01 (2026-07-11, causa 2 — bug demostrado)**: reenvío de Recepción ahora trata también los códigos `[43]`/`[45]` (no solo `[70]`) como "ya existe, consultar autorización" en vez de rechazo automático — 2 tests de regresión agregados, ningún contrato modificado. Solo `Invoice` tiene builder/provider/validador activo — CreditNote/DebitNote/ShippingGuide/Retention/PurchaseSettlement tienen XSD/catálogo pero sin implementación (`activeVersion: null`), documentado como límite explícito. Deuda técnica aceptada y no bloqueante (ver ADR-023, sección "Cierre oficial"): búsqueda del Monitor acoplada a Sales, contraseñas de certificado legacy en texto plano, `AVG` en memoria, `GetRetryCandidatesAsync` sin paginación. Cambios futuros al núcleo solo por: cambio obligatorio SRI, bug demostrado, vulnerabilidad de seguridad, o rendimiento crítico. |
| **Infraestructura de Diagnóstico SRI reutilizable** | 2026-07-11 | `docs/decisions/ADR-024-electronic-document-diagnostic-infrastructure.md` | Extensión aditiva y controlada de ADR-023 (causa 1: campo real de la Ficha Técnica, `<mensaje>/<tipo>`, descartado silenciosamente). `SriMessage` (Domain value object) capturado por `SriSoapClient` en paralelo al texto aplanado existente — corrigió en el camino un bug real de parsing (mensaje fantasma por reutilización del tag `<mensaje>` en el esquema SRI). Solo `ElectronicDocument.MarkRejected` gana un parámetro opcional; `MarkFailed`/`MarkDeadLetter` sin cambios. Segundo suscriptor de `ElectronicDocumentRejectedEvent` (`ElectronicDocumentSriMessageAuditHandler`, tabla nueva `electronic_document_sri_message`) — mismo patrón `PricingRuleAudit`/`ElectronicDocumentAudit`, sin tocar `IAuditReader<T>`/`IAuditWriter<T>` genéricos. `ElectronicDocumentDiagnosticDto` único contrato reutilizable (retira `ElectronicDocumentErrorInfoDto`), ensamblado por `ElectronicDocumentDiagnosticAssembler` y consumido por Monitor, el reintento manual (cierra un bug real de contrato: `RetryElectronicDocumentCommandHandler` devolvía `ElectronicDocumentDto` en vez del detalle completo) y el nuevo `GET /api/v1/electronic-documents/by-source` agnóstico de módulo. Frontend: `ElectronicDocumentDiagnosticPanel` (`components/zh/electronicDocuments/`) integrado en Monitor y en Ventas (`SalesElectronicDiagnosticDrawer`, segundo consumidor real). Retenciones/Notas/Guías quedan explícitamente fuera (sin emisión activa, ver límites de ADR-023). |
| **Recepción XML de Compras → Compra** — **CIERRE OFICIAL** | 2026-07-28 | `docs/decisions/ADR-028-purchase-reception-to-purchase-flow-freeze.md` | Flujo congelado: Recepción XML → Descargar XML → Crear Compra → Formulario precargado → Guardar Compra. `PurchaseReceptionDocument.XmlContent` es evidencia fiscal inmutable; `PurchaseReceptionLine` es el único snapshot operativo (nunca se elimina una línea por ausencia de Item o fallo de matching); `IPurchaseReceptionDetailProcessor` es la única interpretación de XML→snapshot+Item Matching, reutilizada por la descarga inicial y por la reconstrucción transparente e interna de `CreatePurchaseReceptionDraftHandler` (dispara solo si `ProcessingStatus.Failed`, persiste de inmediato, nunca reconstruye dos veces — verificado por tests dedicados). Un único botón "Crear Compra", sin endpoints ni acciones de "reprocesar" expuestos al usuario. Deuda aceptada y documentada (no bloqueante, ver ADR-028 "Consecuencias"/"Riesgos"): `PurchaseReceptionDocument.MarkProcessed(...)` existe pero no tiene invocador real — `CreatePurchaseDraftCommand` (creación de `PurchaseInvoice`) no recibe todavía un `PurchaseReceptionDocumentId`. Evolución futura (workflow de aprobación de Compras, no implementado) documentada en `docs/decisions/ADR-029-purchase-approval-workflow-future-evolution.md`. |

### Items Administration
Estado: FROZEN

Contrato cerrado:
- Item master data
- Item pricing base
- Item child entities
- Item audit

### Pricing Administration
Estado: FROZEN

Contrato cerrado:
- Price Lists
- Price List assignments
- Pricing Rules
- Pricing resolution rules
- Pricing audit

Restricciones:
- Pricing no calcula impuestos.
- Pricing no soporta ItemVariantId.
- PricingRule requiere PriceListItem activo.
- Auditoría mediante Domain Events.

### Items — PVP fix (2026-06-24)

Fix de actualización de PVP en formulario de edición de ítems:
- Schema de validación correcto (`updateItemSchema` sin `sku`) al editar
- Precio se carga desde `itemPriceService.list()` al abrir edición
- Precio se persiste via `itemPriceService.setInitial()` al guardar

### Compras — Auditoría UX + SSOT (2026-06-24)

Auditoría completa del formulario de Compras. Build: **0 errores frontend + backend**. Tests: **47/47 PASS**.

| Mejora | Detalle |
|--------|---------|
| Código muerto eliminado | `ItemContextPanel`, `creditDays`, `profileLoading`, `expandedLines`/`toggleExpand` (−184 líneas neto) |
| Duplicidad visual eliminada | SKU en select bodega, nombre producto en panel contexto |
| Descuento por línea | Input editable 0-100% (backend ya lo soportaba, UI no lo exponía) |
| Cálculo local IVA/ICE | Estimación en borrador nuevo usando `ctx.vatPercent`/`ctx.icePercent` — elimina totales engañosos $0 |
| Alerta costo fuera de rango | Warning visual cuando costo difiere >20% del promedio SSOT |
| Selector condición de pago | Backend: `Guid? PaymentTermId` opcional en commands (backwards compatible). Frontend: select en cabecera con regeneración automática de cuotas |
| Secciones colapsables | Info Electrónica y Observaciones colapsables, auto-expand si tienen datos |
| Lógica extraída + testeable | `purchaseCalc.ts` con funciones puras; 27 tests unitarios (Vitest) |
| Import huérfano eliminado | `UpdatePurchasePayload` |
| CSS huérfano eliminado | `.pdl-line__disc-badge*`, `.pf-mini-card--obs` |

---

## Architecture (current)

| Area | State |
|------|--------|
| Modular monolith (Clean + CQRS) | ✅ |
| EF baseline `20260606040144_ErpBaselineClean` | ✅ |
| Tenant / Company / Membership model (`SubscriberId → TenantId` consolidado FASE 4) | ✅ |
| `CompanyScopeBehavior` (pipeline MediatR) | ✅ |
| Wave 1 `company_id` (inventory core) | ✅ (in baseline) |
| PostgreSQL RLS (enterprise tables) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Architecture guardrails CI (scripts + NetArchTest) | ✅ (2026-05-21) |
| **Frontend architecture checks (Node ESM)** | ✅ 12/12, score 100/100 (2026-05-24) — controllers backend ≤150 líneas |
| **Architecture governance v2** (ADRs, backend Node checks, score, PR annotations) | ✅ (2026-05-21) |
| Architecture baseline v1.0 remediation (lint, E2E smoke, legacy platform controller, SYSTEM_TRUTH) | ✅ (2026-05-21) |
| Post-audit remediation (session SEC, Sales unify, Kardex CQRS, Cash validators) | ✅ (2026-05-21) |
| Post-audit wave 2 (menu builder split, services→modules, access/security pages) | ✅ (2026-05-21) |
| Post-audit wave 3 (menu builder modular split, test sessionStorage) | ✅ (2026-05-21) |
| Enterprise monorepo root (`infrastructure/`, `scripts/`, `tools/`, docs stubs) | ✅ (2026-05-21) |
| Post-reorg stabilization (paths, CI green, company-scoped inventory movements) | ✅ (2026-05-21) |
| Post-audit P2 + wave 4 (services eliminados, AppLayout/Companies split) | ✅ (2026-05-21) |
| Post-audit wave 5 (PR-7 TSX: catálogo, clientes, contabilidad, menu builder, platform shell) | ✅ (2026-05-21) |
| Post-audit wave 6 (handlers C-03, lazy routes, grandfather vacío) | ✅ (2026-05-21) |
| **docs/architecture/ multi-agent governance** (`docs/architecture/*` canonical; `CLAUDE.md`/`backend/CLAUDE.md`/`frontend/CLAUDE.md` + `.mdc` adapters) | ✅ (2026-05-21, reorganizado 2026-08-07) |

Details: [ARCHITECTURE.md](./ARCHITECTURE.md), [DATABASE.md](./DATABASE.md).

### Post-audit remediation (2026-05-21)

| Item | Estado |
|------|--------|
| Frontend: tokens en memoria + perfil/bootstrap/permisos en `sessionStorage`; `SessionBootstrap` + cookie refresh | ✅ |
| Backend: `ERP.Application/Sales` consolidado bajo `Modules/Sales` + validators Notas/Retenciones | ✅ |
| Backend: `EnqueueKardexReportCommand` (controller sin `SaveChangesAsync`) | ✅ |
| Backend: validators Cash (caja/bancos/conciliación) | ✅ |
| Pendiente PR-7 TSX >500 | ✅ (grandfather `tsxMaxLines500` vacío 2026-05-21) |

### Post-audit wave 5 (2026-05-21)

| Item | Estado |
|------|--------|
| `MenuBuilder` + `NavigationMenuEditorPanel` modularizados (controller + subpaneles) | ✅ |
| `PlatformPanelPage` + `PlatformPlansSection` en hook + tabs/modales | ✅ |
| `AccountingPage`, `BranchesPage`, `CustomersPage`, `SriConfigPage`, `BodegasPage` | ✅ |
| `CatalogPages`, `CatalogStructurePage`, categorías/subcategorías | ✅ |
| `architecture-grandfather.json`: `tsxMaxLines500` vacío | ✅ (`tools/architecture/`) |

### Post-audit wave 6 (2026-05-21)

| Item | Estado |
|------|--------|
| Handlers C-03: `CrearVenta`, `CreateProduct`, `UpdateProduct`, `EmitirFactura`, `EnviarNotaSri` (Handle ≤150) | ✅ |
| `ProductCommandMutationHelper` compartido create/update | ✅ |
| Rutas lazy: `accessRoutes`, `companiesRoutes`, `companyManagementRoutes`, `publicRoutes`, `mainRoutes` (placeholder) | ✅ |
| Grandfather vacío (`handlerHandleMaxLines150`, `tsxMaxLines500`, `tsxPageWrapperMaxLines15`) | ✅ |
| Chunk `index-*.js` ~362 KB (límite 650 KB) | ✅ |

### Post-audit P2 (2026-05-21)

| Item | Estado |
|------|--------|
| Carpeta `frontend/src/services/` eliminada (cero consumidores; API solo en `modules/*/api`) | ✅ |
| `SalesReportPage` → `modules/reportes/pages/` + wrapper 1 línea | ✅ |
| Placeholders → `modules/shared/pages/` + wrappers delgados | ✅ |
| `components/ui` sustituido por ZH en company-management, access, security, companies | ✅ |

### Post-audit wave 4 (2026-05-21)

| Item | Estado |
|------|--------|
| `AppLayout.tsx` (~634 → ~216) + `AppLayoutMainMenu`, `useAppLayoutNavigation`, banner | ✅ |
| `CompaniesPage.tsx` (~820 → ~252) + `useCompaniesPage`, `CompaniesPageDataTab` | ✅ |
| Grandfather: retirados `AppLayout`, `CompaniesPage`, `SalesReportPage` | ✅ |

### Post-audit wave 3 (2026-05-21)

| Item | Estado |
|------|--------|
| `usePlatformGateMenuBuilder` (~844 → ~371 líneas) + effects/actions/persist extraídos | ✅ |
| `PlatformMenuBuilderCrmWorkspace` (~934 → ~259 líneas) + panels/preview/audit/modals | ✅ |
| Test `syncSessionEntitlements` con stub `sessionStorage`/`localStorage` | ✅ |
| Grandfather: `PlatformMenuBuilderCrmWorkspace` retirado de PR-7 | ✅ |

### Post-audit wave 2 (2026-05-21)

| Item | Estado |
|------|--------|
| `PlatformMenuBuilderSection` dividido en entry + hook + CRM/legacy panels | ✅ |
| Imports `services/` → `modules/*/api` (cero consumidores directos en `src/`) | ✅ |
| `ProfilesPage`, `SubscriberAccessPage`, `SecuritySettingsPage` en `modules/` + wrappers delgados | ✅ |
| Re-exports `@deprecated` en `frontend/src/services/` para compatibilidad | ✅ (carpeta eliminada 2026-05-21) |
| Grandfather JSON actualizado (CRM workspace, sin legacy service imports) | ✅ |

## SaaS platform y ERP backend (snapshot histórico — pre FASE 1)

> ⚠️ **Snapshot pre-refactor (2026-05-23/24).** Las dos tablas siguientes describen el estado **anterior** a "FASE 1 — ERP Kernel Cleanup" (2026-06-05, ver banner al inicio de este documento), que eliminó por completo Billing domain, Subscriptions domain, Platform entities, Commercial plans y Entitlements, y a "FASE 4" (consolidación `SubscriberId → TenantId` + BP V2). Items como *Billing governance*, *Entitlements snapshot*, *Commercial limits*, *Sales/Accounting/Cash* descritos abajo **ya no existen** como módulos activos del backend — ver el inventario real de módulos en [`docs/ARCHITECTURE.md`](./ARCHITECTURE.md#bounded-contexts) y el estado vigente en "ERP CORE BASELINE v1.0" arriba. Se conservan como registro histórico de delivery, no como estado actual.

| Component (histórico) | Status (al 2026-05-23) |
|-----------|--------|
| Subscribers / plans / features | ✅ |
| Platform UI naming + API JSON aliases + middleware rename | ✅ (2026-05-23) |
| Subscriber ficha unificada + impersonación con retorno | ✅ (2026-05-23) |
| Company management API + UI (`/companies`) | ✅ |
| Switch company + JWT claims | ✅ |
| Commercial limits (companies, users, branches, warehouses) | ✅ |
| Entitlements snapshot API | ✅ |
| Billing governance + API | ✅ backend |
| Billing UI | ⏳ not built |
| Stripe / real payment provider | ⏳ `NullPaymentProviderAdapter` |

| Module (histórico) | Status (al 2026-05-24) |
|--------|--------|
| **Business Partners (Clientes/Proveedores) — FROZEN** | ✅ FROZEN 2026-06-02 — ver `docs/decisions/ADR-017-business-partner-scope.md` (sigue vigente como BP V2) |
| Products, catalogs, customers, suppliers | ✅ |
| Inventory, transfers, adjustments, kardex | ✅ |
| Purchases (OC, bills, expenses) | ✅ (UX/SSOT audit 2026-06-24) |
| Sales + electronic invoice (SRI code) | ✅ code / 🟡 real SRI validation pending |
| **Sales commercial pipeline** (quote → order → invoice, `DocumentRelation`) | ✅ API + UI + E2E (2026-05-24) |
| Accounting, cash | ✅ |
| Retenciones / guía remisión | 🟡 partial / placeholder UI |

### Backend architecture hardening (audit 2026-05-21)

| Item | Status |
|------|--------|
| SRI post-auth atomic transactions (`IUnitOfWork` ambient + journal entry nested) | ✅ |
| `SriSettings.CertPassword` encrypted at rest (Data Protection, legacy plaintext fallback) | ✅ |
| `Company` → `ISubscriberScopedEntity` + global EF subscriber filter | ✅ |
| `AccountingService` orchestration in Application layer | ✅ |
| API DbContext leakage → CQRS (`GetAppFeatureTree`, `ListPendingSriRetry`, `IAppFeatureRepository`) | ✅ |

## Frontend

| Area | Status |
|------|--------|
| Auth, subscriber select, company select | ✅ |
| Core ERP modules (sales, purchases, inventory, settings) | ✅ |
| **Ventas pipeline UI** (`/sales/quotes`, `/sales/orders`, `/sales/invoices`, credit notes) | ✅ (2026-05-24) |
| **`fullLogout()` centralizado** (stores + localStorage + `erp.saas.*`) | ✅ |
| **Products/customers — fuente única en `modules/*`** (`apiEnvelope`, adapters `@deprecated`) | ✅ |
| **Consolidación modular P3** (auth, branches, accounting, dashboard, platform API + pages) | ✅ |
| **Catálogo + bodegas + auth UI** en `modules/catalog`, `modules/inventario/warehouses`, `modules/auth/pages` | ✅ |
| **Lazy routes P4** (`routes/lazyPage.tsx`, main/catalog/platform split) | ✅ |
| **Platform naming cleanup** (`/platform/*`, `platformAuth.ts`, sin `isPlatformOperator`) | ✅ (2026-05-23) |
| **ZH UI estándar** (`components/ui` delega clases ZH; catálogo usa `ZHCard`/`ZHSearchBar`) | ✅ |
| Company management module | ✅ |
| SaaS billing pages | ⏳ |
| Kardex / stock dedicated UI | ⏳ placeholder routes |
| Legacy `tenant` i18n aliases | 🟡 rename deferred |

## PostgreSQL

| Item | Status |
|------|--------|
| Schema from single baseline | ✅ |
| Naming `_subscriber_` on indexes/FK | ✅ |
| RLS enabled (inventory, sales core) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Session vars via interceptor | ✅ |
| Company scope on operational entities | ✅ (baseline + query filters) |

## Security

| Item | Status |
|------|--------|
| JWT + refresh rotation (FamilyId, grace configurable, revocación por familia, rate limit IP/user/family, audit logs) | ✅ |
| Multi-tab SPA (Web Locks + BroadcastChannel + bootstrap retry) | ✅ |
| Permission policies | ✅ |
| Company isolation (app layer) | ✅ |
| SRI certificate password encryption (Data Protection) | ✅ |
| RLS (DB layer) | ❌ no implementado — ver [DATABASE.md#rls](DATABASE.md#rls) |
| Platform operator bypass (JWT global) | ✅ controlled |
| Permissions cache in handler hot path | ⏳ service exists, wiring partial |
| SPA session cleanup (`fullLogout`) | ✅ frontend |

## Cache

| Cache | Status |
|-------|--------|
| Entitlements snapshot (Redis-ready) | ✅ |
| Permissions (distributed impl) | ✅ registered |
| Dedicated `commercial-limits:{id}` cache | ⏳ optional future |

## Tests

| Project | Status (2026-05-21) |
|---------|---------------------|
| `ERP.Infrastructure.Tests` (limits/entitlements + optional Postgres unified-doc) | ✅ 23/23 |
| `ERP.Domain.Tests` | ✅ 24/24 |
| `ERP.Application.Tests` | ✅ 190/190 (2026-06-05) |
| `ERP.API.Tests` | ✅ 33/33 SecurityTests (2026-06-05); integration suite stable |
| `ERP.Architecture.Tests` (NetArchTest + controller guardrails) | ✅ 30/32 — 2 pre-existing failures (Items module permissions pending plan catalog registration) |
| Frontend ESLint (`npm run lint`) | ✅ 0 errors (2026-05-21 remediation) |
| Frontend Vitest | ✅ 47/47 (27 purchase calc tests added 2026-06-24) |
| Frontend build | ✅ |
| Playwright smoke | ✅ PASS |
| Playwright enterprise E2E | 🟡 requiere API local; skip controlado sin backend |

### Sales commercial pipeline greenfield (2026-05-24)

| Item | Estado |
|------|--------|
| API: quotes (list/detail/create/approve/cancel), orders (list/detail/create/confirm/cancel/invoice) | ✅ |
| API: invoices (list/detail/validar/emitir/reintentar/anular) + permisos `sales.invoices.*` | ✅ |
| API: `DocumentRelation` (`QUOTE_TO_ORDER`, `ORDER_TO_INVOICE`) en detalle | ✅ |
| UI: `/sales/quotes`, `/sales/orders`, `/sales/invoices` + legacy redirects | ✅ |
| UI: trazabilidad cotización↔pedido↔factura; factura directa walk-in | ✅ |
| UI: filtros servidor en listado facturas; permiso `sales.credit-notes.send` | ✅ |
| E2E: `SalesCommercialPipelineEndToEndTests`, `SalesOrderInvoiceEndToEndTests`, `SalesCommercialCancelEndToEndTests` | ✅ |
| Tenants con perfil Facturador anterior al seed | 🟡 re-seed o migración manual de permisos `sales.quotes.*`, `sales.orders.*` |

Flujo canónico: **Cotización → Aprobar → Pedido → Confirmar → Factura → Validar/Emitir SRI**.

## MVP commercial (~85–90%)

**Done:** Core ERP operational flows, platform control plane, plans, multi-company foundation.

**Blocking / high priority:**

1. Validate SRI in `celcer.sri.gob.ec` with test certificate
2. Billing + retenciones UI gaps
3. Playwright enterprise E2E con API en CI (smoke ya verde)

See [ROADMAP.md](./ROADMAP.md) for prioritized backlog.

### Enterprise hardening — MasterData + security (2026-05-23)

| Item | Estado |
|------|--------|
| Explicit scope markers (`ICompanyScopedRequest` / CI AR-SEC-4) | ✅ |
| PostgreSQL unique violation → `Result.Conflict` (409) | ✅ |
| Testcontainers concurrency tests | ✅ (`Category=PostgreSql`) |
| Security metrics wired (refresh, 403, dual-write, namespace fallback) | ✅ |
| MasterData reconciliation (READ-ONLY) + health + Hangfire job | ✅ |
| SRI foundation (`SupplierProfile` retention defaults) | ✅ |
| Docs: [security/MULTI-TENANT-HARDENING.md](./security/MULTI-TENANT-HARDENING.md), [observability/METRICS.md](./observability/METRICS.md) | ✅ |

## Risks

| Risk | Mitigation |
|------|------------|
| Cross-company data leak | `CompanyScopeBehavior` + EF query filters |
| Production migration from old chain | Use baseline + planned data migration — never `DROP SCHEMA` in prod |
| Billing suspend without UI visibility | Entitlements snapshot exposes status; build `/saas/billing` |
| Test drift | Fix controller/DTO names before release gate |

## Quick start

```powershell
docker compose up -d
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd ../ERP.API
dotnet run
```

First-run admin: banner en consola al arrancar API (`GET /api/setup/status` + `POST /api/setup/admin`, token-gated).

## Related

- [ROADMAP.md](./ROADMAP.md) — what’s next
- [DEVELOPMENT.md](./DEVELOPMENT.md) — how to contribute safely
