# ZH Background Processing - entrega 2026-10-15

Clasificacion: STRUCTURAL DEBT. Implementado y validado. Sin commit/push.

La arquitectura y configuracion final estan en
[ADR-043](../decisions/ADR-043-background-processing-ssot.md) y
[appsettings.json](../../backend/src/ERP.API/appsettings.json).

## Resultado

- Catalogo unico de siete jobs, derivando DI, validacion, cola y sincronizacion.
- HangfireOptions unico, binding estricto y ValidateOnStart; sin fallback de storage.
- RecurringJobRunner unico, scope fresco y contexto de sistema limpio; lock PostgreSQL
  no bloqueante por ID; AutomaticRetry(0). Job OFF y IDs retirados no se ejecutan.
- Sync elimina OFF y huerfanos. Cuatro servidores, queues explicitas y workers 1/2/2/1.
- API Server.Enabled=false sin storage/worker; entrada --worker con Generic Host sin HTTP.
- BatchSize explicitamente propagado a consultas/procesadores; fairness durable.
- Retentions: solo antes de existir ElectronicDocument; ElectronicDocuments: desde
  que existe, incluido Draft. Sales recovery permanece create-only.
- Eliminados masterdata-reconciliation completo, Dashboard y filtros, ocho interfaces
  por job, DisableConcurrentExecution y cursor singleton del reconciliador.
- Ready sin checks de integridad; ERP.Jobs emite las seis metricas requeridas.

## Configuracion

Todos los jobs ON en el archivo versionado; servidor OFF y Dashboard OFF.
Workers: core=1, communications=2, sri=2, maintenance=1. MaxConcurrency=1 en los siete.
BatchSize=50 salvo reconciliador=200; retenciones reparte 50 entre sus tres streams.
Cron UTC: cada minuto; reconciliador cada diez minutos; sesiones 04:00 UTC.
Catalogo completo y matriz por job en ADR-043.
StorageConnectionString es explicito, sin conexion heredada de DefaultConnection.
Las dos claves locales de user-secrets fueron renombradas conservando valores;
no se imprimieron ni copiaron secretos al repositorio. Otros entornos deben eliminar
sus claves viejas antes del despliegue.

## Tests focales

| Grupo | Resultado |
|---|---|
| API Hangfire: configuracion, contexto y aislamiento, locks entre instancias, ON/OFF, sync, hosts y metricas | PASS 30/30 |
| Revision final de catalogo/runner/worker; resuelve los siete jobs; cuatro servidores y WorkerCount; contexto Data Protection igual a API | PASS 19/19 |
| Trigger real: cola core; default vacia | PASS 1/1 |
| Communications, fairness con reinicio, preferencias SQL, sesiones y lote Sales | PASS 20/20 |
| Recovery Sales, dashboard, ownership Retention Draft y guard de filtros | PASS 39/39 |
| Handler sesiones y politica de retry | PASS 12/12 |
| Build mediante proyectos de tests y diff check | PASS |

Los grupos tienen solapamiento; no sumar como pruebas unicas. PostgreSQL 16 real
con Testcontainers para persistencia, locks, concurrencia, scope e invariantes.
No SQLite, in-memory ni mocks para esas verificaciones. No suites completas.
Dobles de transporte impiden correos/SRI reales durante las pruebas.

## Pendiente de despliegue y deuda real

- Aplicar 20261015000000_BackgroundJobProgress antes del worker separado. Migracion
  validada contra PostgreSQL real; no aplicada a dberpsaas ni a datos del piloto.
- Sustituir/reiniciar los procesos viejos, con base ERP/storage/keyring compartidos.
- STRUCTURAL DEBT: afinar cron/lotes/workers con metricas de carga real. No se afirma
  un benchmark de produccion; agregados COUNT/MIN y anti-join se ejecutan en SQL.
- STRUCTURAL DEBT: el worker emite ERP.Jobs, pero su exportacion necesita collector
  en ese proceso. El exporter Prometheus de API incluye el Meter de ese proceso;
  no recoge automaticamente instrumentos de otro proceso.
- FUTURE: UI/AdminCore y Dashboard, fuera de esta entrega.

## Archivos

Inventario respecto de HEAD. Incluye la integracion de los cambios locales previos
relacionados con Hangfire; .claude permanece fuera del alcance.

| Estado | Archivo |
|---|---|
| M | `STATUS.md` |
| NEW | `backend/src/ERP.API.Tests/Hangfire/BackgroundApiHostPostgreSqlTests.cs` |
| M | `backend/src/ERP.API.Tests/Hangfire/ElectronicDocumentRetryJobTenantContextTests.cs` |
| M | `backend/src/ERP.API.Tests/Hangfire/ExpireUserSessionsJobTests.cs` |
| NEW | `backend/src/ERP.API.Tests/Hangfire/HangfireRecurringJobsTests.cs` |
| NEW | `backend/src/ERP.API.Tests/Hangfire/RecurringJobRunnerPostgreSqlTests.cs` |
| M | `backend/src/ERP.API.Tests/Hangfire/SalesElectronicDocumentRecoveryJobTests.cs` |
| M | `backend/src/ERP.API.Tests/Support/PostgreSqlTestWebAppFactory.cs` |
| D | `backend/src/ERP.API.Tests/Unit/ElectronicDocumentRetryJobConcurrencyTests.cs` |
| NEW | `backend/src/ERP.API/Hangfire/BackgroundWorkerHost.cs` |
| M | `backend/src/ERP.API/Hangfire/ElectronicDocumentRetryJob.cs` |
| M | `backend/src/ERP.API/Hangfire/ExpireUserSessionsJob.cs` |
| D | `backend/src/ERP.API/Hangfire/HangfireDashboardAuthorizationFilter.cs` |
| NEW | `backend/src/ERP.API/Hangfire/HangfireOptions.cs` |
| NEW | `backend/src/ERP.API/Hangfire/HangfireRecurringJobs.cs` |
| D | `backend/src/ERP.API/Hangfire/IExpireUserSessionsJob.cs` |
| D | `backend/src/ERP.API/Hangfire/IProcessCommunicationsJob.cs` |
| D | `backend/src/ERP.API/Hangfire/IProcessOutboxJob.cs` |
| D | `backend/src/ERP.API/Hangfire/MasterDataReconciliationJob.cs` |
| M | `backend/src/ERP.API/Hangfire/ProcessCommunicationsJob.cs` |
| M | `backend/src/ERP.API/Hangfire/ProcessOutboxJob.cs` |
| M | `backend/src/ERP.API/Hangfire/ReconcileElectronicDocumentCommunicationsJob.cs` |
| NEW | `backend/src/ERP.API/Hangfire/RecurringJobRunner.cs` |
| M | `backend/src/ERP.API/Hangfire/RetentionElectronicRecoveryJob.cs` |
| M | `backend/src/ERP.API/Hangfire/SalesElectronicDocumentRecoveryJob.cs` |
| D | `backend/src/ERP.API/Health/MasterDataReconciliationHealthCheck.cs` |
| M | `backend/src/ERP.API/Program.cs` |
| M | `backend/src/ERP.API/appsettings.Production.json` |
| M | `backend/src/ERP.API/appsettings.json` |
| M | `backend/src/ERP.Application.Tests/Access/ExpireUserSessionsTests.cs` |
| D | `backend/src/ERP.Application/MasterData/Reconciliation/IMasterDataReconciliationService.cs` |
| M | `backend/src/ERP.Application/Modules/Access/UseCases/ExpireUserSessions/ExpireUserSessionsCommand.cs` |
| M | `backend/src/ERP.Application/Modules/Access/UseCases/ExpireUserSessions/ExpireUserSessionsHandler.cs` |
| M | `backend/src/ERP.Application/Modules/Communications/Services/ICommunicationOutboxProcessor.cs` |
| M | `backend/src/ERP.Application/Modules/Sales/Services/ISalesElectronicDocumentRecovery.cs` |
| M | `backend/src/ERP.Domain/Modules/Access/Interfaces/IUserSessionRepository.cs` |
| M | `backend/src/ERP.Domain/Modules/ElectronicDocuments/Interfaces/IElectronicDocumentRepository.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Accounting/SalesInvoiceAuthorizedPostingIntegrationTests.A9.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Communications/CommunicationContractIntegrationTests.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Communications/CommunicationOutboxDeliveryIntegrationTests.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Communications/CommunicationTemplateIntegrationTests.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Communications/ElectronicDocumentCommunicationFlow.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Communications/ElectronicDocumentCommunicationIntegrationTests.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/ElectronicDocuments/ElectronicDocumentRepositoryDashboardPostgreSqlTests.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseRetentionConfirmIntegrationTests.ElectronicCancellation.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Persistence/ExpireUserSessionsIntegrationTests.cs` |
| M | `backend/src/ERP.Infrastructure.Tests/Persistence/IgnoreQueryFiltersAuditTests.cs` |
| NEW | `backend/src/ERP.Infrastructure/BackgroundProcessing/BackgroundJobProgress.cs` |
| NEW | `backend/src/ERP.Infrastructure/BackgroundProcessing/JobMetrics.cs` |
| M | `backend/src/ERP.Infrastructure/Communications/CommunicationOutboxDeliveryStore.cs` |
| M | `backend/src/ERP.Infrastructure/Communications/CommunicationOutboxProcessor.cs` |
| M | `backend/src/ERP.Infrastructure/Communications/ElectronicDocumentCommunicationReconciler.cs` |
| M | `backend/src/ERP.Infrastructure/Communications/ElectronicDocumentCommunicationReconciliationQuery.cs` |
| M | `backend/src/ERP.Infrastructure/DependencyInjection.cs` |
| D | `backend/src/ERP.Infrastructure/MasterData/Reconciliation/BusinessPartnerReconciliationService.cs` |
| NEW | `backend/src/ERP.Infrastructure/Migrations/20261015000000_BackgroundJobProgress.cs` |
| M | `backend/src/ERP.Infrastructure/Persistence/Outbox/IOutboxProcessor.cs` |
| M | `backend/src/ERP.Infrastructure/Persistence/Outbox/OutboxProcessor.cs` |
| M | `backend/src/ERP.Infrastructure/Persistence/Repositories/ElectronicDocuments/ElectronicDocumentRepository.cs` |
| M | `backend/src/ERP.Infrastructure/Persistence/Repositories/Retentions/RetentionAnnulmentRequestRepository.cs` |
| M | `backend/src/ERP.Infrastructure/Persistence/Repositories/Retentions/RetentionDocumentRepository.cs` |
| M | `backend/src/ERP.Infrastructure/Persistence/Repositories/Sales/SalesElectronicDocumentRecovery.cs` |
| M | `backend/src/ERP.Infrastructure/Persistence/Repositories/UserSessionRepository.cs` |
| NEW | `docs/agent/BACKGROUND_PROCESSING_DELIVERY.md` |
| M | `docs/agent/PROJECT_STATE.md` |
| M | `docs/architecture/frozen-infrastructure.md` |
| M | `docs/decisions/ADR-036-retention-electronic-document-cancellation.md` |
| M | `docs/decisions/ADR-039-communications-architecture.md` |
| NEW | `docs/decisions/ADR-043-background-processing-ssot.md` |
| M | `docs/decisions/README.md` |
| M | `docs/observability/METRICS.md` |
| M | `docs/security/MULTI-TENANT-HARDENING.md` |
