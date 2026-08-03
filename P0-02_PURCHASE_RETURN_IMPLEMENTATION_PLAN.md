```text
DESIGN_STATUS: APPROVED
DESIGN_APPROVED: YES
IMPLEMENTATION_PLAN_STATUS: APPROVED
IMPLEMENTATION_PLAN_APPROVED: YES
IMPLEMENTATION_AUTHORIZED: YES
PHASE_0_STATUS: COMPLETED
PHASE_0_ACCEPTED: YES
PHASE_1_AUTHORIZED: NO
```

**Nota de autorización de implementación**: `IMPLEMENTATION_AUTHORIZED` pasó a `YES` por autorización formal explícita del usuario (2026-07-31), posterior a `FINAL_ARB_REVIEW: APPROVED`. Esta autorización habilita el inicio de FASE 0 exclusivamente — cada fase posterior sigue requiriendo su propio `PHASE_X_ACCEPTED: YES` antes de que la siguiente pueda comenzar (sección 15 de este plan).

**Nota de enmienda (Architecture Review Board, corrección documental controlada)**: esta versión del plan corrigió los seis hallazgos del veredicto ARB (`FINAL_PLAN_REVIEW: REJECTED`) sobre la versión previa — `PLAN-REV-01` (BLOCKER, propagación de `BranchId` desde el diseño enmendado, §5.2 del diseño), `PLAN-REV-02` (catálogo de errores: 44 códigos, no 39), `PLAN-REV-03` (nota de integridad: SHA-256 tiene 64 caracteres, no 66, con el hash actualizado del diseño enmendado), `PLAN-REV-04` (19 apartados obligatorios por fase, no 18), `PLAN-REV-05` (numeración `### 11.1`–`### 11.6` bajo `## 11`, no `### 10.x`) y `PLAN-REV-06` (el diseño fuente pasó de `AMENDED_DRAFT_FOR_REVIEW` a `APPROVED` tras la segunda revisión ARB, una vez corregido también el hallazgo residual `P0-02-ARB2-01` — `DESIGN_APPROVED: YES`).

# Plan de Implementación — PurchaseReturn + SupplierCredit (P0-02)

**Tipo de documento:** Plan de implementación técnico. No contiene código, no modifica archivos productivos, no crea ADR, no ejecuta ninguna fase.
**Fecha:** 2026-07-31
**Basado en:** `P0-02_PURCHASE_RETURN_DESIGN.md` (diseño técnico definitivo, autoridad funcional y técnica de P0-02; hash declarado en el encargo de esta tarea: ver Nota de integridad al final del documento). Documentos de evidencia adicional: `P0-02_PURCHASE_RETURN_AUDIT.md`, `P0-02_PURCHASE_RETURN_AUDIT_CLOSURE.md`. Plan hermano usado como plantilla de formato y patrones reutilizables: `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` (COMPLETED/CLOSED 2026-07-31).

Este plan **no** reinterpreta ni reemplaza ninguna decisión del diseño aprobado. Donde el código real del repositorio contradice una premisa del diseño, se documenta como **bloqueante** en la sección correspondiente (formato: Evidencia real / Decisión aprobada afectada / Fase afectada / Por qué bloquea / Qué debe resolverse antes de implementar) — nunca se resuelve aquí de forma silenciosa.

---

## 1. Estado y autorización

| Campo | Valor |
|---|---|
| Diseño fuente | `P0-02_PURCHASE_RETURN_DESIGN.md` — `DESIGN_STATUS: APPROVED`, `DESIGN_APPROVED: YES` (aprobado documentalmente en la segunda revisión ARB, tras corregir `PLAN-REV-01`/`PLAN-REV-06` y el hallazgo residual `P0-02-ARB2-01`; la aprobación documental no autoriza por sí sola la implementación, ver §1 del diseño) |
| Este plan | `IMPLEMENTATION_PLAN_STATUS: APPROVED` |
| Aprobación de este plan | `IMPLEMENTATION_PLAN_APPROVED: YES` — aprobado en la segunda revisión ARB |
| Autorización de implementación | `IMPLEMENTATION_AUTHORIZED: YES` — autorizado formalmente por el usuario (2026-07-31); habilita el inicio de FASE 0. Las fases 1-14 permanecen bloqueadas hasta que cada `PHASE_X_ACCEPTED` precedente esté en `YES` (sección 15) |

Este documento no se autoaprueba. Ningún agente o desarrollador puede tratar la existencia de este archivo como autorización para escribir código de producción.

---

## 2. Objetivo

Convertir el diseño técnico aprobado de `PurchaseReturn + SupplierCredit` (P0-02) en una secuencia de 15 fases (0–14) ejecutables de forma incremental, cada una compilable, testeable y aceptable de forma aislada mediante un gate binario (`PHASE_X_ACCEPTED: YES/NO`), sin dejar ninguna decisión abierta y sin trasladar al backlog ningún elemento que el diseño (§16.2, §16.2ter, §16.3, §16.5, §25.2) declare bloqueante.

Este plan **no autoriza** ejecutar ninguna fase — solo ordena el trabajo futuro.

---

## 3. Fuentes y jerarquía de autoridad

| Nivel | Documento | Rol |
|---|---|---|
| 1 | `CLAUDE.md` + `AI-RULES/**` | Reglas generales del repositorio (Estándar de Precisión Numérica, Fechas, Infraestructuras CLOSED: Secuencias Documentales ADR-019, Entity Tracking ADR-020, Configuración Tributaria, Item Types, Auditoría ADR-022, ElectronicDocuments ADR-023) — vinculante sobre *cómo* se construye |
| 2 | `P0-02_PURCHASE_RETURN_DESIGN.md` | Autoridad funcional y técnica de P0-02 — *qué* se construye, sin reinterpretación |
| 3 | `P0-02_PURCHASE_RETURN_AUDIT.md` / `P0-02_PURCHASE_RETURN_AUDIT_CLOSURE.md` | Evidencia histórica que originó el diseño — contexto, no autoridad normativa nueva |
| 4 | `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` / `P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md` | Plantilla de formato/nivel de detalle y fuente de patrones reutilizables (advisory lock, idempotencia, translators, RHF+Zod) — nunca autoridad sobre decisiones específicas de P0-02 |
| 5 | Código real del repositorio (inspeccionado para este plan) | Verificación empírica de rutas, nombres, firmas — cualquier discrepancia con el diseño se documenta como bloqueante, nunca se resuelve por inferencia |

Ante cualquier contradicción entre este plan y `P0-02_PURCHASE_RETURN_DESIGN.md`, prevalece el diseño — este plan se corrige, nunca al revés.

---

## 4. Alcance incluido

Las 15 fases de este plan cubren, en conjunto, la totalidad de §24 (Cambios previstos por capa) del diseño: modelo de dominio completo (`PurchaseReturn`, `PurchaseReturnDetail`, `SupplierCredit`, `SupplierCreditMovement`, `PurchaseReturnSequence`, `CompanyFinancialDestination`, `SupplierCreditRefundTransaction`, auditorías de dominio), persistencia e índices, endurecimiento de los 5 handlers existentes de Compras/Finance, administración limitada de `CompanyFinancialDestination`, ciclo completo de `PurchaseReturn` (Draft → Authorize → Cancel), aplicación/reversa de `SupplierCredit`, reembolso/reversa con destino financiero real, registro de NC recibida, invariantes cruzadas de §5.1, API + permisos, frontend de Compras y de Finance, e integración/regresión final.

---

## 5. Exclusiones

Explícitamente fuera de este plan (idénticas a §25.1 del diseño, sin ampliación): validación en línea de la NC contra el SRI; conciliación XML avanzada de la NC; lotes/series; Nota de Débito emitida por el comprador; cardinalidad N:M `PurchaseReturn ↔ PurchaseReceptionDocument`; mejoras visuales/UX no indispensables; refactors generales no requeridos por el diseño. Ningún ítem de §25.2 del diseño (concurrencia, idempotencia, locks, crédito, contabilidad, retención, trazabilidad, pruebas de validación previa) se traslada a esta sección ni al backlog de la §17 de este plan — todos están asignados a una fase obligatoria (ver matriz §11.2/§11.6).

---

## 6. Decisiones inmutables heredadas del diseño

Lista de control — cada decisión se referencia por su origen en el diseño, no se repite su desarrollo completo aquí:

1. `PurchasePayable.BalanceDue` es el SSOT del saldo (§3.4, §11, §12) — nunca `Status`.
2. `SupplierCreditMovement` es el SSOT del saldo del crédito, fórmula completa con signo (§13.5).
3. `SupplierCreditRefundTransaction` es el hecho financiero del reembolso/reversa, con `AccountingAccountId` congelado (§6.4bis).
4. `PurchaseReturnSequence` es independiente de `DocumentSequence`, participa en la transacción ambiente de `Authorize()`, nunca abre transacción propia (§7.1bis).
5. La NC del proveedor se registra como recibida — el ERP nunca emite NC propia (§3.13, §18).
6. La devolución usa exclusivamente la bodega original congelada de cada línea — sin selector de usuario (§14.2).
7. Stock insuficiente en cualquier línea bloquea toda la autorización — todo o nada (§4.3, §14.2).
8. `SourceType` de `SupplierCredit` fue eliminado explícitamente — `SourcePurchaseReturnId` es la única referencia de origen (§6.1).
9. Sin CRUD genérico para `CompanyFinancialDestination` — solo 4 casos de uso limitados, campos estructurales inmutables tras creación (§6.4ter, §24).
10. Sin eliminación física en ningún componente — solo `IsActive`/estados terminales (§3.21, regla general del proyecto).
11. Cardinalidad NC↔`PurchaseReturn` es 1:1 en v1 (§18.3).
12. No se modifica el Posting Engine, `DocumentSequence`, `ElectronicDocuments`/RIDE, el esquema de `Payment`/`PaymentApplicationLine`, el esquema de Caja, ni `Account` (§24, filas "elemento expresamente prohibido").
13. Locks: siempre Lock A antes que Lock B; múltiples Lock A en orden ascendente de `PurchaseInvoiceId` (§15.4).
14. Las 8 operaciones idempotentes (§16.2) son obligatorias, no opcionales — `ClientRequestId` + `RequestPayloadHash` incluyendo el agregado objetivo.
15. La prueba de §16.3 (interacción `SaveChangesWithSequenceRetryAsync` + transacción explícita) y las 26 pruebas de §16.5 son prerrequisitos de implementación — nunca backlog.

---

## 7. Inventario verificado del repositorio

Verificado por inspección directa (Glob/Grep/Read) antes de redactar este plan — clasificación según §13 de la especificación de esta tarea.

### 7.1 Backend — componentes EXISTING_VERIFIED

| Componente | Ruta verificada |
|---|---|
| `PurchasePayable` (entidad) | `backend/src/ERP.Domain/Modules/Purchases/Entities/PurchasePayable.cs` |
| `PurchasePayableConfiguration` | `backend/src/ERP.Infrastructure/Persistence/Configurations/Purchases/PurchasePayableConfiguration.cs` |
| `PurchasePayableRepository` | `backend/src/ERP.Infrastructure/Persistence/Repositories/Purchases/PurchasePayableRepository.cs` |
| `PurchasePayableUseCases.cs` | `backend/src/ERP.Application/Modules/Purchases/UseCases/PurchasePayableUseCases.cs` |
| `PurchasePayablesController` | `backend/src/ERP.API/Controllers/PurchasePayablesController.cs` |
| `RegisterPaymentCommandHandler` / `ReversePaymentCommandHandler` | `backend/src/ERP.Application/Modules/Finance/UseCases/Payments/PaymentUseCases.cs` (líneas 215 y 391 respectivamente — mismo archivo, ambas clases) |
| `IssueWithholdingHandler` | `backend/src/ERP.Application/Modules/Purchases/UseCases/IssueWithholdingUseCases.cs` |
| `CancelWithholdingHandler` | `backend/src/ERP.Application/Modules/Purchases/UseCases/CancelWithholdingUseCases.cs` |
| `CancelPurchaseHandler` | `backend/src/ERP.Application/Modules/Purchases/UseCases/CancelPurchaseUseCases.cs` (línea 35) |
| `StockMovement` (entidad) | `backend/src/ERP.Domain/Modules/Inventory/Entities/StockMovement.cs` |
| `StockMovementConfiguration` | `backend/src/ERP.Infrastructure/Persistence/Configurations/Inventory/StockMovementConfiguration.cs` |
| `IStockRepository` / `StockRepository` (`AppendMovementAsync`) | `backend/src/ERP.Domain/Modules/Inventory/Interfaces/IStockRepository.cs` / `backend/src/ERP.Infrastructure/Persistence/Repositories/Inventory/StockRepository.cs` |
| `PurchaseReceptionDocument` | `backend/src/ERP.Domain/Modules/Purchases/PurchaseReception/Entities/PurchaseReceptionDocument.cs` |
| `PurchaseReceptionDocumentConfiguration` / `PurchaseReceptionDocumentRepository` | `backend/src/ERP.Infrastructure/Persistence/Configurations/Purchases/PurchaseReceptionDocumentConfiguration.cs` / `backend/src/ERP.Infrastructure/Persistence/Repositories/Purchases/PurchaseReceptionDocumentRepository.cs` |
| `SalesReturnRepository` (patrón de referencia de advisory lock por documento — `AcquireReturnLockAsync`) | `backend/src/ERP.Infrastructure/Persistence/Repositories/Sales/SalesReturnRepository.cs` |
| `Account` (entidad) | `backend/src/ERP.Domain/Modules/Accounting/Entities/Account.cs` |
| `CashRegister` (entidad) | `backend/src/ERP.Domain/Modules/Caja/Entities/CashRegister.cs` |
| `PaymentMethod` (catálogo) | `backend/src/ERP.Domain/Modules/Sales/Entities/PaymentMethod.cs` (vive en `Modules/Sales`, no en un módulo `Caja`/`Finance` propio — confirmado, coherente con §6.4 del diseño) |
| `DocumentSequenceRepository` (`CaptureNextAsync`, patrón de advisory lock a referenciar por analogía, nunca reutilizar — §7.1bis) | `backend/src/ERP.Infrastructure/Persistence/Repositories/DocumentSequenceRepository.cs` |
| `AuditRecordBase` | `backend/src/ERP.Domain/Audit/AuditRecordBase.cs` |
| `IUnitOfWork` | `backend/src/ERP.Application/Modules/Common/IUnitOfWork.cs` |
| `PostingIdempotencyGuard`/gates SEQ | `backend/src/ERP.Infrastructure.Tests/Persistence/DocumentSequenceExclusivityTests.cs` (`SEQ-GATE-01..04`) |
| `NewChildEntityTrackingArchitectureTests.cs` (`ATT-GATE-01`) | `backend/src/ERP.Infrastructure.Tests/Persistence/NewChildEntityTrackingArchitectureTests.cs` |
| `ERP.Domain/Modules/Finance/Entities/` (existente — `Payment`, `PaymentApplicationLine`, `CreditTerm`, `CreditInstallment`) | `backend/src/ERP.Domain/Modules/Finance/Entities/` — **confirmado sin `CompanyFinancialDestination` ni `SupplierCreditRefundTransaction` ni `SupplierCredit`** (no existen todavía, verificado por listado directo) |
| `frontend/src/modules/purchases/` | Existe (`styles/purchase-reception.css` y otros — confirmado por Glob) |
| `frontend/src/modules/finance/` | Existe (confirmado por listado de directorios) |
| `SalesReturnEndToEndTests.cs` (patrón de referencia E2E) | `backend/src/ERP.API.Tests/Integration/SalesReturnEndToEndTests.cs` |
| `AuthorizeSalesReturnUseCases.cs`/`AuthorizeSalesUseCases.cs` (patrón de referencia de autorización con inventario) | `backend/src/ERP.Application/Modules/Sales/UseCases/` |

### 7.2 Backend — componentes NEW_CONFIRMED_BY_DESIGN

Todos los listados en §24 del diseño sin equivalente existente: `PurchaseReturn`, `PurchaseReturnDetail`, `PurchaseReturnAudit`, `SupplierCredit`, `SupplierCreditMovement`, `SupplierCreditAudit`, `PurchaseReturnSequence` (+ `IPurchaseReturnSequenceRepository`) en `ERP.Domain/Modules/Purchases/`; `CompanyFinancialDestination`, `SupplierCreditRefundTransaction`, `CompanyFinancialDestinationAudit` en `ERP.Domain/Modules/Finance/Entities/` (confirmado que no existen — §7.1); enums `PurchaseReturnStatus`/`PurchaseReturnFiscalStatus`/`SupplierCreditMovementType` en `ERP.Domain/Modules/Purchases/Enums/`; enums `FinancialDestinationTypeCode`/`RefundTransactionTypeCode` en `ERP.Domain/Modules/Finance/Enums/`.

### 7.3 Backend — componentes TO_BE_LOCATED_BEFORE_PHASE

| Elemento | Nombre lógico aprobado | Ubicación arquitectónica | Ruta final a verificar antes de crear | Patrón de referencia |
|---|---|---|---|---|
| Solución .NET | — | Raíz de `backend/` | **No se encontró ningún archivo `.sln`** en el repositorio (verificado con búsqueda recursiva) — los proyectos se compilan/testean individualmente por carpeta de proyecto (`dotnet test <Proyecto>` desde `backend/src/<Proyecto>`, confirmado en `docs/DEVELOPMENT.md` líneas 191-194). Este plan usa esa misma convención en todos los "Comandos de validación" — no asume un `.sln` inexistente | `docs/DEVELOPMENT.md` |
| `ERP.Domain/Modules/Purchases/Interfaces/IPurchaseReturnSequenceRepository.cs` | `IPurchaseReturnSequenceRepository` | `ERP.Domain/Modules/Purchases/Interfaces/` | A verificar que la carpeta `Interfaces/` de `Modules/Purchases` sigue el mismo patrón que `Modules/Finance/Interfaces/` (confirmado existente) antes de crear el archivo | `ERP.Domain/Modules/Finance/Interfaces/IPaymentRepository.cs` |
| `ERP.Application/Modules/Finance/UseCases/` (para `SupplierCredit`) | `ApplySupplierCreditUseCases`, `RegisterSupplierCreditRefundUseCases`, etc. | `ERP.Application/Modules/Finance/UseCases/` | Carpeta ya existe (`Payments/` confirmado) — verificar convención de subcarpeta antes de agregar `SupplierCredit/` | `ERP.Application/Modules/Finance/UseCases/Payments/PaymentUseCases.cs` |

### 7.4 Frontend — inventario

`frontend/src/modules/purchases/` y `frontend/src/modules/finance/` existen y contienen ya código de producción (confirmado por listado). Antes de la Fase 12/13, se debe volver a listar el contenido exacto de ambas carpetas (páginas, servicios, schemas ya existentes) para la auditoría de reutilización obligatoria (`AI-RULES/FRONTEND-RULES.md`) — no asumido en este plan, es una tarea explícita de la Fase 12 (ver Comandos de validación de esa fase).

### 7.5 Comandos reales verificados

| Comando | Fuente verificada |
|---|---|
| `dotnet test ERP.Domain.Tests` (desde `backend/src/ERP.Domain.Tests`) | `docs/DEVELOPMENT.md` líneas 191-194 |
| `dotnet test ERP.Infrastructure.Tests` | Ídem |
| `dotnet test ERP.Application.Tests` | Ídem |
| `dotnet test ERP.API.Tests` | Ídem |
| `dotnet test ERP.Architecture.Tests` (proyecto confirmado existente, 20 archivos `.cs`, no listado explícitamente en `DEVELOPMENT.md` pero verificado por `.csproj` real) | `backend/src/ERP.Architecture.Tests/ERP.Architecture.Tests.csproj` |
| `npm run lint` | `frontend/package.json` |
| `npm run build` (`tsc -b && node ../tools/ci/run-platform-guard.mjs && vite build`) | `frontend/package.json` |
| `npm run test:unit` (vitest) | `frontend/package.json` |
| `npm run test:e2e` (playwright) | `frontend/package.json` |
| `npm run architecture:check` / `architecture:backend` / `architecture:design-system` | `frontend/package.json` |
| PostgreSQL real para pruebas de integración: `Testcontainers.PostgreSql` (paquete ya referenciado en `ERP.Infrastructure.Tests.csproj`, mismo patrón que `DocumentSequenceConcurrencyTests.cs`/`StockMovementBranchOwnershipIntegrationTests.cs`) — único prerrequisito real: Docker Desktop activo | Verificado empíricamente en Fase 0 — `.\scripts\dev-restart.ps1`, citado en una versión anterior de este plan, no existe en el repositorio y no es necesario |
| `dotnet ef migrations add <Name>` (desde `ERP.Infrastructure`, `--startup-project ../ERP.API/ERP.API.csproj`) | `docs/DEVELOPMENT.md` |

Ningún comando de este plan se inventa — todos están verificados contra el repositorio real.

---

## 8. Dependencias entre componentes

```
Fase 0 (prerrequisitos empíricos)
   │
   ▼
Fase 1 (dominio: PurchaseReturn/SupplierCredit/CompanyFinancialDestination/SupplierCreditRefundTransaction/PurchaseReturnSequence/PurchasePayable ext./StockMovement ext./PurchaseReceptionDocument ext.)
   │
   ▼
Fase 2 (persistencia: EF configs, migración, repos, locks A/B)
   │
   ├──▶ Fase 3 (endurecimiento de los 5 handlers existentes — depende solo de Fase 2: xmin + Lock A)
   │
   ├──▶ Fase 4 (administración CompanyFinancialDestination — depende de Fase 2)
   │
   ▼
Fase 5 (Draft + consultas de PurchaseReturn — depende de Fases 1,2)
   │
   ▼
Fase 6 (Authorize — depende de Fases 1,2,3,5; usa Lock A ya endurecido en Fase 3)
   │
   ├──▶ Fase 7 (Apply/Reverse SupplierCredit — depende de Fase 6 + Fase 3 (Lock A en destino))
   │
   ├──▶ Fase 8 (Refund/ReverseRefund — depende de Fase 6 + Fase 4 (CompanyFinancialDestination activo))
   │
   ├──▶ Fase 9 (registro NC recibida — depende de Fase 6, columna CurrencyCode de Fase 1)
   │
   ▼
Fase 10 (Cancel + invariantes cruzadas §5.1 — depende de Fases 3,6,7,8,9)
   │
   ▼
Fase 11 (API + permisos — depende de Fases 4,5,6,7,8,9,10)
   │
   ├──▶ Fase 12 (Frontend Compras — depende de Fase 11)
   │
   ├──▶ Fase 13 (Frontend Finance — depende de Fase 11)
   │
   ▼
Fase 14 (Integración, regresión y cierre — depende de Fases 0-13 completas)
```

Ninguna fase depende de una fase **posterior** — cada una es compilable y testeable de forma aislada en el momento de cerrarse, igual principio que `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` §1.

---

## 9. Estrategia de implementación incremental

- Cada fase toca una superficie principal identificable (Dominio → Infraestructura → Application por partes → API → Frontend), replicando el principio de secuenciación de `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` §1.
- Las fases de mayor riesgo (concurrencia de `PurchasePayable`/`SupplierCredit`, dinero real vía `CompanyFinancialDestination`/Caja, numeración `PurchaseReturnSequence`) están deliberadamente separadas de las fases de bajo riesgo (CRUD de Draft, consultas, administración de catálogo).
- La Fase 0 es un gate empírico bloqueante: ninguna fase de código productivo (Fase 1 en adelante) puede comenzar si su prueba de PostgreSQL real (§16.3 del diseño) no se ejecuta y aprueba primero.
- Las pruebas obligatorias no bloqueantes-como-backlog (§16.2ter, §16.3, §16.5 del diseño — 26 pruebas) se ejecutan como prerrequisito de las fases que codifican la operación correspondiente (Fase 6 para §16.3, Fases 6/7/9 para §16.2ter representativo de "crea agregado"/"actúa sobre existente", Fase 8 para las 26 de §16.5) — nunca se difieren a la Fase 14.
- El endurecimiento de los 5 handlers existentes (Fase 3) se aísla deliberadamente antes de introducir el nuevo agregado `PurchaseReturn`, para poder validar que `xmin` + Lock A no rompen ningún comportamiento actual de Compras/Finance antes de sumarle la complejidad de la devolución.

---

## 10. Fases de implementación

Cada fase incluye los 19 apartados obligatorios. Donde un apartado no aplica, se indica explícitamente con justificación — nunca se omite.

### FASE 0 — Prerrequisitos empíricos y línea base

**Objetivo**: Establecer la línea base real del repositorio (build/tests en verde) y ejecutar, antes de cualquier código productivo, la prueba de PostgreSQL real exigida por §16.3 del diseño (interacción `SaveChangesWithSequenceRetryAsync` + transacción explícita ambiente + advisory lock + conflicto de secuencia forzado). Sin esta fase aprobada, ninguna fase de código puede comenzar.

**Dependencias**: Ninguna — fase raíz.

**Archivos existentes a modificar**: Ninguno (fase de verificación, no de escritura de producción).

**Archivos nuevos a crear**:
- `ERP.Infrastructure.Tests/Persistence/PurchaseReturnSequenceTransactionInteractionTests.cs` (prueba exigida por §16.3/§7.1bis del diseño — reproduce: transacción explícita del handler → advisory lock (`pg_advisory_xact_lock`) → `SaveChangesWithSequenceRetryAsync` → conflicto de secuencia forzado sobre `CurrentStock` dentro de esa transacción ambiente → verificación de si `RecoverFromConflictAndRetrackAsync` reintenta con éxito o si PostgreSQL aborta la transacción completa).

**Cambios exactos**:
1. Ejecutar `dotnet test ERP.Domain.Tests`, `ERP.Application.Tests`, `ERP.Infrastructure.Tests`, `ERP.API.Tests`, `ERP.Architecture.Tests` y registrar el conteo exacto de pruebas en verde como línea base (sin modificar código).
2. Ejecutar `npm run lint`, `npm run build`, `npm run test:unit`, `npm run architecture:check` en `frontend/` y registrar el resultado como línea base.
3. Escribir y ejecutar la prueba de integración de §16.3 contra PostgreSQL real vía `Testcontainers.PostgreSql` (mecanismo real ya usado por `ERP.Infrastructure.Tests` — `.\scripts\dev-restart.ps1` no existe en el repositorio y no es necesario; único prerrequisito real: Docker Desktop activo).
4. Revisar (solo lectura, sin modificar) los patrones reutilizables de `SalesReturnRepository.cs` (advisory lock por documento), `PurchaseInvoiceConfirmedPostingTranslator.cs`/`SalesReturnAuthorizedPostingTranslator.cs` (translators), `SalesReturnAudit`/`SalesReturnAuditHandler` (Entity Audit), `CajaPage.tsx`/`cajaService.ts` (Caja), y `frontend/src/modules/sales/` (formularios RHF+Zod) — documentar hallazgos en el resultado de esta fase, sin copiarlos aún.

**Elementos expresamente fuera de alcance**: Ninguna entidad de dominio nueva; ninguna migración; ningún endpoint.

**Invariantes protegidas**: No aplica — justificación: esta fase no muta ningún invariante de negocio, solo verifica el comportamiento de infraestructura ya existente.

**Locks y orden de adquisición**: El propio advisory lock transaccional bajo prueba (namespace de prueba dedicado, distinto de `"PurchaseInvoice.FinancialLock"`/`"SupplierCredit.Lock"`/`"PurchaseReturn.Sequence"` para no colisionar con locks de producción futuros).

**Frontera transaccional**: La de la prueba misma — una transacción explícita por escenario, replicando exactamente el patrón que usará `AuthorizePurchaseReturnUseCases` en la Fase 6.

**Idempotencia**: No aplica — justificación: esta fase no expone ninguna operación de negocio idempotente, es una prueba de infraestructura.

**Errores de negocio involucrados**: Ninguno propio — justificación: los códigos `PR-*`/`SC-*` no existen hasta la Fase 1.

**Pruebas unitarias**: No aplica en esta fase — justificación: el objeto bajo prueba es la interacción con PostgreSQL real, no lógica de dominio aislable.

**Pruebas de integración**: La descrita en "Archivos nuevos a crear" — mínimo los escenarios: (a) conflicto de secuencia dentro de transacción explícita con advisory lock ya adquirido, reintento in-process exitoso; (b) mismo escenario con la transacción abortada por PostgreSQL tras el primer error — demostrar cuál de los dos ocurre realmente.

**Pruebas PostgreSQL reales**: Es el objeto completo de esta fase — ver "Archivos nuevos a crear".

**Pruebas frontend**: No aplica — justificación: esta fase es exclusivamente backend/infraestructura.

**Comandos de validación**:
```
cd backend/src/ERP.Domain.Tests && dotnet test
cd backend/src/ERP.Application.Tests && dotnet test
cd backend/src/ERP.Infrastructure.Tests && dotnet test
cd backend/src/ERP.API.Tests && dotnet test
cd backend/src/ERP.Architecture.Tests && dotnet test
cd frontend && npm run lint && npm run build && npm run test:unit && npm run architecture:check
```

**Criterios de aceptación**:
- Línea base de build/tests registrada (0 errores de compilación, conteo exacto de tests en verde por proyecto). **Cumplido** — backend: Domain 447/447, Infrastructure 273/273 (incluye las 2 pruebas nuevas de §16.3), Architecture 97/97, Application 623/625 (2 fallos preexistentes ajenos a P0-02), API 235/236 (1 fallo preexistente ajeno a P0-02); frontend: `test:unit` 248/248 (`lint`/`build`/`architecture:check` con fallos preexistentes ajenos a Compras, ver deuda preexistente abajo).
- La prueba de §16.3 ejecutada y su resultado determina explícitamente el patrón que la Fase 6 debe usar para `AuthorizePurchaseReturnUseCases`. **Cumplido — resultado empírico (4/4 corridas deterministas contra PostgreSQL real vía Testcontainers.PostgreSql, sin `PostgresException 25P02`): el reintento in-process de `SaveChangesWithSequenceRetryAsync` se recupera con éxito dentro de la misma transacción explícita ambiente, gracias al `SAVEPOINT` implícito que EF Core/Npgsql crea automáticamente ante una transacción externa abierta. Ninguna de las dos alternativas planteadas requiere código nuevo: `AuthorizePurchaseReturnUseCases` reutilizará sin modificación la composición ya usada por `AuthorizeSalesReturnUseCases` (`BeginTransactionAsync` → advisory lock → `SaveChangesWithSequenceRetryAsync` → `CommitAsync`), sin `SAVEPOINT` manual y sin reapertura de transacción.**
- Revisión de patrones reutilizables documentada (lista concreta de archivos y qué se reutilizará de cada uno, no una afirmación genérica). **Cumplido** — `SalesReturnRepository.cs` (advisory lock + `StableHash`), `PurchaseInvoiceConfirmedPostingTranslator.cs`/`SalesReturnAuthorizedPostingTranslator.cs` (translator contable), `SalesReturnAudit`/`SalesReturnAuditHandler` (Entity Audit), `CajaPage.tsx`/`cajaService.ts` (composición de página), `AuthorizeSalesReturnModal.tsx`/`salesReturnSchema.ts` (F-V1..F-V8).

**Condiciones de detención**: Si la prueba de §16.3 no puede ejecutarse contra PostgreSQL real (ausencia de entorno), la Fase 0 no se aprueba y ninguna fase posterior puede iniciar código de `Authorize()`. **No se activó** — la prueba se ejecutó con éxito contra PostgreSQL real. Si la línea base de tests tiene fallos preexistentes no relacionados con P0-02, se documentan explícitamente como deuda preexistente antes de continuar (no se corrigen en esta fase salvo que bloqueen la propia prueba de §16.3). **Deuda preexistente registrada, sin corregir, sin atribuir a P0-02, sin efecto bloqueante sobre esta fase**: 2 fallos en `ERP.Application.Tests.Inventory.ItemMatching.ItemMatchFinderTests`; 1 fallo en `ERP.API.Tests.Integration.PostgreSqlSecurityIntegrationTests.PG_unique_business_partner_identification_enforced`; `npm run lint` (265 errores/32 warnings preexistentes); `npm run build` (falla por `TS1127` en `MasterDataBusinessPartnerDetailPage.tsx:243`, ajeno a Compras); `npm run architecture:check` (760 violaciones preexistentes, generalizado en todo el frontend). Ninguna de estas fallas toca `StockRepository`, `StockMovement` ni ningún archivo de `PurchaseReturn`, y ninguna bloqueó la prueba de §16.3.

**Entregable de la fase**: Documento de resultado de la prueba de §16.3 (patrón elegido para Fase 6, con evidencia) + línea base de build/tests registrada + lista de patrones reutilizables confirmados. **Entregado** — ver criterios de aceptación arriba y prueba real en `backend/src/ERP.Infrastructure.Tests/Persistence/PurchaseReturnSequenceTransactionInteractionTests.cs`.

`PHASE_0_STATUS: COMPLETED`
`PHASE_0_ACCEPTED: YES`

---

### FASE 1 — Modelo de dominio

**Objetivo**: Modelar en `ERP.Domain` el agregado `PurchaseReturn`/`PurchaseReturnDetail`, el agregado `SupplierCredit`/`SupplierCreditMovement`, las entidades nuevas de Finance (`CompanyFinancialDestination`, `SupplierCreditRefundTransaction`), `PurchaseReturnSequence`, las auditorías (`PurchaseReturnAudit`, `SupplierCreditAudit`, `CompanyFinancialDestinationAudit`), y las extensiones a `PurchasePayable`/`StockMovement`/`PurchaseReceptionDocument` — sin persistencia ni casos de uso, compilable y testeable de forma aislada (mismo principio que `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` Fase 1).

**Dependencias**: Fase 0 aprobada (patrón de `Authorize()` ya decidido).

**Archivos existentes a modificar**:
- `ERP.Domain/Modules/Purchases/Entities/PurchasePayable.cs` — agregar `ReturnAppliedAmount`, `SupplierCreditAppliedAmount`, `ApplyReturnCredit()`, `ReverseReturnCredit()`, `ApplySupplierCredit()`, `ReverseSupplierCredit()`, extender fórmula `BalanceDue` (§12 del diseño). Cambio estrictamente aditivo — ningún método existente (`RegisterPayment`/`ReversePayment`/`ApplyRetention`/`ReverseRetention`/`CancelPayable`) cambia de firma ni de comportamiento.
- `ERP.Domain/Modules/Inventory/Entities/StockMovement.cs` — agregar `SourceDocLineId (Guid?)`, genérico, nullable (§10.3).
- `ERP.Domain/Modules/Purchases/PurchaseReception/Entities/PurchaseReceptionDocument.cs` — agregar `CurrencyCode (string, NOT NULL)`, y el parámetro `currencyCode` en el factory `Create(...)` ya existente (§18.1bis/§18.2) — sin cambiar el resto de la firma ni el comportamiento del parser TXT existente.

**Archivos nuevos a crear**:
- `ERP.Domain/Modules/Purchases/Entities/PurchaseReturn.cs`, `PurchaseReturnDetail.cs`, `PurchaseReturnAudit.cs`, `SupplierCredit.cs`, `SupplierCreditMovement.cs`, `SupplierCreditAudit.cs`, `PurchaseReturnSequence.cs`.
- `ERP.Domain/Modules/Purchases/Enums/PurchaseReturnStatus.cs`, `PurchaseReturnFiscalStatus.cs` (3 valores), `SupplierCreditMovementType.cs` (5 valores).
- `ERP.Domain/Modules/Purchases/Events/PurchaseReturnAuthorizedEvent.cs`, `PurchaseReturnCancelledEvent.cs`, `SupplierCreditAppliedEvent.cs`, `SupplierCreditApplicationReversedEvent.cs`, `SupplierCreditRefundedEvent.cs`, `SupplierCreditRefundReversedEvent.cs`.
- `ERP.Domain/Modules/Finance/Entities/CompanyFinancialDestination.cs`, `SupplierCreditRefundTransaction.cs`, `CompanyFinancialDestinationAudit.cs`.
- `ERP.Domain/Modules/Finance/Enums/FinancialDestinationTypeCode.cs` (2 valores), `RefundTransactionTypeCode.cs` (2 valores).
- `ERP.Domain/Modules/Purchases/Interfaces/IPurchaseReturnSequenceRepository.cs` (firma únicamente — implementación en Fase 2).
- `ERP.Domain.Tests/Purchases/PurchaseReturnTests.cs`, `PurchaseReturnDetailTests.cs`, `SupplierCreditTests.cs`, `SupplierCreditMovementTests.cs`, `PurchasePayableTests.cs` (extensión de la suite ya existente — `backend/src/ERP.Application.Tests/Purchases/PurchasePayableTests.cs` confirmado existente, pero como está en `Application.Tests`, la extensión de dominio va en un archivo nuevo o extendiendo el existente según ubicación real — a verificar exactamente en el momento de implementación cuál proyecto aloja los tests de dominio de `PurchasePayable` hoy).
- `ERP.Domain.Tests/Finance/CompanyFinancialDestinationTests.cs`, `SupplierCreditRefundTransactionTests.cs`.

**Cambios exactos**:
1. `PurchaseReturn.CreateDraft(...)` exige `tenantId`, `companyId`, `branchId` (obligatorio, `Guid`, no nullable — Branch Ownership Rule, §5.2 del diseño; resuelto en Application desde `ICurrentBranch.BranchId`, nunca recibido del comando/DTO, Fase 5), `purchaseInvoiceId`, `supplierId` (snapshot), `reason` no vacío, líneas ≥1 con `originalInvoiceDetailId`+`quantity`; `FiscalStatus = NotApplicable`. `BranchId` no expone setter público ni método `ChangeBranch`/`SetBranch`.
2. `PurchaseReturn.Authorize(returnNumber, ...)` congela líneas (`IsFrozen`), calcula `AuthorizedSubtotal/VatTotal/IceTotal/DiscountTotal/GrandTotal` (§11.1), `HistoricalCostTotal/CostVarianceTotal` (§19.1bis), `AppliedToPayableAmount/SupplierCreditAmount` (§11.2), transiciona `Draft→Authorized`, `FiscalStatus→PendingSupplierCreditNote`, dispara `PurchaseReturnAuthorizedEvent`. Si se crea `SupplierCredit`, `SupplierCredit.CreateFromReturn(...)` copia `BranchId` literalmente del `PurchaseReturn` que autoriza (§5.2 del diseño) — nunca un valor independiente.
3. `PurchaseReturn.Cancel(reason, ...)` válido desde `Draft` (sin reversas) o desde `Authorized` (con precondición `SupplierCredit.AvailableAmount == OriginalAmount` si existe crédito — validada por el caller bajo lock, el método de dominio solo aplica la mutación una vez que la Application ya confirmó la precondición).
4. `SupplierCredit.ApplyToPayable(amount, targetPayableId, ...)`, `RegisterRefund(amount, financialDestinationId, ...)`, `ReverseApplication(...)`, `ReverseRefund(...)` — cada uno crea el `SupplierCreditMovement` correspondiente y recalcula `AvailableAmount` con la fórmula completa de signo (§13.5), con guard `0 ≤ AvailableAmount ≤ OriginalAmount`.
5. `CompanyFinancialDestination.Create(...)` con los 8 campos estructurales inmutables (§6.4ter); `UpdateName(...)`, `SetActive(bool)`, `ChangeAccountingAccount(accountId)` — únicos 3 métodos mutadores.
6. `SupplierCreditRefundTransaction` — entidad append-only, sin métodos mutadores tras `Create()`, factory separado para `REFUND_RECEIVED` y `REFUND_REVERSED` (hereda campos del original en el segundo caso, §6.4quinquies).
7. `PurchasePayable.ApplyReturnCredit(recognizedAmount)`/`ReverseReturnCredit(appliedAmount)`/`ApplySupplierCredit(amount)`/`ReverseSupplierCredit(amount)` exactamente como §12.2.

**Elementos expresamente fuera de alcance**: Persistencia EF Core (Fase 2); casos de uso/MediatR (Fases 5-9); locks reales de PostgreSQL (Fase 2, solo se referencian por contrato de interfaz aquí); `PurchaseReturnSequence.CaptureNextAsync` real (solo la interfaz se declara, la implementación con `pg_advisory_xact_lock` es Fase 2).

**Invariantes protegidas**: Las 9 de §5.1 se modelan como guards de dominio donde corresponde (p. ej. `Cancel()` exige la precondición de crédito íntegro) — la revalidación bajo lock real es responsabilidad de la Application (Fases 6/7/8/10), documentada aquí como contrato que esos handlers deben cumplir.

**Locks y orden de adquisición**: No aplica en esta fase — justificación: el dominio no conoce PostgreSQL ni advisory locks, solo expone los guards que la Application revalidará bajo lock.

**Frontera transaccional**: No aplica en esta fase — justificación: sin persistencia todavía.

**Idempotencia**: No aplica en esta fase — justificación: `ClientRequestId`/`RequestPayloadHash` son campos de persistencia (Fase 2) y de contrato de comando (Fases 5-9), no de dominio puro.

**Errores de negocio involucrados**: Se definen aquí como excepciones de dominio (`DomainException`/equivalente ya usado en el repo) que la Application traducirá a los códigos `PR-*`/`SC-*` de §21 — no se codifican los strings `PR-001`..`SC-029` en el dominio, eso vive en la capa de traducción de errores ya existente (mismo patrón que el resto del ERP).

**Pruebas unitarias**: Creación válida/inválida de `Draft`; `Authorize` feliz y sus rechazos (sin líneas, retención — mock de guard, cantidad excede remanente); `Cancel` desde `Draft` y desde `Authorized` con/sin crédito íntegro; fórmula `AvailableAmount` con los 5 tipos de movimiento incluyendo `SourceReturnCancelled`; `CompanyFinancialDestination.Create` con los `CHECK` combinados de §6.4 (banco vs. caja); inmutabilidad de los 8 campos estructurales tras creación; `PurchasePayable.ApplyReturnCredit`/`ApplySupplierCredit` con `BalanceDue` insuficiente → rechazo; regresión completa de `PurchasePayableTests.cs` ya existente sin cambio de resultado; `BranchId` obligatorio en `PurchaseReturn.CreateDraft` (rechazo si `Guid.Empty`), sin setter público; `SupplierCredit.CreateFromReturn` hereda exactamente `PurchaseReturn.BranchId` (Branch Ownership Rule, §5.2 del diseño).

**Pruebas de integración**: No aplica en esta fase — justificación: sin persistencia, no hay integración con PostgreSQL todavía (cubierto en Fase 2).

**Pruebas PostgreSQL reales**: No aplica en esta fase — mismo motivo.

**Pruebas frontend**: No aplica — justificación: fase de dominio backend puro.

**Comandos de validación**:
```
cd backend/src/ERP.Domain.Tests && dotnet test
```

**Criterios de aceptación**: 0 dependencias de `ERP.Domain` hacia `ERP.Application`/`ERP.Infrastructure` (verificado por `LayerDependencyTests.cs` de `ERP.Architecture.Tests`); `PurchaseInvoice`/`PurchasePayable` original referenciados solo por `Guid` desde `PurchaseReturn` (sin FK de navegación); ningún método permite reabrir un `PurchaseReturn`/`SupplierCredit` a un estado anterior fuera de las transiciones de §9.

**Condiciones de detención**: Si `LayerDependencyTests.cs` falla (dependencia indebida hacia capas superiores), la fase no se cierra hasta corregirlo.

**Entregable de la fase**: Los 7 archivos de entidades nuevas + 3 extensiones + enums + eventos, todos compilando y con la suite de `ERP.Domain.Tests` en verde.

`PHASE_1_ACCEPTED: NO`

---

### FASE 2 — Persistencia e infraestructura

**Objetivo**: Persistir el modelo de la Fase 1 — configuraciones EF Core, migración única, repositorios con los locks A/B reales (`pg_advisory_xact_lock`), `PurchaseReturnSequenceRepository` con `CaptureNextAsync` real dentro de la transacción ambiente (§7.1bis).

**Dependencias**: Fase 1 completa.

**Archivos existentes a modificar**:
- `ERP.Infrastructure/Persistence/Configurations/Purchases/PurchasePayableConfiguration.cs` — agregar `xmin` (RowVersion) + 2 columnas nuevas `numeric(18,2)`.
- `ERP.Infrastructure/Persistence/Configurations/Inventory/StockMovementConfiguration.cs` — agregar `SourceDocLineId (uuid, nullable)`.
- `ERP.Infrastructure/Persistence/Configurations/Purchases/PurchaseReceptionDocumentConfiguration.cs` — agregar `CurrencyCode (varchar(3), not null)`.

**Archivos nuevos a crear**:
- `ERP.Infrastructure/Persistence/Configurations/Purchases/PurchaseReturnConfiguration.cs`, `PurchaseReturnDetailConfiguration.cs`, `PurchaseReturnAuditConfiguration.cs`, `SupplierCreditConfiguration.cs`, `SupplierCreditMovementConfiguration.cs`, `SupplierCreditAuditConfiguration.cs`, `PurchaseReturnSequenceConfiguration.cs`.
- `ERP.Infrastructure/Persistence/Configurations/Finance/CompanyFinancialDestinationConfiguration.cs`, `SupplierCreditRefundTransactionConfiguration.cs`, `CompanyFinancialDestinationAuditConfiguration.cs`.
- `ERP.Infrastructure/Persistence/Repositories/Purchases/PurchaseReturnRepository.cs` (implementa `IPurchaseReturnRepository`, incluye `AcquireFinancialLockAsync` — Lock A, namespace `"PurchaseInvoice.FinancialLock"`), `SupplierCreditRepository.cs` (`AcquireLockAsync` — Lock B, namespace `"SupplierCredit.Lock"`), `PurchaseReturnSequenceRepository.cs` (`CaptureNextAsync` — `pg_advisory_xact_lock` namespace `"PurchaseReturn.Sequence"`, dentro de la transacción ambiente del caller, nunca transacción propia).
- `ERP.Infrastructure/Persistence/Repositories/Finance/CompanyFinancialDestinationRepository.cs`, `SupplierCreditRefundTransactionRepository.cs`.
- `ERP.Domain/Modules/Purchases/Interfaces/IPurchaseReturnRepository.cs`, `ISupplierCreditRepository.cs`; `ERP.Domain/Modules/Finance/Interfaces/ICompanyFinancialDestinationRepository.cs`, `ISupplierCreditRefundTransactionRepository.cs`.
- 1 migración EF nueva (`dotnet ef migrations add AddPurchaseReturnAndSupplierCredit`) que crea: `purchase_returns`, `purchase_return_details`, `purchase_return_audit`, `supplier_credits`, `supplier_credit_movements`, `supplier_credit_audit`, `purchase_return_sequence`, `company_financial_destinations`, `supplier_credit_refund_transactions`, `company_financial_destination_audit`; y modifica `purchase_payables` (+`xmin`+2 columnas), `stock_movements` (+`source_doc_line_id`), `purchase_reception_documents` (+`currency_code`).
- `ERP.Infrastructure.Tests/Persistence/PurchaseReturnRepositoryTests.cs`, `SupplierCreditRepositoryTests.cs`, `CompanyFinancialDestinationRepositoryTests.cs`, `PurchaseReturnSequenceRepositoryTests.cs`.

**Cambios exactos**:
1. `PurchaseReturnConfiguration`: `BranchId (uuid, NOT NULL)` (Branch Ownership Rule, §5.2 del diseño); `UNIQUE (TenantId, CompanyId, ReturnNumber) WHERE ReturnNumber IS NOT NULL`; `UNIQUE (TenantId, CreateClientRequestId)`; `UNIQUE (TenantId, AuthorizeClientRequestId) WHERE NOT NULL`; `UNIQUE (TenantId, CancelClientRequestId) WHERE NOT NULL`; `UNIQUE (TenantId, LinkCreditNoteClientRequestId) WHERE NOT NULL`; `UNIQUE (TenantId, SupplierCreditNoteDocumentId) WHERE NOT NULL`; índice `(TenantId, CompanyId, BranchId)` para listados/reportes por sucursal; sin FK de navegación hacia `PurchaseInvoice` (mismo patrón `SalesReturn.SalesInvoiceId`), `DeleteBehavior.Restrict` donde aplique.
2. `PurchaseReturnDetailConfiguration`: `UNIQUE (PurchaseReturnId, OriginalInvoiceDetailId)`.
3. `SupplierCreditConfiguration`: `BranchId (uuid, NOT NULL)` (heredado de `PurchaseReturn.BranchId`, §5.2 del diseño); `UNIQUE (TenantId, SourcePurchaseReturnId)`; `xmin`.
4. `SupplierCreditMovementConfiguration`: `CHECK Amount > 0`; `CHECK` combinado `MovementType IN (Application, ReversalOfApplication) ⇒ TargetPurchasePayableId NOT NULL`; `CHECK` combinado `MovementType = SourceReturnCancelled ⇒ TargetPurchasePayableId IS NULL`; `CHECK` combinado `MovementType IN (ReversalOfApplication, ReversalOfRefund) ⇒ ReversalOfMovementId NOT NULL`; `UNIQUE (ReversalOfMovementId) WHERE NOT NULL`; `UNIQUE (TenantId, ClientRequestId)`.
5. `CompanyFinancialDestinationConfiguration`: `CHECK` combinado por `DestinationTypeCode` exactamente como §6.4 (sin `IsActive=true` estructural — corrección explícita del diseño); `UNIQUE (TenantId, CompanyId, Code)`; `UNIQUE (TenantId, CompanyId, BankInstitutionCode, BankAccountIdentifierNormalized) WHERE DestinationTypeCode='BANK_ACCOUNT'`; `UNIQUE (TenantId, CompanyId, CashRegisterId) WHERE DestinationTypeCode='CASH_REGISTER'`.
6. `SupplierCreditRefundTransactionConfiguration`: `UNIQUE (TenantId, CompanyId, SupplierCreditMovementId)` (relación 1:1 estricta); `UNIQUE (TenantId, CompanyId, OriginalTransactionId) WHERE TransactionTypeCode='REFUND_REVERSED'`; FK real `AccountingAccountId → Account` (`NOT NULL`); índice `(TenantId, CompanyId, AccountingAccountId)`; columna `AccountingAccountCodeSnapshot`.
7. `PurchaseReturnSequenceConfiguration`: PK compuesta o `UNIQUE (TenantId, CompanyId)`, `CurrentSeq int`.
8. `PurchaseReturnRepository.AcquireFinancialLockAsync(tenantId, purchaseInvoiceId, ct)`: `SELECT pg_advisory_xact_lock(hashtext('PurchaseInvoice.FinancialLock' || tenantId || purchaseInvoiceId))` (o mecanismo de hash equivalente ya usado por `SalesReturnRepository`/`DocumentSequenceRepository`, verificado exacto en el momento de implementación — namespace distinto confirmado, sin colisión).
9. `PurchaseReturnSequenceRepository.CaptureNextAsync`: recibe el `DbContext`/transacción ambiente ya abierta (nunca `BeginTransactionAsync` propio), ejecuta los 13 pasos exactos de §7.1bis del diseño.

**Elementos expresamente fuera de alcance**: Casos de uso MediatR (Fases 5-9); cualquier endpoint HTTP.

**Invariantes protegidas**: Todos los `CHECK`/`UNIQUE` de §7 del diseño, verificados por prueba PostgreSQL real (no solo por el modelo EF).

**Locks y orden de adquisición**: Implementación real de Lock A (`"PurchaseInvoice.FinancialLock"`) y Lock B (`"SupplierCredit.Lock"`) — namespaces distintos entre sí y de `"SalesReturn.Lock"`/`IJournalEntryRepository.AcquireIdempotencyLockAsync` (verificado por prueba dedicada, ver Pruebas PostgreSQL reales).

**Frontera transaccional**: Cada método de repositorio opera dentro de la transacción/`DbContext` que el caller le pase — ningún repositorio de esta fase abre ni comitea su propia transacción (excepto los métodos de solo lectura estándar).

**Idempotencia**: Las columnas `ClientRequestId`/`RequestPayloadHash` se persisten en esta fase (índices únicos) — el mecanismo de idempotencia completo (búsqueda→inserción→recuperación de carrera, §16.2bis) se implementa en las Fases 5-9 que lo consumen; aquí solo se garantiza que la restricción `UNIQUE` existe y es correcta.

**Errores de negocio involucrados**: Ninguno propio de esta fase — las violaciones de `CHECK`/`UNIQUE` se traducen en la Application (Fases 5-9) vía `IDatabaseExceptionTranslator` ya existente.

**Pruebas unitarias**: No aplica — justificación: esta fase es de persistencia real, no de lógica aislable sin BD.

**Pruebas de integración**: CRUD básico de cada entidad nueva contra PostgreSQL real (Testcontainers, mismo patrón que `SalesReturnRepositoryTests.cs`); verificación de cada `CHECK`/`UNIQUE` forzando su violación y confirmando el rechazo a nivel de BD.

**Pruebas PostgreSQL reales**:
1. Lock A: dos transacciones concurrentes con el mismo `(TenantId, PurchaseInvoiceId)` se serializan; con `PurchaseInvoiceId` distintos, proceden en paralelo.
2. Lock B: mismo patrón para `SupplierCreditId`.
3. Namespace de Lock A/B no colisiona con `"SalesReturn.Lock"` ni con el lock de `IJournalEntryRepository` — verificado con una prueba que adquiere ambos en paralelo sin bloqueo cruzado.
4. `PurchaseReturnSequence.CaptureNextAsync` — los 7 escenarios (a)-(g) exigidos por §7.1bis del diseño (concurrencia, ámbitos distintos, rollback tras captura, conflicto de restricción única, retry de `SaveChangesWithSequenceRetryAsync`, idempotencia tras commit sin respuesta, ausencia de doble numeración).
5. Migración aplica limpio sobre BD de desarrollo; `dotnet ef migrations has-pending-model-changes` → `No changes`.
6. `BranchId` mapeado `NOT NULL` en `purchase_returns`/`supplier_credits` — verificado por prueba que intenta insertar con `BranchId = null`/`Guid.Empty` a nivel de columna y confirma el rechazo por el propio motor (Branch Ownership Rule, §5.2 del diseño).

**Pruebas frontend**: No aplica.

**Comandos de validación**:
```
cd backend/src/ERP.Infrastructure && dotnet ef migrations add AddPurchaseReturnAndSupplierCredit --startup-project ../ERP.API/ERP.API.csproj
cd backend/src/ERP.Infrastructure && dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd backend/src/ERP.Infrastructure && dotnet ef migrations has-pending-model-changes --startup-project ../ERP.API/ERP.API.csproj
cd backend/src/ERP.Infrastructure.Tests && dotnet test
```

**Criterios de aceptación**: Migración única, aplicada limpio; `has-pending-model-changes` → `No changes`; los 7 escenarios de la prueba de secuencia de §7.1bis en verde; `CHECK` de `CompanyFinancialDestination` verificado sin exigir `IsActive=true` estructural (confirmado por prueba que persiste `IsActive=false` sin violar ningún `CHECK`).

**Condiciones de detención**: Si el hash de Lock A/B colisiona con un lock ya existente en el sistema (detectado por la prueba 3 de "Pruebas PostgreSQL reales"), la fase no se cierra hasta cambiar el namespace.

**Entregable de la fase**: Migración aplicada + repositorios con locks reales + suite de integración en verde.

`PHASE_2_ACCEPTED: NO`

---

### FASE 3 — Endurecimiento de handlers existentes

**Objetivo**: Adquirir Lock A (y transacción explícita donde falte) en los 5 handlers existentes de Compras/Finance que hoy pueden mutar `PurchasePayable`/`IssuedWithholding` sin serialización — cierra las carreras cruzadas documentadas en §15.2/§5.1 del diseño, antes de que exista `PurchaseReturn`.

**Dependencias**: Fase 2 completa (Lock A real + `xmin` en `PurchasePayable`).

**Archivos existentes a modificar**:
- `ERP.Application/Modules/Finance/UseCases/Payments/PaymentUseCases.cs` — `RegisterPaymentCommandHandler` (línea 215) y `ReversePaymentCommandHandler` (línea 391): agregar `IUnitOfWork.BeginTransactionAsync()` + `IPurchaseReturnRepository.AcquireFinancialLockAsync` por **cada** `PurchaseInvoiceId` distinto involucrado, en orden ascendente de `Guid` (comparación como texto, §15.4).
- `ERP.Application/Modules/Purchases/UseCases/IssueWithholdingUseCases.cs` — `IssueWithholdingHandler`: agregar transacción explícita + Lock A por `PurchaseInvoiceId` antes de emitir.
- `ERP.Application/Modules/Purchases/UseCases/CancelWithholdingUseCases.cs` — `CancelWithholdingHandler`: igual.
- `ERP.Application/Modules/Purchases/UseCases/CancelPurchaseUseCases.cs` — `CancelPurchaseHandler` (línea 35): agregar transacción explícita + Lock A; agregar las validaciones nuevas `PI-CANC-01` (existe `PurchaseReturn.Authorized` asociada — bajo lock, con dependencia lógica hacia el repositorio de `PurchaseReturn` ya creado en Fase 2, aunque el agregado `PurchaseReturn` en sí todavía no tenga casos de uso propios: esta fase solo agrega la consulta de existencia, no el flujo completo) y `PI-CANC-02` (`PurchasePayable.SupplierCreditAppliedAmount > 0`).

**Archivos nuevos a crear**: Ninguno — fase estrictamente de modificación. Extensión de los archivos de test ya existentes: `ERP.Application.Tests/Finance/RegisterPaymentCommandHandlerTests.cs` (confirmado existente), `ERP.Application.Tests/Purchases/IssueWithholdingHandlerTests.cs` (confirmado existente), y tests nuevos para `ReversePaymentCommandHandler`/`CancelWithholdingHandler`/`CancelPurchaseHandler` si no existen ya casos de concurrencia (a verificar exactamente en el momento de implementación cuáles ya tienen suite y cuáles no).

**Cambios exactos**:
1. Cada uno de los 5 handlers pasa de "sin transacción explícita, sin lock" (o, en el caso de `RegisterPayment`/`ReversePayment`, su estado actual verificado exacto en el momento de implementación) a: abrir transacción → adquirir Lock A → recargar `PurchasePayable`/`IssuedWithholding` → revalidar guard de dominio ya existente (sin cambiarlo) → ejecutar mutación → `SaveChangesAsync` → commit.
2. `CancelPurchaseHandler` agrega las 2 validaciones nuevas bajo el mismo Lock A ya adquirido — sin abrir un segundo lock.
3. Ningún guard de dominio existente cambia de comportamiento — solo se cierra la ventana de carrera alrededor de él (mismo criterio que §12.3/§15.6 del diseño: "no debería fallar nunca si el flujo es correcto, hoy protegido solo por guard optimista, ahora también por lock").

**Elementos expresamente fuera de alcance**: `PurchaseReturn` en sí (no existe todavía como caso de uso); cualquier cambio a la lógica de negocio de pago/retención/cancelación más allá de la sincronización.

**Invariantes protegidas**: §5.1 casos 1, 2, 3 (parcial — el guard ya existía, ahora bajo lock), 9 (cancelación concurrente factura/devolución — preparación para Fase 10, que es quien realmente completa este caso una vez exista `PurchaseReturn`).

**Locks y orden de adquisición**: Lock A únicamente en esta fase (Lock B no aplica — no hay `SupplierCredit` todavía). Múltiples Lock A (caso `RegisterPayment` multi-factura) en orden ascendente de `Guid` como texto.

**Frontera transaccional**: Una transacción explícita por invocación de cada uno de los 5 handlers — reemplaza el `SaveChangesAsync` implícito/no transaccional que pudieran tener hoy.

**Idempotencia**: No aplica en esta fase — justificación: estos 5 handlers no forman parte de las 8 operaciones idempotentes de §16.2 (esas son exclusivas de `PurchaseReturn`/`SupplierCreditMovement`, Fases 5-9); esta fase no introduce `ClientRequestId` en ellos.

**Errores de negocio involucrados**: `PI-CANC-01`, `PI-CANC-02` (nuevos, catálogo de `CancelPurchaseUseCases`); traducción de `DbUpdateConcurrencyException` a un código de negocio de concurrencia existente (`PY-CONCURRENCY-01` o equivalente ya usado por el proyecto — verificar el código exacto ya usado en el repo antes de introducir uno nuevo).

**Pruebas unitarias**: Cada handler modificado mantiene sus pruebas existentes en verde sin cambio de resultado esperado (regresión); pruebas nuevas para las 2 validaciones de `CancelPurchaseHandler`.

**Pruebas de integración**: No aplica en el sentido de Testcontainers aquí — cubierto por las pruebas PostgreSQL reales siguientes.

**Pruebas PostgreSQL reales**:
1. `RegisterPaymentCommandHandler`: dos pagos concurrentes sobre la misma factura se serializan, sin lost update (§23 escenario 11 preparación).
2. `IssueWithholdingHandler` vs. una operación futura de `PurchaseReturn` (simulada con un lock manual en el test, ya que `AuthorizePurchaseReturnUseCases` no existe hasta Fase 6) — confirmar que el Lock A adquirido por `IssueWithholdingHandler` bloquea a cualquier otro tomador del mismo lock.
3. `CancelPurchaseHandler`: `PI-CANC-01`/`PI-CANC-02` rechazan determinísticamente bajo lock cuando la precondición existe (simulada con datos sembrados directamente, ya que `PurchaseReturn.Authorized` real no existe hasta Fase 6 — esta prueba se **repite y amplía** en la Fase 10 con el flujo real end-to-end).

**Pruebas frontend**: No aplica.

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~Payment|FullyQualifiedName~Withholding|FullyQualifiedName~CancelPurchase"
cd backend/src/ERP.Infrastructure.Tests && dotnet test
```

**Criterios de aceptación**: Los 5 handlers abren transacción explícita y adquieren Lock A; 0 regresiones en la suite existente de Compras/Finance; `PI-CANC-01`/`PI-CANC-02` implementados y probados con datos sembrados.

**Condiciones de detención**: Si la suite de regresión de `RegisterPaymentCommandHandlerTests.cs`/`IssueWithholdingHandlerTests.cs` (ya existentes) cambia de resultado esperado, la fase no se cierra — indica un cambio de comportamiento no autorizado.

**Entregable de la fase**: 5 handlers endurecidos, 0 regresión, 2 códigos de error nuevos documentados.

`PHASE_3_ACCEPTED: NO`

---

### FASE 4 — Administración limitada de destinos financieros

**Objetivo**: Exponer los 4 casos de uso limitados de `CompanyFinancialDestination` (crear, renombrar, cambiar cuenta contable, activar/desactivar) — sin CRUD genérico, sin update estructural, sin delete físico (§6.4ter, §24).

**Dependencias**: Fase 2 completa (persistencia de `CompanyFinancialDestination`).

**Archivos existentes a modificar**: Ninguno.

**Archivos nuevos a crear**:
- `ERP.Application/Modules/Finance/UseCases/CompanyFinancialDestinationUseCases.cs` (o carpeta `CompanyFinancialDestination/` con 4 archivos Command+Validator+Handler): `CreateCompanyFinancialDestinationCommand`, `UpdateCompanyFinancialDestinationNameCommand`, `ChangeCompanyFinancialDestinationAccountingAccountCommand`, `SetCompanyFinancialDestinationActiveCommand` — 4 handlers.
- `ERP.Application/Modules/Finance/EventHandlers/CompanyFinancialDestinationAuditHandler.cs`.
- `ERP.API/Controllers/CompanyFinancialDestinationController.cs` (`api/v1/finance/financial-destinations`).
- `ERP.Application.Tests/Finance/CompanyFinancialDestinationUseCasesTests.cs`.
- `ERP.API.Tests/Finance/CompanyFinancialDestinationControllerTests.cs`.

**Cambios exactos**:
1. `CreateCompanyFinancialDestinationCommand`: recibe los 8 campos estructurales (§6.4) — `Code`, `Name`, `DestinationTypeCode`, `AccountingAccountId`, `CurrencyCode`, y condicionalmente `CashRegisterId` o (`BankInstitutionCode`+`BankAccountIdentifierNormalized`). Valida `CHECK` combinado en FluentValidation (espejo del `CHECK` de BD) antes de tocar el dominio.
2. `UpdateCompanyFinancialDestinationNameCommand`: solo `Id`+`Name`.
3. `ChangeCompanyFinancialDestinationAccountingAccountCommand`: solo `Id`+`AccountingAccountId` — valida `Account.IsActive=true`+`AllowsPosting=true`+mismo tenant/company antes de aplicar; **no** afecta transacciones ya confirmadas (`SupplierCreditRefundTransaction.AccountingAccountId` ya congelado).
4. `SetCompanyFinancialDestinationActiveCommand`: solo `Id`+`IsActive (bool)`.
5. Los 4 handlers disparan evento de auditoría → `CompanyFinancialDestinationAuditHandler` registra antes/después únicamente de los 3 campos editables (§20.1).

**Elementos expresamente fuera de alcance**: Cualquier endpoint de update estructural genérico (`PUT /financial-destinations/{id}` con body libre); cualquier endpoint `DELETE`; edición de `Code`/`DestinationTypeCode`/`CurrencyCode`/`CashRegisterId`/`BankInstitutionCode`/`BankAccountIdentifierNormalized` post-creación (§6.4ter, incluso sin historial — ver "Regla de edición de un destino con historial" del diseño, que aplica igual con y sin historial).

**Invariantes protegidas**: Inmutabilidad de los 8 campos estructurales (§6.4ter); `CHECK` combinado por `DestinationTypeCode` (§6.4); `IsActive=true` nunca exigido como parte del `CHECK` estructural, solo como condición de uso en `RegisterRefund` (Fase 8).

**Locks y orden de adquisición**: `xmin` de `CompanyFinancialDestination` como única defensa de concurrencia en esta fase (sin Lock A/B — la administración del catálogo no compite con `SupplierCredit`/`PurchasePayable`); los bloqueos `FOR SHARE` reales sobre esta entidad se agregan en la Fase 8 (`RegisterRefund`).

**Frontera transaccional**: Una transacción por comando (create/rename/change-account/set-active), cada una con su propio `SaveChangesAsync`.

**Idempotencia**: No aplica — justificación: estas 4 operaciones no están en la lista de las 8 operaciones idempotentes de §16.2 del diseño (esa lista es exclusiva de `PurchaseReturn`/`SupplierCreditMovement`); administración de catálogo no requiere `ClientRequestId` según el diseño.

**Errores de negocio involucrados**: `SC-022` (configuración incompleta por tipo), `SC-023` (cuenta no existe/no pertenece al tenant), `SC-024` (cuenta no postable/inactiva), `SC-026` (`CashRegisterId` no existe/no pertenece al tenant).

**Pruebas unitarias**: Creación válida banco/caja; creación inválida (banco sin institución, caja sin `CashRegisterId`, ambos completos simultáneamente); rename; cambio de cuenta contable con cuenta inactiva → rechazo; activar/desactivar; intento de mutar cualquier campo estructural → sin endpoint que lo permita (verificado por ausencia de parámetro, no por validación en runtime, mismo criterio que §6.4quinquies aplicado por analogía).

**Pruebas de integración**: `Code` duplicado en el mismo tenant/company → `IDatabaseExceptionTranslator` traduce a error de negocio, no 500.

**Pruebas PostgreSQL reales**: Persistencia de `IsActive=false` sin violar el `CHECK` estructural (confirma la corrección explícita del diseño — `IsActive=true` no es parte del `CHECK`).

**Pruebas frontend**: No aplica en esta fase (UI en Fase 13).

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~CompanyFinancialDestination"
cd backend/src/ERP.API.Tests && dotnet test --filter "FullyQualifiedName~CompanyFinancialDestination"
```

**Criterios de aceptación**: Los 4 casos de uso implementados y probados; ningún endpoint expone un campo estructural como editable; auditoría registra únicamente los 3 campos editables.

**Condiciones de detención**: Si se detecta la necesidad de un 5.º caso de uso (p. ej. update estructural) durante la implementación, la fase se detiene y se documenta como bloqueante — no se improvisa un endpoint fuera de los 4 aprobados por el diseño.

**Entregable de la fase**: Controller + 4 casos de uso + auditoría, con permiso de Company Settings (§20.2).

`PHASE_4_ACCEPTED: NO`

---

### FASE 5 — Borrador y consultas de devolución

**Objetivo**: CRUD de `Draft` (crear/editar/cancelar) + consultas (líneas devolvibles, detalle, lista) — sin efectos colaterales sobre inventario/CxP/crédito/contabilidad, mismo principio que `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` Fase 4.

**Dependencias**: Fases 1, 2 completas.

**Archivos existentes a modificar**: Ninguno.

**Archivos nuevos a crear**:
- `ERP.Application/Modules/Purchases/UseCases/PurchaseReturnDraftUseCases.cs` (`CreateDraftCommand`, `UpdateDraftCommand`, `CancelDraftCommand` + handlers + validators).
- `ERP.Application/Modules/Purchases/UseCases/GetReturnableLinesByPurchaseInvoiceUseCases.cs` (usa la consulta derivada de §10.2 — `CantidadDevuelta`/`CantidadRemanente`, filtrando `PurchaseReturn.Status == Authorized`).
- `ERP.Application/Modules/Purchases/UseCases/PurchaseReturnQueryUseCases.cs` (`GetPurchaseReturnByIdQuery`, `GetPurchaseReturnListQuery`).
- `ERP.Application/Modules/Purchases/DTOs/PurchaseReturnDto.cs`, `ReturnableLineDto.cs`.
- `ERP.Application.Tests/Purchases/PurchaseReturnDraftUseCasesTests.cs`, `GetReturnableLinesByPurchaseInvoiceHandlerTests.cs`.

**Cambios exactos**:
1. `CreateDraftCommand` — obligatorio `CreateClientRequestId` (§7.1, §16.2 — a diferencia del criterio "recomendado" de versiones previas del diseño, aquí es NOT NULL desde el primer momento). El comando **no** expone `BranchId` como campo de entrada — el handler resuelve `BranchId` desde `ICurrentBranch.BranchId` del contexto backend (Branch Ownership Rule, §5.2 del diseño; mismo patrón que `PurchaseDraftUseCases.cs`) y lo pasa a `PurchaseReturn.CreateDraft(...)`. Valida: `PurchaseInvoice` existe y `Confirmed` (`PR-001`/`PR-002`); cada línea pertenece a esa factura (`PR-003`); `Quantity ≤ remanente` preventivo, no bajo lock (`PR-004`).
2. `GetReturnableLinesByPurchaseInvoiceQuery` devuelve, por línea: cantidad original, ya devuelta (§10.2), remanente, `WarehouseId` congelado (solo lectura — nunca seleccionable, §14.2).
3. Ningún caso de uso de esta fase invoca `IStockRepository`, `ICompanyFinancialDestinationRepository`, `IPostingEngine`, ni ningún componente de Caja/Contabilidad.
4. Mecanismo de idempotencia de `CreateDraft` (§16.2/§16.2bis): búsqueda por `(TenantId, CreateClientRequestId)` → si existe con mismo hash, retorna el draft ya creado; si existe con hash distinto, `PR-012`; si no existe, algoritmo de recuperación de carrera de §16.2bis (rollback + reconsulta tras violación de índice único).

**Elementos expresamente fuera de alcance**: `Authorize` (Fase 6); cualquier efecto sobre inventario/CxP/crédito.

**Invariantes protegidas**: Unicidad `(PurchaseReturnId, OriginalInvoiceDetailId)`; `Quantity > 0`; `WarehouseId` nunca editable por el comando (se snapshotea del detalle original, el comando ni siquiera lo acepta como parámetro); `BranchId` nunca editable por el comando, resuelto exclusivamente de `ICurrentBranch` (Branch Ownership Rule, §5.2 del diseño).

**Locks y orden de adquisición**: Ninguno — justificación: `CreateDraft`/`UpdateDraft`/`CancelDraft` (de un borrador) no tienen efectos que requieran serialización con `PurchasePayable`/`SupplierCredit` (§16.1, fila ausente para Draft porque no está en la tabla de fronteras transaccionales del diseño, que solo lista operaciones con efecto real).

**Frontera transaccional**: Una transacción simple por comando (`SaveChangesAsync`), sin `BeginTransactionAsync` explícito — no hay lock que coordinar.

**Idempotencia**: `CreateDraft` es una de las 8 operaciones obligatorias de §16.2 — implementada completa en esta fase, incluido el algoritmo de recuperación de carrera de §16.2bis.

**Errores de negocio involucrados**: `PR-001`, `PR-002`, `PR-003`, `PR-004`, `PR-012`.

**Pruebas unitarias**: Crear draft válido 1 y múltiples líneas; rechazos `PR-001`/`PR-002`/`PR-003`/`PR-004`; actualizar/cancelar solo permitido en `Draft`; `GetReturnableLinesByPurchaseInvoiceQuery` con 0 y N devoluciones autorizadas previas (requiere datos sembrados — en esta fase se simulan directamente en BD porque `Authorize` no existe hasta Fase 6, y se **repite** contra el flujo real en la Fase 6); `CreateDraft` persiste `BranchId` igual a `ICurrentBranch.BranchId` del handler, y lo hace incluso si el payload HTTP intentara enviar un `BranchId` distinto (el comando no expone la propiedad).

**Pruebas de integración**: Idempotencia de `CreateDraft` — mismo `ClientRequestId`+mismo payload → mismo resultado; mismo `ClientRequestId`+payload distinto → `PR-012`.

**Pruebas PostgreSQL reales**: Los 4 escenarios base de §16.2ter para `CreateDraft` (representativo de "crea agregado nuevo"): concurrentes mismo CRI+mismo payload → 1 efecto; concurrentes mismo CRI+payload distinto → 1 éxito + 1 `PR-012`; claves distintas → 2 efectos independientes; commit exitoso sin respuesta + reintento → resultado ya confirmado sin duplicar.

**Pruebas frontend**: No aplica en esta fase (UI en Fase 12).

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~PurchaseReturn"
cd backend/src/ERP.Infrastructure.Tests && dotnet test --filter "FullyQualifiedName~PurchaseReturn"
```

**Criterios de aceptación**: `CreateDraft` idempotente y probado con los 4 escenarios de §16.2ter contra PostgreSQL real; remanente correcto en `GetReturnableLinesByPurchaseInvoiceQuery`; 0 efectos colaterales fuera de `PurchaseReturn`/`PurchaseReturnDetail`.

**Condiciones de detención**: Si la prueba de carrera de §16.2bis (violación de índice único concurrente) no se comporta según el algoritmo exacto descrito en el diseño, la fase no se cierra.

**Entregable de la fase**: CRUD de Draft + consultas, idempotencia de creación probada contra PostgreSQL real.

`PHASE_5_ACCEPTED: NO`

---

### FASE 6 — Autorización de devolución

**Objetivo**: Implementar `AuthorizePurchaseReturnUseCases` — el flujo atómico completo: `PurchaseReturnSequence` + inventario + `PurchasePayable` + `SupplierCredit` condicional + contabilidad + auditoría, bajo Lock A, con idempotencia completa y las 26+7 pruebas obligatorias que correspondan a esta operación.

**Dependencias**: Fases 1, 2, 3, 5 completas. Requiere el resultado de la Fase 0 (confirmado: `AuthorizePurchaseReturnUseCases` reutiliza sin modificación la composición `BeginTransactionAsync` → advisory lock → `SaveChangesWithSequenceRetryAsync` → `CommitAsync` — el reintento se recupera con éxito gracias al `SAVEPOINT` automático de EF Core/Npgsql, sin `SAVEPOINT` manual ni reapertura de transacción).

**Archivos existentes a modificar**: Ninguno adicional a lo ya cubierto en Fases 1-2 (`PurchasePayable`, `StockMovement` ya extendidos).

**Archivos nuevos a crear**:
- `ERP.Application/Modules/Purchases/UseCases/AuthorizePurchaseReturnUseCases.cs` (`AuthorizePurchaseReturnCommand` + `AuthorizePurchaseReturnHandler`).
- `ERP.Application/Modules/Accounting/Posting/Translators/PurchaseReturnAuthorizedPostingTranslator.cs`.
- `ERP.Application/Modules/Purchases/EventHandlers/PurchaseReturnAuditHandler.cs` (parcial — cubre `Authorized`; se completa en Fase 10 con `Cancelled`).
- `ERP.Application.Tests/Purchases/AuthorizePurchaseReturnHandlerTests.cs`.
- `ERP.Application.Tests/Accounting/PurchaseReturnAuthorizedPostingTranslatorTests.cs`.
- `ERP.Infrastructure.Tests/Accounting/PurchaseReturnAuthorizedPostingIntegrationTests.cs`.
- `ERP.Infrastructure.Tests/Persistence/AuthorizePurchaseReturnConcurrencyTests.cs` (26+7 pruebas correspondientes a esta operación específica, ver "Pruebas PostgreSQL reales").

**Cambios exactos** (secuencia exacta de §16.1 del diseño, fila `Authorize`):
1. Abrir transacción explícita (`IUnitOfWork.BeginTransactionAsync`).
2. Adquirir Lock A por `PurchaseInvoiceId`.
3. Recargar `PurchasePayable`/`IssuedWithholding`/líneas de factura bajo el lock.
4. Revalidar: remanente por línea (§10.2), `IssuedWithholding.Status != Issued` (`PR-006`, §17), stock suficiente en la bodega original de cada línea (§14.2, `PR-005`).
5. Verificar idempotencia (`AuthorizeClientRequestId`, hash incluye `PurchaseReturnId` — §16.2).
6. `PurchaseReturnSequence.CaptureNextAsync(tenantId, companyId, ct)` — dentro de la misma transacción/conexión, siguiendo el patrón confirmado en Fase 0.
7. `PurchaseReturn.Authorize(returnNumber)` — congela líneas, calcula snapshots financieros (§11.1) y de costo (§19.1bis).
8. `StockRepository.AppendMovementAsync(...)` por cada línea — `Quantity` negativa, `UnitCost = LandedUnitCost` congelado, `WarehouseId` congelado, `SourceDocType="PurchaseReturn"`, `SourceDocLineId=PurchaseReturnDetail.Id` (§14.1).
9. `PurchasePayable.ApplyReturnCredit(recognizedAmount)`.
10. Si excedente > 0: crear `SupplierCredit(OriginalAmount=excedente)` — `SupplierCredit.CreateFromReturn(...)` copia `BranchId` literalmente de `PurchaseReturn.BranchId` (Branch Ownership Rule, §5.2 del diseño), nunca un valor resuelto de forma independiente.
11. `SaveChangesWithSequenceRetryAsync` (patrón confirmado en Fase 0 ante conflicto).
12. `CommitAsync` — dispara `PurchaseReturnAuthorizedEvent` → `PurchaseReturnAuditHandler` + `PurchaseReturnAuthorizedPostingTranslator` de forma síncrona (infraestructura FROZEN).
13. `PurchaseReturnAuthorizedPostingTranslator` construye el `PostingFact` compuesto balanceado de §19.1bis (débitos: CxP aplicada + crédito de proveedor + `max(CostVarianceTotal,0)`; créditos: inventario histórico + IVA + ICE + `max(-CostVarianceTotal,0)`), llama `IPostingEngine.PostAsync` — 0 líneas modificadas en `PostingFact.cs`/`PostingEngine.cs`/`PostingPipeline.cs`/`JournalFactory.cs`.

**Elementos expresamente fuera de alcance**: `SupplierCredit.ApplyToPayable`/`RegisterRefund` (Fases 7/8 — procesos independientes posteriores, nunca en la misma transacción que `Authorize`); vínculo de NC (Fase 9); `Cancel` (Fase 10).

**Invariantes protegidas**: `appliedToPayable == MIN(GrandTotal, BalanceDue antes)` (§11.2); `Σdébitos == Σcréditos` del `PostingFact` (§19.1bis, demostrado algebraicamente en el diseño); todo o nada por artículo transferido (§14.2 — stock insuficiente en cualquier línea bloquea toda la autorización).

**Locks y orden de adquisición**: Lock A únicamente (Lock B no aplica — la creación de `SupplierCredit` ocurre dentro de la misma transacción de `Authorize`, no requiere Lock B porque el crédito recién se está creando, no compite con otra operación sobre un `SupplierCredit` ya existente).

**Frontera transaccional**: Una única transacción — inventario + CxP + crédito condicional + secuencia + auditoría + contabilidad, todo o nada (§4.3, §16.1).

**Idempotencia**: `Authorize` es una de las 8 operaciones de §16.2 — hash incluye `PurchaseReturnId` obligatorio (corrección explícita del diseño respecto a versiones previas que dejaban el hash constante). Reintento tras commit exitoso sin respuesta → retorna snapshot ya confirmado sin reejecutar efectos.

**Errores de negocio involucrados**: `PR-002`, `PR-004` (revalidado bajo lock), `PR-005`, `PR-006`, `PR-007`, `PR-008`, `PR-009`, `PR-012`.

**Pruebas unitarias**: Autorización feliz (factura impaga, parcial, totalmente pagada — 3 casos de §11.3); rechazo por retención `Issued`; rechazo por stock insuficiente en la bodega original (nunca toma de otra bodega); cálculo de `CostVarianceTotal` con los valores exactos del ejemplo (g) de §11.3 (`381.00 = 381.00`); en el caso de factura totalmente pagada, `SupplierCredit.BranchId == PurchaseReturn.BranchId` verificado explícitamente (Branch Ownership Rule, §5.2 del diseño).

**Pruebas de integración**: Traducción evento→`PostingFact` con valores correctos; idempotencia contable ante republicación (mismo `SourceEventId`).

**Pruebas PostgreSQL reales**:
1. Los 7 escenarios de `PurchaseReturnSequence.CaptureNextAsync` bajo el flujo real de `Authorize` (no simulado — ya cubiertos de forma aislada en Fase 2, aquí se repiten integrados al handler completo).
2. Los 4 escenarios de §16.2ter para `Authorize` (representativo de "actúa sobre agregado existente bajo lock"): mismo CRI+mismo payload concurrente → 1 efecto; mismo CRI+payload distinto → 1 éxito+1 `PR-012`; claves distintas → 2 efectos; commit sin respuesta+reintento → sin duplicar.
3. La prueba obligatoria de §16.3 (ya validada de forma aislada en Fase 0, aquí se confirma integrada: conflicto de secuencia de `StockMovement` dentro de la transacción de `Authorize`, reintento exitoso según el patrón elegido en Fase 0).
4. Dos autorizaciones concurrentes sobre la misma factura donde la suma excede el remanente → solo una tiene éxito, la otra `PR-004` determinista (§23 escenario 10).
5. Devolución y pago simultáneos (`RegisterPaymentCommandHandler` ya endurecido en Fase 3) → serializados por Lock A, sin lost update (§23 escenario 11).
6. Devolución y emisión de retención simultáneas → serializados (§23 escenario 12, §15.7).
7. Ausencia de doble numeración verificada por consulta directa tras cada escenario.

**Pruebas frontend**: No aplica en esta fase (UI en Fase 12).

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~AuthorizePurchaseReturn"
cd backend/src/ERP.Infrastructure.Tests && dotnet test --filter "FullyQualifiedName~AuthorizePurchaseReturn|FullyQualifiedName~PurchaseReturnAuthorizedPosting"
```

**Criterios de aceptación**: Los 3 casos numéricos de §11.3 (a/c/g) reproducidos exactamente; `Σdébitos == Σcréditos` verificado en cada escenario con variación de costo; las 7+4+1 pruebas PostgreSQL reales listadas arriba, todas en verde; 0 líneas modificadas en el Posting Engine compartido.

**Condiciones de detención**: Si `Σdébitos ≠ Σcréditos` en cualquier escenario probado, la fase no se cierra — indica un error en la implementación de §19.1bis, no una decisión de diseño a reinterpretar.

**Entregable de la fase**: `AuthorizePurchaseReturnUseCases` completo, translator contable, auditoría de autorización, idempotencia y concurrencia probadas contra PostgreSQL real.

`PHASE_6_ACCEPTED: NO`

---

### FASE 7 — Aplicación y reversa de crédito

**Objetivo**: `ApplySupplierCreditUseCases`/`ReverseSupplierCreditApplicationUseCases` — Lock A (destino) + Lock B, en ese orden fijo (§15.4).

**Dependencias**: Fase 6 completa (existe al menos un `SupplierCredit` real para probar contra) + Fase 3 (Lock A ya endurecido en los handlers existentes, para la prueba de concurrencia cruzada).

**Archivos existentes a modificar**: Ninguno.

**Archivos nuevos a crear**:
- `ERP.Application/Modules/Finance/UseCases/ApplySupplierCreditUseCases.cs`, `ReverseSupplierCreditApplicationUseCases.cs`.
- `ERP.Application/Modules/Accounting/Posting/Translators/SupplierCreditAppliedPostingTranslator.cs`, `SupplierCreditApplicationReversedPostingTranslator.cs`.
- `ERP.Application/Modules/Finance/EventHandlers/SupplierCreditAuditHandler.cs` (parcial — cubre `Applied`/`ApplicationReversed`; se completa en Fases 8/10).
- `ERP.Application.Tests/Finance/ApplySupplierCreditUseCasesTests.cs`, `ReverseSupplierCreditApplicationUseCasesTests.cs`.
- `ERP.Infrastructure.Tests/Persistence/ApplySupplierCreditConcurrencyTests.cs`.

**Cambios exactos**:
1. `ApplyToPayable`: abrir tx → adquirir Lock A (del `PurchasePayable` destino) → luego Lock B (del `SupplierCredit`) → recargar ambos → revalidar `AvailableAmount`, `BalanceDue` destino, proveedor/moneda coinciden → `SupplierCredit.ApplyToPayable()` → `PurchasePayable.ApplySupplierCredit()` (destino) → `SaveChangesWithSequenceRetryAsync` → commit.
2. `ReverseApplication`: mismo orden de locks, invertido en efecto — revalida que `PurchasePayable` destino no esté `cancelled` (`SC-014`, §5.1 caso 5) antes de aplicar la reversa.

**Elementos expresamente fuera de alcance**: `RegisterRefund` (Fase 8); cualquier mutación de `PurchaseReturn` en sí.

**Invariantes protegidas**: `amount ≤ AvailableAmount` (`SC-003`); proveedor/moneda coinciden (`SC-004`/`SC-005`); destino no `cancelled` (`SC-002`, §5.1 caso 4); reversa bloqueada si destino ya `cancelled` tras la aplicación original (`SC-014`, §5.1 caso 5 — callejón sin salida documentado, sin reversa posible); `SupplierCredit.BranchId` es siempre el valor ya persistido del agregado cargado bajo Lock B — la operación nunca lo sustituye por la sucursal activa del operador (Branch Ownership Rule, §5.2 del diseño).

**Locks y orden de adquisición**: Lock A (destino) → Lock B, siempre en ese orden (§15.4) — verificado con prueba de deadlock (dos operaciones que intentarían adquirir en orden inverso deben serializarse sin ciclo).

**Frontera transaccional**: Una transacción por operación, ambos agregados (`PurchasePayable` destino + `SupplierCredit`) mutados en el mismo `SaveChanges`.

**Idempotencia**: `ApplyToPayable`/`ReverseApplication` son 2 de las 8 operaciones de §16.2 — hash incluye `SupplierCreditId`+`TargetPurchasePayableId` (aplicación) o `ReversalOfMovementId` (reversa).

**Errores de negocio involucrados**: `SC-001` a `SC-006`, `SC-010`, `SC-011`, `SC-014`.

**Pruebas unitarias**: Aplicación feliz; sobreaplicación → `SC-003`; proveedor distinto → `SC-004`; moneda distinta → `SC-005`; destino `cancelled` → `SC-002`; reversa de movimiento ya revertido → `SC-011`; reversa con destino cancelado después de la aplicación → `SC-014`.

**Pruebas de integración**: Traducción evento→`PostingFact` para ambos translators.

**Pruebas PostgreSQL reales**:
1. Dos aplicaciones simultáneas del mismo crédito → Lock B serializa, la segunda revalida `AvailableAmount` ya reducido y falla con `SC-003` si excede (§23 escenario 13).
2. Orden de locks A→B verificado sin deadlock con operaciones concurrentes que involucran ambos locks en direcciones cruzadas simuladas.
3. Idempotencia de `ApplyToPayable`/`ReverseApplication` — mismo patrón de 4 escenarios de §16.2ter.
4. §5.1 caso 4 (aplicar sobre CxP `cancelled`) y caso 5 (revertir tras cancelación del destino) reproducidos con datos reales.

**Pruebas frontend**: No aplica en esta fase (UI en Fase 13).

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~SupplierCredit"
cd backend/src/ERP.Infrastructure.Tests && dotnet test --filter "FullyQualifiedName~ApplySupplierCredit"
```

**Criterios de aceptación**: Orden de locks A→B verificado sin deadlock; `SC-001..006/010/011/014` todos probados; `AvailableAmount` nunca fuera de `[0, OriginalAmount]` en ningún escenario.

**Condiciones de detención**: Si se detecta un escenario donde Lock B se adquiere antes que Lock A en cualquier ruta de código, la fase no se cierra — viola §15.4 de forma directa.

**Entregable de la fase**: Aplicación/reversa de crédito completas, orden de locks probado, 0 sobreaplicación posible.

`PHASE_7_ACCEPTED: NO`

---

### FASE 8 — Reembolso y reversa

**Objetivo**: `RegisterSupplierCreditRefundUseCases`/`ReverseSupplierCreditRefundUseCases` — el flujo más sensible del plan junto con la Fase 6: Lock B + `FOR SHARE` de `CompanyFinancialDestination`+`Account`+`CashSession` condicional, cuenta contable congelada, y las 26 pruebas PostgreSQL obligatorias de §16.5.

**Dependencias**: Fase 6 (existe `SupplierCredit`) + Fase 4 (`CompanyFinancialDestination` administrable) + Fase 2 (bloqueos `FOR SHARE`, `SupplierCreditRefundTransaction` persistida).

**Archivos existentes a modificar**: Ninguno — el reembolso crea un `CashMovement` real usando el factory ya existente de `ERP.Domain/Modules/Caja/Entities/CashMovement.cs`, sin modificar su esquema (§24, fila Caja — "esquema sin modificar, solo consumo").

**Archivos nuevos a crear**:
- `ERP.Application/Modules/Finance/UseCases/RegisterSupplierCreditRefundUseCases.cs`, `ReverseSupplierCreditRefundUseCases.cs`.
- `ERP.Application/Modules/Accounting/Posting/Translators/SupplierCreditRefundedPostingTranslator.cs`, `SupplierCreditRefundReversedPostingTranslator.cs`.
- Extensión de `SupplierCreditAuditHandler.cs` (Fase 7) para cubrir `Refunded`/`RefundReversed`.
- `ERP.Application.Tests/Finance/RegisterSupplierCreditRefundUseCasesTests.cs`, `ReverseSupplierCreditRefundUseCasesTests.cs`.
- `ERP.Infrastructure.Tests/Persistence/SupplierCreditRefundConcurrencyTests.cs` (las 26 pruebas de §16.5, ver "Pruebas PostgreSQL reales").

**Cambios exactos** (orden exacto de §6.4quater/§13.6 del diseño):

`RegisterRefund`:
1. Adquirir Lock B (`SupplierCreditId`).
2. Recargar y validar `SupplierCredit` (`AvailableAmount`).
3. Cargar y bloquear `CompanyFinancialDestination` (`SELECT ... FOR SHARE`) por `FinancialDestinationId`.
4. Validar bajo el bloqueo: mismo tenant/company, `IsActive=true`, `DestinationTypeCode` estructuralmente completo, moneda compatible (`SC-020`/`SC-021`/`SC-022`/`SC-025`).
5. Cargar y bloquear `Account` (`FOR SHARE`) por `AccountingAccountId` del destino.
6. Validar bajo el bloqueo: mismo tenant/company, `IsActive=true`, `AllowsPosting=true` (`SC-023`/`SC-024`).
7. Validar `PaymentMethod.RequiresReference` contra `ExternalReference` (obligatoria solo si el método lo exige).
8. Si `CASH_REGISTER`: resolver y bloquear (`FOR SHARE`) la `CashSession` activa compatible — sin sesión, `SC-027`.
9. Crear `SupplierCreditMovement(Refund)` + `SupplierCreditRefundTransaction(REFUND_RECEIVED)` con `AccountingAccountId` congelado desde el destino validado en el paso 6 (§6.4bis) + `CashMovement` real si aplica.
10. Emitir `SupplierCreditRefundedEvent` (único hecho contable, §19.1ter).
11. Persistir auditoría e idempotencia.
12. `SaveChangesWithSequenceRetryAsync` → commit.

`ReverseRefund`:
1. Adquirir Lock B.
2. Cargar y bloquear (`FOR SHARE`) el `SupplierCreditRefundTransaction(REFUND_RECEIVED)` original.
3. Bajo ese bloqueo, verificar ausencia de `REFUND_REVERSED` previa (`SC-011`).
4. Heredar del original: `FinancialDestinationId`, `AccountingAccountId`/`AccountingAccountCodeSnapshot`, `PaymentMethodCode`, `Amount`, `CurrencyCode`, `CashRegisterId`/`BankInstitutionCode`/`BankAccountIdentifierNormalized` — **sin** bloquear ni revalidar `CompanyFinancialDestination`/`PaymentMethod`/`Account` vigentes (§6.4quinquies).
5. Si el original corresponde a caja: usar `CashRegisterId` heredada, resolver y bloquear (`FOR SHARE`) `CashSession` activa compatible — sin sesión, `SC-027`, rollback completo.
6. Crear `SupplierCreditMovement(ReversalOfRefund)` + `SupplierCreditRefundTransaction(REFUND_REVERSED, ExternalReference=null)` + `CashMovement` compensatorio si aplica.
7. Emitir `SupplierCreditRefundReversedEvent` (crédito a la misma cuenta congelada heredada, §19.1ter).
8. `SaveChangesWithSequenceRetryAsync` → commit.

**Elementos expresamente fuera de alcance**: Cualquier resolución nueva de la cuenta contable desde `CompanyFinancialDestination` en `ReverseRefund` (prohibido explícitamente, §19.1ter); cualquier campo del comando de reversa más allá de `OriginalRefundTransactionId`/`Reason`/`ClientRequestId`/`EffectiveDate` (§6.4quinquies — el contrato no acepta destino/cuenta/método/importe/moneda).

**Invariantes protegidas**: `SupplierCreditRefundTransaction.AccountingAccountId` congelado e inmutable tras creación (§6.4bis); relación 1:1 estricta `SupplierCreditRefundTransaction↔SupplierCreditMovement` (`SC-029` si se viola); `ExternalReference` nunca artificial (`N/A` prohibido — ausencia real = `null`); reversa única por ingreso (`UNIQUE ... WHERE TransactionTypeCode='REFUND_REVERSED'`); `RegisterRefund`/`ReverseRefund` operan siempre bajo el `BranchId` ya persistido del `SupplierCredit` cargado bajo Lock B — nunca bajo la sucursal activa de quien ejecuta el reembolso (Branch Ownership Rule, §5.2 del diseño).

**Locks y orden de adquisición**: `Lock B` (advisory) + `FOR SHARE CompanyFinancialDestination` + `FOR SHARE Account` + `FOR SHARE CashSession` (condicional) para `RegisterRefund`; `Lock B` + `FOR SHARE REFUND_RECEIVED original` + `FOR SHARE CashSession` (condicional, sin repetir `FOR SHARE` sobre destino/cuenta) para `ReverseRefund` — exactamente el orden de §6.4quater/§16.1.

**Frontera transaccional**: Una transacción por operación — crédito + transacción financiera + `CashMovement` condicional + contabilidad + auditoría + idempotencia, todo o nada.

**Idempotencia**: `RegisterRefund`/`ReverseRefund` son 2 de las 8 operaciones de §16.2 — hash de `RegisterRefund` incluye `SupplierCreditId`+`FinancialDestinationId`+`PaymentMethodCode`+`Amount`+`CurrencyCode`+`EffectiveDate`+`ExternalReference` normalizada; hash de `ReverseRefund` incluye `SupplierCreditId`+`OriginalTransactionId`+`Reason` (nunca campos financieros, que se derivan del original).

**Errores de negocio involucrados**: `SC-003`, `SC-011`, `SC-015`, `SC-020` a `SC-029` (10 códigos nuevos del destino financiero).

**Pruebas unitarias**: `RegisterRefund` feliz banco y caja; `ReverseRefund` feliz banco y caja; herencia exacta de campos en la reversa (verificado campo por campo).

**Pruebas de integración**: Traducción evento→`PostingFact` para ambos translators, con `AccountingAccountId` correcto (congelado, no vigente).

**Pruebas PostgreSQL reales** — **las 26 exigidas por §16.5 del diseño, sin reducir el conteo**:
1. Reembolso bancario válido — efectos correctos en movimiento/transacción/`PostingFact`.
2. `FinancialDestinationId` inexistente → `SC-020`.
3. Destino de otro tenant → `SC-020`.
4. Destino de otra company (mismo tenant) → `SC-020`.
5. Destino inactivo → `SC-021`, ningún efecto.
6. `AccountingAccountId` del destino no postable → `SC-024`.
7. Moneda del reembolso ≠ moneda del destino → `SC-025`.
8. Reembolso en caja con `CashSession` activa → `CashMovement` real vinculado.
9. Reembolso en caja sin `CashSession` activa → `SC-027`, ningún efecto.
10. Mismo `ClientRequestId`+mismo payload → resultado idéntico, sin duplicar.
11. Mismo `ClientRequestId`+payload distinto → `SC-006`.
12. Dos solicitudes concurrentes del mismo reembolso → exactamente 1 efecto.
13. Commit exitoso sin respuesta + reintento → resultado ya confirmado, 0 efectos adicionales.
14. Reversa válida — hereda destino/cuenta/moneda/importe/método, `CashMovement` compensatorio si aplica.
14bis. Reversa después de que la cuenta del destino fue desactivada/reemplazada → usa la cuenta congelada del original, nunca la vigente.
15. Segunda reversa del mismo `REFUND_RECEIVED` → `SC-011`.
16. Dos reversas concurrentes del mismo `REFUND_RECEIVED` → 1 éxito, 1 `SC-011`/`SC-010`.
17. Intento de cambiar destino/cuenta/moneda/importe/método en la reversa → rechazado por ausencia de parámetro en el contrato.
18. Rollback tras crear `SupplierCreditMovement` pero antes de `SupplierCreditRefundTransaction` → 0 efectos.
19. Rollback tras crear `CashMovement` pero antes del `PostingFact` → 0 efectos, incluida la caja.
20. Rollback antes del asiento contable → 0 `PostingFact` huérfano.
21. Ausencia de FK circular — `SupplierCreditMovement` sin columna hacia `SupplierCreditRefundTransaction`, verificado por consulta directa.
22. Unicidad 1:1 — segundo intento de vincular el mismo movimiento → `SC-029`.
23. Reporte neto excluye ingresos revertidos al agrupar por destino/proveedor.
24. Separación correcta de reportes por destino/proveedor/moneda/método/fecha con ≥2 destinos y ≥2 monedas.
26. Reversa de reembolso de caja sin sesión activa → `SC-027`, sin efecto parcial, `REFUND_RECEIVED` original intacto, reintento posterior con sesión válida ejecuta correctamente (recuperación idempotente estándar, no "mismo contenido ya confirmado").

**Total: 26 pruebas** (el diseño numera hasta 26 saltando el 25 explícitamente — se preserva la numeración original del diseño sin renumerar, incluyendo el salto).

**Pruebas frontend**: No aplica en esta fase (UI en Fase 13).

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~SupplierCreditRefund"
cd backend/src/ERP.Infrastructure.Tests && dotnet test --filter "FullyQualifiedName~SupplierCreditRefund"
```

**Criterios de aceptación**: Las 26 pruebas de §16.5 en verde, sin excepción, sin reducción de alcance; `SupplierCreditRefundTransaction.AccountingAccountId` verificado inmutable tras creación en cada escenario de reversa.

**Condiciones de detención**: Si cualquiera de las 26 pruebas no puede pasar sin modificar el contrato de `ReverseRefund` para aceptar campos financieros del cliente, la fase no se cierra — indicaría una violación de §6.4quinquies, no un ajuste menor.

**Entregable de la fase**: Reembolso/reversa completos, 26 pruebas PostgreSQL reales en verde, cuenta contable históricamente congelada y verificada.

`PHASE_8_ACCEPTED: NO`

---

### FASE 9 — Registro de NC recibida

**Objetivo**: `RegisterSupplierCreditNoteUseCases` — registro manual de la NC del proveedor + vínculo 1:1 con `PurchaseReturn`, con la validación cuantitativa obligatoria de §18.4bis (tolerancia `0.01`) — sin ningún efecto sobre inventario/CxP/crédito/contabilidad (§18.5).

**Dependencias**: Fase 6 (existe `PurchaseReturn.Authorized`) + Fase 1 (columna `CurrencyCode` en `PurchaseReceptionDocument`).

**Archivos existentes a modificar**: Ninguno adicional — el factory `PurchaseReceptionDocument.Create(...)` ya recibió el parámetro `currencyCode` en la Fase 1; esta fase solo lo invoca desde el caso de uso nuevo.

**Archivos nuevos a crear**:
- `ERP.Application/Modules/Purchases/UseCases/RegisterSupplierCreditNoteUseCases.cs` (`RegisterAndLinkSupplierCreditNoteCommand` + handler + validator).
- `ERP.Application.Tests/Purchases/RegisterSupplierCreditNoteUseCasesTests.cs`.
- `ERP.Infrastructure.Tests/Persistence/RegisterSupplierCreditNoteIntegrationTests.cs`.

**Cambios exactos**:
1. Abrir transacción (documental, sin Lock A/B — no compite con inventario/CxP/crédito, §16.1 fila "Vínculo de NC").
2. Registrar/obtener `PurchaseReceptionDocument(CreditNote)` (reutilizar por `AccessKey` si ya existe registrado, o crear nuevo).
3. Validar: proveedor coincide (`SC-008`); tipo `CreditNote`; `AccessKey` único (`SC-007`); no vinculada previamente a otra `PurchaseReturn` (`SC-012`); `PurchaseReturn` no tiene ya otra NC (`SC-009`); moneda coincide (`SC-013`); fecha de emisión no anterior a la factura original.
4. Validación cuantitativa obligatoria (§18.4bis): `Difference = ABS(PurchaseReceptionDocument.TotalAmount − PurchaseReturn.GrandTotal)`; `TotalAmount` no verificable → `SC-016`; `Difference > 0.01` y menor → `SC-017`; `Difference > 0.01` y mayor → `SC-018`; `CurrencyCode` no verificable → `SC-019`.
5. Si todas las validaciones pasan: `PurchaseReturn.FiscalStatus: PendingSupplierCreditNote → SupplierCreditNoteRegistered`, en la misma transacción que la validación (nunca en dos pasos).
6. `SaveChangesAsync` → commit. Auditoría (`PurchaseReturnAudit`). **Sin** `PostingFact` (§19.5).

**Elementos expresamente fuera de alcance**: Cualquier modificación de inventario/CxP/crédito/contabilidad; validación en línea contra el SRI (backlog §25.1); cardinalidad N:M (backlog §25.1).

**Invariantes protegidas**: 1:1 estricto (`SC-009`/`SC-012`); tolerancia exacta `0.01`, no configurable; `FiscalStatus` solo transiciona `PendingSupplierCreditNote → SupplierCreditNoteRegistered` en esta operación.

**Locks y orden de adquisición**: Ninguno — justificación explícita del diseño (§16.1: "no compite con inventario/CxP/crédito").

**Frontera transaccional**: Una transacción — registro/reutilización del documento + validación cuantitativa + mutación de `FiscalStatus`, todo o nada.

**Idempotencia**: Vincular NC es 1 de las 8 operaciones de §16.2 — hash incluye `PurchaseReturnId`+`PurchaseReceptionDocumentId`+`AccessKey`+`InvoiceNumber`+`IssueDate`+`CurrencyCode`+`TotalAmount`.

**Errores de negocio involucrados**: `SC-007`, `SC-008`, `SC-009`, `SC-012`, `SC-013`, `SC-016`, `SC-017`, `SC-018`, `SC-019`, `PR-012`, `PR-013`.

**Pruebas unitarias**: Vínculo feliz (`Difference = 0`); vínculo feliz dentro de tolerancia (`Difference = 0.01` exacto); rechazo `Difference = 0.02` en ambas direcciones (`SC-017`/`SC-018`); rechazo moneda distinta (`SC-013`); rechazo NC ya vinculada a otra devolución (`SC-012`); rechazo devolución con NC previa (`SC-009`).

**Pruebas de integración**: `AccessKey` duplicado detectado por `IDatabaseExceptionTranslator` → `SC-007`, no 500.

**Pruebas PostgreSQL reales**: Validación cuantitativa ejecutada dentro de la misma transacción que la mutación de `FiscalStatus` (verificado con rollback forzado tras la validación exitosa pero antes del commit → `FiscalStatus` permanece `PendingSupplierCreditNote`).

**Pruebas frontend**: No aplica en esta fase (UI en Fase 12).

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~SupplierCreditNote"
```

**Criterios de aceptación**: Tolerancia `0.01` verificada en el límite exacto (`0.01` acepta, `0.011`/redondeado a `0.02` rechaza); 0 efectos financieros/inventario disparados por esta operación en ningún escenario.

**Condiciones de detención**: Si se detecta cualquier código que dispare un evento de dominio con efecto financiero desde esta operación, la fase no se cierra — viola §18.5/§19.5 de forma directa.

**Entregable de la fase**: Registro y vínculo de NC completo, validación cuantitativa probada en el límite exacto de tolerancia.

`PHASE_9_ACCEPTED: NO`

---

### FASE 10 — Cancelación de devolución e invariantes cruzadas

**Objetivo**: `CancelPurchaseReturnUseCases` + cierre completo de las 9 invariantes cruzadas de §5.1 (incluida la revalidación real, ahora con `PurchaseReturn`/`SupplierCredit` existentes, de `PI-CANC-01`/`PI-CANC-02` ya introducidos en Fase 3).

**Dependencias**: Fases 3, 6, 7, 8, 9 completas — es la fase que cierra el círculo entre `PurchaseReturn`, `PurchasePayable`, `SupplierCredit` y la cancelación de factura.

**Archivos existentes a modificar**:
- `ERP.Application/Modules/Purchases/UseCases/CancelPurchaseUseCases.cs` — las validaciones `PI-CANC-01`/`PI-CANC-02` introducidas en Fase 3 con datos simulados se conectan aquí a la consulta real de `IPurchaseReturnRepository`/`ISupplierCreditRepository`.

**Archivos nuevos a crear**:
- `ERP.Application/Modules/Purchases/UseCases/CancelPurchaseReturnUseCases.cs` (`CancelPurchaseReturnCommand` + handler).
- Extensión de `PurchaseReturnAuditHandler.cs` (Fase 6) para cubrir `Cancelled`.
- `ERP.Application/Modules/Accounting/Posting/Translators/PurchaseReturnCancelledPostingTranslator.cs`.
- `ERP.Application.Tests/Purchases/CancelPurchaseReturnUseCasesTests.cs`.
- `ERP.Infrastructure.Tests/Persistence/PurchaseReturnCrossInvariantTests.cs` (los 9 casos de §5.1, ver "Pruebas PostgreSQL reales").

**Cambios exactos**:
1. `Cancel` (§16.1 fila `Cancel`): abrir tx → adquirir Lock A (+ Lock B si existe `SupplierCredit`) → recargar → revalidar `SupplierCredit.AvailableAmount == OriginalAmount` (si existe, `PR-011` si no) → movimiento inverso de inventario (`+Quantity`, mismo `UnitCost`, misma bodega) → `PurchasePayable.ReverseReturnCredit()` → si hay `SupplierCredit`, crear movimiento `SourceReturnCancelled` (`Amount = AvailableAmount` vigente, que por precondición ya es `= OriginalAmount`) → `PurchaseReturn.Cancel()` → `SaveChangesWithSequenceRetryAsync` → commit.
2. `CancelPurchaseHandler` (extendido) — bajo el mismo Lock A ya adquirido en Fase 3, consulta real: `PI-CANC-01` si existe `PurchaseReturn.Authorized` asociada a la factura; `PI-CANC-02` si `PurchasePayable.SupplierCreditAppliedAmount > 0`.
3. `PurchaseReturnCancelledPostingTranslator`: reverso exacto del hecho compuesto de §19.1bis, mismos montos snapshot (incluida la línea de variación de costo si existió), dirección invertida — nunca recalcula `CostVarianceTotal`.

**Elementos expresamente fuera de alcance**: Cualquier compensación automática o "reversa parcial" no explícita — todo o nada según §5.1.

**Invariantes protegidas**: Las 9 de §5.1 completas — ver matriz §11.4 de este plan. `Cancel` opera siempre bajo el `BranchId` ya persistido del `PurchaseReturn` cargado bajo Lock A — nunca se sustituye por la sucursal activa de quien ejecuta la cancelación (Branch Ownership Rule, §5.2 del diseño).

**Locks y orden de adquisición**: Lock A siempre primero; Lock B solo si existe `SupplierCredit` asociado (§15.4).

**Frontera transaccional**: Una transacción — reversa de inventario + CxP + crédito condicional + contabilidad + auditoría, todo o nada.

**Idempotencia**: `Cancel` es 1 de las 8 operaciones de §16.2 — hash incluye `PurchaseReturnId`+`ClientRequestId`+`CancellationReason` normalizada.

**Errores de negocio involucrados**: `PR-007`, `PR-008`, `PR-009`, `PR-011`, `PR-012`, `PI-CANC-01`, `PI-CANC-02`, `SC-010`, `SC-014`.

**Pruebas unitarias**: Cancelar `Draft` (sin reversas); cancelar `Authorized` sin crédito usado (reversa completa); cancelar `Authorized` con crédito íntegro (movimiento `SourceReturnCancelled`, `Amount = OriginalAmount` exacto); rechazo `PR-011` con crédito parcialmente aplicado o reembolsado.

**Pruebas de integración**: Traducción evento→`PostingFact` reverso, montos exactos idénticos al original con signo invertido.

**Pruebas PostgreSQL reales — los 9 casos de §5.1, cada uno con datos reales de extremo a extremo**:
1. Cancelar factura con devolución `Authorized` asociada → bloqueado, `PI-CANC-01`.
2. Cancelar factura cuya CxP recibió aplicación de crédito → bloqueado, `PI-CANC-02`.
3. Pagar CxP `cancelled` → bloqueado (guard ya existente, ahora bajo lock).
4. Aplicar crédito sobre CxP `cancelled` → bloqueado, `SC-002`.
5. Revertir aplicación tras cancelar la CxP destino → bloqueado, `SC-014`, callejón sin salida confirmado.
6. Cancelar devolución con crédito aplicado → bloqueado, `PR-011`.
7. Cancelar devolución con crédito reembolsado → bloqueado, `PR-011` (misma fórmula de `AvailableAmount`, sin comprobación adicional).
8. Cancelar devolución después de registrar NC → **permitido**, `FiscalStatus` permanece `SupplierCreditNoteRegistered` congelado.
9. Cancelación concurrente de factura y de su devolución `Authorized` → serializada por el mismo Lock A, determinista según orden de adquisición, nunca ambas exitosas de forma inconsistente.

**Pruebas frontend**: No aplica en esta fase (UI en Fase 12).

**Comandos de validación**:
```
cd backend/src/ERP.Application.Tests && dotnet test --filter "FullyQualifiedName~CancelPurchaseReturn|FullyQualifiedName~CancelPurchase"
cd backend/src/ERP.Infrastructure.Tests && dotnet test --filter "FullyQualifiedName~PurchaseReturnCrossInvariant"
```

**Criterios de aceptación**: Los 9 casos de §5.1 reproducidos con datos reales (no simulados) y en verde; caso 8 confirmado como el único "permitido" de la tabla, con `FiscalStatus` verificado congelado tras la cancelación.

**Condiciones de detención**: Si cualquiera de los 9 casos permite un estado mixto (p. ej. inventario revertido pero CxP no), la fase no se cierra — viola §4.3 (todo o nada) de forma directa.

**Entregable de la fase**: Cancelación completa + los 9 casos de §5.1 verificados de extremo a extremo contra PostgreSQL real.

`PHASE_10_ACCEPTED: NO`

---

### FASE 11 — API, permisos y contratos

**Objetivo**: Consolidar los controladores REST, FluentValidation, permission keys y OpenAPI para todo lo construido en las Fases 4-10.

**Dependencias**: Fases 4, 5, 6, 7, 8, 9, 10 completas.

**Archivos existentes a modificar**: Catálogo de permission keys de Access/IAM (archivo de seed/registro ya existente — ruta exacta a verificar en el momento de implementación, mismo patrón usado por `sales.returns.*` en P0-01).

**Archivos nuevos a crear**:
- `ERP.API/Controllers/PurchaseReturnController.cs` (`api/v1/purchases/returns`).
- `ERP.API/Controllers/SupplierCreditController.cs` (`api/v1/finance/supplier-credits`).
- (`CompanyFinancialDestinationController.cs` ya creado en Fase 4 — esta fase solo confirma su exposición completa y permisos).
- Validators FluentValidation: `CreateDraftCommandValidator`, `AuthorizePurchaseReturnCommandValidator`, `CancelPurchaseReturnCommandValidator`, `ApplySupplierCreditCommandValidator`, `RegisterSupplierCreditRefundCommandValidator`, `ReverseSupplierCreditRefundCommandValidator`, `RegisterAndLinkSupplierCreditNoteCommandValidator`.
- `ERP.API.Tests/Purchases/PurchaseReturnControllerTests.cs`, `ERP.API.Tests/Finance/SupplierCreditControllerTests.cs`.

**Cambios exactos**:
1. Endpoints de `PurchaseReturnController`: `GET /purchases/invoices/{id}/returnable-lines`, `POST /purchases/returns`, `PUT /purchases/returns/{id}`, `POST /purchases/returns/{id}/cancel`, `POST /purchases/returns/{id}/authorize`, `POST /purchases/returns/{id}/credit-note`, `GET /purchases/returns/{id}`, `GET /purchases/returns`.
2. Endpoints de `SupplierCreditController`: `GET /finance/supplier-credits`, `GET /finance/supplier-credits/{id}`, `POST /finance/supplier-credits/{id}/apply`, `POST /finance/supplier-credits/{id}/apply/{movementId}/reverse`, `POST /finance/supplier-credits/{id}/refund`, `POST /finance/supplier-credits/{id}/refund/{movementId}/reverse`.
3. Permisos nuevos: Compras (`purchases.returns.create/.view/.authorize/.cancel/.credit-note`); Finance (`finance.supplier-credits.apply/.reverse-apply/.refund/.reverse-refund/.view`); Company Settings (`settings.financial-destinations.manage/.view`) — separación exacta de §20.2. Todos los endpoints que actúan sobre un `PurchaseReturn`/`SupplierCredit` ya persistido quedan sujetos, además, a `BranchScopeBehavior`/`IBranchAccessGuard` (infraestructura ya existente) evaluados contra el `BranchId` persistido — mismo mecanismo ya vigente para `PurchaseInvoice` (Branch Ownership Rule, §5.2 del diseño).
4. Todos los endpoints que reciben una de las 8 operaciones idempotentes rechazan con 422 si falta `ClientRequestId` (FluentValidation, `B-V1`) antes de tocar cualquier agregado.
5. Ningún endpoint expone entidades de dominio directamente — solo DTOs.

**Elementos expresamente fuera de alcance**: Frontend (Fases 12/13); cualquier endpoint fuera de los listados arriba.

**Invariantes protegidas**: Ningún campo del payload puede inyectar un valor que el dominio no valide (p. ej. `WarehouseId` nunca es parámetro de `CreateDraft`/`Authorize` — se congela server-side); `BranchId` tampoco es parámetro de ningún comando (`CreateDraftCommand`, `ApplySupplierCreditCommand`, etc. — Branch Ownership Rule, §5.2 del diseño), verificado por la misma prueba de inyección de payload.

**Locks y orden de adquisición**: No aplica en esta capa — los controladores delegan íntegramente en los handlers ya construidos (Fases 5-10), que ya implementan los locks.

**Frontera transaccional**: No aplica en esta capa — delegada a los handlers.

**Idempotencia**: Verificación de contrato — cada uno de los 8 endpoints correspondientes exige `ClientRequestId` en el body, rechaza con 422 si falta.

**Errores de negocio involucrados**: Todos los de §21 del diseño, mapeados a su HTTP status (404/409/422 según la tabla de §21) vía `ExceptionMiddleware` ya existente — sin texto plano, sin excepciones técnicas expuestas (`B-V4`/`B-V5`).

**Pruebas unitarias**: No aplica en esta fase — justificación: la lógica ya está probada en las Fases 5-10, aquí se prueba el contrato HTTP.

**Pruebas de integración**: Contrato de cada endpoint (200/201/400/401/403/404/409/422); test de permisos (usuario sin permission key → 403) para cada uno de los permisos nuevos.

**Pruebas PostgreSQL reales**: No aplica en esta fase — cubierto en Fases 5-10.

**Pruebas frontend**: No aplica en esta fase.

**Comandos de validación**:
```
cd backend/src/ERP.API.Tests && dotnet test --filter "FullyQualifiedName~PurchaseReturn|FullyQualifiedName~SupplierCredit"
```

**Criterios de aceptación**: Los 14 endpoints (8 de `PurchaseReturn` + 6 de `SupplierCredit`) documentados en OpenAPI; permisos separados Compras/Finance/Settings exactamente como §20.2; 422 estructurado camelCase en todos los casos de FluentValidation.

**Condiciones de detención**: Si un endpoint expone un campo que el dominio no valida (detectado por prueba de inyección de payload), la fase no se cierra.

**Entregable de la fase**: API REST completa, permisos separados, contratos probados.

`PHASE_11_ACCEPTED: NO`

---

### FASE 12 — Frontend de Compras

**Objetivo**: UI de `PurchaseReturn` — listado, creación/edición de Draft, detalle, autorización, cancelación, vínculo de NC recibida — con bodega en solo lectura, reutilización auditada de `SalesReturn` y del Design System existente.

**Dependencias**: Fase 11 completa (API + permisos).

**Archivos existentes a modificar**: Ninguno de producción — antes de crear cualquier componente, se ejecuta la auditoría de reutilización obligatoria (`AI-RULES/FRONTEND-RULES.md`) listando explícitamente qué se reutiliza de `frontend/src/modules/sales/` (formulario de `SalesReturn`, `salesReturnSchema.ts`, patrón de página) y de `frontend/src/modules/purchases/` (componentes ya existentes de recepción/factura de compra) antes de escribir un componente nuevo — declaración de auditoría como entregable explícito de esta fase, no un supuesto implícito.

**Archivos nuevos a crear**:
- `frontend/src/modules/purchases/api/purchaseReturnService.ts`.
- `frontend/src/modules/purchases/pages/PurchaseReturnListPage.tsx`, `PurchaseReturnFormPage.tsx` (Draft), `PurchaseReturnDetailPage.tsx`.
- `frontend/src/modules/purchases/schemas/purchaseReturnSchema.ts` (Zod).
- Botón "Devolución" en la vista de detalle de `PurchaseInvoice`, visible solo si `Confirmed` y hay remanente devolvible.
- Sección de vínculo de NC dentro del detalle de la devolución (`PurchaseReturnCreditNoteSection.tsx`).
- `frontend/src/modules/purchases/pages/PurchaseReturnFormPage.test.tsx` (o equivalente, según convención de tests ya usada en el módulo).

**Cambios exactos**:
1. `PurchaseReturnFormPage.tsx` usa **React Hook Form + Zod desde el inicio** (a diferencia del hallazgo de deuda técnica registrado en el backlog de cierre de P0-01 sobre `SalesReturnFormPage.tsx` con `useState` manual — este plan no repite ese defecto en P0-02, es una condición explícita de esta fase, no una opción).
2. Selección de líneas devolvibles consume `GET /purchases/invoices/{id}/returnable-lines` (Fase 5/11) en tiempo real; cantidad remanente mostrada por línea.
3. `WarehouseId` de cada línea se muestra como campo de solo lectura (informativo) — **ningún** control de formulario permite seleccionarlo ni editarlo (§14.2, decisión de negocio cerrada). `BranchId` del documento se muestra igualmente como dato de solo lectura en el listado/detalle (proveniente de `PurchaseReturnDto`, §24 del diseño) — nunca como campo seleccionable del formulario de creación (Branch Ownership Rule, §5.2 del diseño).
4. Autorización invoca `POST /purchases/returns/{id}/authorize` con `ClientRequestId` generado client-side (`crypto.randomUUID()` o equivalente ya usado en `salesReturnService.ts`).
5. Errores 422 mapeados exclusivamente con `applyServerErrors<T>()` de `modules/lib/validationErrors.ts` — prohibido `setError()` manual, prohibido parseo de strings `"Campo: Mensaje"`.
6. Mensajes visuales vía `message`/`MSG` de `lib/messages` — sin condicionales manuales de error (`AI-RULES/VISUAL-MESSAGES.md`).
7. Montos con `ZhDecimalInput`, formateo con `formatMoney()`; fechas con `formatDate()`/`todayIso()` (`lib/formatters/dateFormatters.ts`).

**Elementos expresamente fuera de alcance**: Cualquier selector de bodega alternativa; cualquier UI de aplicación/reembolso de crédito (Fase 13).

**Invariantes protegidas (Architecture Gate F-V1..F-V8, CLAUDE.md)**: F-V1 (RHF como motor), F-V2 (schema Zod completo), F-V3 (errores bajo el campo), F-V4 (valores conservados ante error), F-V5 (`applyServerErrors<T>()` exclusivo), F-V6/F-V7/F-V8 (prohibiciones — sin `setError()` manual, sin parseo de strings, sin mensajes genéricos).

**Locks y orden de adquisición**: No aplica — capa de presentación.

**Frontera transaccional**: No aplica — delegada al backend.

**Idempotencia**: El frontend genera y envía `ClientRequestId` una única vez por intento de envío del formulario (regenerado solo si el usuario reinicia la operación explícitamente, nunca en un reintento automático silencioso) — mismo patrón que `salesReturnService.ts`.

**Errores de negocio involucrados**: Todo el catálogo de §21 relevante a Compras (`PR-*`, `SC-007/008/009/012/013/016/017/018/019`), mapeado a mensajes en español orientados a corregir — nunca "Error de validación" genérico (`F-V8`).

**Pruebas unitarias**: No aplica como suite aislada de lógica — cubierto por pruebas de componente.

**Pruebas de integración**: No aplica en el sentido backend — cubierto por pruebas frontend.

**Pruebas PostgreSQL reales**: No aplica — capa de presentación.

**Pruebas frontend**:
- Golden path guiado (dev server): factura confirmada → crear Draft → seleccionar líneas → autorizar → devolución `Authorized`/`PendingSupplierCreditNote` visible.
- Verificación de que los valores ingresados se conservan ante un error 422 (`F-V4`).
- Verificación de que `WarehouseId` nunca aparece como campo editable en el DOM (assertion negativa explícita).
- `npm run test:unit` para componentes con lógica aislable (schema Zod, mapeo de DTO).

**Comandos de validación**:
```
cd frontend && npm run lint
cd frontend && npm run build
cd frontend && npm run test:unit
cd frontend && npm run architecture:check
cd frontend && npm run architecture:design-system
```

**Criterios de aceptación**: Declaración de auditoría de reutilización entregada (componentes revisados, reutilizados/extendidos, justificación de cualquier componente nuevo — `feedback_ui_reuse_audit`); `F-V1..F-V8` cumplidos sin excepción; `WarehouseId` verificado no editable.

**Condiciones de detención**: Si la auditoría de reutilización no se entrega antes de escribir el primer componente nuevo, la fase no puede iniciar — regla de gobernanza del proyecto (`AI-RULES/FRONTEND-RULES.md`, `project_ds_reuse_rule`).

**Entregable de la fase**: UI completa de `PurchaseReturn` (Compras), Architecture Gate F-V1..F-V8 cumplido, auditoría de reutilización documentada.

`PHASE_12_ACCEPTED: NO`

---

### FASE 13 — Frontend de Finance

**Objetivo**: UI de `SupplierCredit` (consulta, aplicación, reversa de aplicación, reembolso, reversa de reembolso) + administración limitada de `CompanyFinancialDestination` (Fase 4) — ubicada en `frontend/src/modules/finance/`, coherente con §6.3 del diseño (CxP/Pagos ya viven ahí).

**Dependencias**: Fase 11 completa (API + permisos de Finance).

**Archivos existentes a modificar**: Ninguno de producción — misma exigencia de auditoría de reutilización que la Fase 12, esta vez contra `frontend/src/modules/finance/` (páginas de CxP/Pagos ya existentes) y contra `ConfigTabsLayout`/`items-catalog.css` (patrón oficial de tabs de configuración, `AI-RULES` — Infraestructura Master Configuration UI CLOSED) para la pantalla de `CompanyFinancialDestination`.

**Archivos nuevos a crear**:
- `frontend/src/modules/finance/api/supplierCreditService.ts`, `financialDestinationService.ts`.
- `frontend/src/modules/finance/pages/SupplierCreditListPage.tsx`, `SupplierCreditDetailPage.tsx` (saldo disponible, movimientos), `ApplySupplierCreditModal.tsx`, `RegisterSupplierCreditRefundModal.tsx`.
- `frontend/src/modules/finance/pages/FinancialDestinationsPage.tsx` (siguiendo `ConfigTabsLayout`, patrón Lista→Editor obligatorio).
- `frontend/src/modules/finance/schemas/supplierCreditSchema.ts`, `financialDestinationSchema.ts` (Zod).

**Cambios exactos**:
1. `SupplierCreditDetailPage.tsx` muestra `AvailableAmount` (cacheado) — sin exponer al usuario final el recálculo administrativo mencionado en §4.2 del diseño como mecanismo de detección de desincronización (eso es una consulta de soporte/auditoría, no una pantalla de usuario final, salvo que se decida explícitamente ampliar el alcance — fuera de este plan si no está en §24).
2. `ApplySupplierCreditModal.tsx`: selector de `PurchasePayable` destino del mismo proveedor (filtrado server-side, nunca client-side sobre una lista completa), validación Zod espejo de `amount ≤ AvailableAmount` antes de enviar.
3. `RegisterSupplierCreditRefundModal.tsx`: selector de `CompanyFinancialDestination` activo (`GET` filtrado por `IsActive=true` — el frontend nunca envía `AccountingAccountId`, se deriva server-side); campo `ExternalReference` condicionalmente obligatorio según `PaymentMethod.RequiresReference` (consultado del catálogo real, nunca hardcodeado).
4. `FinancialDestinationsPage.tsx`: patrón Lista→Editor; formulario de creación expone los 8 campos estructurales (inmutables tras guardar — el editor de un destino ya creado solo permite `Name`/`IsActive`/`AccountingAccountId`, los demás campos se muestran de solo lectura).
5. Todos los formularios: RHF + Zod + `applyServerErrors<T>()` + `message`/`MSG` + `ZhDecimalInput`/`formatMoney()` — mismos estándares que Fase 12.

**Elementos expresamente fuera de alcance**: Cualquier endpoint de edición estructural de `CompanyFinancialDestination` más allá de los 3 campos editables; cualquier botón de eliminación física.

**Invariantes protegidas (F-V1..F-V8)**: Idénticas a Fase 12, aplicadas a los formularios de esta fase.

**Locks y orden de adquisición**: No aplica — capa de presentación.

**Frontera transaccional**: No aplica — delegada al backend.

**Idempotencia**: `ClientRequestId` generado client-side por cada envío de aplicación/reembolso/reversa — mismo patrón que Fase 12.

**Errores de negocio involucrados**: Catálogo `SC-*` completo relevante a Finance, mapeado con `applyServerErrors<T>()`.

**Pruebas unitarias**: No aplica como suite aislada — cubierto por pruebas de componente.

**Pruebas de integración**: No aplica en sentido backend.

**Pruebas PostgreSQL reales**: No aplica — capa de presentación.

**Pruebas frontend**:
- Golden path: crédito con saldo → aplicar a otra CxP del mismo proveedor → saldo reducido visible.
- Golden path: crédito con saldo → reembolso bancario → reembolso caja (con sesión abierta) → reversa de reembolso.
- Verificación de que el campo `AccountingAccountId` nunca aparece como input en el modal de reembolso (se deriva server-side, assertion negativa).
- `npm run test:unit`.

**Comandos de validación**:
```
cd frontend && npm run lint
cd frontend && npm run build
cd frontend && npm run test:unit
cd frontend && npm run architecture:check
cd frontend && npm run architecture:design-system
```

**Criterios de aceptación**: Declaración de auditoría de reutilización entregada; `F-V1..F-V8` cumplidos; `FinancialDestinationsPage.tsx` sigue el patrón `ConfigTabsLayout` obligatorio (Infraestructura Master Configuration UI CLOSED); ningún botón de delete físico presente.

**Condiciones de detención**: Si se detecta un formulario de destino financiero que no sigue `ConfigTabsLayout`, la fase no se cierra — violaría la infraestructura CLOSED de Master Configuration UI sin una decisión arquitectónica global nueva.

**Entregable de la fase**: UI completa de `SupplierCredit` + administración de `CompanyFinancialDestination`, Architecture Gate cumplido.

`PHASE_13_ACCEPTED: NO`

---

### FASE 14 — Integración, regresión y cierre

**Objetivo**: Validar los escenarios completos de §23 del diseño de punta a punta contra PostgreSQL real, confirmar cero regresión sobre Compras/Inventario/CxP/Caja/Contabilidad/`SalesReturn`, y cerrar los gates de arquitectura CI.

**Dependencias**: Fases 0-13 completas y aceptadas (`PHASE_0_ACCEPTED` a `PHASE_13_ACCEPTED`, todas `YES`).

**Archivos existentes a modificar**: Ninguno de producción — esta fase es de validación y actualización de documentación de avance (`docs/STATUS.md`, `FEATURES.md`, y el propio estado de este plan), **solo después** de que la implementación completa esté validada (nunca durante).

**Archivos nuevos a crear**:
- `ERP.API.Tests/Integration/PurchaseReturnEndToEndTests.cs` (mismo patrón que `SalesReturnEndToEndTests.cs`, confirmado existente en `backend/src/ERP.API.Tests/Integration/`).

**Cambios exactos**:
1. Implementar y ejecutar contra PostgreSQL real todos los escenarios de §23 del diseño (17 numerados, incluyendo 7bis): devolución parcial/total factura impaga; menor/superior al saldo factura parcial; factura totalmente pagada; crédito aplicado a otra CxP; crédito cerrado por reembolso (bancario y caja con reversa); NC recibida después; factura con retención emitida; dos devoluciones simultáneas; devolución y pago simultáneos; devolución y retención simultáneas; dos aplicaciones simultáneas del mismo crédito; cancelación de devolución (con/sin crédito usado); timeout y reintento; autorización con diferencia costo/valor reconocido; las 9 invariantes cruzadas de §5.1 (ya cubiertas en detalle en Fase 10, aquí se ejecutan como parte de la regresión E2E consolidada, no se repiten como prueba nueva independiente).
2. Regresión completa: `ERP.Domain.Tests`, `ERP.Application.Tests`, `ERP.Infrastructure.Tests`, `ERP.API.Tests`, `ERP.Architecture.Tests` — 0 regresiones respecto a la línea base de la Fase 0, incluyendo explícitamente los 5 handlers endurecidos en Fase 3 y toda la suite de `SalesReturn`/`SalesReceivable`/`SalesInvoiceAuthorizedHandler` (P0-01, ya CLOSED — debe permanecer en verde sin cambio de comportamiento).
3. Gates CI-bloqueantes (`SEQ-GATE-01..04` en `DocumentSequenceExclusivityTests.cs`, `ATT-GATE-01` en `NewChildEntityTrackingArchitectureTests.cs`) siguen en verde sin cambios — verificado explícitamente porque `PurchaseReturnSequence` es una infraestructura nueva deliberadamente distinta de `DocumentSequence` (§7.1bis) y no debe activar ni relajar ningún gate de esa infraestructura FROZEN.
4. Regresión frontend completa: `npm run lint`, `npm run build`, `npm run test:unit`, `npm run test:e2e` (si hay infraestructura E2E vigente para el flujo de compras — a confirmar en el momento de implementación), `npm run architecture:check`.
5. Solo tras validar 1-4 en verde: actualizar `docs/STATUS.md` (nueva entrada "P0-02 — Devolución de Compra (PurchaseReturn) + SupplierCredit: COMPLETED / CLOSED", siguiendo el formato exacto de la entrada de cierre de P0-01 ya existente) y `FEATURES.md` (módulo de Compras — nueva capacidad).
6. Actualizar el propio bloque de estado de este documento (`IMPLEMENTATION_PLAN_STATUS`) únicamente en una revisión posterior formal — esta fase no se autoaprueba a sí misma.

**Elementos expresamente fuera de alcance**: Cualquier código de producción nuevo más allá de los tests E2E — esta fase es de validación cruzada, consistente con `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` Fase 15.

**Invariantes protegidas**: Todas las de §5.1, §11.1/§11.2/§19.1bis (ecuación balanceada), §13.5 (fórmula de `AvailableAmount`) — verificadas de extremo a extremo en el contexto E2E, no de forma aislada.

**Locks y orden de adquisición**: No aplica como cambio nuevo — se verifica que el orden A→B se respeta en todos los escenarios E2E combinados (p. ej. un escenario que encadena `Authorize` → `ApplyToPayable` → `RegisterRefund` → `Cancel` de la factura original, todo en secuencia real).

**Frontera transaccional**: No aplica como cambio nuevo — se verifica la atomicidad de cada operación dentro del flujo E2E completo.

**Idempotencia**: Se verifica que el mecanismo de §16.2 se comporta correctamente cuando las 8 operaciones se encadenan en un flujo de negocio real (no aisladas), incluyendo reintentos cruzados entre operaciones distintas del mismo `PurchaseReturn`.

**Errores de negocio involucrados**: Todo el catálogo de §21 verificado con al menos 1 escenario E2E que lo dispare (matriz de cobertura, ver §11.5 de este plan).

**Pruebas unitarias**: No aplica como trabajo nuevo — regresión de las ya existentes.

**Pruebas de integración**: No aplica como trabajo nuevo — regresión de las ya existentes.

**Pruebas PostgreSQL reales**: Los 17 escenarios de §23 (incluido 7bis) implementados como suite E2E única, contra PostgreSQL real, sin mocks.

**Pruebas frontend**: Prueba manual guiada del golden path completo (dev server): factura de compra confirmada → crear devolución → autorizar → ver crédito generado (si aplica) → aplicar/reembolsar crédito → registrar NC → intentar cancelar factura original (bloqueado) → cancelar devolución (si no hay crédito usado).

**Comandos de validación**:
```
cd backend/src/ERP.Domain.Tests && dotnet test
cd backend/src/ERP.Application.Tests && dotnet test
cd backend/src/ERP.Infrastructure.Tests && dotnet test
cd backend/src/ERP.API.Tests && dotnet test
cd backend/src/ERP.Architecture.Tests && dotnet test
cd frontend && npm run lint && npm run build && npm run test:unit && npm run architecture:check
```

**Criterios de aceptación**: Los 17 escenarios de §23 en verde; 0 regresiones respecto a la línea base de Fase 0; `SEQ-GATE-01..04`/`ATT-GATE-01` en verde sin cambios; `docs/STATUS.md`/`FEATURES.md` actualizados solo después de validación completa.

**Condiciones de detención**: Si cualquier escenario de §23 falla, la fase no se cierra y P0-02 no puede declararse `COMPLETED/CLOSED` — se documenta el escenario fallido como bloqueante puntual de esa combinación, remitiendo a la fase de origen del componente que falló.

**Entregable de la fase**: Suite E2E completa en verde, regresión total confirmada, documentación de avance actualizada, P0-02 listo para una revisión de cierre formal (fuera del alcance de este plan — ese cierre es un documento propio, análogo a `P0-02_PURCHASE_RETURN_AUDIT_CLOSURE.md` de la fase de diseño).

`PHASE_14_ACCEPTED: NO`

---

## 11. Matriz de trazabilidad diseño → fase

### 11.1 Secciones del diseño → fase

| Sección del diseño | Fase(s) |
|---|---|
| §5.1 (invariantes cruzadas) | Fase 1 (guards de dominio) → Fase 3 (Lock A en handlers existentes) → Fase 10 (los 9 casos completos con datos reales) |
| §5.2 (Branch Ownership Rule — `BranchId` obligatorio, `PLAN-REV-01`) | Fase 1 (dominio: `CreateDraft`/`CreateFromReturn`) → Fase 2 (persistencia, `NOT NULL`, índice) → Fase 5 (resolución desde `ICurrentBranch` en `CreateDraft`) → Fase 6 (herencia en `SupplierCredit`) → Fases 7/8/10 (operan bajo el `BranchId` persistido, nunca sustituido) → Fase 11 (contrato HTTP no lo expone; `BranchScopeBehavior`) → Fase 12 (solo lectura en UI) |
| §6.4–§6.4quinquies (destino financiero) | Fase 1 (entidades) → Fase 2 (persistencia, `CHECK`) → Fase 4 (administración) → Fase 8 (`RegisterRefund`/`ReverseRefund`, locks `FOR SHARE`) |
| §7 (campos/índices) | Fase 1 (campos de dominio) → Fase 2 (persistencia/índices/`CHECK`/`UNIQUE`) |
| §9 (estados/transiciones) | Fase 1 (máquina de estados de dominio) → Fase 5 (Draft) → Fase 6 (Authorize) → Fase 9 (vínculo NC) → Fase 10 (Cancel) |
| §10 (cálculo de cantidades) | Fase 5 (`GetReturnableLines`, consulta derivada §10.2) → Fase 6 (`SourceDocLineId`, §10.3) |
| §11 (cálculos financieros) | Fase 1 (fórmulas de dominio) → Fase 6 (`Authorize`, ejemplos numéricos §11.3) |
| §12 (integración `PurchasePayable`) | Fase 1 (métodos nuevos) → Fase 2 (`xmin`) → Fase 6/7/10 (consumo) |
| §13 (`SupplierCredit`) | Fase 1 (modelo) → Fase 2 (persistencia) → Fase 6 (creación) → Fase 7 (aplicación) → Fase 8 (reembolso) → Fase 10 (cancelación de origen) |
| §14 (inventario/costo) | Fase 6 (movimiento de autorización) → Fase 10 (reversa) |
| §15 (concurrencia/locks) | Fase 2 (implementación real de Lock A/B) → Fase 3 (adopción en handlers existentes) → Fases 6/7/8/10 (consumo por operación) |
| §16 (transacciones/idempotencia) | Fase 0 (§16.3 prerrequisito) → Fase 2 (columnas de idempotencia) → Fases 5/6/7/8/9/10 (las 8 operaciones) |
| §17 (retenciones) | Fase 3 (Lock A compartido) → Fase 6 (`PR-006` bajo lock) |
| §18 (NC recibida) | Fase 1 (columna `CurrencyCode`) → Fase 9 (registro/vínculo completo) |
| §19 (contabilidad) | Fase 6 (`PurchaseReturnAuthorizedPostingTranslator`) → Fase 7 (translators de aplicación) → Fase 8 (translators de reembolso, §19.1ter) → Fase 10 (translator de cancelación) |
| §20 (auditoría/permisos) | Fase 1 (entidades de auditoría) → Fases 6/7/8/9/10 (handlers de auditoría por evento) → Fase 11 (permisos) |
| §21 (errores de negocio) | Distribuidos por operación — ver matriz §11.5 de este plan |
| §22 (condiciones finales de confiabilidad) | Fase 14 (verificación E2E de cada fila de la tabla) |
| §23 (matriz de escenarios) | Fase 14 (implementación como suite E2E completa) |
| §24 (cambios por capa) | Cubierto en su totalidad — ver matriz §11.2 de este plan |
| §25 (deudas/exclusiones) | Sección 5 y 17 de este plan |
| §28 (checklist de completitud del diseño) | No aplica a este plan — es checklist del documento de diseño, ya cerrado; no se repite aquí |

### 11.2 Componentes de §24 → fase

| Componente de §24 | Fase |
|---|---|
| `PurchaseReturn.cs`, `PurchaseReturnDetail.cs`, `PurchaseReturnAudit.cs`, `SupplierCredit.cs`, `SupplierCreditMovement.cs`, `SupplierCreditAudit.cs`, `PurchaseReturnSequence.cs` | Fase 1 |
| `CompanyFinancialDestination.cs`, `SupplierCreditRefundTransaction.cs`, `CompanyFinancialDestinationAudit.cs` | Fase 1 |
| `PurchaseReturnStatus`, `PurchaseReturnFiscalStatus`, `SupplierCreditMovementType` | Fase 1 |
| `FinancialDestinationTypeCode`, `RefundTransactionTypeCode` | Fase 1 |
| `IPurchaseReturnSequenceRepository.cs` (interfaz) | Fase 1 (interfaz) → Fase 2 (implementación) |
| `PurchasePayable.cs` (extensión) | Fase 1 (dominio) → Fase 2 (persistencia) → Fase 3 (consumo en handlers existentes) |
| `PurchasePayableConfiguration.cs` (`xmin` + columnas) | Fase 2 |
| `StockMovement.cs` (`SourceDocLineId`) | Fase 1 (dominio) → Fase 2 (persistencia) → Fase 6 (consumo) |
| `PurchaseReceptionDocument.cs` + `PurchaseReceptionDocumentConfiguration.cs` (`CurrencyCode`) | Fase 1 (dominio) → Fase 2 (persistencia) → Fase 9 (consumo) |
| `IPurchaseReturnRepository`/`PurchaseReturnRepository`, `ISupplierCreditRepository`/`SupplierCreditRepository`, `PurchaseReturnSequenceRepository` | Fase 2 |
| `ICompanyFinancialDestinationRepository`/`Repository`, `ISupplierCreditRefundTransactionRepository`/`Repository` | Fase 2 |
| `CompanyFinancialDestinationConfiguration`, `SupplierCreditRefundTransactionConfiguration` | Fase 2 |
| `RegisterPaymentUseCases.cs`/`IssueWithholdingUseCases.cs`/`CancelWithholdingUseCases.cs` (Lock A) | Fase 3 |
| `CancelPurchaseUseCases.cs` (Lock A + `PI-CANC-01`/`PI-CANC-02`) | Fase 3 (Lock A + validaciones con datos simulados) → Fase 10 (conexión real) |
| `PurchaseReturnDraftUseCases`, `AuthorizePurchaseReturnUseCases`, `CancelPurchaseReturnUseCases`, `RegisterSupplierCreditNoteUseCases`, `PurchaseReturnQueryUseCases`, `PurchaseReturnAuditHandler` | Fase 5 (Draft/Query) → Fase 6 (Authorize) → Fase 9 (NC) → Fase 10 (Cancel) |
| `ApplySupplierCreditUseCases`, `RegisterSupplierCreditRefundUseCases`, `ReverseSupplierCreditApplicationUseCases`, `ReverseSupplierCreditRefundUseCases`, `SupplierCreditAuditHandler`, `CompanyFinancialDestinationAuditHandler` | Fase 7 (aplicación/reversa) → Fase 8 (reembolso/reversa) |
| `CreateCompanyFinancialDestinationUseCase`, `UpdateCompanyFinancialDestinationNameUseCase`, `ChangeCompanyFinancialDestinationAccountingAccountUseCase`, `SetCompanyFinancialDestinationActiveUseCase` | Fase 4 |
| `PurchaseReturnAuthorizedPostingTranslator`, `PurchaseReturnCancelledPostingTranslator` | Fase 6, Fase 10 |
| `SupplierCreditAppliedPostingTranslator`, `SupplierCreditApplicationReversedPostingTranslator` | Fase 7 |
| `SupplierCreditRefundedPostingTranslator`, `SupplierCreditRefundReversedPostingTranslator` | Fase 8 |
| `PurchaseReturnController`, `SupplierCreditController`, `CompanyFinancialDestinationController` | Fase 4 (controller) / Fase 11 (los otros 2 + consolidación) |
| `ElectronicDocuments`/`Ride`/`DocumentSequence` (prohibido tocar) | Ninguna fase los modifica — verificado en Fase 14 como parte de la regresión |
| `Payment`/`PaymentApplicationLine` (prohibido tocar) | Ninguna fase los modifica — verificado en Fase 14 |
| `CashMovement.cs`/`CashSession.cs`/`CashRegister.cs` (solo consumo) | Fase 8 (consumo vía factory ya existente, sin modificar esquema) |
| `Account.cs` (prohibido tocar) | Ninguna fase lo modifica — Fase 4/8 solo lo referencian por FK ya soportada |
| Posting Engine (`PostingFact`/`IPostingEngine`/`PostingRuleResolver`) | Ninguna fase lo modifica — solo translators nuevos (Fases 6/7/8/10) |
| Entity Audit (`AuditRecordBase`, etc.) | Ninguna fase lo modifica — solo entidades/handlers nuevos (Fase 1 entidades, Fases 6/7/8/9/10 handlers) |
| `frontend/src/modules/purchases/` | Fase 12 |
| `frontend/src/modules/finance/` | Fase 13 |

**Confirmación**: cada fila de §24 del diseño tiene exactamente una fase de origen (donde se crea/modifica por primera vez) y, cuando corresponde, fases posteriores de consumo — ningún componente queda sin asignar, ninguno está asignado de forma ambigua a "cualquier fase", ninguno está duplicado sin justificación (los casos con 2+ fases listadas son consumo incremental legítimo del mismo componente, no reimplementación).

### 11.3 Operaciones idempotentes (§16.2) → fase

| # | Operación | Fase | Clave de unicidad | Restricción única (BD) | Respuesta ante repetición | Prueba concurrente | Resultado tras timeout |
|---|---|---|---|---|---|---|---|
| 1 | `CreateDraft` | Fase 5 | `(TenantId, CreateClientRequestId)` | `PurchaseReturn.CreateClientRequestId` | Retorna `Id` del draft ya creado | §16.2ter (Fase 5) | Reejecuta de cero si no hubo commit |
| 2 | `Authorize` | Fase 6 | `(TenantId, AuthorizeClientRequestId)` | `PurchaseReturn.AuthorizeClientRequestId` | Retorna snapshot ya confirmado | §16.2ter (Fase 6) | Ídem |
| 3 | `Cancel` | Fase 10 | `(TenantId, CancelClientRequestId)` | `PurchaseReturn.CancelClientRequestId` | Retorna `Status=Cancelled` ya confirmado | Prueba de integración (Fase 10) | Ídem |
| 4 | `ApplyToPayable` | Fase 7 | `(TenantId, ClientRequestId)` fila `Application` | `SupplierCreditMovement.ClientRequestId` | Retorna movimiento ya creado | Prueba de integración (Fase 7) | Ídem |
| 5 | `ReverseApplication` | Fase 7 | `(TenantId, ClientRequestId)` fila `ReversalOfApplication` | `SupplierCreditMovement.ClientRequestId` | Retorna reversa ya creada | Prueba de integración (Fase 7) | Ídem |
| 6 | `RegisterRefund` | Fase 8 | `(TenantId, ClientRequestId)` | `SupplierCreditMovement.ClientRequestId` + `SupplierCreditRefundTransaction.ClientRequestId` | Retorna reembolso ya creado | §16.5 escenarios 10-13 (Fase 8) | Ídem |
| 7 | `ReverseRefund` | Fase 8 | `(TenantId, ClientRequestId)` | Ídem, fila `ReversalOfRefund` | Retorna reversa ya creada | §16.5 escenarios 15-16 (Fase 8) | Ídem |
| 8 | Vincular NC | Fase 9 | `(TenantId, LinkCreditNoteClientRequestId)` | `PurchaseReturn.LinkCreditNoteClientRequestId` | Retorna vínculo ya confirmado | Prueba de integración (Fase 9) | Ídem |

**Confirmación**: las 8 operaciones idempotentes de §16.2 del diseño están mapeadas 1:1 a una fase de implementación, cada una con su prueba concurrente obligatoria — ninguna quedó fuera del plan ni relegada a backlog.

### 11.4 Invariantes cruzadas (§5.1) → fase

| Caso | Descripción | Fase de cierre | Lock | Revalidación | Error |
|---|---|---|---|---|---|
| 1 | Cancelar factura con devolución `Authorized` asociada | Fase 3 (preparación) → Fase 10 (cierre real) | Lock A | `PurchaseReturn.Status==Authorized` bajo lock | `PI-CANC-01` |
| 2 | Cancelar factura con CxP que recibió aplicación de crédito | Fase 3 → Fase 10 | Lock A | `SupplierCreditAppliedAmount>0` bajo lock | `PI-CANC-02` |
| 3 | Pagar CxP `cancelled` | Fase 3 | Lock A | Guard ya existente, ahora bajo lock | Código ya existente |
| 4 | Aplicar crédito sobre CxP `cancelled` | Fase 7 | Lock A + Lock B | `Status!=cancelled` bajo lock | `SC-002` |
| 5 | Revertir aplicación tras cancelar CxP destino | Fase 7 | Lock A + Lock B | `Status!=cancelled` del destino bajo lock | `SC-014` |
| 6 | Cancelar devolución con crédito aplicado | Fase 10 | Lock A + Lock B | `AvailableAmount==OriginalAmount` bajo lock | `PR-011` |
| 7 | Cancelar devolución con crédito reembolsado | Fase 10 | Lock A + Lock B | Misma fórmula que caso 6 | `PR-011` |
| 8 | Cancelar devolución con NC ya registrada | Fase 10 | Lock A (+B si crédito) | Igual que `Cancel` estándar | `PR-011` solo si aplica por crédito — permitido en lo demás |
| 9 | Cancelación concurrente factura/devolución | Fase 10 | Lock A (compartido) | Determinista por orden de adquisición | `PI-CANC-01` o éxito, nunca ambos |

**Confirmación**: las 9 invariantes cruzadas de §5.1 están mapeadas a su fase de cierre real con datos de extremo a extremo (Fase 10), con preparación explícita desde la Fase 3 para los casos que dependen de los handlers existentes.

### 11.5 Errores de negocio (§21) → fase

| Código | Operación | Fase | Capa que lo produce | Prueba |
|---|---|---|---|---|
| `PR-001`/`PR-002`/`PR-003`/`PR-004` | Crear/editar draft | Fase 5 | Application (FluentValidation + guard de dominio) | Fase 5 |
| `PR-005` | Autorizar (stock) | Fase 6 | Application (traducción de guard de `CurrentStock`) | Fase 6 |
| `PR-006` | Autorizar (retención) | Fase 6 | Application | Fase 6 |
| `PR-007`/`PR-008` | Cualquier mutación (concurrencia) | Fases 2 (mecanismo)/6/7/8/10 (consumo) | Infrastructure (`IDatabaseExceptionTranslator`) | Fases respectivas |
| `PR-009` | Transición inválida | Fases 5/6/9/10 | Application/Domain | Fases respectivas |
| `PR-010` | Exceso de aplicación de crédito sobre CxP | Fase 7 | Domain (`PurchasePayable.ApplySupplierCredit`) | Fase 7 |
| `PR-011` | Cancelar con crédito no íntegro | Fase 10 | Application | Fase 10 |
| `PR-012` | Idempotencia (`PurchaseReturn`) | Fases 5/6/10/9 | Application | Fases respectivas |
| `PR-013` | Transición inválida de `FiscalStatus` | Fase 9 | Domain | Fase 9 |
| `PI-CANC-01`/`PI-CANC-02` | Cancelar factura | Fase 3 (con datos simulados) → Fase 10 (real) | Application | Fase 10 |
| `SC-001`..`SC-005` | Operaciones de crédito | Fase 7 | Application/Domain | Fase 7 |
| `SC-006` | Idempotencia (`SupplierCreditMovement`) | Fases 7/8 | Application | Fases respectivas |
| `SC-007`/`SC-008`/`SC-009`/`SC-012`/`SC-013` | Vincular NC | Fase 9 | Application | Fase 9 |
| `SC-010`/`SC-011` | Concurrencia/doble reversa de crédito | Fases 7/8 | Infrastructure/Application | Fases respectivas |
| `SC-014` | Revertir aplicación tras CxP cancelada | Fase 7 | Application | Fase 7 |
| `SC-015` | Método de pago inactivo | Fase 8 | Application | Fase 8 |
| `SC-016`..`SC-019` | Validación cuantitativa NC | Fase 9 | Application | Fase 9 |
| `SC-020`..`SC-029` | Destino financiero del reembolso | Fase 4 (`SC-022/023/024/026`, alta/edición) / Fase 8 (todos, en `RegisterRefund`/`ReverseRefund`) | Application | Fases 4 y 8 |

**Confirmación**: los 44 códigos de error de §21 del diseño (`PR-001..013` = 13, `PI-CANC-01..02` = 2, `SC-001..029` = 29; 13+2+29=44) están mapeados a una fase de implementación y a una prueba concreta — ninguno quedó sin asignar.

### 11.6 Pruebas obligatorias → fase

| Prueba obligatoria del diseño | Fase | Estado en este plan |
|---|---|---|
| §16.3 — prueba bloqueante de interacción `SaveChangesWithSequenceRetryAsync` + transacción explícita | Fase 0 (aislada) → Fase 6 (integrada al handler real) | **Prerrequisito bloqueante, no backlog** |
| §16.2ter — prueba de carrera de idempotencia (representativa `CreateDraft`/`Authorize`) | Fase 5 (`CreateDraft`) y Fase 6 (`Authorize`) | **Prerrequisito bloqueante, no backlog** |
| §16.5 — las 26 pruebas del destino financiero del reembolso | Fase 8 | **Prerrequisito bloqueante, no backlog — las 26 completas, sin reducción** |
| §23 — 17 escenarios completos (incluido 7bis) | Fase 14 (consolidación E2E; cada escenario individual ya validado en su fase de origen: 1-5 en Fase 6, 6-7bis en Fases 7-8, 8 en Fase 9, 9-12 en Fases 3/6/7, 13 en Fase 7, 14 en Fase 10, 15 en Fases 5-9, 16 en Fase 6, 17 en Fase 10) | Cubiertos, no backlog |
| §19.1bis — ecuación contable balanceada (`Σdébitos=Σcréditos`) | Fase 6 (demostración algebraica + ejemplo numérico (g) de §11.3 reproducido) | Cubierta, no backlog |
| Reversas exactas (inventario/CxP/crédito/contabilidad) | Fase 10 | Cubiertas, no backlog |
| Aislamiento multi-tenant | Fase 2 (índices `TenantId`-scoped) + Fase 14 (regresión de aislamiento ya cubierta por la infraestructura general del ERP, `TenantIsolationInvariantTests.cs` de `ERP.Architecture.Tests`, confirmado existente) | Cubierto, no backlog |
| Branch Ownership Rule (§5.2 del diseño — `PLAN-REV-01`) — `BranchId` persistido correcto en `CreateDraft`/`Authorize`, nunca sustituible, rechazo por `BranchScopeBehavior` sin acceso a la sucursal | Fase 2 (`NOT NULL` a nivel de columna) → Fase 6 (prueba adicional obligatoria de §16.2ter del diseño) → Fase 11 (rechazo 403 por `BranchScopeBehavior` en los 5 endpoints mutadores) | **Prerrequisito bloqueante de sus fases respectivas, no backlog** |

**Confirmación explícita**: §16.3, §16.2ter y las 26 pruebas de §16.5 están asignadas como prerrequisitos bloqueantes de sus fases respectivas (Fase 0/6 para §16.3, Fases 5/6 para §16.2ter, Fase 8 para las 26 de §16.5) — **ninguna de las tres aparece en la sección 17 "Backlog no bloqueante permitido" de este plan**, consistente con §25.2 del diseño ("no permitido como backlog").

---

## 12. Estrategia de migración y persistencia

- **Una única migración EF** (Fase 2, `AddPurchaseReturnAndSupplierCredit`) agrupa las 10 tablas nuevas (`purchase_returns`, `purchase_return_details`, `purchase_return_audit`, `supplier_credits`, `supplier_credit_movements`, `supplier_credit_audit`, `purchase_return_sequence`, `company_financial_destinations`, `supplier_credit_refund_transactions`, `company_financial_destination_audit`) y las 3 modificaciones a tablas existentes (`purchase_payables` +`xmin`+2 columnas, `stock_movements` +1 columna, `purchase_reception_documents` +1 columna) — evita migraciones fragmentadas que dejarían el modelo EF inconsistente con la BD en un estado intermedio.
- Naming: `snake_case` para tablas/columnas, `ix_*`/`ux_*`/`uq_*` para índices, `fk_*` con `_tenant_` (nunca `_subscriber_`) — regla ya vigente de `docs/DEVELOPMENT.md`.
- Todas las columnas decimales nuevas siguen el Estándar de Precisión Numérica INMUTABLE de `CLAUDE.md`: `numeric(18,2)` para montos (`GrandTotal`, `Amount` de movimientos, `AvailableAmount`, `HistoricalCostTotal`, `CostVarianceTotal`, `ReturnAppliedAmount`, `SupplierCreditAppliedAmount`), `numeric(18,4)` para cantidades (`Quantity` de `PurchaseReturnDetail`), `numeric(18,6)` para `UnitCost` (= `LandedUnitCost` congelado), `numeric(5,2)` para `VatRate`/`IceRate`. Ninguna columna decimal de este plan se desvía de estas 4 escalas — no se requiere revisión arquitectónica formal adicional porque cada una ya está justificada por tipo/precisión/escala/motivo en §7 del diseño.
- `PurchaseReturnSequence.CurrentSeq` es `int`, no decimal — sin ambigüedad de escala.
- Verificación obligatoria tras cada migración: `dotnet ef migrations has-pending-model-changes` → `No changes` (Fase 2, criterio de aceptación explícito).
- `ErpDbContextModelSnapshot.cs` se actualiza automáticamente por `dotnet ef migrations add` — no se edita a mano (prohibido por `docs/DEVELOPMENT.md`, "Migraciones `.cs` a mano sin `dotnet ef migrations add`").
- Sin `DELETE` físico en ninguna tabla nueva — todas las entidades usan `IsActive`/estados terminales (`Cancelled`, `Closed` derivado) consistente con la regla absoluta del proyecto (`feedback_no_delete`).

---

## 13. Estrategia de concurrencia, transacciones e idempotencia

- **Dos advisory locks nuevos** (Fase 2): Lock A `"PurchaseInvoice.FinancialLock"` sobre `(TenantId, PurchaseInvoiceId)`, Lock B `"SupplierCredit.Lock"` sobre `(TenantId, SupplierCreditId)` — namespaces verificados sin colisión con `"SalesReturn.Lock"` ni con `IJournalEntryRepository.AcquireIdempotencyLockAsync` (prueba dedicada en Fase 2).
- **Orden fijo universal**: Lock A siempre antes que Lock B (§15.4) — aplicado en Fases 7, 8, 10; verificado explícitamente con pruebas de ausencia de deadlock en cada fase que usa ambos locks.
- **Múltiples Lock A**: orden ascendente de `Guid` como texto (caso `RegisterPaymentCommandHandler` con varias facturas) — implementado en Fase 3, sin cambios posteriores.
- **`xmin` como segunda defensa**: en `PurchasePayable` (Fase 2), `SupplierCredit` (Fase 2), `CompanyFinancialDestination`/`Account`/`SupplierCreditRefundTransaction` (Fase 2/8) — nunca la única defensa donde el diseño exige `FOR SHARE` explícito (§6.4quater: una lectura sin `FOR SHARE` no detecta un `UPDATE` concurrente antes del commit propio).
- **`PurchaseReturnSequence.CaptureNextAsync`**: única y exclusivamente dentro de la transacción ambiente del caller — nunca abre transacción propia (corrección explícita del diseño respecto a `DocumentSequence`, §7.1bis) — implementado en Fase 2, validado por la Fase 0 en cuanto al patrón de reintento ante conflicto.
- **Las 8 operaciones idempotentes de §16.2**: `ClientRequestId` + `RequestPayloadHash` obligatorios desde el primer momento (columnas NOT NULL o `WHERE NOT NULL` según corresponda) — nunca "recomendado", implementadas en Fases 5, 6, 7, 8, 9, 10 (ver matriz §11.3 de este plan).
- **Algoritmo de recuperación de carrera de §16.2bis**: implementado en `CreateDraft` (Fase 5) y en el vínculo de NC (Fase 9) — las dos operaciones con patrón "buscar → si no existe, insertar"; las operaciones sobre agregado ya existente (`Authorize`, `Cancel`, aplicaciones/reembolsos) siguen el mismo algoritmo de recuperación como defensa secundaria, aunque su ventana de carrera sea estructuralmente menor por estar ya serializadas por Lock A/B.
- **Frontera transaccional por operación**: exactamente una transacción explícita por cada una de las 8 operaciones idempotentes más las administrativas de Fase 4 — nunca dos operaciones de negocio combinadas en la misma transacción (regla general §2.1 del diseño: solo lo que ocurre dentro de `Authorize()` es una unidad; todo lo demás es un proceso propio).

---

## 14. Estrategia de pruebas

| Nivel | Proyecto | Qué cubre en este plan |
|---|---|---|
| Dominio | `ERP.Domain.Tests` | Guards de entidades/agregados nuevos y extendidos (Fase 1) |
| Aplicación (mocks) | `ERP.Application.Tests` | Handlers de las 8 operaciones idempotentes + administración de destinos + endurecimiento de los 5 handlers existentes (Fases 3-10) |
| Infraestructura (PostgreSQL real, Testcontainers) | `ERP.Infrastructure.Tests` | Persistencia, `CHECK`/`UNIQUE`, locks A/B, `PurchaseReturnSequence`, las 26 pruebas de §16.5, los 9 casos de §5.1, translators contables de integración (Fases 2, 6, 7, 8, 9, 10) |
| API (contrato HTTP) | `ERP.API.Tests` | Contratos REST, permisos, 422 estructurado (Fase 11) + E2E (Fase 14) |
| Arquitectura (gates CI) | `ERP.Architecture.Tests` | `LayerDependencyTests` (Fase 1), `SEQ-GATE-01..04`/`ATT-GATE-01` sin cambios (verificado en Fase 14), `TenantIsolationInvariantTests` sin cambios |
| Frontend unitario | `vitest` (`npm run test:unit`) | Schemas Zod, mapeo de DTO, componentes aislables (Fases 12, 13) |
| Frontend E2E | `playwright` (`npm run test:e2e`) | A confirmar en el momento de implementación si existe infraestructura E2E vigente específica de Compras — si no existe, se documenta como hallazgo, no se inventa (Fase 14) |
| Design System / arquitectura frontend | `npm run architecture:*` | F-V1..F-V8, `ConfigTabsLayout`, ausencia de estilos inline (Fases 12, 13) |

Cada fase (§10 de este plan) declara sus propios apartados "Pruebas unitarias/de integración/PostgreSQL reales/frontend" — esta sección consolida el mapa de proyectos, no repite el detalle ya declarado por fase.

---

## 15. Gates de compilación y regresión

Cada fase tiene un gate binario `PHASE_X_ACCEPTED: YES/NO` (§7.1 de la especificación de esta tarea). Regla de bloqueo: **una fase no puede comenzar si la anterior está en `NO`** — aplicado estrictamente al grafo de dependencias de la sección 8 de este plan, no solo al orden numérico (p. ej. Fase 12 depende de Fase 11, no de Fase 4 directamente, pero Fase 4 debe estar en `YES` antes de que Fase 13 pueda cerrarse porque Fase 13 consume la administración de destinos).

| Gate | Bloqueante para |
|---|---|
| `PHASE_0_ACCEPTED` | Toda fase de código productivo (1-14) |
| `PHASE_1_ACCEPTED` | Fase 2 |
| `PHASE_2_ACCEPTED` | Fases 3, 4, 5 |
| `PHASE_3_ACCEPTED` | Fase 6 (Lock A endurecido), Fase 10 (`PI-CANC-01/02`) |
| `PHASE_4_ACCEPTED` | Fase 8, Fase 13 |
| `PHASE_5_ACCEPTED` | Fase 6 |
| `PHASE_6_ACCEPTED` | Fases 7, 8, 9, 10 |
| `PHASE_7_ACCEPTED` | Fase 10 (caso 6 de §5.1) |
| `PHASE_8_ACCEPTED` | Fase 10 (caso 7 de §5.1) |
| `PHASE_9_ACCEPTED` | Fase 10 (caso 8 de §5.1) |
| `PHASE_10_ACCEPTED` | Fase 11 |
| `PHASE_11_ACCEPTED` | Fases 12, 13 |
| `PHASE_12_ACCEPTED` | Fase 14 |
| `PHASE_13_ACCEPTED` | Fase 14 |
| `PHASE_14_ACCEPTED` | Cierre formal de P0-02 (fuera de este plan) |

La implementación completa de P0-02 solo puede declararse terminada cuando los 15 gates (`PHASE_0` a `PHASE_14`) estén en `YES` — actualmente **todos están en `NO`**, este plan no ejecuta ninguna fase.

---

## 16. Riesgos y condiciones de detención

| Riesgo | Fase donde se materializa | Mitigación / condición de detención ya declarada en la fase |
|---|---|---|
| `SaveChangesWithSequenceRetryAsync` + transacción explícita aborta ante conflicto de secuencia | Fase 0/6 | **Riesgo descartado empíricamente en Fase 0** — 4/4 corridas contra PostgreSQL real (Testcontainers) confirman que el reintento se recupera con éxito dentro de la transacción ambiente (`SAVEPOINT` automático de EF Core/Npgsql); `AuthorizePurchaseReturnUseCases` reutiliza la composición ya validada, sin código nuevo |
| Namespace de Lock A/B colisiona con un lock ya existente | Fase 2 | Prueba dedicada de no colisión — condición de detención explícita en Fase 2 |
| `Σdébitos ≠ Σcréditos` en algún escenario de variación de costo | Fase 6 | Demostración algebraica de §19.1bis reproducida + los 3 ejemplos numéricos de §11.3 — condición de detención explícita en Fase 6 |
| Deadlock por orden de locks A/B invertido en alguna ruta de código | Fases 7, 8, 10 | Prueba de ausencia de deadlock — condición de detención explícita en cada fase que usa ambos locks |
| Reducción encubierta del alcance de las 26 pruebas de §16.5 | Fase 8 | Enumeradas una por una en este plan (10.6/Fase 8) — condición de detención explícita: ninguna puede omitirse |
| Contrato de `ReverseRefund` ampliado para aceptar campos financieros del cliente (violación de §6.4quinquies) | Fase 8 | Condición de detención explícita — el contrato solo acepta `OriginalRefundTransactionId`/`Reason`/`ClientRequestId`/`EffectiveDate` |
| Regresión en `SalesReturn`/`SalesReceivable`/Compras existente | Fase 3, Fase 14 | Línea base de Fase 0 + regresión completa en Fase 14 — condición de detención explícita en ambas |
| Auditoría de reutilización de frontend omitida antes de crear componentes nuevos | Fases 12, 13 | Condición de detención explícita — la fase no puede iniciar sin la declaración de auditoría entregada |
| Endpoint de `CompanyFinancialDestination` expone update estructural genérico o delete físico | Fase 4, Fase 11 | Condición de detención explícita — solo los 4 casos de uso limitados |
| Gates CI (`SEQ-GATE-01..04`, `ATT-GATE-01`) se relajan indirectamente por la nueva infraestructura `PurchaseReturnSequence` | Fase 14 | Verificación explícita de que ambos gates permanecen en verde sin cambios — `PurchaseReturnSequence` es deliberadamente independiente (§7.1bis), nunca debe tocar el código de esos gates |
| Ausencia de archivo `.sln` provoque comandos de build inventados | Toda fase | Resuelto — este plan usa `dotnet test <Proyecto>` por carpeta, verificado contra `docs/DEVELOPMENT.md`, nunca un comando `dotnet build <archivo.sln inexistente>` |

---

## 17. Backlog no bloqueante permitido

Idéntico y exclusivamente el listado de §25.1 del diseño — ningún ítem adicional se agrega en este plan:

1. Validación automática en línea de la NC contra el servicio público de consulta del SRI.
2. Automatización avanzada de conciliación XML de la NC recibida (más allá de validación estructural y de duplicidad, ya cubierta en Fase 9).
3. Lotes/series (sin infraestructura de origen en Compras).
4. Nota de Débito emitida por el comprador (sin caso de negocio evidenciado).
5. Cardinalidad N:M entre `PurchaseReturn` y NC recibidas (v1 es 1:1, Fase 9).
6. Mejoras visuales/UX no indispensables del frontend de aplicación de crédito (más allá de lo exigido por F-V1..F-V8 en Fase 13).
7. Refactors generales no requeridos por este diseño.

**Confirmación explícita**: ningún ítem de §25.2 del diseño (concurrencia, cantidades, idempotencia, advisory locks, crédito, aplicación, reembolso, reversas, contabilidad, bloqueo por retención, consistencia transaccional, trazabilidad, ni las pruebas de validación previa de §16.3/§16.2ter/§16.5) aparece en esta lista — todos están asignados a una fase obligatoria en la sección 10 y en la matriz §11.6 de este plan.

---

## 18. Cierre documental futuro

Al completar y validar la Fase 14 (todos los gates `YES`), el cierre formal de P0-02 requiere, como acción **posterior** a este plan (no parte de él):

1. Documento de cierre análogo a `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` (sección "Estado de cierre" al inicio del documento) — actualizar este mismo archivo con un bloque de cierre, sin alterar el contenido de las 15 fases ya ejecutadas (constancia histórica, mismo criterio que P0-01).
2. Entrada nueva en `docs/STATUS.md` siguiendo el formato exacto de la entrada de cierre de P0-01 (capacidades entregadas, mejoras de infraestructura registradas, pendientes operativos no bloqueantes, backlog técnico no bloqueante).
3. Entrada nueva en `FEATURES.md` (módulo de Compras).
4. Si durante la implementación surge deuda técnica no bloqueante análoga a la registrada en el cierre de P0-01 (ej. inconsistencias de naming, duplicación menor de fixtures de test), se registra en una sección "Backlog técnico no bloqueante (registrado al cierre)" de este mismo documento, con el mismo formato de tabla (`# / Ítem / Motivo por el que no se corrigió en el cierre`) — nunca se mezcla con la sección 17 de este plan (que es backlog de **diseño**, no de **hardening post-implementación**).
5. Ninguna de estas 4 acciones se ejecuta durante la redacción de este plan ni durante ninguna de sus 15 fases — son posteriores a `PHASE_14_ACCEPTED: YES`.

---

## 19. Checklist de aprobación del plan

Cada punto es verificable leyendo este documento — no es una opinión a recabar, mismo criterio que §28 del diseño:

1. **15 fases (0-14) definidas, cada una con los 19 apartados obligatorios** — verificable en la sección 10.
2. **Gate binario por fase, ninguno en `YES` todavía** — verificable en la sección 15.
3. **Los 5 handlers existentes de Compras/Finance endurecidos con Lock A** — verificable en Fase 3.
4. **Las 8 operaciones idempotentes de §16.2 mapeadas a su fase con prueba concurrente** — verificable en la matriz §11.3.
5. **Las 9 invariantes cruzadas de §5.1 mapeadas a su fase de cierre real** — verificable en la matriz §11.4.
6. **Los 44 códigos de error de §21 mapeados a operación/fase/capa/prueba** — verificable en la matriz §11.5.
7. **§16.3, §16.2ter y las 26 pruebas de §16.5 declaradas prerrequisito bloqueante, no backlog** — verificable en la matriz §11.6 y en la sección 17 (ausencia explícita de esos ítems).
8. **Todos los componentes de §24 del diseño asignados a una fase, sin ambigüedad ni duplicación no justificada** — verificable en la matriz §11.2.
9. **Sin CRUD genérico ni delete físico en `CompanyFinancialDestination`** — verificable en Fase 4 y en la sección 16 (riesgos).
10. **Sin modificación de `DocumentSequence`, Posting Engine, esquema de Caja, esquema de `Payment`, `Account`** — verificable en la matriz §11.2 (fila "prohibido tocar") y en la sección 16.
11. **Backlog no bloqueante idéntico a §25.1 del diseño, sin ítems adicionales ni ítems de §25.2 trasladados** — verificable en la sección 17.
12. **Comandos de validación de cada fase verificados contra el repositorio real (sin `.sln` inventado)** — verificable en la sección 7.5 y en el riesgo correspondiente de la sección 16.
13. **`BranchId` obligatorio, inmutable, sin excepción, en `PurchaseReturn`/`SupplierCredit` (Branch Ownership Rule, §5.2 del diseño — corrige `PLAN-REV-01`)** — verificable en Fases 1, 2, 5, 6, 7, 8, 10, 11, 12 y en la matriz §11.1.

`IMPLEMENTATION_PLAN_APPROVED` pasó a `YES` en la segunda revisión ARB, tras corregir el hallazgo residual `P0-02-ARB2-01`. `IMPLEMENTATION_AUTHORIZED` pasó a `YES` por autorización formal explícita posterior del usuario (2026-07-31) — habilita exclusivamente el inicio de FASE 0; cada fase siguiente requiere su propio `PHASE_X_ACCEPTED: YES` antes de continuar.

---

## Nota de integridad del diseño fuente

**Corrección `PLAN-REV-03`**: la versión previa de esta nota afirmaba que el hash SHA-256 declarado tenía 66 caracteres hexadecimales. Es incorrecto — SHA-256 produce siempre **64 caracteres hexadecimales**, nunca 66; la cadena citada en la versión previa medía en realidad 64 caracteres (el conteo de "66" era una afirmación falsa de esa versión, no una propiedad real de la cadena). Se corrigió aquí, y además se reemplazó el hash por el que corresponde al diseño una vez aprobado (`APPROVED`, `DESIGN_APPROVED: YES` — segunda revisión ARB, hallazgo residual `P0-02-ARB2-01` corregido), no a versiones previas del diseño.

Hash SHA-256 de `P0-02_PURCHASE_RETURN_DESIGN.md`, calculado sobre el archivo en disco **después** de registrar en §16.3 el resultado empírico de la Fase 0 (prueba de integración contra PostgreSQL real, PASS — ver §16.3 del diseño y `PHASE_0_ACCEPTED: YES` de este plan):

```
702c3f01aacc3e808b74c340225e2bae604422a3b64feda8f612d1f80854d4d5
```

Esta cadena tiene **64 caracteres hexadecimales**, la longitud exacta que produce SHA-256. No se conserva ni se referencia ningún hash de versiones previas del diseño como si siguiera vigente — corresponden a versiones con un defecto bloqueante (`PLAN-REV-01`), un hallazgo residual (`P0-02-ARB2-01`) o pendientes de registrar el resultado empírico de §16.3, todos ya corregidos/registrados.

---

```text
DESIGN_STATUS: APPROVED
DESIGN_APPROVED: YES
IMPLEMENTATION_PLAN_STATUS: APPROVED
IMPLEMENTATION_PLAN_APPROVED: YES
IMPLEMENTATION_AUTHORIZED: YES
PHASE_0_STATUS: COMPLETED
PHASE_0_ACCEPTED: YES
PHASE_1_AUTHORIZED: NO
```







