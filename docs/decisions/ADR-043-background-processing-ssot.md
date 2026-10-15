# ADR-043 — Background Processing SSOT

Estado: Accepted. Fecha: 2026-10-15. Clasificación: STRUCTURAL DEBT.

## Alcance aprobado

Reemplaza la infraestructura recurrente FROZEN anterior: registros, interfaces por
job, wrappers, exclusión declarativa, configuración y cursor local. Las auditorías
permanecen cerradas. Pipeline fiscal, estados de ElectronicDocument, reglas de
negocio y entrega SMTP se conservan. Sin Dashboard ni UI/AdminCore.

## Arquitectura única

`HangfireRecurringJobs.All` es el catálogo inmutable ID → tipo concreto. Deriva DI,
validación, registro y sincronización. Los siete tipos implementan `IRecurringJob`
y reciben BatchSize explícito. `HangfireOptions` es el único modelo: binding
estricto, IValidateOptions y ValidateOnStart. Claves desconocidas o faltantes
abortan incluso con Server OFF. Storage exige cadena PostgreSQL explícita con
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

| Job | Cola | Cron UTC | BatchSize | MaxConcurrency |
|---|---|---|---:|---:|
| process-outbox | core | `* * * * *` | 50 | 1 |
| process-communications | communications | `* * * * *` | 50 | 1 |
| reconcile-electronic-document-communications | communications | `*/10 * * * *` | 200 | 1 |
| electronic-document-retry | sri | `* * * * *` | 50 | 1 |
| sales-electronic-document-recovery | sri | `* * * * *` | 50 | 1 |
| retention-electronic-recovery | sri | `* * * * *` | 50 | 1 |
| expire-user-sessions | maintenance | `0 4 * * *` | 50 | 1 |

Todos declaran Enabled explícito. Cron admite cinco campos numéricos con listas,
rangos y pasos. Workers: core=1, communications=2, sri=2, maintenance=1. Son valores
iniciales explícitos para afinar con métricas. MaxConcurrency=1 es obligatorio:
varios workers permiten correr distintos jobs de la cola, sin solapar el mismo ID.

API sin workers: `Hangfire:Server:Enabled=false` (versionado). Host combinado:
Server.Enabled=true. Worker separado, desde el directorio publicado de API:

```powershell
dotnet ERP.API.dll --worker --Hangfire:Server:Enabled true
```

El worker usa Generic Host sin Kestrel. Comparte Application/Infrastructure y el
registro único de Background Processing. El despliegue aplica migraciones antes
de arrancar workers separados; API conserva su migración previa al bootstrap.
Ambos requieren misma base ERP, storage Hangfire y volumen FileStorage/keyring.
Dashboard.Enabled=false es obligatorio. Se eliminan las claves antiguas del
entorno, incluidos user-secrets: Enabled → Server:Enabled y ConnectionString →
StorageConnectionString, preservando valores. No se leen como aliases.

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
tenant/company/scope en SQL antes del límite. Los defaults operativos vigentes de
valores faltantes/corruptos se conservan. Communications resuelve el perfil de cada
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
