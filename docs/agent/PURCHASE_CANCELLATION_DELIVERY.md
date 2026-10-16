# Entrega focal — CancelPurchase y política global SRI

Fecha: 2026-10-16. Sin commit ni push. Solo validación focal.

## A. Implementación realizada

- BUG REAL: reproducción PostgreSQL 10@$2 + 1@$10 − 8 unidades. El reverso
  histórico anterior produce `Inventory cost cannot be negative.`
- EXTEND: `PurchaseCancellationEligibility` concentra los guards operativos y se
  ejecuta antes de todo reverso por el flujo único de Cancel (API/finalización SRI).
  Cualquier salida efectiva posterior del mismo Item/Bodega bloquea definitivamente,
  incluso revertida. Una entrada sola no bloquea. Secuencia Kardex, no CurrentStock.
- EXTEND: lock transaccional de inventario compartido con AppendMovementAsync,
  adquirido antes de comprobar salidas y retenido hasta commit/rollback. Sin cache
  de locks que pueda sobrevivir a la transacción del llamador.
- KEEP: reverso histórico permitido, devolución, NC, Discard y contabilidad existentes.
  Regla fiscal única en RetentionCanceller; no se ejecuta anulación SRI de facturas
  recibidas del proveedor. El código del bloqueo tiene entrada pública en el catálogo
  de errores existente; no requiere cambios de controller ni rutas nuevas.

## B. Archivos modificados

Inventario exacto en `git status` al final. Agrupación funcional:

- Application: regla de elegibilidad, CancelPurchase, ApiResponseCodes/MessageCatalog,
  ISriCatalogResolver, RetentionAnnulmentRequester y comentario del DTO.
- Domain: contrato IStockRepository, concepto/versión SriAnnulmentPolicy y snapshot
  de plazo requerido por RetentionAnnulmentRequest. Eliminado RetentionAnnulmentDeadline
  con su constante fiscal.
- Infrastructure: StockRepository, SriCatalogResolver, configuración tipada de política,
  fuentes normativas, migración EF y snapshot. No cambia la baseline.
- Pruebas: fixtures de Cancel/retención, casos Domain, parcial PostgreSQL de elegibilidad
  y parametrización mínima del helper de compra existente.
- Frontend: texto informativo de RetentionAnnulmentPanel y expectativa correspondiente;
  muestra la fecha del backend sin un día fiscal fijo.
- Documentación: ADR-044/índice, arquitectura backend, STATUS/FEATURES y espejos de agente.
  WORK_QUEUE no contiene una entrada contradictoria relacionada y queda intacto.

## C. SSOT global SRI reutilizada

ADR-037: esquema `global`, HasData → migraciones EF, `sri_normative_source`,
ISriCatalogResolver/SriCatalogResolver existentes. Concepto y versión tipados:
`sri_annulment_policy` y `sri_annulment_policy_version`. Vigencias inclusivas,
resolución única por fecha de emisión, fail-closed en huecos/solapamientos.

Historia conservada: 90 días (2016-02-22…2025-07-31) y día N del siguiente mes
(N vigente = 7 desde emisión 2025-08-01), con extensión al siguiente día hábil.
El día 10 original fue reformado antes de su aplicabilidad: no se inventa una
vigencia efectiva histórica. Fuentes oficiales y límites en ADR-044.

## D. ADR

[ADR-044](../decisions/ADR-044-purchase-cancellation-and-global-sri-policy.md), Accepted.
Incluye decisión conservadora aprobada, evidencia, cinco operaciones documentales,
norma, emitidos/recibidos, SSOT, impactos y alternativas rechazadas.

## E. Migración

`20261016121008_SriAnnulmentPolicyVersioning`, generada con EF.
Necesaria: las tablas anteriores de tarifas no representan plazos de anulación.
Crea concepto/versión y agrega fuentes normativas; sin reescribir documentos ni
plazos históricos de solicitudes. Aplicada exclusivamente en PostgreSQL 16 aislado
de Testcontainers. **No aplicada al piloto.** `has-pending-model-changes`: PASS.

## F. Tests focales

| Grupo | Resultado |
|---|---|
| PostgreSQL: elegibilidad, reproducción, permanencia del bloqueo tras reversa, entrada sola, concurrencia, scope, NC aplicada, vigencias/ambigüedad y Kardex | PASS 8/8 |
| PostgreSQL: reverso histórico válido con CxP/asiento y cascada terminal de retención | PASS 2/2 (un caso repetido del grupo anterior) |
| PostgreSQL: solicitud SRI con política global, sin reversos y con hold de CxP | PASS 1/1 |
| Application: CancelPurchaseHandlerTests + RetentionAnnulmentAccessTests | PASS 24/24 |
| Domain: RetentionSriAnnulmentDomainTests | PASS 25/25 |
| Architecture: ApiResponseContractTests | PASS 6/6 |
| UI: RetentionAnnulmentPanel | PASS 12/12 |
| TypeScript, build frontend y ESLint de los dos archivos UI | PASS |
| Build Infrastructure, modelo EF y git diff --check | PASS |
| architecture:check | PASS, 0 nuevas violaciones |

El bloqueo compara persistencia antes/después: compra Confirmed, cantidad/promedio/
valoración iguales, CxP con los mismos importes y estado, mismos asientos/estados,
mismos movimientos, sin reversos parciales. La carrera espera evidencia en pg_locks
antes de confirmar el escritor y verifica el rechazo posterior de Cancel.
Las fechas retroactivas de los movimientos no evaden el control de secuencia.
La prueba del calendario usa un calendario controlado, no un feriado productivo supuesto.
Total PostgreSQL: 10 casos distintos PASS; 11 ejecuciones al reforzar el caso contable.

No se ejecutaron suites generales de backend/frontend ni se enviaron comprobantes
al SRI real. El guard estático incluye sus propios self-tests.

## G. Hallazgos clasificados

- BUG REAL: faltaba el bloqueo por salidas posteriores; corregido.
- KEEP: costo histórico de Cancel válido; reglas fiscales y financieras existentes;
  flujos CLOSED de devolución/NC/Discard.
- EXTEND: elegibilidad, lock compartido y patrón de política SRI global versionada.
- STRUCTURAL DEBT: no hay calendario oficial nacional/local. La política representa
  la extensión; producción conserva OrdinaryDeadline informativo y el aviso para
  verificar el plazo efectivo en SRI en Línea. No se inventan feriados ni se considera
  el plazo ordinario como límite legal final. Sigue siendo obligatoria la confirmación
  fiscal ANULADO antes de completar el reverso de una retención autorizada.
- STRUCTURAL DEBT: no hay mecanismo adecuado de ventana interna de Cancel por Company.
  No se creó uno paralelo. Una futura política interna nunca podrá ampliar el límite SRI.
- DO NOT TOUCH: `.claude/` ya estaba sin seguimiento al iniciar; no se modificó.

## H. Suites largas — comandos para el usuario (PowerShell)

Desde el repositorio, con Docker disponible para PostgreSQL/Testcontainers:

```powershell
Set-Location C:\ProyectCursor\erp-saas
dotnet test .\backend\src\ERP.slnx --no-restore --logger "console;verbosity=minimal"

Push-Location .\frontend
npm.cmd run test:unit
npm.cmd run lint
npx.cmd tsc --noEmit
npm.cmd run build
Pop-Location
```

No se ejecutaron estos comandos de suites completas durante la tarea.
Los E2E Playwright requieren el entorno API/E2E configurado por separado.

## I. git status

Sin staging, commit ni push. Salida capturada al terminar la entrega:

```text
 M FEATURES.md
 M STATUS.md
 M backend/src/ERP.Application.Tests/Purchases/CancelPurchaseHandlerTests.cs
 M backend/src/ERP.Application.Tests/Retentions/RetentionAnnulmentAccessTests.cs
 M backend/src/ERP.Application/Common/ApiResponseCodes.cs
 M backend/src/ERP.Application/Common/ISriCatalogResolver.cs
 M backend/src/ERP.Application/Common/MessageCatalog.cs
 M backend/src/ERP.Application/Modules/Purchases/UseCases/CancelPurchaseUseCases.cs
 M backend/src/ERP.Application/Modules/Retentions/DTOs/RetentionAnnulmentRequestDto.cs
 M backend/src/ERP.Application/Modules/Retentions/Services/RetentionAnnulmentService.cs
 M backend/src/ERP.Domain.Tests/Retentions/RetentionSriAnnulmentDomainTests.cs
 M backend/src/ERP.Domain/Modules/Inventory/Interfaces/IStockRepository.cs
 M backend/src/ERP.Domain/Modules/Retentions/Entities/RetentionAnnulmentRequest.cs
 D backend/src/ERP.Domain/Modules/Retentions/RetentionAnnulmentDeadline.cs
 M backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseRetentionConfirmIntegrationTests.cs
 M backend/src/ERP.Infrastructure.Tests/TestData/RetentionElectronicTestWiring.cs
 M backend/src/ERP.Infrastructure/Migrations/ErpDbContextModelSnapshot.cs
 M backend/src/ERP.Infrastructure/Persistence/Catalogs/SriCatalogResolver.cs
 M backend/src/ERP.Infrastructure/Persistence/Configurations/SriCatalogs/SriNormativeSourceConfiguration.cs
 M backend/src/ERP.Infrastructure/Persistence/Repositories/Inventory/StockRepository.cs
 M docs/agent/DECISIONS.md
 M docs/agent/PROJECT_STATE.md
 M docs/architecture/backend.md
 M docs/decisions/README.md
 M frontend/src/modules/retentions/components/RetentionAnnulmentPanel.test.tsx
 M frontend/src/modules/retentions/components/RetentionAnnulmentPanel.tsx
?? .claude/
?? backend/src/ERP.Application/Modules/Purchases/Services/PurchaseCancellationEligibility.cs
?? backend/src/ERP.Domain/Modules/SriCatalogs/Entities/SriAnnulmentPolicy.cs
?? backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseRetentionConfirmIntegrationTests.CancellationEligibility.cs
?? backend/src/ERP.Infrastructure/Migrations/20261016121008_SriAnnulmentPolicyVersioning.Designer.cs
?? backend/src/ERP.Infrastructure/Migrations/20261016121008_SriAnnulmentPolicyVersioning.cs
?? backend/src/ERP.Infrastructure/Persistence/Configurations/SriCatalogs/SriAnnulmentPolicyConfiguration.cs
?? docs/agent/PURCHASE_CANCELLATION_DELIVERY.md
?? docs/decisions/ADR-044-purchase-cancellation-and-global-sri-policy.md
```
