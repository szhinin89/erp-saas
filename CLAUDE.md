# ERP SaaS ZH Technologies — Guía para Claude Code

Onboarding rápido. Reglas normativas de implementación: [`AI-RULES/`](AI-RULES/README.md).

---

## Jerarquía documental

El repositorio tiene **una única jerarquía de 4 niveles**. Ante cualquier contradicción entre niveles, **prevalece siempre el nivel más bajo numéricamente** (Nivel 1 > Nivel 2 > Nivel 3 > Nivel 4).

### Nivel 1 — Fuente de verdad del producto (qué es el ERP, qué estado tiene)
- [`README.md`](README.md)
- [`CLAUDE.md`](CLAUDE.md) (este archivo)
- [`docs/STATUS.md`](docs/STATUS.md)
- [`FEATURES.md`](FEATURES.md)
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- [`ERP_CORE_FREEZE.md`](ERP_CORE_FREEZE.md) — acta de congelamiento: módulos incluidos/excluidos, frontera de integración, reglas obligatorias *ERP never depends on Platform* / *Platform may consume ERP APIs only*

Define: visión de producto, estado de delivery, módulos, arquitectura vigente. **Es la referencia que prevalece sobre cualquier otro documento del repositorio.**

### Nivel 2 — Reglas normativas (cómo se construye)
- [`AI-RULES/**`](AI-RULES/README.md)

Define: reglas de implementación, convenciones, gates bloqueantes para PR/CI/agentes. Vinculante para *cómo* se construye, subordinado a Nivel 1 en *qué* es el producto.

### Nivel 3 — Documentación técnica especializada (detalle de un dominio)
- [`docs/IDENTITY.md`](docs/IDENTITY.md)
- [`docs/DATABASE.md`](docs/DATABASE.md)
- [`docs/security/**`](docs/security/)

Profundiza un tema puntual referenciado desde Nivel 1/2. Si contradice a Nivel 1 o 2, se considera desactualizado y debe corregirse para alinearse — nunca al revés.

### Nivel 4 — Históricos (snapshots, no usar para decidir)
- [`docs/archive/**`](docs/archive/)

Documentación congelada: releases pasadas, auditorías de limpieza ya ejecutadas, planes de ejecución completados. **No debe usarse para** implementar funcionalidades, tomar decisiones arquitectónicas, definir comportamiento, reglas de negocio, contratos, seguridad ni el modelo multiempresa. Solo tiene valor de registro/bitácora.

---

## Source of Truth (detalle Nivel 2 — AI-RULES)

| Tema | Canónico |
|------|----------|
| Índice y anti-drift | [AI-RULES/README.md](AI-RULES/README.md) |
| Precedencia | [AI-RULES/HIERARCHY.md](AI-RULES/HIERARCHY.md) |
| Multi-agente | [AI-RULES/AGENT-COMPATIBILITY.md](AI-RULES/AGENT-COMPATIBILITY.md) |
| Arquitectura core | [AI-RULES/CORE-ARCHITECTURE.md](AI-RULES/CORE-ARCHITECTURE.md) |
| Backend | [AI-RULES/BACKEND-RULES.md](AI-RULES/BACKEND-RULES.md) |
| Frontend | [AI-RULES/FRONTEND-RULES.md](AI-RULES/FRONTEND-RULES.md) |
| SaaS | [AI-RULES/SAAS-RULES.md](AI-RULES/SAAS-RULES.md) |
| Seguridad / auth | [AI-RULES/SECURITY.md](AI-RULES/SECURITY.md) |
| Stack permitido | [AI-RULES/STACK.md](AI-RULES/STACK.md) → [docs/DEVELOPMENT.md#stack-oficial](docs/DEVELOPMENT.md#stack-oficial) |
| Naming | [AI-RULES/NAMING.md](AI-RULES/NAMING.md) |
| Enforcement / 4 capas | [AI-RULES/ENFORCEMENT.md](AI-RULES/ENFORCEMENT.md) |
| PR bloqueante (B-xx/F-xx) | [AI-RULES/PR-RULES-CATALOG.md](AI-RULES/PR-RULES-CATALOG.md) |
| Validación de formularios | [CLAUDE.md#estándar-de-validación-de-formularios](CLAUDE.md#estándar-de-validación-de-formularios) |
| Mensajes visuales | [AI-RULES/VISUAL-MESSAGES.md](AI-RULES/VISUAL-MESSAGES.md) |
| Manejo de errores (Backend↔Frontend) | [AI-RULES/ERROR-HANDLING.md](AI-RULES/ERROR-HANDLING.md) |
| Modales | [AI-RULES/MODAL-STANDARD.md](AI-RULES/MODAL-STANDARD.md) |
| Auditoría (Entity Audit / Process Audit) | [AI-RULES/AUDIT-INFRASTRUCTURE.md](AI-RULES/AUDIT-INFRASTRUCTURE.md) |
| Branch Ownership (TenantId/CompanyId/BranchId en aggregates operativos) | [AI-RULES/CORE-ARCHITECTURE.md#branch-ownership-rule-obligatoria](AI-RULES/CORE-ARCHITECTURE.md#branch-ownership-rule-obligatoria) |

---

## Contexto del repo (no reglas)

| Necesidad | Documento | Nivel |
|-----------|-----------|-------|
| Índice maestro | [CONTEXT.md](CONTEXT.md) | — (índice) |
| Estado de delivery | [docs/STATUS.md](docs/STATUS.md) | 1 |
| Arranque local, Docker, tests | [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | 3 |
| Arquitectura vigente | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 1 |
| Auth / First Run | [docs/IDENTITY.md](docs/IDENTITY.md) | 3 |
| Módulos del producto | [FEATURES.md](FEATURES.md) | 1 |

---

## Antes de actuar

1. Verificar si el archivo **ya existe** → editar, no regenerar.
2. Seguir [flujo jerárquico](AI-RULES/CORE-ARCHITECTURE.md#flujo-jerárquico-implementar-una-feature).
3. **No inventar reglas** fuera de `AI-RULES/*` sin confirmación del usuario.

---

## Al terminar una tarea

Actualizar docs de avance → [AI-RULES/ENFORCEMENT.md#sincronización-docs-de-avance](AI-RULES/ENFORCEMENT.md#sincronización-docs-de-avance).

## Hardening multiempresa (resumen)

- Scope explícito en MediatR: `ICompanyScopedRequest` / `ISubscriberScopedRequest` / `IPlatformScopedRequest`
- Concurrencia PG: `IDatabaseExceptionTranslator` → nunca 500 por UNIQUE
- Métricas: `docs/observability/METRICS.md` · Seguridad: `docs/security/MULTI-TENANT-HARDENING.md`

---

## Estándar de Validación de Formularios

Todo formulario del ERP implementa validación en dos niveles. El incumplimiento es desviación de arquitectura y bloquea la aprobación del módulo.

### 1. Validación local (Frontend)

- **React Hook Form** para el estado del formulario.
- **Zod** como fuente de reglas de validación de interfaz.
- Mensajes en español, orientados a corregir: `"El RUC debe tener 13 dígitos."` — no `"Error de validación."`.
- Objetivo: retroalimentación inmediata antes de hacer la petición HTTP.

### 2. Validación del servidor (Backend)

- **FluentValidation** es la fuente de verdad de las reglas de negocio.
- `ExceptionMiddleware` transforma `ValidationException` → HTTP 422 con estructura de campo.
- Nombres de propiedad en **camelCase** en la respuesta.

Contrato de respuesta 422:

```json
{
  "data": {
    "errors": {
      "taxIdentificationNumber": ["El RUC debe tener 13 dígitos."]
    }
  }
}
```

### 3. Mecanismo estándar para errores HTTP 422 (Frontend)

```ts
applyServerErrors<T>(error, setError)
```

Importar desde `modules/lib/validationErrors.ts`. Está **prohibido**:

- Parsear manualmente strings de error dentro de páginas o componentes.
- Crear condicionales del tipo `if (field === "email") setError(...)`.
- Depender del formato concatenado anterior `"Campo: Mensaje"`.

### 4. Responsabilidades por capa

| Capa | Responsabilidad |
|------|----------------|
| Frontend — Zod | Reglas de formato e interfaz; mensajes inmediatos |
| Frontend — RHF | Estado del formulario; muestra errores bajo el campo |
| Frontend — `applyServerErrors` | Mapea errores 422 estructurados a campos RHF |
| Backend — FluentValidation | Reglas de negocio; fuente de verdad |
| Backend — `ExceptionMiddleware` | Serializa `ValidationException` → 422 con mapa campo→mensajes |

### 5. Architecture Gate — Criterios de cierre de módulo

Un módulo **no puede considerarse cerrado** si incumple cualquiera de los siguientes puntos. La presencia de un incumplimiento es un **FAIL de arquitectura** y debe corregirse antes de aprobar el módulo.

#### Frontend

| # | Criterio | Estado |
|---|----------|--------|
| F-V1 | El formulario usa React Hook Form como motor | ✅ obligatorio |
| F-V2 | Existe un schema Zod para todas las validaciones de interfaz | ✅ obligatorio |
| F-V3 | Los errores se muestran debajo del campo correspondiente | ✅ obligatorio |
| F-V4 | Los valores ingresados se conservan cuando hay errores | ✅ obligatorio |
| F-V5 | Los errores HTTP 422 se mapean exclusivamente con `applyServerErrors<T>()` de `modules/lib/validationErrors.ts` | ✅ obligatorio |
| F-V6 | No existe `setError()` manual para interpretar errores del API | ❌ prohibido |
| F-V7 | No existe parseo de strings concatenados `"Campo: Mensaje"` | ❌ prohibido |
| F-V8 | No existen mensajes genéricos como `"Error de validación"`, `"Campo inválido"` o `"Dato incorrecto"` | ❌ prohibido |

#### Backend

| # | Criterio | Estado |
|---|----------|--------|
| B-V1 | Toda regla de negocio existe en FluentValidation | ✅ obligatorio |
| B-V2 | `ValidationException` → HTTP 422 via `ExceptionMiddleware` | ✅ obligatorio |
| B-V3 | La respuesta mantiene el mapa `campo → lista de mensajes` (camelCase) | ✅ obligatorio |
| B-V4 | No se devuelven errores de validación como texto plano | ❌ prohibido |
| B-V5 | No se exponen excepciones técnicas al usuario | ❌ prohibido |

---

## Convenciones esenciales (resumen — detalle en canónico)

- Capas: `ERP.API → Application → Domain ← Infrastructure`
- Validación 4 capas para datos persistidos
- Soft delete; factories `Create(...)`; sin AutoMapper
- Frontend: módulos `modules/{dominio}/`, ZH Form, i18n es/en/qu
- SaaS: IDs tenant en `sessionStorage` (`erp.saas.*`), no en URL
- Stack: solo herramientas en `docs/DEVELOPMENT.md#stack-oficial`

- Mensajes visuales: API pública `import { message, MSG } from 'lib/messages'`. Tipos: success/error/warning/info/confirm. Store interno encapsulado — nunca importar `_internal/`. Ver `AI-RULES/VISUAL-MESSAGES.md`

**NO duplicar reglas aquí.** Editar siempre el archivo canónico en `AI-RULES/`.

---

## Estándar de Precisión Numérica (INMUTABLE)

Decisión arquitectónica congelada 2026-06-25. No modificar sin revisión arquitectónica formal.

### PostgreSQL — Precisiones oficiales

| Tipo | Precision | Aplica a |
|------|-----------|----------|
| Montos/totales | `numeric(18,2)` | Subtotales, impuestos, grand total, pagos, CxC, CxP, asientos |
| Cantidades | `numeric(18,4)` | Stock, qty líneas, movimientos, tipo de cambio |
| Precios unitarios | `numeric(18,6)` | UnitPrice, LandedCost, DiscountAmount, costo promedio |
| Porcentajes | `numeric(5,2)` | IVA, ICE, descuento %, retención %, margen % |

### Frontend

- **Input obligatorio**: `ZhDecimalInput` para todo decimal, `ZhNumberInput` para enteros
- **Separador**: solo punto (`.`) — coma prohibida
- **Utilities**: `sanitizeDecimal()`, `parseDecimal()`, `formatMoney()` de `lib/sanitizers.ts`
- **Decimales configurables**: `getDecimalConfig()` carga desde `GET /api/v1/config/decimals` por empresa

### Backend

- **Domain**: solo `decimal`/`int`/`long` — prohibido string monetario
- **Infrastructure**: `CultureInfo.InvariantCulture` obligatorio en todo parsing
- **API**: JSON numbers nativos — prohibido strings numéricos

### Gate para nuevas columnas decimales

Cualquier nueva columna decimal debe justificar antes de implementar:

1. **Tipo de dato** (monto, cantidad, precio, porcentaje)
2. **Precisión** (18 o 5)
3. **Escala** (2, 4 o 6)
4. **Motivo de negocio**

Si no coincide con `numeric(18,2)`, `numeric(18,4)`, `numeric(18,6)` o `numeric(5,2)` → requiere revisión arquitectónica formal.

### Prohibido en todo el sistema

- `toLocaleString()` / `Intl.NumberFormat()` para montos
- `<input type="number">` para campos decimales
- `decimal.Parse` sin `InvariantCulture`
- `Convert.ToDecimal` para datos financieros
- Crear columnas decimales sin justificar tipo/precisión/escala/motivo

---

## Estándar de Fechas y Horas (INMUTABLE)

Decisión arquitectónica congelada 2026-06-25.

### Visualización (frontend)

| Contexto | Formato | Función |
|----------|---------|---------|
| Fecha | `dd/MM/yyyy` | `formatDate()` |
| Fecha + hora | `dd/MM/yyyy HH:mm` | `formatDateTime()` |
| Auditoría | `dd/MM/yyyy HH:mm:ss` | `formatDateTimeSeconds()` |
| Fecha ISO para inputs | `yyyy-MM-dd` | `todayIso()` |

Fuente única: `lib/formatters/dateFormatters.ts`. Usa `getUTC*()` para evitar desfase por timezone del navegador.

### Backend

- Persistencia: `DateTime.UtcNow` siempre — nunca `DateTime.Now`
- API: ISO 8601 (`2026-06-25T19:35:42Z`)
- Fechas sin hora: `DateOnly` → PostgreSQL `date`
- Timestamps: `DateTime` → PostgreSQL `timestamptz`

### Prohibido

- `toLocaleDateString()` / `toLocaleString()` para fechas de negocio
- `new Date(iso).toLocaleString('es-EC')` — desfase por timezone
- `DateTime.Now` en backend (usar `DateTime.UtcNow`)
- Hardcodear locale en formateo de fechas financieras

---

## Infraestructuras CLOSED — Regla General de Gobernanza (INMUTABLE)

> Toda infraestructura clasificada como **CLOSED** forma parte de la **Baseline Arquitectónica del ERP**. Ningún cambio funcional podrá modificar su comportamiento sin un nuevo ADR, evidencia técnica, pruebas automatizadas y revisión de compatibilidad hacia atrás.

Esta regla rige a **todas** las infraestructuras transversales declaradas CLOSED en `docs/STATUS.md` (tabla "Módulos FROZEN"), incluyendo —sin limitarse a— Mensajes Visuales (ADR-018), Secuencias Documentales (ADR-019) y Entity Tracking / Change Tracking (ADR-020). Aplica también a toda infraestructura futura que se declare CLOSED bajo el mismo proceso.

Implicaciones:

- **Ningún módulo de negocio** puede alterar, sortear o reimplementar el comportamiento de una infraestructura CLOSED para resolver una necesidad puntual.
- Un cambio de comportamiento solo es válido si viene acompañado de: (1) una nueva ADR que documente contexto, alternativas y decisión; (2) evidencia técnica (tests, métricas, validación end-to-end); (3) pruebas automatizadas que cubran el nuevo comportamiento; (4) revisión explícita de compatibilidad hacia atrás con los consumidores existentes.
- Los gates CI-bloqueantes asociados a cada infraestructura CLOSED (p. ej. `SEQ-GATE-01..04`, `ATT-GATE-01`) son el mecanismo de cumplimiento automático de esta regla — no deben relajarse ni desactivarse para acomodar una excepción puntual.
- Un agente o desarrollador que detecte una necesidad de cambio sobre una infraestructura CLOSED debe tratarlo como una decisión arquitectónica formal, no como una corrección de código ordinaria.

---

## Infraestructura CLOSED — Secuencias Documentales (INMUTABLE)

Decisión arquitectónica congelada 2026-06-29. ADR: [`docs/adr/ADR-019-document-sequence-infrastructure.md`](docs/adr/ADR-019-document-sequence-infrastructure.md).

Infraestructura transversal del ERP. Asigna numeración SRI (`000000001`…`999999999`) por punto de emisión y tipo de comprobante bajo concurrencia garantizada.

### API pública congelada

La **única operación autorizada** para obtener un consecutivo documental es:

```csharp
IDocumentSequenceRepository.CaptureNextAsync(tenantId, companyId, emissionPointId, docTypeCode, ct)
```

Esta llamada es atómica (advisory lock + transacción propia), concurrentemente segura y crea la fila on-demand si no existe.

### Prohibido en todo el sistema

- Llamar directamente a `DocumentSequence.CaptureAndIncrement()` desde cualquier handler o servicio.
- Leer `CurrentSeq` y luego escribirlo fuera del repositorio oficial.
- Emitir SQL raw de escritura sobre `document_sequence` fuera de `DocumentSequenceRepository`.
- Llamar a `IDocumentSequenceRepository.GetForUpdateAsync()` desde capa Application (patrón obsoleto).
- Implementar lógica propia de numeración en cualquier módulo del ERP.
- Resetear o decrementar `current_seq` directamente en la BD.

### Reglas de evolución

- Todo nuevo tipo documental SRI se incorpora sin cambio de modelo: `CaptureNextAsync(…, "04", …)`.
- Cualquier cambio en estrategia de concurrencia requiere nueva ADR + repetir suite de pruebas con PostgreSQL real.
- Los 4 architecture gates CI-bloqueantes (`SEQ-GATE-01..04` en `ERP.Infrastructure.Tests`) garantizan que ningún módulo viole esta regla automáticamente.

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `DocumentSequence` (entidad) | `ERP.Domain/Modules/Company/Entities/` | FROZEN |
| `IDocumentSequenceRepository` (interfaz) | `ERP.Domain/Modules/Company/Interfaces/` | FROZEN |
| `DocumentSequenceRepository` (impl.) | `ERP.Infrastructure/Persistence/Repositories/` | FROZEN |
| `DocumentSequenceConfiguration` (EF) | `ERP.Infrastructure/Persistence/Configurations/Company/` | FROZEN |
| Gates CI | `ERP.Infrastructure.Tests/Persistence/DocumentSequenceExclusivityTests.cs` | FROZEN |
| Suite concurrente | `ERP.API.Tests/Integration/DocumentSequenceConcurrencyTests.cs` | FROZEN |

---

## Infraestructura CLOSED — Entity Tracking / Change Tracking (INMUTABLE)

Decisión arquitectónica congelada 2026-06-30. ADR: [`docs/adr/ADR-020-entity-tracking-infrastructure.md`](docs/adr/ADR-020-entity-tracking-infrastructure.md).

Infraestructura transversal del ERP. Corrige automáticamente una clasificación errónea de EF Core: una entidad hija **nueva**, con clave generada por dominio (`Guid.NewGuid()` en factory `Create()`), agregada a la colección de navegación de un agregado **ya trackeado** (p. ej. desde un domain event handler, entre dos `SaveChangesAsync`), es descubierta recién por `DetectChanges()` y queda mal clasificada como `Modified` con `OriginalValue == CurrentValue` en todas sus propiedades. El `UPDATE` no-op resultante afecta 0 filas → `DbUpdateConcurrencyException`.

### Propósito

Garantizar que cualquier módulo del ERP que agregue hijos nuevos a un agregado ya trackeado quede protegido automáticamente, sin requerir cambios en sus propios handlers ni reimplementar lógica de tracking.

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `NewChildEntityTrackingInterceptor` (`ISaveChangesInterceptor`) | `ERP.Infrastructure/Persistence/Interceptors/` | FROZEN |
| `ErpDbContext.WasTrackedFromQuery` + suscripción `ChangeTracker.Tracked` | `ERP.Infrastructure/Persistence/ErpDbContext.cs` | FROZEN |
| Registro DI (`AddInterceptors`) | `ERP.Infrastructure/DependencyInjection.cs` | FROZEN |
| Gate CI | `ERP.Infrastructure.Tests/Persistence/NewChildEntityTrackingArchitectureTests.cs` (`ATT-GATE-01`) | FROZEN |
| Suite de integración (PostgreSQL real) | `ERP.Infrastructure.Tests/Persistence/NewChildEntityTrackingInterceptorTests.cs` | FROZEN |

### API pública / comportamiento congelado

La **única infraestructura autorizada** para corregir una clasificación ambigua del `ChangeTracker` es `NewChildEntityTrackingInterceptor`. Su regla de decisión:

1. Entidad `Modified` sin diferencia real de valores **y** nunca materializada por una query en este `DbContext` → se corrige a `Added` (firma inequívoca de entidad nueva).
2. Entidad `Modified` sin diferencia real de valores pero **sí** materializada por una query (combinación anómala) → lanza `InvalidOperationException` explícita. No se adivina ni se autocorrige.

### Regla arquitectónica permanente

> Ningún agregado existente podrá ser reatachado mediante `DbSet.Attach()`, `DbSet.Update()` o mecanismos equivalentes sin haber sido previamente cargado mediante una consulta del mismo `DbContext`. Toda modificación de un agregado existente deberá iniciarse desde una entidad obtenida mediante el repositorio correspondiente. La infraestructura de persistencia asume este invariante y lo protege mediante `ATT-GATE-01` y la validación interna del `ISaveChangesInterceptor`. Si este invariante se viola, la infraestructura deberá fallar explícitamente mediante una excepción en lugar de intentar corregir automáticamente el estado del `ChangeTracker`. Esta regla debe considerarse permanente.

### Prohibido en todo el sistema

- Mutar manualmente `EntityState` de una entrada del `ChangeTracker` como mecanismo de negocio en cualquier handler o servicio.
- Llamar a `DbSet<T>.Attach()`/`DbSet<T>.Update()` directo sobre una entidad detached con ID real fuera de la lista blanca cerrada de `ATT-GATE-01` (`PaymentTermRepository.cs`, `PaymentMethodRepository.cs`, `SriSettingsRepository.cs`, `ItemTypeRepository.cs` — catálogos sin colecciones de navegación hijas).
- Implementar lógica propia de corrección de tracking en cualquier módulo del ERP.
- Reatachar un agregado sin haberlo cargado antes con una query en el `DbContext` activo.

### Reglas de evolución

- Cualquier necesidad real de reatachar un agregado sin pasar por query previa requiere ampliar explícitamente la lista blanca de `ATT-GATE-01`, justificando que la entidad no tiene colecciones de navegación hijas expuestas al patrón de fixup.
- Cualquier cambio en la señal `WasTrackedFromQuery`, la condición de clasificación o la estrategia fail-fast requiere nueva ADR + repetir la suite de integración con PostgreSQL real.
- El gate CI-bloqueante (`ATT-GATE-01` en `ERP.Infrastructure.Tests`) garantiza que ningún módulo viole la regla de reatachamiento automáticamente.

---

## Infraestructura CLOSED — Configuración Tributaria (INMUTABLE)

Decisión arquitectónica congelada 2026-07-01.

Infraestructura transversal del ERP. Define la fuente única de verdad para toda configuración tributaria y prohíbe que los documentos transaccionales generen, asuman o sustituyan impuestos.

### Reglas permanentes

**Regla 1 — Fuente de verdad tributaria**
Toda configuración tributaria pertenece exclusivamente a la entidad de negocio correspondiente (ítem, servicio o cualquier entidad master futura). El documento transaccional solo consume — nunca define — dicha configuración.

**Regla 2 — Los documentos no generan impuestos**
Los documentos transaccionales (Facturas, Notas de Crédito/Débito, Cotizaciones, Órdenes y cualquier documento futuro) únicamente consumen la configuración tributaria del ítem. Queda prohibido asumir IVA, asumir ICE, asumir códigos tributarios o generar impuestos por defecto.

**Regla 3 — Error de configuración**
Si una entidad obligatoria carece de configuración tributaria, el sistema la trata como un error de configuración del maestro. Nunca inventa valores, usa fallbacks, completa automáticamente ni sustituye información tributaria.

**Regla 4 — Motor único de cálculo**
Todo cálculo tributario usa exclusivamente: (1) configuración tributaria del ítem, (2) catálogos oficiales SRI (vía `ISriTaxResolver` en backend, `sriLookupService.*Rates()` en frontend), (3) reglas del dominio (`SalesInvoiceDetail.ApplyTaxes()`). Nunca reglas locales del módulo.

**Regla 5 — Catálogos**
Todos los códigos tributarios provienen exclusivamente de los catálogos oficiales (`sri_vat_rates`, `sri_ice_rates`). Nunca listas hardcodeadas ni catálogos reconstruidos manualmente en ninguna capa.

### Estado actual — componentes alineados

| Componente | Archivo | Estado |
|---|---|---|
| Fuente de verdad IVA | `item.TaxConfig.SaleVatCode` → `SalesLineInput.VatCode` | FROZEN |
| Fuente de verdad ICE | `item.TaxConfig.ExciseTaxCode` → `SalesLineInput.IceCode` | FROZEN |
| Cálculo IVA + ICE backend | `SalesTaxHelper.ResolveTaxesAsync()` vía `ISriTaxResolver` | FROZEN |
| Cálculo IVA + ICE frontend | `salesCalc.ts` vía `vatRatesMap` + `iceRatesMap` de catálogo | FROZEN |
| Validación obligatoriedad | `FluentValidation VatCode.NotEmpty()` + Zod `.min(1)` | FROZEN |
| Prohibición fallback tributario | `vatCode: saleVatCode ?? ''` — sin `'10'` ni `purchaseVatCode` | FROZEN |
| Catálogos SRI | `GET /api/v1/catalog/sri-vat-rates`, `/sri-ice-rates` | FROZEN |

### Prohibido en todo el sistema

- Usar cualquier código tributario literal (`'10'`, `'0'`, `'8'`, etc.) como valor por defecto en documentos transaccionales.
- Usar `purchaseVatCode` como fallback en documentos de venta.
- Asignar `vatCode` o `iceCode` desde el módulo de ventas sin que provengan del ítem.
- Crear `DefaultVatCode`, `DefaultIceCode` o cualquier configuración tributaria a nivel empresa.
- Resolver impuestos en módulos de negocio mediante reglas locales en lugar de `ISriTaxResolver` / `sriLookupService.*Rates()`.
- Crear listas de catálogos tributarios hardcodeadas en ninguna capa.

### Reglas de evolución

- Cualquier nuevo impuesto requiere actualizar **únicamente** `ISriTaxResolver` (backend) y los catálogos SRI (`sri_*_rates`), sin tocar los documentos transaccionales.
- Un módulo nuevo que calcule impuestos debe consumir `ISriTaxResolver` en backend y `sriLookupService.*Rates()` en frontend — nunca implementar lógica tributaria propia.
- Cualquier cambio en las reglas de obligatoriedad (`VatCode.NotEmpty()`) requiere análisis de compatibilidad hacia atrás.
- El mensaje de validación Zod `'El producto no tiene código IVA de venta configurado. Verifique el maestro de productos.'` es parte de esta infraestructura y no se modifica sin justificación formal.

---

## Infraestructura CLOSED — Tipos de Ítem (Item Types) (INMUTABLE)

Decisión arquitectónica congelada 2026-07-04. Reemplaza el enum C# fijo `ItemType { Physical, Service, Digital, Kit, Bundle }` (eliminado) por un catálogo tenant-editable.

### Reglas permanentes

**Regla 1 — Catálogo, no enum**
`ItemTypeDefinition` (`Id, TenantId, Code, Name, SortOrder, IsActive`) es la única fuente de verdad de los tipos de ítem. Cada tenant administra su propio catálogo (crear/editar/activar/desactivar/ordenar) vía `api/v1/item-types`, sin tocar código para agregar un tipo nuevo.

**Regla 2 — Relación por Id, nunca por texto**
`items.item_type_id (uuid)` es la única columna de relación, con FK física a `item_types.id`. Prohibido persistir o comparar por `Code`/`Name` como si fueran el identificador de la relación.

**Regla 3 — Clasificación pura, sin comportamiento**
`ItemTypeDefinition` no controla inventario, venta, compra ni ningún comportamiento funcional (decisión explícita 2026-07-04). El comportamiento por ítem vive exclusivamente en `ItemStockConfig`/`SaleConfig`, independientes del tipo. Evolucionar esto a flags de comportamiento (`EsServicio`, `PermiteVenta`, etc.) requiere una ADR nueva, no una extensión menor.

**Regla 4 — Fuente única de consumo en frontend**
`useItemTypeOptions()` (`modules/items/hooks/useItemTypeOptions.ts`) es el único punto de acceso al catálogo desde React, con caché de módulo para evitar peticiones duplicadas cuando varios componentes se montan en la misma vista. Prohibido hacer `apiGet('/api/v1/item-types')` directo fuera de este hook o de `itemTypeService.ts` (admin).

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `ItemTypeDefinition` (entidad) | `ERP.Domain/Modules/Items/Entities/` | FROZEN |
| `IItemTypeRepository`/`ItemTypeRepository` | `ERP.Domain` / `ERP.Infrastructure/Persistence/Repositories/Items/` | FROZEN |
| `ItemTypeUseCases.cs` (CQRS completo) | `ERP.Application/Modules/Items/UseCases/` | FROZEN |
| `ItemTypesController.cs` (`api/v1/item-types`) | `ERP.API/Controllers/` | FROZEN |
| FK `items.item_type_id → item_types.id` | Migración `20260704193028_AddItemTypeIdForeignKey` | FROZEN |
| `itemTypeService.ts` / `useItemTypeOptions.ts` | `frontend/src/modules/items/api/` y `hooks/` | FROZEN |
| `ItemTypesPage.tsx` (`/inventory/item-types`) | `frontend/src/modules/items/pages/` | FROZEN |

### Prohibido en todo el sistema

- Reintroducir un enum fijo de tipos de ítem en backend o frontend.
- Guardar, filtrar o comparar por `Code`/`Name` del tipo de ítem como si fuera el identificador de relación (`item.itemType === 'Service'` y equivalentes).
- Implementar un segundo fetch independiente a `/api/v1/item-types` fuera de `useItemTypeOptions()`/`itemTypeService.ts`.
- Agregar flags de comportamiento a `ItemTypeDefinition` sin una ADR formal.
- Incluir `itemTypeId` en el payload de actualización de ítem (`UpdateItemCommand` no lo acepta — es inmutable post-creación).

### Reglas de evolución

- Un nuevo campo descriptivo en `ItemTypeDefinition` (ej. un ícono) se agrega como columna nueva sin cambiar el modelo de relación.
- Convertir la clasificación en comportamiento funcional (que el tipo controle inventario/venta/kardex) requiere ADR nueva, evidencia técnica y reconciliación explícita con `ItemStockConfig`/`SaleConfig`.
- Cualquier módulo nuevo que necesite el nombre del tipo de ítem debe resolverlo vía `ItemTypeName` ya expuesto en los DTOs (`ItemDto`, `ItemDetailDto`), nunca reimplementando la resolución.

---

## Infraestructura CLOSED — Valores por Defecto de Facturación (INMUTABLE)

Decisión arquitectónica congelada 2026-07-01. **Migrado a org_settings 2026-07-01 (Phase 8).**

Infraestructura transversal del ERP. Gestiona los 5 parámetros por defecto que se precargan al crear una nueva factura de venta. **Fuente de verdad: tabla `org_settings` con `scope=Company`** — ya no `SriSettings`. Los 5 campos fueron eliminados de `SriSettings` y sus columnas dropeadas vía migración `RemoveSriSettingsInvoiceDefaults`.

### Parámetros congelados

| Clave `org_settings` | Tipo | Propósito |
|---|---|---|
| `invoice.default_doc_type_code` | `String` | Tipo de documento SRI por defecto |
| `invoice.default_payment_method_code` | `String` | Forma de pago SRI por defecto |
| `invoice.default_emission_point_id` | `Guid` | Punto de emisión por defecto |
| `invoice.default_warehouse_id` | `Guid` | Bodega por defecto |
| `invoice.default_payment_term_id` | `Guid` | Condición de pago por defecto |

Todos son opcionales: ausencia de fila significa "sin configurar" — el usuario lo seleccionará manualmente en cada factura.

### API pública congelada

```csharp
// Única operación autorizada para leer los defaults desde módulos de negocio:
GetSalesInvoiceDefaultsQuery  →  GET /api/v1/electronic-invoicing/invoice-defaults

// Única operación autorizada para mutar los defaults (Company Settings Hub):
UpdateSalesInvoiceDefaultsCommand  →  PUT /api/v1/electronic-invoicing/sales-defaults
```

El handler lee/escribe via `IOrgSettingsRepository` con `OrgScope.Company`. Los valores fallback SRI (`"01"`) vienen de las constantes `SriSettings.FallbackDocTypeCode` y `SriSettings.FallbackSriPaymentMethodCode`.

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `UpdateSalesInvoiceDefaultsCommand` | `ERP.Application/Modules/ElectronicInvoicing/UseCases/UpdateSalesInvoiceDefaults/` | FROZEN |
| `UpdateSalesInvoiceDefaultsCommandHandler` | `ERP.Application/Modules/ElectronicInvoicing/UseCases/UpdateSalesInvoiceDefaults/` | FROZEN |
| `UpdateSalesInvoiceDefaultsCommandValidator` | `ERP.Application/Modules/ElectronicInvoicing/UseCases/UpdateSalesInvoiceDefaults/` | FROZEN |
| `GetSalesInvoiceDefaultsQueryHandler` | `ERP.Application/Modules/Sales/UseCases/GetSalesInvoiceDefaults/` | FROZEN |
| `GET /electronic-invoicing/sales-defaults` (endpoint) | `ERP.API/Controllers/ElectronicInvoicingController.cs` | FROZEN |
| `PUT /electronic-invoicing/sales-defaults` (endpoint) | `ERP.API/Controllers/ElectronicInvoicingController.cs` | FROZEN |
| `salesDefaultsSchema.ts` + `SalesDefaultsValues` | `frontend/src/modules/configuracion/empresa/schemas/` | FROZEN |
| `SalesDefaultsSettingsSection.tsx` | `frontend/src/modules/configuracion/empresa/sections/` | FROZEN |
| `salesDefaultsService.getSettings()` / `updateSettings()` | `frontend/src/modules/sales/api/salesDefaultsService.ts` | FROZEN |
| Tab `'sales-defaults'` en `companySettingsTabs.ts` | `frontend/src/modules/configuracion/empresa/` | FROZEN |

### Reglas permanentes

**Regla 1 — Fuente de verdad**
Los 5 defaults viven en `org_settings` con `scope=Company`. `SriSettings` no almacena defaults de factura.

**Regla 2 — Todos los campos son opcionales**
`null` / ausencia de fila es el estado válido para cualquiera de los 5 parámetros. El módulo de ventas debe manejar `null` sin inventar fallbacks de negocio.

**Regla 3 — Separación de concern con Facturación Electrónica**
Los defaults de factura son un concern de Company Settings, no de Electronic Invoicing. Los endpoints viven en `ElectronicInvoicingController` por afinidad histórica, pero la UI vive en Company Settings Hub (`/settings/company`).

**Regla 4 — Org Config Hierarchy**
Los defaults de nivel Empresa son el nivel base. Los niveles Sucursal, Establecimiento, PuntoEmisión y Bodega pueden sobrescribir campo a campo via `org-config/{scope}/{id}/invoice-defaults`. El módulo de ventas deberá aplicar la resolución jerárquica al momento de precargar una factura.

### Prohibido en todo el sistema

- Almacenar defaults de factura de venta en `SriSettings` ni en ninguna entidad distinta de `org_settings`.
- Reintroducir `DefaultDocTypeCode`, `DefaultSriPaymentMethodCode`, `DefaultEmissionPointId`, `DefaultWarehouseId` o `DefaultPaymentTermId` en `SriSettings`.
- Reintroducir `SriSettings.CreateForDefaults()` o `SriSettings.UpdateInvoiceDefaults()`.
- Usar valores hardcodeados como fallback cuando el campo es `null` en cualquier módulo de negocio.
- Crear selectores de defaults de factura fuera de la pestaña "Valores por Defecto" del Company Settings Hub.

### Reglas de evolución

- Un 6.° parámetro por defecto (p. ej. `DefaultCurrencyCode`) se agrega como nueva clave en `OrgSettingKeys.Invoice.*` — sin nueva entidad ni nueva tabla.
- Cualquier cambio en el contrato de `SalesInvoiceDefaultsDto` (campo nuevo o renombrado) requiere actualizar `GetSalesInvoiceDefaultsQuery`, `UpdateSalesInvoiceDefaultsCommand` y el servicio frontend `salesDefaultsService` de forma sincronizada.

---

## Infraestructura CLOSED — ElectronicDocuments v1.0 (Facturación Electrónica SRI) (INMUTABLE)

Decisión arquitectónica congelada 2026-07-11. ADR: [`docs/adr/ADR-023-electronic-documents-v1-closure.md`](docs/adr/ADR-023-electronic-documents-v1-closure.md).

Núcleo funcional de facturación electrónica SRI Ecuador (esquema offline) — generación de XML, validación XSD, firma XAdES-BES, recepción, autorización, reintentos, auditoría. Cerrado tras tres rondas de verificación: auditoría de robustez (críticos/altos corregidos con evidencia y reproducción), validación de cumplimiento del Anexo Técnico SRI texto por texto contra el PDF oficial, y pruebas reales contra el ambiente de Pruebas del SRI (`celcer.sri.gob.ec`) con certificado real, incluyendo un rechazo real confirmado.

### Regla permanente

A partir de este cierre, cualquier cambio al núcleo de `ElectronicDocuments` debe estar justificado por una de estas cuatro causas — nunca por "mejora", "refactor" o "podría hacerse mejor":

1. **Cambio obligatorio del SRI** (nueva versión de XSD, nuevo código de error, cambio de URL de servicio, nuevo campo exigido por una actualización de la Ficha Técnica).
2. **Bug demostrado** (con reproducción, causa raíz y test de regresión).
3. **Vulnerabilidad de seguridad** (con evidencia de explotabilidad real).
4. **Rendimiento crítico** (con medición objetiva, no percepción).

### Componentes congelados

| Componente | Ubicación | Estado |
|---|---|---|
| `ElectronicDocument` (agregado, máquina de estados) | `ERP.Domain/Modules/ElectronicDocuments/Entities/` | FROZEN |
| `IElectronicDocumentIssuer` (`RegisterAsync`/`RetryAsync`) | `ERP.Application/Modules/ElectronicDocuments/Services/` | FROZEN |
| Pipeline (`ElectronicDocumentIssuer.RunPipelineAsync`) | `ERP.Application/Modules/ElectronicDocuments/Services/` | FROZEN |
| `XadesBesSigner` / `SriSoapClient` / `SriReceptionClient` / `SriAuthorizationClient` | `ERP.Infrastructure/Services/Sri/` | FROZEN |
| `ElectronicDocumentRetryPolicy` (5 intentos, backoff 1-16 min) + `ElectronicDocumentRetryJob` | `ERP.Application`/`ERP.API/Hangfire/` | FROZEN |
| `EmbeddedXmlSchemaProvider` + `manifest.json` + XSD oficiales | `ERP.Infrastructure/ElectronicDocuments/Resources/SRI/` | FROZEN |
| Controladores `ElectronicDocumentsController` / `ElectronicInvoicingController` | `ERP.API/Controllers/` | FROZEN |

### Prohibido en todo el sistema

- Modificar la máquina de estados, el pipeline o los contratos públicos listados arriba por "limpieza" o "consistencia" sin una de las 4 causas permitidas.
- Agregar builders/providers/validadores para nuevos tipos de comprobante (CreditNote, DebitNote, ShippingGuide, Retention, PurchaseSettlement — hoy solo XSD/catálogo, sin implementación activa) como si fuera mantenimiento — es funcionalidad nueva, requiere su propia fase con roadmap explícito.
- Reintroducir cálculo tributario, numeración documental o auditoría propia dentro de este módulo — son infraestructuras FROZEN de otros ADR (Configuración Tributaria, ADR-019, ADR-022), se consumen, nunca se reimplementan.
- Relajar el catálogo `sri_error_code` con datos no verificados textualmente contra la Ficha Técnica oficial (`docs/FICHA TECNICA COMPROBANTES ELECTRONICOS ESQUEMA OFFLINE Versio232.pdf`).

### Reglas de evolución

- Todo cambio, incluso bajo una de las 4 causas permitidas, sigue el protocolo de gate ya establecido: ¿es un bug real? ¿existe evidencia? ¿es reproducible? ¿cuál es el riesgo? ¿qué impacto tiene? ¿rompe compatibilidad? — antes de tocar código.
- Detalle completo de responsabilidades, límites, dependencias, interfaces públicas, estados, pipeline, eventos y deuda aceptada conscientemente: ver ADR-023.

## Infraestructura CLOSED — Auditoría por Dominio: Entity Audit (INMUTABLE) + Process Audit (diseño futuro)

Decisión arquitectónica congelada 2026-07-07. ADR: [`docs/adr/ADR-022-audit-infrastructure-entity-vs-process.md`](docs/adr/ADR-022-audit-infrastructure-entity-vs-process.md). Reglas ejecutables completas: [`AI-RULES/AUDIT-INFRASTRUCTURE.md`](AI-RULES/AUDIT-INFRASTRUCTURE.md).

Infraestructura transversal del ERP. Reemplaza el patrón anterior de escribir auditoría de negocio a mano contra la tabla genérica `UserActivity` (que queda reservada exclusivamente para el feed liviano "mi actividad reciente", nunca para auditoría de negocio con valores tipados antes/después).

### Componentes congelados (Entity Audit)

| Componente | Ubicación | Estado |
|---|---|---|
| `AuditRecordBase`, `AuditActor`, `AuditSource`, `IAuditEvent` | `ERP.Domain/Audit/` | FROZEN |
| `IAuditWriter<T>`, `IAuditReader<T>`, `IAuditContext`, `IAuditService` | `ERP.Application/Audit/` | FROZEN |
| `EfAuditWriter<T>`, `EfAuditReader<T>`, `HttpAuditContext`, `AuditService`, `ConfigureAuditBase<T>()` | `ERP.Infrastructure/Audit/` | FROZEN |
| Dispatcher (`AggregateRoot.RaiseDomainEvent` → `ErpDbContext.SaveChangesAsync` → Outbox → MediatR `IPublisher` → `*AuditHandler`) | `ERP.Infrastructure/Persistence/ErpDbContext.cs` (ya FROZEN por ADR-007/008) | FROZEN |
| Pilotos de referencia: `PricingRuleAudit`/`PricingRuleAuditHandler`, `PriceListItemAudit`/`PriceListItemAuditHandler` | `ERP.Domain/Modules/Pricing/Entities/`, `ERP.Application/Modules/Pricing/EventHandlers/` | FROZEN (como referencia de patrón, no como límite de dominios) |

### Regla 1 — Open/Closed

Todo dominio nuevo (Inventory, Sales, Purchasing, Accounting, Cash & Banks, Electronic Documents, CRM, Assets, Manufacturing, RRHH) agrega **únicamente**: su entidad de auditoría (hereda `AuditRecordBase`), sus domain events, y su `*AuditHandler`. Ninguno modifica los componentes FROZEN de la tabla anterior.

### Regla 2 — Dos categorías oficiales, complementarias, nunca sustitutas

- **Entity Audit** (implementado): audita una entidad de negocio identificable por su propio `EntityId`. Responde ¿qué cambió?, ¿quién?, ¿cuándo?, ¿valor anterior?, ¿valor nuevo?
- **Process Audit** (futuro, diseñado y no implementado): audita la ejecución de un proceso completo sin una única entidad como sujeto (importación masiva, recálculo de precios, cierre contable, cierre diario, conteo físico, recosteo, sincronización SRI, facturación masiva, generación de asientos, backups, jobs Hangfire, provisionamiento SaaS, migraciones, ETL, integraciones externas). Responde ¿qué proceso?, ¿inicio/fin/duración?, ¿quién o qué job lo ejecutó?, ¿cuántos registros/errores?, ¿resultado?
- Un proceso masivo que modifica entidades individuales genera **su propia fila de Process Audit** (la corrida) **y** sigue generando **una fila de Entity Audit por cada entidad modificada** si pasa por el mismo camino de dominio que un cambio manual.

### Regla 3 — Cómo Process Audit deberá extender esto sin modificarlo

`EntityId` no está atado a una fila de tabla de negocio: puede ser el `ProcessRunId` (Guid) de una corrida de proceso. Por eso Process Audit reutilizará los mismos contratos FROZEN (pseudo-agregado de proceso con eventos `Started/Completed/Failed` + entidad `XxxProcessAudit : AuditRecordBase` + `XxxProcessAuditHandler` + una implementación nueva de `IAuditContext` para contextos no-HTTP) — nunca modificándolos. Detalle completo en `AI-RULES/AUDIT-INFRASTRUCTURE.md` sección 4.

### Prohibido en todo el sistema

- Modificar `AuditRecordBase`, `IAuditWriter<T>`, `IAuditReader<T>`, `IAuditService`, `IAuditContext`, `AuditActor`, `AuditSource` o `IAuditEvent` desde un dominio de negocio.
- Escribir auditoría desde un Controller, un Repository, un handler de negocio fuera de un `*AuditHandler` dedicado, o desde React.
- Reutilizar `UserActivity`/`IUserActivityRepository` para auditoría de negocio con valores tipados antes/después.
- Crear una segunda implementación de `IAuditWriter<T>`/`IAuditReader<T>` específica de un dominio — la genérica ya sirve a todos.
- Agregar columnas de un dominio a la entidad de auditoría de otro dominio ("God table").
- Implementar Process Audit modificando la infraestructura base de Entity Audit en vez de extenderla como consumidor nuevo.
- Agregar columnas de identidad del actor (nombre, email, rol o variantes) directamente en la entidad de auditoría de un dominio — esa información vive exclusivamente en `AuditActor`.

### AuditActor — único modelo del actor, snapshot histórico (ampliado 2026-07-07)

`AuditActor` (`ERP.Domain/Audit/AuditActor.cs`) es el **único** lugar donde vive información sobre el actor: `UserId` (identidad, obligatorio) + `UserName` (snapshot histórico obligatorio, no-nullable, nunca vacío) + `FullName`/`Email`/`RoleName` (detalle opcional adicional, ya poblados hoy sin costo extra) + `CorrelationId`/`RequestId`/`Source`. Es un **snapshot inmutable**: se calcula una vez en el momento del evento y nunca se recalcula, resincroniza ni actualiza — prohibido agregar columnas de identidad del usuario en las entidades de auditoría de cada dominio (ver "Prohibido en todo el sistema").

`AccessTokenService` embebe `ClaimTypes.Email`/`ClaimTypes.Name` en el JWT al emitirlo — snapshot al momento de login/refresh, nunca una consulta en vivo a la tabla de usuarios. `ClaimTypes.Name` (no `ClaimTypes.GivenName`) representa el nombre visible completo; `GivenName` se descartó por representar semánticamente "solo el nombre". `CurrentUserService` lee `ClaimTypes.Name` con un fallback transitorio a `ClaimTypes.GivenName` solo por compatibilidad con tokens ya emitidos antes de esta corrección (expira con el vencimiento natural del token). `HttpAuditContext` resuelve `UserName = FullName ?? Email ?? "Unknown"` (con log de advertencia si cae al fallback); `AuditRecordBase.SetCommon` aplica el mismo fallback como última defensa. Columna `user_name` es `NOT NULL` (migración `MakeAuditUserNameRequired`, con backfill defensivo a `'Unknown'`). Si un usuario cambia su nombre después, los registros de auditoría ya persistidos **no se actualizan** — consistente con append-only. Todo dominio futuro (Entity Audit o Process Audit) hereda este comportamiento automáticamente sin ningún cambio propio.

### Deuda técnica conocida (no bloquea el freeze del contrato, sí requiere remediación antes de confiar en los datos)

1. ~~`CurrentUserService.Email`/`FullName` devuelven `null` hardcodeado~~ — **RESUELTO (2026-07-07)**, ver "UserName como snapshot histórico" arriba.
2. `HttpAuditContext.Actor.Source` está hardcodeado a `AuditSource.UserAction` — falta una implementación de `IAuditContext` para jobs/sistema.
3. `CorrelationId`/`RequestId` no se truncan antes de persistir en columnas `varchar(100)` — un header `X-Correlation-Id` fuera de rango puede abortar la transacción de negocio.

Ninguno de los tres requirió ni requiere modificar los contratos FROZEN — son fixes internos de las implementaciones concretas ya listadas.

### Reglas de evolución

- Un nuevo dominio de Entity Audit sigue el checklist de `AI-RULES/AUDIT-INFRASTRUCTURE.md` sección 2 sin abrir una nueva ADR.
- La primera implementación real de Process Audit sigue el patrón de la sección 4 de `AI-RULES/AUDIT-INFRASTRUCTURE.md` y actualiza ese documento con el primer ejemplo concreto, sin nueva ADR — salvo que el modelo "proceso como pseudo-entidad" resulte insuficiente, en cuyo caso sí se requiere ADR nueva.
- Cualquier cambio real a los contratos FROZEN de la tabla de componentes requiere una nueva ADR aprobada.
