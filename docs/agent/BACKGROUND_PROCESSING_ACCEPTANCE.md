# Background Processing — verificación de aceptación (2026-10-15)

Clasificación: KEEP para los comportamientos verificados. Sin cambios de código,
commit ni push. Se distingue evidencia de dberpsaas de pruebas aisladas.

| Punto | Resultado | Evidencia y alcance |
|---|---|---|
| 1. API sin workers | PASS aislado | Host API real con PostgreSQL Testcontainers; no registra JobStorage ni workers. Arranque contra dberpsaas pendiente de aprobación. |
| 2. Worker con configuración nueva | PASS aislado | Generic Host arranca sin HTTP, resuelve los siete jobs y registra cuatro servidores con sus WorkerCount explícitos. No hay worker activo en dberpsaas al consultar. |
| 3. Exactamente siete recurring jobs | PASS dberpsaas | Siete IDs en hangfire.set; masterdata-reconciliation ausente. |
| 4. Colas correctas | PASS dberpsaas y aislado | Job.Queue coincide con la matriz inferior. Trigger real de prueba entra en core; default queda vacía. |
| 5. Disabled desaparece | PASS aislado | Sincronización registra, deshabilita y elimina process-outbox; también elimina ID huérfano. No se alteró el scheduler compartido. |
| 6. Ready ligero | PASS aislado; pendiente local | HTTP 200 en prueba con PostgreSQL real. Ready no incluye checks de integridad MasterData/membresías. localhost:5003 no respondió; arranque temporal bloqueado por revisión automática. |
| 7. Checkpoints/fairness | PASS dberpsaas y aislado | Tabla background_job_progress existe; migración 20261015000000_BackgroundJobProgress figura una vez; checkpoint communications-reconciliation presente. Rotación durable sin starvation pasa en PostgreSQL aislado. |
| 8. Sin errores de configuración/lock/DI/job/queue/migración | PASS en gates positivos; pendiente operación local | Hosts, DI, scheduler y migraciones de pruebas pasan. Los casos de configuración inválida fallan intencionalmente como prueba fail-fast. Sin hosts locales activos no se afirma ausencia de errores en logs de operación. |
| 9. Communications Pending → Sent | PASS aislado | Cuarenta comunicaciones pendientes terminan Sent con tres processors concurrentes y sin duplicados; transporte simulado. Otro test verifica dos nodos independientes. En dberpsaas se observaron 2 Sent, 2 Failed y ninguna Pending; no se atribuye una transición nueva a esta verificación. |
| 10. Sin solapes del mismo recurring ID | PASS aislado | Dos instancias independientes compiten por el lock PostgreSQL: la segunda sale skipped mientras la primera permanece activa; tras liberar el lock puede ejecutar. No hubo workers activos locales para observar concurrencia real del piloto. |

## Scheduler leído en dberpsaas

| ID | Job.Queue efectivo |
|---|---|
| process-outbox | core |
| process-communications | communications |
| reconcile-electronic-document-communications | communications |
| electronic-document-retry | sri |
| sales-electronic-document-recovery | sri |
| retention-electronic-recovery | sri |
| expire-user-sessions | maintenance |

El campo de almacenamiento antiguo `Queue` conserva `default`. En Hangfire 1.8 la
cola explícita está en `Job.Queue`; se verificó además el encolado efectivo en la
prueba de Trigger. No se debe interpretar aquel campo antiguo como cola efectiva.

Consultas al entorno compartido ejecutadas en transacciones READ ONLY. Se observó
cero filas en hangfire.server. No se aplicaron migraciones, se iniciaron workers ni
se enviaron correos/SRI sobre datos del piloto durante esta verificación.

## Pruebas ejecutadas en esta verificación

- API: 20/20 PASS. Filtro BackgroundApiHostPostgreSqlTests,
  RecurringJobRunnerPostgreSqlTests y HangfireRecurringJobsTests.
- Infrastructure: 4/4 PASS. Tres_workers_concurrentes_entregan,
  Dos_nodos_independientes_sobre_el_mismo_PostgreSQL,
  Faltantes_no_elegibles_antiguos_no_bloquean y A9_bounded_discovery.
- PostgreSQL 16 real en Testcontainers; SMTP sustituido por transporte de pruebas.
  Sin suites completas.
- Resultados TRX: backend/src/ERP.API.Tests/TestResults/background-acceptance-api.trx
  y backend/src/ERP.Infrastructure.Tests/TestResults/background-acceptance-infra.trx.

## Verificación local pendiente

PILOT BLOCKER para declarar completado el smoke local: el arranque contra dberpsaas
fue rechazado por revisión automática porque Program ejecuta migraciones, bootstrap
y backfills al iniciar. Se pidió autorización explícita para ese arranque con
workers apagados. La validación aislada sí está completa; no sustituye evidencia
de un host local operando. La migración antes pendiente ya fue confirmada aplicada
mediante consulta de solo lectura; no se infiere quién la aplicó.
