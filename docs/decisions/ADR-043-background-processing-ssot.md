# ADR-043 — Background Processing SSOT

Estado: Accepted. Fecha: 2026-10-15. Clasificación: STRUCTURAL DEBT.

## Alcance aprobado

Reemplaza la infraestructura recurrente FROZEN anterior: registros, interfaces por
job, wrappers, exclusión declarativa, configuración y cursor local. Las auditorías
permanecen cerradas. Pipeline fiscal, estados de ElectronicDocument, reglas de
negocio y entrega SMTP se conservan. Sin Dashboard ni UI/AdminCore.

## Arquitectura única

`HangfireRecurringJobs.All` es el catálogo inmutable ID → tipo concreto y solo define
identidad. Deriva DI, validación, registro y sincronización. Los siete tipos implementan
`IRecurringJob` y reciben BatchSize explícito. `HangfireOptions` es el único modelo de
valores operativos (Server, storage, Workers por cola y, por job, Enabled/Cron/Queue/
BatchSize): binding estricto, IValidateOptions y ValidateOnStart. Claves desconocidas
o faltantes abortan incluso con Server OFF. Storage exige cadena PostgreSQL explícita con
Host/Database/Username, sin fallback a la base ERP; puede apuntar a dberpsaas.
Multiplexing se rechaza porque el lock requiere afinidad de sesión.

`RecurringJobRunner.RunAsync(id)` es la única entrada de Hangfire: revisa Enabled,
adquiere `pg_try_advisory_lock(hashtextextended('ERP.Jobs/' + id, 734108))` en una
conexión física mantenida durante la ejecución, crea scope fresco y emite logs y
métricas. Lock ocupado significa skipped, sin error ni espera. Finalmente libera
el lock también al fallar/cancelar; una caída del proceso lo libera en PostgreSQL.
AutomaticRetry(0): la siguiente corrida descubre nuevamente el trabajo durable.
Los scopes tenant/company por candidato y los controles de persistencia existentes
siguen siendo autoridad de aislamiento e idempotencia.

Un solo hosted service sincroniza antes de iniciar workers: AddOrUpdate para ON;
RemoveIfExists para OFF y todos los IDs fuera del catálogo. La cola explícita se
guarda en `Job.Queue` mediante Hangfire 1.8. Cada cola tiene su servidor y workers
explícitos. Ningún servidor consume `default`.

## Configuración final

| Job | Cola | Cron UTC | BatchSize |
|---|---|---|---:|
| process-outbox | core | `* * * * *` | 50 |
| process-communications | communications | `* * * * *` | 50 |
| reconcile-electronic-document-communications | communications | `*/10 * * * *` | 200 |
| electronic-document-retry | sri | `* * * * *` | 50 |
| sales-electronic-document-recovery | sri | `* * * * *` | 50 |
| retention-electronic-recovery | sri | `* * * * *` | 50 |
| expire-user-sessions | maintenance | `0 4 * * *` | 50 |

Todos declaran Enabled explícito. Cron admite cinco campos numéricos con listas,
rangos y pasos. La cola de cada job es configuración (minúsculas, nunca `default`);
`Workers` declara exactamente las colas usadas por los jobs, con 1..256 workers cada
una: core=1, communications=2, sri=2, maintenance=1 como valores iniciales para afinar
con métricas. Concurrencia máxima 1 por ID es invariante del runner (advisory lock),
no una opción: varios workers corren distintos jobs de la cola, nunca el mismo ID.

API sin workers: `Hangfire:Server:Enabled=false` (versionado). Host combinado:
Server.Enabled=true. Worker separado, desde el directorio publicado de API:

```powershell
dotnet ERP.API.dll --worker --Hangfire:Server:Enabled true
```

El worker usa Generic Host sin Kestrel. API y worker componen la infraestructura común
solo con `ErpHostRegistration` (fuentes de configuración, Serilog, caché Redis/memoria,
Infrastructure/Application, Data Protection y opciones) más el registro único de
Background Processing; lo exclusivo de HTTP queda en `Program.cs`. El despliegue aplica migraciones antes
de arrancar workers separados; API conserva su migración previa al bootstrap.
Ambos requieren misma base ERP, storage Hangfire y volumen FileStorage/keyring.
No hay Dashboard de Hangfire. Se eliminan las claves antiguas del entorno, incluidos
user-secrets: Enabled → Server:Enabled y ConnectionString → StorageConnectionString,
preservando valores; `Dashboard:*` y `Jobs:*:MaxConcurrency` se eliminan. Ninguna se
lee como alias: el binding estricto las rechaza al arrancar.

## Lotes, fairness y ownership

Todas las consultas de candidatos usan LIMIT/Take explícito. Retentions distribuye
el presupuesto total B entre floor(B/3) finalizaciones, B−2×floor(B/3) transmisiones
y floor(B/3) verificaciones. B mínimo=3; cada stream tiene progreso independiente.

La migración `20261015000000_BackgroundJobProgress` crea checkpoints técnicos en
PostgreSQL, sin datos de negocio ni preferencias por empresa. Reintentos, Sales y
Retentions rotan por UUID estable después del último lote descubierto, incluyendo
los candidatos fallidos. Guardar antes de procesar evita que un poison item acapare
los lotes y conserva progreso al reiniciar o cambiar de nodo. Sin OFFSET creciente.

El reconciliador conserva orden `(created_at, id)` y persiste su checkpoint al
terminar cada lote; vuelve al principio al llegar al final. Si el último lote llena
exactamente B, la siguiente corrida vacía detecta el final. Las anomalías se
revisitan en otra vuelta. Se elimina el cursor singleton en memoria. El anti-join
conserva el CTE materializado; LIMIT acota filas cargadas, COUNT/MIN quedan en SQL.

Las preferencias auto_retry_enabled y email_on_authorization se correlacionan por
tenant/company/scope en SQL antes del límite, con la clave de `OrgSettingKeys` y la
semántica de `OperationalPreferencesResolver` (`DefaultOnCompanySwitch`: solo un valor
que se lee como `false` apaga; faltante/corrupto conserva el default activo). Es la
única decisión previa al lote: el job de reintentos y el reconciliador no vuelven a
resolverlas; el servicio de comunicación conserva su guard al encolar. La elegibilidad
de reintento (estados, m?ximo efectivo por empresa y backoff `min(1·2^n, 16)` minutos) existe una sola
vez como predicado de `ElectronicDocumentRetryPolicy`, traducido a SQL. Communications resuelve el perfil de cada
fila y conserva claims, leases, fencing y política de reintento. No hay preferencias
por empresa nuevas para procesos técnicos.

Retentions recupera la transmisión inicial únicamente antes del registro de
ElectronicDocument. Desde que existe, incluido Draft, ElectronicDocuments es el
único dueño de recuperación fiscal. Retentions excluye cualquier documento por
origen existente; el issuer canónico conserva unicidad e idempotencia ante carreras.
Sales recovery permanece create-only.

## Health y métricas

Se elimina masterdata-reconciliation completo: servicio, interfaz, job y health.
Integridad de membresías/MasterData sale de ready y conserva endpoints diagnósticos.
Ready conserva conectividad y probes configurados.

Meter ERP.Jobs: duración (s), procesados, fallidos, skipped por lock, backlog y edad
del pendiente más antiguo (s). Etiquetas técnicas job/stream, sin secretos ni PII.
Backlog/edad son gauges por stream; agregados SQL sin cargar históricos. Prometheus
de API incluye ERP.Jobs. Un worker separado emite el mismo Meter; su exportación
requiere un collector en ese proceso (no se añade un servidor HTTP al worker).

## Entrega

Despliegue requiere migración, configuración nueva y reemplazo/reinicio de todos los
workers antiguos. No hay rutas paralelas ni compatibilidad. Validación focal con
PostgreSQL 16 real para locks entre instancias, migraciones, scopes, fairness,
ownership y sincronización. Resultados en STATUS.md. Sin ejecutar jobs del piloto,
suites completas, commit ni push.

## Fase A - máximo por empresa y eliminación legacy

`electronic_documents.max_retry_attempts` gobierna candidatos y DeadLetter.
`ElectronicDocumentRetryMaximum` centraliza default 5, rango 1..10 y la expresión
única de resolución (faltante/corrupto: default), compilada para el resolver y
traducida a SQL para candidatos/contador. `ElectronicDocumentRetryPolicy` conserva
estados/backoff y compone la elegibilidad; el filtro efectivo precede al BatchSize.
El job no interpreta configuración. El issuer lee el mismo setting scoped y aplica
la misma resolución para DeadLetter. Se elimina el límite fijo `MaxAttempts`.

`communications.sales_invoice_authorized.enabled` duplicaba la decisión vigente
`electronic_documents.email_on_authorization`: se eliminan clave, definición y
contrato legacy, y una migración elimina sus filas (también la variante underscore).
No hay alias ni segunda ruta; Communications conserva su guard y perfil SMTP.
