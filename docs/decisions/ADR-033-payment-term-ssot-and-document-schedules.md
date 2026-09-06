# ADR-033: PaymentTerm como SSOT operativo + defaults por empresa/rol + cronograma final por documento

**Estado:** Approved (diseño) — pendiente de implementación por fases (ver Fase 1 completada, Fases 2-5 pendientes).
**Fecha aprobación:** 2026-09-06
**Autor:** Sebastian Zhinin (decisión tomada tras auditoría de código guiada)
**Contexto:** Auditoría de código confirmó tres focos de inconsistencia en el manejo de plazos de pago/cobro:
1. Ventas no persiste el cronograma que el usuario edita en el simulador de crédito (`CreditSimulatorModal`) — la CxC real se recalcula algorítmicamente desde el snapshot de cabecera al autorizar, ignorando la edición.
2. El default de condición de pago de Proveedor (`SupplierRoleConfig.PaymentTermId`) es único por tenant, no por empresa — a diferencia del default de Cliente (`CompanyBpTradingSettings.PaymentTermId`), que sí es por empresa.
3. `CreditTerm` (Condiciones de Crédito) es infraestructura completa (dominio + CRUD + UI) sin ningún caso de uso real conectado — ni Ventas, ni Compras, ni Gastos la consumen.

No se detectó fuga de datos entre tenants ni entre empresas en el catálogo `PaymentTerm` ni en los defaults auditados — el filtro global fail-closed (`EnterpriseQueryFilterConfigurator`) cubre correctamente las tablas involucradas.

---

## Decisión

1. **`PaymentTerm` permanece como catálogo operativo único (SSOT) de plantillas de plazos/planes de pago y cobro**, con **scope tenant-wide** (sin `CompanyId`). No se migra a company-scope: es una plantilla genérica ("Neto 30", "3 cuotas c/15 días"), no una política de empresa. La variación por empresa vive en el *default* (asignación tercero↔condición), no en la plantilla.

2. **La condición asignada a un cliente o proveedor es únicamente un default/propuesta inicial**, nunca una regla rígida del documento:
   - Cliente: default de venta, por empresa (`CompanyBpTradingSettings.PaymentTermId`, ya correcto en modelo — pendiente conectarlo al backend de Ventas).
   - Proveedor: default de compra/gasto, **debe migrar a scope por empresa** (hoy es único por tenant vía `SupplierRoleConfig.PaymentTermId`) — corrige el gap donde dos empresas del mismo tenant no pueden negociar plazos distintos con el mismo proveedor.
   - Un mismo Business Partner con rol Cliente y Proveedor mantiene defaults independientes por naturaleza del modelo (estructuras separadas).

3. **Resolución de default centralizada en backend**, mismo orden para los tres flujos (Ventas, Compras, Gastos): documento explícito → default del tercero (rol + empresa) → **exigir selección explícita** si no hay default válido. Prohibido: elegir "primer registro" del catálogo o inferir por coincidencia de días — ambos patrones existen hoy solo como fallback de UI en Ventas y deben eliminarse al centralizar la resolución en Application.

4. **El documento manda al confirmar.** Cada agregado documental (`PurchaseInvoice`, `ExpenseDocument`, y el nuevo `SalesInvoice`) tiene su propia colección de cuotas (`PaymentSchedule`), replicando el contrato de invariantes ya validado en Compras — no se crea una entidad de cronograma compartida entre módulos (no existe `ERP.Shared` y no se introduce un equivalente):
   - `Installments > 0`.
   - `Σ Amount == GrandTotal` (validación estricta, sin tolerancia de redondeo salvo en la última cuota).
   - `DueDate >= IssueDate` para toda cuota.
   - Numeración de cuota correlativa, sin huecos.
   - El usuario puede personalizar días, fechas, montos y notas por cuota antes de confirmar, sujeto a permisos.
   - Una vez generado el cronograma, la cabecera (`PaymentTermId`/`Installments`/`DaysBetween`) queda congelada — mismo patrón ya vigente en `PurchaseInvoice.EnsureDraft()`.

5. **CxC/CxP nacen exclusivamente del cronograma final del documento**, nunca de un recálculo del catálogo `PaymentTerm` vigente. Este comportamiento ya es correcto en Compras y Ventas (aunque en Ventas la fuente es hoy un cálculo algorítmico de cabecera, no un cronograma editable — ver punto 7) y debe mantenerse como principio no negociable para cualquier extensión futura.

6. **Histórico estable**: cambios posteriores en el catálogo `PaymentTerm` o en el default de cliente/proveedor **no modifican documentos ya creados o confirmados**. Esto ya se cumple hoy (ninguna CxC/CxP consulta `PaymentTerm` en tiempo de lectura) y se mantiene como invariante de diseño para toda entidad nueva.

7. **Ventas debe alinearse al patrón de Compras**: se introduce `SalesInvoice.PaymentSchedule` (espejo de `PurchasePaymentSchedule`), y `AuthorizeSalesInvoiceCommand` acepta un cronograma opcional explícito (igual que `ConfirmPurchaseUseCases` acepta `cmd.Schedule`). El `CreditSimulatorModal` deja de ser una vista desconectada: sus ediciones deben viajar en el comando de confirmación y ser la fuente real de `SalesReceivable.GenerateInstallments` (o su reemplazo por cuotas explícitas).

8. **Validación de `IsActive` en backend**, ausente hoy en Ventas y Compras al resolver `PaymentTermId`:
   - Operación nueva: condición inactiva no seleccionable ni asignable como nuevo default.
   - Histórico: se respeta sin cambios (una condición desactivada no afecta documentos ya confirmados).
   - Borrador existente que referencia una condición recién desactivada: exige decisión explícita del usuario al confirmar (no bloqueo silencioso, no paso silencioso) — el mecanismo concreto se diseña en Fase 2.

9. **Atomicidad de confirmación + generación de cartera**: Compras y Ventas ya comitean documento + CxC/CxP en el mismo `SaveChanges`. Gastos tiene una ruta (`CreateFromOriginAsync`) que comitea aparte — se unifica a `StageFromOriginAsync` + `SaveChanges` único, igual que Compras, para eliminar la posibilidad de un documento confirmado sin cartera generada.

## Fuera de alcance (explícito)

- **`CreditTerm` (Condiciones de Crédito) no se toca, no se conecta a ningún flujo transaccional y no se fusiona con `PaymentTerm`.** Queda documentado como infraestructura completa y desconectada, pendiente de una decisión futura independiente: conectarla a una función real de riesgo crediticio, o renombrar/ocultar/deprecar para no confundir en el UI. Esa decisión no se toma en este ADR.
- No se crea un tercer catálogo de plazos/planes.
- No se implementan reglas de riesgo crediticio.
- No se resuelven anticipos complejos — el modelo de `SalesPaymentSchedule` se diseña de forma que no bloquee esa evolución futura, sin implementarla ahora.
- No se ejecuta ningún cambio de código en esta entrega — este ADR cierra el diseño; la implementación se divide en fases pequeñas (ver Consecuencias).

## Consecuencias

- **Fase 2 (P1)**: validación `IsActive` server-side en creación/edición de borrador de Ventas y Compras; unificar atomicidad de confirmación en Gastos (`CreateFromOriginAsync` → `StageFromOriginAsync`). No incluye cambios de modelo de datos.
- **Fase 3**: caso de uso `ResolvePaymentTermDefault` centralizado en Application, consumido por Ventas/Compras/Gastos; migración de default de Proveedor a scope por empresa (nueva tabla análoga a `CompanyBpTradingSettings`, con backfill del valor actual del tenant hacia cada empresa activa del proveedor). Frontend de Ventas deja de decidir fallback (`useSalesPage.ts`) y pasa a consumir el resultado del backend.
- **Fase 4**: `SalesInvoice.PaymentSchedule` + `AuthorizeSalesInvoiceCommand.Schedule` opcional; conectar `CreditSimulatorModal` a persistencia real; `SalesReceivable` nace del cronograma explícito cuando exista, en vez de recalcularse solo desde `PaymentTermSnapshot`.
- **Fase 5**: UX (mostrar origen de la condición, indicador "personalizado", acción "Restablecer desde default"), revisión de permisos finos (ver catálogo vs. asignar default vs. personalizar cronograma vs. confirmar con cambios — no auditado en detalle en este ADR), y decisión definitiva sobre `CreditTerm`.
- Ningún cambio de este ADR afecta el esquema de `CreditTerm`, `AccountsPayable`/`AccountsReceivable` (estructura), ni el motor de generación de CxP de Compras/Gastos (ya conforme al diseño).

## Referencias

- Auditoría de código previa a esta decisión: sesión de arquitectura 2026-09-06 (hallazgos completos en la conversación — `PaymentTerm.cs`, `CreditTerm.cs`, `CompanyBpTradingSettings.cs`, `SupplierRoleConfig.cs`, `PurchaseInvoice.cs`, `ExpenseDocument.cs`, `SalesInvoice.cs`, `AuthorizeSalesUseCases.cs`, `ConfirmPurchaseUseCases.cs`, `AccountsPayable.cs`, `SalesReceivable.cs`, `EnterpriseQueryFilterConfigurator.cs`).
- Relacionado: [ADR-017](./ADR-017-business-partner-scope.md) (scope de Business Partner), [ADR-021](./ADR-021-pricing-engine-ssot.md) (precedente de SSOT + resolver centralizado con patrón similar aplicado aquí a plazos de pago).
