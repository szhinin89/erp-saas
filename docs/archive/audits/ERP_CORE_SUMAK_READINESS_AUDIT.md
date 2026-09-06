# ERP Core — Sumak Readiness Audit

**Tipo de documento:** Auditoría de diagnóstico (Fase 1). No contiene correcciones aplicadas.
**Fecha:** 2026-07-30
**Alcance:** `backend/src/ERP.{Domain,Application,Infrastructure,API}` + `frontend/src/modules` (repo `c:\ProyectCursor\erp-saas`)
**Metodología:** exploración de código en paralelo por 6 agentes de solo lectura + build/test real del backend. Ningún archivo de código fue modificado durante esta auditoría.
**Regla de lectura:** cada hallazgo está marcado como **[HECHO]** (verificado leyendo código/ejecutando comandos), **[INFERENCIA]** (deducción razonable no 100% verificada) o **[RECOMENDACIÓN]** (opinión sobre qué hacer, no un hecho).

> **Nota (2026-09-06):** Documento histórico. La fila "PaymentMethods/PaymentTerms/CreditTerms" (§6, columna "Integrado: Sí") quedó superada por [ADR-033](../../decisions/ADR-033-payment-term-ssot-and-document-schedules.md): `CreditTerm` no tiene ningún caso de uso transaccional conectado. No se reescribe el resto de esta auditoría.

---

## 1. Resumen ejecutivo

El ERP tiene una base arquitectónica sólida y disciplinada: Clean Architecture real (no solo de nombre), CQRS/MediatR consistente, multi-tenant/multi-empresa con query filters fail-closed, infraestructuras transversales (Secuencias Documentales, Entity Tracking, Configuración Tributaria, Auditoría por Dominio, Motor de Precios, Posting Engine contable) diseñadas con cuidado y en su mayoría bien implementadas. El **build compila sin errores** [HECHO] y **1,341 de 1,344 tests no-integración pasan** [HECHO].

Sin embargo, el ERP **no está listo hoy para operar un supermercado real (Sumak)** por bloqueos funcionales concretos, no por debilidad arquitectónica:

1. **Devoluciones de venta y de compra no existen** — ni dominio, ni API, ni UI. Operación diaria de cualquier supermercado. **[P0]**
2. **Cobros (CxC) y Pagos (CxP) están implementados en Application/Domain pero nunca expuestos por API** — el ERP genera deuda de clientes y proveedores pero no puede liquidarla desde la aplicación. **[P0]**
3. **No existe pantalla de caja rápida tipo POS** — la venta se hace vía formulario de factura tradicional. Operable pero no apto para alto volumen de caja de supermercado. **[P1]**
4. **Notas de Crédito/Débito electrónicas SRI sin implementar** — solo Factura tiene builder/provider activo. Relacionado directamente con el punto 1: en Ecuador una devolución de venta formalmente requiere Nota de Crédito ante el SRI. **[P0]**
5. **Condición de carrera real y demostrable en apertura de sesión de caja** — sin índice único que impida dos aperturas simultáneas para la misma caja/usuario. **[P1]**
6. **Reportes gerenciales casi inexistentes** — solo Ventas y Kardex; nada de Compras, CxC/CxP, Caja consolidada o Contabilidad (Balance, Estado de Resultados). **[P1]**
7. **Contabilidad (partida doble) funciona en backend pero sin ninguna UI** — un contador no podría operar con esto hoy. **[P1]**
8. **Un gate de arquitectura CI-bloqueante (`SEQ-GATE-01`) está actualmente en rojo por un falso positivo**, no por una violación real — evidencia de que la gobernanza automática necesita mantenimiento. **[P1]**

Lo que **sí está sólido y operable hoy**: Empresas/Sucursales/Establecimientos SRI (multiempresa/multi-sucursal real), Clientes/Proveedores (BusinessPartner V2), Catálogo de Ítems con precios (Pricing Engine v2), Compras (alta + recepción XML), Ventas con facturación electrónica SRI real y probada contra el ambiente de pruebas del SRI, Inventario/Kardex con bodegas, Caja para ventas de contado, y Acceso/Seguridad (declarado apto para producción).

---

## 2. Estado general

| Dimensión | Estado |
|---|---|
| Arquitectura (Clean Architecture, CQRS, multi-tenant) | ✅ Sólida, consistente, sin violaciones estructurales graves detectadas |
| Build backend | ✅ 0 errores **[HECHO]** |
| Tests backend (no-integración) | ✅ 1,341/1,344 pasan (99.8%) — 3 fallos, ninguno bloqueante hoy, ver §17 **[HECHO]** |
| Infraestructuras CLOSED (Secuencias, Tributaria, Auditoría, Pricing, Posting Engine) | ✅ Implementadas y respetadas en el código de negocio verificado, con una excepción de gate roto (§6, §17) |
| Cobertura funcional para operar un supermercado (venta, compra, inventario, caja) | 🟡 Parcial — el flujo "feliz" (venta/compra sin incidencias) funciona; devoluciones y liquidación de deuda no |
| Preparación para escalar (BD, índices, concurrencia) | 🟡 Mayormente sólida, con 1 hallazgo P1 real (caja) y algunos P2 |
| Trazabilidad/auditoría de negocio | 🟡 Infraestructura excelente, cobertura desigual entre módulos (Items/Pricing/Purchases bien cubiertos; Sales/Finance/Inventory/Caja con huecos) |
| Reportes y UI de consulta (contabilidad, auditoría, CxC/CxP) | 🔴 Insuficiente para operación real |

---

## 3. Inventario de módulos

*(Fuente: agente "inventario de módulos y completitud funcional", verificado por exploración directa de código)*

| Módulo | Existe | Backend | Frontend | Integrado | Auditado | Bloquea Sumak |
|---|---|---|---|---|---|---|
| Company/Empresa/Sucursales/Establecimientos/PE | Sí | Completo | Sí | Sí | Parcial | No |
| Business Partners (Clientes/Proveedores) | Sí | Completo | Sí | Sí | Sí | No |
| Items/Catálogo | Sí | Completo | Sí | Sí (SSOT precio) | Sí | No |
| Pricing (Motor de Precios) | Sí | Completo | Sí | Sí | Sí | No |
| Inventory (Stock/Bodegas/Kardex/Lotes/Series) | Sí | Completo | Sí | Sí (directo) | No confirmado | No |
| Purchases (Compras) | Sí | Completo | Sí | Sí | Sí | No |
| Purchase Reception (Recepción XML) | Sí | Completo | Sí | Sí, con deuda documentada (ADR-028) | Parcial | No |
| Sales (Ventas/Facturas) | Sí | Completo | Sí | Sí | No (ver §9) | No |
| **Devoluciones de Venta** | **No existe** | — | — | — | — | **Sí** |
| **Devoluciones de Compra** | **No existe** | — | — | — | — | **Sí** |
| Notas de Crédito/Débito (Documento Electrónico) | Solo esqueleto (XSD/catálogo) | Sin builder activo | No | No | N/A | Sí |
| Caja (Cash Register/Session) | Sí | Completo | Sí | Sí (evento) | No confirmado | Parcial (sin cobros, sin POS) |
| CxC (Sales Receivable) | Parcial | Solo GET, sin cobro | No hay pantalla de cobros | Parcial | No confirmado | **Sí** |
| CxP (Purchase Payable) | Parcial | Sin controller propio | No | Parcial | No confirmado | **Sí** |
| **Finance/Payments (`RegisterCollectionCommand`/`RegisterPaymentCommand`)** | **Existe pero huérfano** | Application/Domain completos, **0 endpoints** | No | No | No | **Sí, crítico** |
| PaymentMethods/PaymentTerms/CreditTerms | Sí | Completo | Sí | Sí | No confirmado | No |
| Accounting (Contabilidad — partida doble) | Sí, robusto | D/A/API completos (18 endpoints) | **No existe UI** | Sí (eventos) | No confirmado | Sí, si se requiere contabilidad operativa real |
| ElectronicDocuments (Facturación SRI) | Sí, solo Factura | Completo y probado contra SRI real | Sí (Monitor) | Sí | Sí | No (para Factura); Sí (para devoluciones/retenciones) |
| Ride (RIDE) | Sí | Completo | Sí | Sí | No confirmado | No |
| Access/Security/IAM | Sí | Completo | Sí | Sí | Confirmado | No — declarado apto para producción |
| Auth | Sí | Completo | Sí | Sí | No confirmado | No |
| Audit (Entity Audit — infraestructura) | Sí, infraestructura | Completo | **Sin UI de consulta** | Sí | Es la infraestructura misma | Parcial |
| Configuration/OrgConfig | Sí | Completo | Sí | Sí | No confirmado | No |
| SriCatalogs | Sí | Completo | Vía API | Sí | No confirmado | No |
| **Reportes** | Muy incompleto | Solo Kardex tiene endpoints propios | Solo Reporte de Ventas + Kardex | Parcial | N/A | Sí |
| Dashboard | Sí | Completo | Sí | Sí | No confirmado | No |

**Nota de honestidad del agente de origen** [HECHO]: no se contó exhaustivamente el número de casos de test por módulo (solo archivos que los contienen), y no se verificó con 100% de certeza si `AuthorizeSalesUseCases.cs` invoca realmente `IPricingResolver` en el momento de creación de línea — el propio `docs/STATUS.md` admite que esa integración "queda pendiente como trabajo de esos módulos" al cerrar el motor de Pricing v2 (2026-07-05). **Recomendación de verificación puntual adicional** si es crítico para la decisión de negocio.

---

## 4. Funcionalidades existentes (sólidas, verificadas)

- Multiempresa/multi-sucursal real: `Company → Branch → (Establishment → EmissionPoint | Warehouse)`, con query filters fail-closed por `TenantId`/`CompanyId` aplicados automáticamente por reflexión (`EnterpriseQueryFilterConfigurator.cs`). **[HECHO]**
- BusinessPartner V2 (roles Customer/Supplier) como única fuente de verdad, sin entidades legacy paralelas detectadas. **[HECHO]**
- Motor de Pricing v2 (`Item.BaseSalePrice` SSOT + `IPricingResolver`) consumido consistentemente por Sales/Purchases, sin cálculo de precio duplicado detectado. **[HECHO]**
- Infraestructura Tributaria (IVA/ICE): sin violaciones activas de la prohibición de códigos hardcodeados como default transaccional. **[HECHO]**
- Facturación electrónica SRI: núcleo cerrado (ADR-023), probado contra `celcer.sri.gob.ec` con comprobantes reales incluido un rechazo real confirmado — el módulo con mayor evidencia de robustez del repo. **[HECHO, según STATUS.md, no re-verificado en esta auditoría]**
- Posting Engine contable (partida doble): idempotencia real vía `pg_advisory_xact_lock` correctamente anidado en la misma transacción física del `SaveChangesAsync`, sin ventana entre lock y commit. **[HECHO]**
- Kardex de inventario: doble defensa (optimistic concurrency vía `xmin` + índice único físico) con reintento aplicativo ante colisión. **[HECHO]**
- Numeración documental SRI (`DocumentSequence`): sin bypass detectado en código de negocio (`CaptureAndIncrement()` solo se invoca desde la propia entidad de dominio). **[HECHO]**

---

## 5. Funcionalidades incompletas / bloqueantes

Ver detalle completo en §15 (Flujos E2E). Resumen:

| Funcionalidad | Estado | Evidencia |
|---|---|---|
| Devolución de venta | No existe (ni Domain, ni API, ni UI) | Búsqueda exhaustiva sin resultados de `SalesReturn` en código fuente activo **[HECHO]** |
| Devolución de compra | No existe | Igual, `PurchaseReturn` no existe **[HECHO]** |
| Registrar cobro de CxC | Lógica lista, sin endpoint | `RegisterCollectionCommand` implementado en Application, 0 resultados de grep en `ERP.API` **[HECHO]** |
| Registrar pago de CxP | Lógica lista, sin endpoint | `RegisterPaymentCommand` igual, huérfano **[HECHO]** |
| Nota de Crédito/Débito electrónica | XSD/catálogo sí, builder no | Confirmado explícitamente en `docs/STATUS.md` línea 168 (`activeVersion: null`) **[HECHO]** |
| UI de Contabilidad | No existe | 0 resultados buscando términos de plan de cuentas/asientos en frontend **[HECHO]** |
| UI de consulta de Auditoría (Entity Audit) | No existe | 0 carpetas `*audit*` bajo `frontend/src/modules` **[HECHO]** |
| Reportes de Compras/CxC/CxP/Caja/Contabilidad | No existen | Solo `SalesReportPage.tsx` y `KardexPage.tsx` **[HECHO]** |

---

## 6. Bloqueantes Sumak (síntesis)

Ver matriz completa en §19. Los bloqueantes P0 reales para operar Sumak día 1 son:

- **B-01**: Sin devoluciones de venta/compra ni Notas de Crédito/Débito electrónicas.
- **B-02**: Sin forma de cobrar CxC ni pagar CxP desde la aplicación (código huérfano listo para conectar).
- **B-03**: Gate de arquitectura `SEQ-GATE-01` (protege infraestructura FROZEN de Secuencias Documentales) actualmente en rojo por falso positivo — riesgo de gobernanza, no de negocio directo, pero compromete la confianza en el CI (ver §17).

---

## 7. Hardcoding encontrado

*(Fuente: agente "hardcoding de reglas y datos de negocio")*

**Ningún hallazgo P0/P1.** La Infraestructura Tributaria CLOSED se respeta estrictamente — no se encontró ningún `VatCode`/`IceCode` literal fuera de catálogo en código productivo. Hallazgos de menor severidad:

| ID | Archivo:línea | Hallazgo | Prioridad |
|---|---|---|---|
| H1 | `ERP.Application/Modules/Purchases/UseCases/IssueWithholdingUseCases.cs:252` | Código de tipo documental SRI `"07"` (Retención) inyectado literal en `CaptureNextAsync`, sin pasar por `ISriDocTypeCatalogResolver` (patrón que sí se usa en Sales) | P2 |
| H2 | `ERP.Domain/MasterData/ValueObjects/SupplierRoleConfig.cs:34-46` | Whitelist de 8 códigos de método de pago SRI hardcodeada como `HashSet` en vez de derivar de la tabla catálogo `sri_payment_method` | P2 |
| H3 | `SalesInvoice.cs:39,105` + `SalesDraftUseCases.cs:279` | Triplicación literal del código `"01"` sin referenciar `SriSettings.FallbackDocTypeCode` centralizado (mismo valor en los 3 sitios, sin bug funcional hoy) | P3 (informativo) |
| H4 | `TaxIdentification.cs:24-29` + 2 archivos frontend | Códigos de tipo de identificación SRI (`"04"`-`"09"`) duplicados backend/frontend, parcialmente justificado por ser tabla de dispatch de algoritmos de validación | P3 (informativo) |

Verificaciones negativas explícitas (sin hallazgo): métodos de pago default, GUIDs de negocio hardcodeados, nombres de empresa/sucursal en lógica, porcentajes de negocio como literales, límites de negocio hardcodeados — ninguno encontrado en código productivo.

---

## 8. Single Source of Truth

*(Fuente: agente "SSOT duplicado y clasificación de enums")*

| Concepto | Autoridad verificada | Riesgo |
|---|---|---|
| Impuestos (IVA/ICE) | `ISriTaxResolver` + `sri_vat_rates`/`sri_ice_rates` | Bajo — fuente única confirmada, sin segunda lógica en Caja/POS |
| Precios | `IPricingResolver` (`Item.BaseSalePrice` SSOT) | Bajo — confirmado consumido por Sales/Purchases sin cálculo paralelo |
| Stock | `CurrentStock.AvailableQuantity` vía `IStockRepository` | Bajo — un solo cálculo derivado reutilizado |
| Clientes/Proveedores | `BusinessPartner` + `BusinessPartnerRole` | Bajo — sin entidad legacy paralela |
| Empresas/Sucursales/Bodegas | `Company`/`Branch`/`Warehouse`/`Establishment`/`EmissionPoint` | Bajo |
| Numeración documental | `DocumentSequence.CaptureNextAsync` | Bajo (con matiz: `IJournalEntrySequenceRepository` es un mecanismo paralelo pero de dominio distinto — contable interno, legítimamente separado) |
| **Estados de documento** | **Fragmentado — sin autoridad única real** | **Medio** — `ERP.Domain.Common.DocumentStatus` existe como intento de enum genérico pero solo 2 de ~10 agregados lo usan; `SalesInvoiceStatus`/`PurchaseStatus`/`WithholdingStatus` son variantes casi idénticas con nombres distintos para el mismo paso intermedio (Authorized/Confirmed/Issued) |
| Catálogos SRI | Tablas `sri_*` vía `ISriCatalogLookupRepository` | Bajo-Medio — pero **triple representación de "tipo de comprobante SRI"** (`ElectronicDocumentType`, `RideDocumentType` espejo documentado, `PurchaseReceptionSourceDocType`) sin una tabla `sri_document_types` que los unifique |

---

## 9. Enums

*(Fuente: mismo agente, tabla completa entregada — resumen de los clasificados como "REQUIERE REVISIÓN" o "SOSPECHOSO" más relevantes)*

| Enum | Clasificación | Nota |
|---|---|---|
| `ERP.Domain.Common.DocumentStatus` | REQUIERE REVISIÓN | Vestigio de un intento de unificación no completado (solo 2 consumidores) |
| `RoleType` | REQUIERE REVISIÓN | Mismo patrón que motivó la migración `ItemType → ItemTypeDefinition`; 8 valores fijos hoy, candidato si roles de partner deben variar por tenant/vertical |
| `SalesInvoiceStatus` / `PurchaseStatus` / `WithholdingStatus` | SOSPECHOSO | Variantes casi idénticas Draft/X/Cancelled con nombres distintos, sin justificación aparente de la divergencia |
| `ElectronicDocumentType` / `RideDocumentType` | SOSPECHOSO | Espejo intencional documentado (ADR-025) pero exige sincronía manual |
| `PurchaseReceptionSourceDocType` | SOSPECHOSO | Tercera variante del mismo concepto de "tipo de comprobante SRI" |
| `ContactRole` | SOSPECHOSO | Presencia de valor `Other=99` — señal típica de conjunto no percibido como cerrado |
| `IdentificationUsageType` | SOSPECHOSO | Duplica conceptualmente `RoleType` desde otro ángulo — riesgo de divergencia silenciosa |
| `CategoryNodeLevel` | SOSPECHOSO | Jerarquía de árbol con profundidad fija de 3 niveles + "Custom" — limitante si se necesitan jerarquías arbitrarias |
| Frontend: `RoleTypeEnum`/`LocationTypeEnum`/`LocationPurposeEnum`/`ContactRoleEnum` (const objects) | REQUIERE REVISIÓN | Mirror manual de enums C#, sin test de contrato que garantice sincronía |

El resto de enums auditados (contables, de estado técnico de pipelines SRI/inventario/caja/media) están clasificados como **ENUM JUSTIFICADO** — vocabulario técnico cerrado y estable, no candidatos a catálogo persistido.

---

## 10. Capacidades duplicadas

*(Fuente: agente "duplicación de capacidades y acoplamiento")*

| Capacidad | Estado |
|---|---|
| Crear/editar Business Partner | Única implementación — sin duplicación |
| Resolver precio de ítem | Única implementación (`IPricingResolver`) — sin duplicación |
| **Calcular impuestos de línea (IVA/ICE)** | **Duplicación real del algoritmo** — `SalesInvoiceDetail.RecalcTaxes()` usa `SriTaxCalculator.Compute` (única fuente correcta), pero `PurchaseInvoiceDetail.RecalcTaxes()` (`Domain/Modules/Purchases/Entities/PurchaseInvoiceDetail.cs:297-319`) **reimplementa manualmente la misma fórmula** (ICE sobre base, IVA sobre base+ICE, mismo redondeo) en vez de invocar el calculador compartido. Además la orquestación "resolver código→validar→ApplyTaxes" está copiada 3 veces dentro de Purchases. **Riesgo alto**: un ajuste normativo futuro en `SriTaxCalculator` puede desincronizar Purchases silenciosamente. **[Prioridad: Alta / P1]** |
| Mover/ajustar inventario | Única implementación (`IStockRepository.AppendMovementAsync`) — sin escritura directa detectada fuera del repositorio |
| Registrar pago / movimiento de caja | Dos conceptos legítimamente distintos, cada uno con implementación única (tender de pago vs. aplicación de cobro/pago contra CxC/CxP) |
| Emitir documento electrónico | Única implementación (`ElectronicDocumentIssuer` + `InvoiceXmlBuilder`) |
| Validar identificación (cédula/RUC) | Única implementación viva en backend y frontend, pero con **código muerto residual**: `frontend/src/lib/validators/documentValidators.ts` líneas 128-150 contienen una segunda copia de `isValidCedula`/`isValidRuc` dentro de un bloque de comentario mal cerrado — no ejecuta, pero es basura que puede confundir a un futuro editor. **[P3 / higiene]** |

---

## 11. Acoplamiento entre módulos

*(Fuente: mismo agente)*

- **Sin dependencias circulares detectadas** entre módulos de Domain — el grafo de `using` es acíclico.
- Patrón correcto y repetido: Caja y Accounting reaccionan a Sales/Purchases exclusivamente vía **domain events** (`INotificationHandler`), sin invocación directa — el diseño ideal para una eventual extracción a servicio independiente.
- Acoplamiento síncrono real y **intencional por consistencia transaccional**: Sales/Purchases invocan `IStockRepository` (Inventory) directamente dentro de la misma transacción de autorizar/confirmar/cancelar. Correcto para consistencia hoy, pero bloquea una extracción futura sin rediseño a sagas.
- Acoplamientos menores de navegación de dominio (`CashRegister` → `Company`/`Warehouse`, `SalesInvoice` → enum de `Company`) — de bajo esfuerzo para desacoplar si se necesitara.
- **Evaluación por módulo de "¿extraíble a microservicio futuro?"**: Accounting (sí, ya funciona 100% por eventos), ElectronicDocuments (sí, con esfuerzo moderado), MasterData/BusinessPartner (sí, relativamente fácil), Caja (con esfuerzo, por navegación de dominio), Sales/Purchases/Inventory (no fácilmente hoy — acoplamiento síncrono transaccional + duplicación tributaria pendiente de resolver primero).

---

## 12. Base de datos

*(Fuente: agente "base de datos, concurrencia y escalabilidad")*

Hallazgos positivos verificados: concurrencia optimista vía `xmin` uniforme en tablas transaccionales grandes; índices de filtro amplios en `stock_movements` para Kardex de alto volumen; integridad referencial coherente (`Cascade` solo padre→hijo, `Restrict` hacia catálogos); query filters globales fail-closed centralizados por reflexión; `journal_entries`/`document_sequence` con únicos compuestos e idempotencia robusta.

| Hallazgo | Evidencia | Prioridad |
|---|---|---|
| `stock_movements.CreatedAt/UpdatedAt` sin `HasColumnType` explícito → riesgo de mapeo `timestamp` naive en vez de `timestamptz` | `StockMovementConfiguration.cs:72-84`; sin `HasColumnType("timestamptz")` ni switch de legacy timestamp behavior encontrado | P2 |
| `DocumentSequenceRepository.GetForUpdateAsync` expone `SELECT ... FOR UPDATE` sin advisory lock ni transacción propia — vía de bypass potencial del patrón FROZEN si un handler futuro lo usa sin disciplina (sin consumidor activo detectado hoy) | `DocumentSequenceRepository.cs:81-92` | P2 |
| Ambigüedad semántica en algunos `numeric(18,6)`/`numeric(18,4)` que no representan estrictamente "precio unitario"/"cantidad" (p.ej. `ExchangeRate` en `numeric(18,4)`, montos derivados como `DiscountAmount`/`TotalLineCost` en `numeric(18,6)`) — calzan con uno de los 4 patrones permitidos pero la semántica no es literal | `SalesInvoiceConfiguration.cs:84-88`, `PurchaseInvoiceDetailConfiguration.cs:77-107` | P3 |
| Índice simple `TenantId` en `purchase_invoice_details` de bajo valor frente a índices compuestos ya existentes | `PurchaseInvoiceDetailConfiguration.cs:211` | P3 |
| No se auditaron en profundidad `SalesReceivableInstallment`, `PurchasePayableInstallment`, `PurchaseReceptionLine/Document` por límite de tiempo del agente — **declarado explícitamente como no verificado**, no como "sin problema" | — | Pendiente de segunda pasada |

---

## 13. Concurrencia

*(Fuente: mismo agente)*

| Operación | ¿Protegido? | Hallazgo |
|---|---|---|
| Numeración documental SRI | Sí | Sin bypass detectado — verificado |
| Posting contable (idempotencia) | Sí | Advisory lock correctamente anidado en la misma transacción física del `SaveChanges` — patrón correcto verificado |
| Numeración de asiento contable | Sí | Mismo patrón, verificado |
| Movimiento de stock (Kardex) | Sí (razonable) | Optimista + reintento; **no se confirmó** si la validación de "hay stock suficiente" ocurre bajo el mismo lock/retry — riesgo teórico de sobreventa concurrente no descartado, requiere verificación adicional |
| **Apertura de sesión de caja** | **No** | **Condición de carrera real y demostrable**: `OpenCashSessionUseCases.cs:77-129` hace `SELECT` de verificación (sin `FOR UPDATE`) seguido de `INSERT`, sin transacción explícita ni índice único parcial en `cash_sessions` que impida dos filas `Open` para el mismo `cash_register_id`/`user_id`. Dos requests concurrentes (doble clic, o dos usuarios abriendo la misma caja casi simultáneamente) pueden ambos pasar la verificación y terminar con dos sesiones abiertas para la misma caja física. **[P1]** |
| Cierre de sesión de caja | Sí | Optimista vía `xmin`, protegido correctamente contra doble cierre |
| Pagos/recepción de mercadería | No determinado | No auditado en profundidad por límite de tiempo — pendiente de verificación específica |

---

## 14. Histórico/auditoría

*(Fuente: agente "histórico, automatización y flujos E2E")*

Solo **8 entidades** en todo el repo heredan `AuditRecordBase` con handler real: `ElectronicDocumentAudit`, `ElectronicDocumentSriMessage`, `ItemAudit`, `PriceListAudit`, `PriceListItemAudit`, `PricingRuleAudit`, `IssuedWithholdingAudit`, `PurchaseInvoiceAudit`, `PurchaseLinePvpAudit`. **No existe ni una sola carpeta `EventHandlers/` de auditoría en los módulos Sales, Finance, Inventory ni Caja.**

| Hallazgo | Detalle |
|---|---|
| `SalesInvoice` sin Entity Audit | `SalesInvoiceAuthorizedEvent` ya implementa `IAuditEvent` (listo para extender sin tocar nada FROZEN) pero nadie lo escucha para auditoría — solo lo consume Accounting |
| `SalesInvoice.Cancel()` no dispara ningún evento | Asimetría directa contra `PurchaseInvoice.Cancel()`, que sí dispara `PurchaseInvoiceCancelledEvent` con su propio handler de auditoría |
| `Payment` (Finance) sin Entity Audit | Los 4 eventos de aplicación/reversa de pago/cobro ya implementan `IAuditEvent`, listos para conectar, pero solo alimentan Accounting hoy |
| Inventario y Caja no levantan **ningún** domain event | Hueco estructural más profundo que un handler faltante — ni siquiera hay evento que capturar; solo `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy` (last-touch, sin historial de valores) |
| `ItemVariantAddedEvent`/`ItemVariantDisabledEvent` se disparan pero sin `case` en `ItemAuditHandler` | Deuda ya documentada explícitamente en `docs/STATUS.md` |
| Process Audit (jobs Hangfire, recálculos masivos) | Diseñado en `AI-RULES/AUDIT-INFRASTRUCTURE.md` §4, **cero implementación real** — solo `ILogger` simple en los 4 jobs Hangfire existentes (`process-outbox`, `masterdata-reconciliation`, `electronic-document-retry`, `expire-user-sessions`) |

---

## 15. Automatización potencial

*(Fuente: mismo agente — solo observaciones, sin implementar)*

| Ya resuelto (referencia positiva) | Pendiente de resolver |
|---|---|
| Naturaleza jurídica desde RUC/CI (commit reciente `f9a4f86b`) | **Bodega por defecto en venta**: el backend devuelve explícitamente `DefaultWarehouseId: null` con comentario propio admitiendo que "no hay contexto de sucursal en esta query"; la cascada real vive solo en frontend (`useSalesPage.ts:422-429`) — lógica de negocio duplicada en React en vez de centralizada en backend |
| Punto de emisión por defecto en ventas (`EmissionPoint.IsDefault`) | — |
| Forma de pago/condición de pago por defecto (`OrgSettingKeys.Invoice.*`) | — |
| Vencimientos de cuotas CxC/CxP (`CreditInstallment.DaysOffset`) | — |
| Cliente por defecto de caja (`sessionData.defaultCustomerId`) | — |

---

## 16. Flujos end-to-end

*(Fuente: síntesis de los agentes de inventario y de histórico/eventos)*

### COMPRA: Proveedor → Compra → Recepción → Inventario → CxP → Contabilidad
Funciona hasta Inventario. **Se rompe en CxP**: `PurchaseInvoice.GeneratePaymentSchedule()` genera `PurchasePayable`/`PurchasePayableInstallment`, pero no hay controller que exponga consulta ni pago — `RegisterPaymentCommand` (Finance) que liquidaría la deuda no tiene endpoint. La deuda se genera pero nunca se puede pagar desde la aplicación. Contabilidad sí funciona (asiento automático al confirmar), pero sin UI de consulta. Nota adicional: no existe "Orden de Compra" separada de la Factura — el flujo va directo a factura de compra; la recepción solo cubre el caso de XML de factura electrónica del proveedor, no una recepción física manual independiente.

### VENTA: Producto → Precio → Venta/POS → Impuestos → Caja/CxC → Inventario → Documento electrónico → Contabilidad
Funciona para venta de contado con Factura electrónica. **Se rompe en CxC** para venta a crédito: se genera `SalesReceivable` (deuda) pero no hay forma de registrar el cobro posterior — el propio `docs/STATUS.md` lo admite: "Sales Receivable... sin cobros". No existe pantalla POS dedicada (carrito táctil/escaneo rápido); la venta usa un formulario de factura tradicional (`SalesPage.tsx`, 1372 líneas). Punto abierto no verificado con certeza: si `AuthorizeSalesUseCases.cs` invoca realmente `IPricingResolver` en tiempo real de línea (recomendado verificar puntualmente).

### DEVOLUCIÓN DE VENTA: Venta → Devolución → Inventario → Caja/CxC → Documento → Contabilidad
**Se rompe en el primer paso.** No existe entidad `SalesReturn`, ni comando, ni evento, ni UI. `SalesInvoice.cs` solo soporta `Cancel()` (anulación total de un Draft, no devolución parcial de mercadería ya entregada/facturada). Sin Nota de Crédito electrónica activa. **Flujo completamente inexistente.**

### DEVOLUCIÓN DE COMPRA: Compra → Devolución → Inventario → CxP → Documento → Contabilidad
**Se rompe en el primer paso**, mismo patrón — no existe `PurchaseReturn`, `PurchaseInvoice.cs` solo soporta `Cancel()`. Sin Nota de Débito activa. **Flujo completamente inexistente.**

---

## 17. Evidencia de tests y build (ejecutado en esta auditoría)

**[HECHO]** — comandos ejecutados: `dotnet build backend/src/ERP.slnx` y `dotnet test backend/src/ERP.slnx --filter "FullyQualifiedName!~Integration"` (se excluyeron tests de integración que requieren PostgreSQL real vía Testcontainers, no disponibles en este entorno de auditoría).

- **Build**: 0 errores.
- `ERP.Domain.Tests`: **385/385 pasan**.
- `ERP.Application.Tests`: **529/531 pasan** — 2 fallos:
  - `ItemMatchFinderTests.Normalized_description_equality_upgrades_the_similarity_score` — falla con "Expected candidates to contain a single item, but the collection is empty". **[INFERENCIA]** consistente con una prueba de scoring por similitud `pg_trgm` que depende de comportamiento real de PostgreSQL no reproducible en el entorno de test usado; no se determinó la causa raíz exacta dentro del alcance de esta auditoría.
  - `ItemMatchFinderTests.Plain_similarity_candidates_use_the_pg_trgm_score` — mismo patrón.
- `ERP.API.Tests`: **137/137 pasan**.
- `ERP.Architecture.Tests`: **97/97 pasan**.
- `ERP.Infrastructure.Tests`: **193/194 pasan** — 1 fallo:
  - `DocumentSequenceExclusivityTests.SEQ_GATE_01_CaptureAndIncrement_is_never_called_outside_domain_entity` — **[HECHO, causa raíz confirmada]**: el gate escanea todo el árbol de código en busca del texto literal `.CaptureAndIncrement(` fuera de una lista blanca de archivos. `backend/src/ERP.Domain/Modules/Accounting/Entities/JournalEntrySequence.cs:9` contiene un comentario de documentación XML que menciona textualmente `DocumentSequence.CaptureAndIncrement()` como referencia comparativa de diseño (módulo Accounting, ADR-026, no relacionado con Secuencias Documentales). El scan de texto plano del gate no distingue comentarios de invocaciones reales de código, por lo que el gate falla en falso positivo. **No hay ninguna violación real del patrón FROZEN de Secuencias Documentales** — se confirmó por grep exhaustivo que no existe ningún caller real de `.CaptureAndIncrement()` en `ERP.Application` ni en ningún módulo fuera de `DocumentSequence.cs` (la propia entidad) y `JournalEntrySequence.cs` (comentario, no invocación).

**Implicación de gobernanza** (no un bug de negocio, pero sí un hallazgo de proceso): un gate CI-bloqueante que protege una infraestructura declarada INMUTABLE está actualmente en rojo en el pipeline de tests, lo que en la práctica puede llevar a los desarrolladores a acostumbrarse a "tests rojos conocidos" e ignorar una futura violación real que aparezca junto a este falso positivo.

---

## 18. Riesgos

| Riesgo | Naturaleza | Relacionado con |
|---|---|---|
| Sobreventa por condición de carrera en apertura de caja | Operativo/financiero | §13 |
| Desincronización silenciosa de cálculo tributario en Compras si se ajusta `SriTaxCalculator` sin actualizar `PurchaseInvoiceDetail.RecalcTaxes()` | Normativo/fiscal | §10 |
| Imposibilidad de cobrar/pagar deuda generada por el propio sistema | Financiero/operativo | §5, §16 |
| Ausencia de devoluciones formales (fiscal y de inventario) | Normativo/operativo | §5, §16 |
| Gate CI-bloqueante en falso positivo permanente puede ocultar una violación real futura | Gobernanza/proceso | §17 |
| Falta de UI de auditoría/contabilidad impide operación real por personal no técnico (contador, auditor) | Operativo | §14 |
| Columnas `timestamp` posiblemente naive en `stock_movements` | Técnico, latente | §12 |

---

## 19. Matriz P0/P1/P2/P3

### P0 — Bloqueante (impide operar Sumak o riesgo de integridad fiscal/contable)

| ID | Hallazgo | Módulo |
|---|---|---|
| P0-01 | Devoluciones de venta y de compra no existen (dominio/API/UI) | Sales, Purchases |
| P0-02 | Notas de Crédito/Débito electrónicas SRI sin implementar | ElectronicDocuments |
| P0-03 | `RegisterCollectionCommand`/`RegisterPaymentCommand` sin endpoint — CxC/CxP no se pueden liquidar | Finance |

### P1 — Crítico (debe corregirse antes de declarar Core Ready)

| ID | Hallazgo | Módulo |
|---|---|---|
| P1-01 | Condición de carrera real en apertura de sesión de caja (sin único parcial) | Caja |
| P1-02 | Duplicación de algoritmo tributario entre `SalesInvoiceDetail` y `PurchaseInvoiceDetail` | Sales/Purchases |
| P1-03 | Sin pantalla tipo POS para caja de alto volumen | Sales/Caja |
| P1-04 | Sin reportes de Compras/CxC/CxP/Caja/Contabilidad | Reportes |
| P1-05 | Sin UI de Contabilidad (plan de cuentas, asientos, balance) | Accounting |
| P1-06 | Gate `SEQ-GATE-01` en falso positivo permanente (riesgo de gobernanza) | Infraestructura CI |
| P1-07 | Sin Entity Audit en Sales/Finance/Inventory/Caja pese a eventos ya tipados como `IAuditEvent` en Sales/Finance | Sales, Finance |
| P1-08 | `SalesInvoice.Cancel()` no dispara evento (asimetría con Purchases) | Sales |

### P2 — Importante (mejorar, no bloquea la primera operación)

| ID | Hallazgo | Módulo |
|---|---|---|
| P2-01 | Código "07" de retención sin pasar por resolver de catálogo SRI | Purchases |
| P2-02 | Whitelist de payment methods duplicada en `SupplierRoleConfig` | MasterData |
| P2-03 | `stock_movements` timestamps sin mapeo explícito `timestamptz` | Inventory |
| P2-04 | `DocumentSequenceRepository.GetForUpdateAsync` como vía de bypass potencial | Company |
| P2-05 | Validación de stock negativo bajo concurrencia no confirmada | Inventory |
| P2-06 | `PurchasePayableInstallment`/`PurchaseReceptionLine` no auditados en profundidad (concurrencia) | Purchases |
| P2-07 | Sin UI de consulta de Entity Audit | Audit |
| P2-08 | Recepción de mercadería solo cubre XML de factura electrónica, sin recepción física manual independiente | Purchases |
| P2-09 | Bodega por defecto en venta resuelta en frontend en vez de backend (`DefaultWarehouseId: null` con cascada duplicada en React) | Sales |
| P2-10 | Fragmentación de enums de estado de documento (`DocumentStatus` vestigial) | Domain (transversal) |
| P2-11 | Triple representación de "tipo de comprobante SRI" sin tabla unificadora | ElectronicDocuments/Ride/Purchases |
| P2-12 | `RoleType` vs `IdentificationUsageType` — mismo concepto desde dos enums | MasterData/SriCatalogs |

### P3 — Futuro (mejora válida, fuera del alcance de Sumak Ready inicial)

| ID | Hallazgo |
|---|---|
| P3-01 | Triplicación literal del código `"01"` sin referenciar constante central |
| P3-02 | Códigos `sri_id_type` duplicados backend/frontend |
| P3-03 | Código muerto residual en `documentValidators.ts` (líneas 128-150) |
| P3-04 | Ambigüedad semántica en algunos `numeric(18,4)/(18,6)` que no son literalmente "cantidad"/"precio unitario" |
| P3-05 | Índice simple `TenantId` de bajo valor en `purchase_invoice_details` |
| P3-06 | Process Audit sin implementar (diseñado, no construido) |
| P3-07 | `CategoryNodeLevel` con profundidad fija de 3 niveles — posible limitante futuro |
| P3-08 | `ItemVariantAddedEvent`/`ItemVariantDisabledEvent` sin `case` en `ItemAuditHandler` (deuda ya documentada) |

---

## 20. Hallazgos fuera de alcance

- Deuda cosmética de naming `subscriber`→`tenant` en variables backend, ya documentada como no bloqueante en `docs/STATUS.md`.
- `HttpAuditContext.Actor.Source` hardcodeado a `UserAction` (falta contexto para jobs/sistema) — ya documentado como deuda conocida en CLAUDE.md.
- `CorrelationId`/`RequestId` sin truncado antes de persistir en `varchar(100)` — ya documentado.
- Deuda técnica de `ElectronicDocuments` ya aceptada explícitamente en ADR-023 (Monitor acoplado a Sales, contraseñas de certificado en texto plano, `AVG` en memoria, `GetRetryCandidatesAsync` sin paginación).
- No se evaluó en esta auditoría el frontend de forma exhaustiva más allá de existencia/tamaño de páginas — no se ejecutó la aplicación en navegador ni se probaron flujos de UI reales.
- No se evaluaron tests de integración (requieren PostgreSQL real vía Testcontainers, no disponible en el entorno de esta auditoría) — se ejecutaron únicamente tests unitarios/arquitectura/API in-memory.
- No se auditó `ERP.AI.Application`/`ERP.AI.Infrastructure` ni `Platform.Contracts` (fuera del alcance funcional de Sumak como supermercado).
- No se auditó `qa-bot`/`qa-engine` en la raíz del repo.

---

## 21. Plan recomendado de corrección

**[RECOMENDACIÓN — no ejecutada, sujeta a validación del usuario antes de iniciar cualquier implementación]**

1. **Cerrar P0 primero, en este orden**: (a) exponer `RegisterCollectionCommand`/`RegisterPaymentCommand` vía API + UI mínima de cobros/pagos — es el menor esfuerzo relativo porque la lógica de dominio ya existe y está probada; (b) diseñar e implementar Devolución de Venta con su Nota de Crédito electrónica (mayor esfuerzo, requiere nuevo agregado + builder XML SRI); (c) Devolución de Compra con Nota de Débito, replicando el patrón anterior.
2. **P1-01 (condición de carrera de caja)**: agregar índice único parcial en `cash_sessions` — cambio de bajo riesgo y alto impacto, candidato a resolverse independientemente y pronto.
3. **P1-02 (duplicación tributaria Compras/Ventas)**: mover `SriTaxCalculator` a un namespace compartido y hacer que `PurchaseInvoiceDetail.RecalcTaxes()` lo invoque — cambio acotado, reduce riesgo normativo.
4. **P1-06 (gate SEQ-GATE-01)**: corregir el mecanismo de escaneo del gate para ignorar comentarios/XML doc, o renombrar el método en `JournalEntrySequence` para no colisionar textualmente — restaura la confianza en el CI.
5. **UI de Contabilidad y Reportes (P1-04/P1-05)**: evaluarlos como iniciativa de producto separada, priorizada según necesidad real de Sumak (¿lleva contabilidad en el ERP o en paralelo?).
6. **Entity Audit en Sales/Finance (P1-07/P1-08)**: extensión aditiva de bajo riesgo (la infraestructura ya soporta esto sin tocar nada FROZEN) — buen candidato de "quick win" después de los P0.
7. Los hallazgos P2/P3 se recomiendan agrupar en un backlog de mantenimiento técnico, sin bloquear el lanzamiento operativo de Sumak.

Toda implementación derivada de este informe requiere, como establece el propio proceso de este ERP, pasar por el flujo jerárquico normal (`AI-RULES/CORE-ARCHITECTURE.md`) y — para cualquier infraestructura declarada CLOSED — el proceso formal de ADR antes de tocar código.

---

*Fin del informe. Ningún archivo de código fue modificado durante esta auditoría.*
