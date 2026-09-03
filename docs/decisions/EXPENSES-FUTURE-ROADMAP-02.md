# EXPENSES-FUTURE-ROADMAP-02 — Roadmap funcional de Gastos

## Estado

**Aprobado para planificación por fases.** 2026-09-03.

Este documento **no implementa cambios por sí mismo**. Es el resultado de una auditoría de código real del módulo de Gastos (Expenses) y define el roadmap funcional/técnico aprobado para su evolución. Cualquier fase (E1–E9) requiere su propia entrega de desarrollo, y cerrar sus propios tests, antes de considerarse completa. La fase E8 requiere además una ADR de implementación previa (ver "Reglas").

## Contexto

El módulo de Gastos ya cuenta con, verificado en código:

- Catálogo jerárquico Tipo → Categoría → Subcategoría, con cuenta contable destino en la subcategoría.
- `ExpenseDocument` / `ExpenseLine` / `ExpensePaymentSchedule`.
- Estados `Draft` / `Confirmed` / `Cancelled`.
- Posting contable al confirmar (estricto: aborta la transacción si falla).
- Integración con `AccountsPayable` (CxP genérica, company-level) al confirmar.
- Cancelación con reverso contable y bloqueo si la CxP tiene pagos aplicados.
- Integración con `DocumentFlowPolicy` (separación correcta permiso vs. política documental).
- Permisos básicos (10 permission keys en `ExpensePermissions`).
- Aislamiento multi-tenant/company/branch fail-closed, verificado en repositorio y handlers (no confía en el body para tenant/company/branch).

## Hallazgos principales

- **Retenciones no existen en Expenses.** El motor (`AccountsPayable.ApplyRetention()`/`ReverseRetention()`, `RetentionCalculator` de Purchases) ya existe y es reutilizable, pero nunca se invoca desde el flujo de Gastos. **Corrección de diseño (2026-09-03):** la propuesta original de E1 modelaba la retención como campos directos en `ExpenseLine`/`ExpenseDocument`. Esa arquitectura fue rediseñada — retención es un proceso tributario transversal, no propiedad de un documento específico. Ver [`RETENTIONS-MODULE-DESIGN-01.md`](./RETENTIONS-MODULE-DESIGN-01.md).
- **`AuthorizationMode` puede bloquear confirmación sin flujo Approve/Reject.** El campo existe en `DocumentFlowPolicy`, pero su enforcement actual (`DocumentFlowPolicyService.EnsureConfirmationFlowAsync`) simplemente lanza excepción y bloquea confirmar si `AuthorizationMode != None` — no existe estado `PendingApproval` ni comando Approve/Reject. Es una trampa operativa activable hoy sin darse cuenta.
- **`RequiresAttachment` existe pero no se valida.** Campo declarado en `DocumentFlowPolicy`, nunca enforced en ningún handler de Expenses — a diferencia de `RequiresCancellationReason`, que sí está implementado. Genera falsa sensación de control si se activa.
- **No hay reportes operativos de Gastos** (por categoría, proveedor, estado, pendientes de pago). Purchases sí tiene su "Reporte de Compras" equivalente.
- **No hay concepto de centro de costo** en Expenses (ni en el ERP Core en general).
- **No hay adjuntos** (factura proveedor, comprobante de pago) ni UI de carga/visor.
- **Faltan permisos finos**: aprobar/rechazar, ver contabilidad generada, ver CxP generado, ver reportes, administrar reglas/aprobaciones.
- **Frontend de Expenses tiene deuda de tests**: cero tests para las páginas de listado/formulario y sus servicios (solo existe test de categorías).
- **XML/RIDE de retención electrónica requiere fase separada y ADR previa**, por tocar `ElectronicDocuments v1.0` (infraestructura CLOSED).

## Roadmap aprobado

- **E1** — Retenciones básicas IVA, mediante el módulo transversal `Retentions`. Gastos es el primer consumidor. Ver [`RETENTIONS-MODULE-DESIGN-01.md`](./RETENTIONS-MODULE-DESIGN-01.md) para arquitectura, entidades, estados, fases (E1-A a E1-G) y decisiones aprobadas — este roadmap ya no detalla su diseño interno.
- **E2** — Retención de renta + comprobante de retención PDF, sin XML SRI.
- **E3** — Adjuntos obligatorios.
- **E4** — Reporte de Gastos v1.
- **E5** — Permisos finos + separación de vistas.
- **E6** — Aprobaciones SingleStep.
- **E7** — Filtros avanzados + export.
- **E8** — XML/RIDE de retención electrónica SRI.
- **E9** — Tests transversales (no al final).

## Orden recomendado de ejecución

1. **E1** — Retenciones IVA vía el módulo `Retentions` (ver [`RETENTIONS-MODULE-DESIGN-01.md`](./RETENTIONS-MODULE-DESIGN-01.md)), primer consumidor Gastos.
2. **E9** — Tests base del ciclo actual, en paralelo/inmediato a E1 (deuda ya existente, independiente de fases nuevas).
3. **E3** — Adjuntos obligatorios.
4. **E4** — Reporte de Gastos v1.
5. **E5** — Permisos finos + vista detalle.
6. **E2** — Retención de renta + comprobante PDF.
7. **E7** — Filtros/export.
8. **E6** — Aprobaciones SingleStep, **solo si negocio lo confirma**.
9. **E8** — XML/RIDE de retención electrónica, con ADR previa.

## Reglas

- Cada fase debe cerrar sus propios tests (dominio/aplicación/API/frontend) antes de darse por completa.
- No implementar E8 sin ADR previa.
- No tocar `ElectronicDocuments v1.0` sin evidencia técnica, tests y revisión de compatibilidad (infraestructura CLOSED).
- No duplicar `RetentionCalculator` — reutilizar el motor ya existente en Purchases/CxP.
- No hardcodear porcentajes/códigos de retención SRI.
- Usar catálogos/config SSOT dinámico para todo dato tributario o configurable (nunca enum/array estático repetido en frontend/backend).
- No crear numeración documental paralela; si una fase requiere secuencia, usar `CaptureNextAsync` (infraestructura CLOSED).
- Mantener Clean Architecture (`ERP.API → ERP.Application → ERP.Domain ← ERP.Infrastructure`).
- Mantener Design System ZH en cualquier UI nueva (auditoría de reutilización antes de crear componentes).
- Mantener aislamiento Tenant/Company/Branch fail-closed en toda query y comando nuevo.

## Detalle de fases E1–E9

### E1 — Retenciones básicas (IVA), vía módulo transversal `Retentions`

**Rediseñada (2026-09-03).** El detalle completo de arquitectura, agregado `RetentionDocument`, entidad `RetentionDocumentLine`, estados/transiciones, flujo desde Gastos, flujo futuro desde Compras, impacto en CxP/contabilidad, fases de implementación (E1-A a E1-G), riesgos y decisiones aprobadas está documentado en [`RETENTIONS-MODULE-DESIGN-01.md`](./RETENTIONS-MODULE-DESIGN-01.md) — no se repite aquí para evitar dos fuentes de verdad.

**Resumen:** Retenciones deja de plantearse como campos directos en `ExpenseLine`/`ExpenseDocument` y pasa a ser un módulo independiente (`ERP.Domain/Modules/Retentions`, etc.) con relación genérica a su documento origen (`SourceDocumentType`/`SourceDocumentId`). Gastos es el primer consumidor implementado; Compras sigue usando `IssuedWithholding` sin cambios hasta una fase de migración separada. Emisión manual y explícita sobre un gasto ya confirmado.

### E2 — Retención de renta + comprobante de retención (PDF, sin XML SRI)

**Impacto funcional:** sin retención de renta el ciclo queda incompleto (Ecuador exige ambas cuando aplica). Sin comprobante imprimible, no hay soporte físico que entregar al proveedor.

**Evidencia:** mismo motor de E1, extendido a código de retención de renta. No existe hoy ningún generador de documento imprimible de retención en el ERP (`GetPurchaseReceptionXmlViewHandler.cs` es XML, no PDF de retención).

**Alcance:** extender E1 a retención de renta; nueva entidad/proyección "documento de retención" 1:1 con el `ExpenseDocument` confirmado (numeración vía `CaptureNextAsync` si aplica); PDF imprimible del comprobante.

**Fuera de alcance:** XML/RIDE, autorización SRI, monitor electrónico (E8).

**Dependencia:** requiere E1 validado antes de avanzar.

### E3 — Adjuntos obligatorios

**Impacto funcional:** `DocumentFlowPolicy.RequiresAttachment` (`DocumentFlowPolicy.cs:35`) existe pero nunca se valida — a diferencia de `RequiresCancellationReason`, sí enforced en `DocumentFlowPolicyService.cs:101`. No existe entidad de adjunto para Expenses ni UI de carga/visor.

**Alcance:** entidad `ExpenseDocumentAttachment` (foto/PDF/XML de factura proveedor, comprobante de pago); endpoint de subida/descarga; enforcement real de `RequiresAttachment` en `DocumentFlowPolicyService` antes de confirmar; visor/descarga en UI.

**Fuera de alcance:** reglas de adjunto obligatorio por categoría/monto específico (se difiere hasta que el negocio lo pida; se empieza con "obligatorio sí/no" a nivel de política).

### E4 — Reporte de Gastos v1

**Impacto funcional:** no existe reporte operativo de Gastos. `SuppliersModule.cs:200-218` define "Reporte de Compras" sin equivalente en Gastos. El Estado de Resultados solo agrega gasto por `AccountType.Expense`, sin visibilidad operativa.

**Alcance:** reporte por categoría, proveedor, estado y pendientes de pago (usando `PaymentSchedule`/CxP existentes); item de navegación propio bajo "Gastos".

**Fuera de alcance:** gastos con retenciones (depende de E1/E2), export Excel/PDF (E7), centro de costo (no existe el concepto — fuera de alcance salvo decisión explícita del negocio).

### E5 — Permisos finos + separación de vistas

**Impacto funcional:** faltan permission keys separados (`expenses.documents.approve/reject`, `expenses.accounting.view`, `expenses.payables.view`, `expenses.reports.view`, `expenses.rules.admin`). Un solo componente (`ExpenseDocumentFormPage.tsx`) maneja create/edit/detail, lo que complica extender timeline/historial y navegación cruzada sin acoplar vistas.

**Alcance:** agregar los permission keys faltantes; separar vista detalle readonly del formulario de edición; agregar timeline/historial (usando datos ya guardados por `AuditableEntity`, hoy no expuestos en UI); navegación directa desde el documento a su CxP y a su asiento contable.

**Fuera de alcance:** navegación a retención (depende de E1/E2); aprobar/rechazar como acción real (depende de E6 — aquí solo se crea el permiso).

### E6 — Aprobaciones (SingleStep)

**Impacto funcional:** hallazgo de mayor riesgo operativo silencioso. `AuthorizationMode` está declarado pero su enforcement actual bloquea confirmar sin ruta de salida si se activa `SingleStep`/`MultiStep`. No existe estado `PendingApproval`/`Approved`/`Rejected` en `ExpenseStatus`, ni comando Approve/Reject.

**Alcance:** nuevo estado `PendingApproval` en `ExpenseStatus` (Draft→PendingApproval→Approved/Rejected→Confirmed/vuelta a Draft); comandos `ApproveExpenseDocument`/`RejectExpenseDocument`; conectar con `AuthorizationMode.SingleStep`; permisos ya creados en E5; UI de aprobar/rechazar con comentario de rechazo.

**Fuera de alcance:** `MultiStep`; reglas de aprobación por monto/categoría/usuario.

**Nota:** según la regla de proyecto "Draft vs confirmación directa" (2026-08-28), el equipo prefiere flujos directos con confirmación simple salvo necesidad real comprobada. Esta fase debe confirmarse con el negocio antes de planificarse en firme.

### E7 — Filtros avanzados + export

**Impacto funcional:** el listado solo filtra por `search`+`status`, sin rango de fechas/categoría/proveedor. No hay export Excel/PDF. Limitación de UX/productividad, no de integridad de datos.

**Alcance:** filtros por rango de fechas, categoría, proveedor (y estado de aprobación si E6 ya existe); export Excel/PDF del listado y del reporte de E4.

**Fuera de alcance:** cambios a los reportes en sí (cubiertos en E4).

### E8 — XML/RIDE de retención electrónica SRI

**Impacto funcional:** no existe nada de comprobante de retención electrónico, autorización SRI ni monitor electrónico para Expenses. Purchases sí tiene `IssuedWithholding`, `GetPurchaseReceptionXmlViewHandler.cs` como patrón de referencia. Única fase que toca `ElectronicDocuments v1.0` (infraestructura CLOSED).

**Alcance:** emisión de XML/RIDE de retención para gastos siguiendo el patrón de Purchases. Requiere ADR + evidencia técnica + tests + revisión de compatibilidad antes de tocar `ElectronicDocuments`.

**Fuera de alcance:** cualquier cambio al comportamiento de `ElectronicDocuments` para otros módulos — esta fase solo consume la infraestructura existente, no la modifica.

### E9 — Tests transversales (no al final)

**Impacto funcional:** gaps identificados — cero tests frontend de Expenses (solo existe test de categorías); no hay test de integración end-to-end contra BD real (confirmar→asiento→CxP→pago→cancelar); no hay test dedicado de fuga cross-tenant/branch para Expenses; no hay test de `DocumentFlowPolicy` con `AuthorizationMode != None` aplicado a Gastos.

**Alcance:** no es una fase aislada al final. Cada fase E1–E8 cierra sus propios tests al completarse. E9 documenta la deuda de tests ya existente **antes** de cualquier fase nueva, priorizada así:
1. Tests frontend del ciclo Draft→Confirm→Cancel actual.
2. Test de integración end-to-end del flujo actual.
3. Test explícito de `AuthorizationMode != None` bloqueando Gastos (documenta el riesgo de E6 mientras no se implemente).

## Riesgos

- **Riesgo fiscal/contable por retenciones ausentes**: el CxP y los asientos de gastos con proveedores sujetos a retención están mal calculados hoy — impacto en cifras contables reales, no solo funcional.
- **Riesgo operativo por `AuthorizationMode`**: activar `SingleStep`/`MultiStep` en la política de Gastos hoy bloquea totalmente la confirmación sin ruta de salida, sin que el sistema lo advierta como tal.
- **Riesgo de falsa seguridad por `RequiresAttachment`**: el campo existe en la política pero no cambia ningún comportamiento — quien lo active cree tener un control que no opera.
- **Riesgo de deuda de tests frontend**: sin cobertura hoy en el ciclo Draft→Confirm→Cancel del frontend, cualquier regresión en ese flujo no se detecta automáticamente.
- **Riesgo normativo SRI para XML/RIDE futuro**: al no existir infraestructura de referencia propia en Expenses, la fase E8 depende enteramente de replicar correctamente el patrón de Purchases y de una ADR de implementación — riesgo de incumplimiento si se implementa sin ese respaldo.

## Entrega

- No se modificó código productivo (backend ni frontend).
- No se realizó ningún cambio funcional.
- Este documento es únicamente la guía oficial de roadmap para futuras fases de desarrollo del módulo de Gastos.
- Cualquier implementación futura requiere su propia entrega de desarrollo y, en el caso de E8, una ADR de implementación previa.
