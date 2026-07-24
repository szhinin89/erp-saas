# ADR-025: Ride v1.0 — Design Freeze (pre-implementación)

## Status

**Design Frozen.** 2026-07-12. El diseño arquitectónico del módulo `Ride` (Representación Impresa del Documento Electrónico) fue completado, auditado contra el código real de `ElectronicDocuments` v1.0 (ADR-023, FROZEN) y su superficie pública fue revisada y congelada. **La implementación no ha comenzado.** Esta ADR documenta únicamente decisiones ya tomadas en las tres rondas previas (diseño, auditoría, congelación de contratos) — no introduce alcance nuevo.

## Contexto

`ElectronicDocuments` v1.0 (ADR-023) cierra el ciclo de vida electrónico de un comprobante SRI (generación de XML, firma, recepción, autorización) pero no produce ninguna representación visual del comprobante. El ERP necesita generar el RIDE (PDF) que el receptor de la factura recibe junto al XML, sin que esa responsabilidad contamine `ElectronicDocuments` ni ningún módulo de negocio (Sales, Purchases).

El diseño se desarrolló en tres rondas disciplinadas, siguiendo el mismo protocolo que se usó para `ElectronicDocuments` antes de su propio cierre (ADR-023):

1. **Diseño de arquitectura** — capas, pipeline, Strategy + Resolver para parsers/plantillas, aislamiento de QuestPDF, branding desacoplado, storage vía `IFileStorage`.
2. **Auditoría contra código real** — no una auditoría en abstracto: se leyó `ElectronicDocumentIssuer.cs`, `ElectronicDocument.cs`, `GetElectronicDocumentXmlQuery`/`GetElectronicDocumentQuery`/`GetElectronicDocumentDetailQuery` para verificar que las suposiciones del diseño sobre el contrato de `ElectronicDocuments` fueran ciertas. Se encontraron y resolvieron 5 hallazgos reales (H1–H5, detallados abajo en "Decisiones derivadas de la auditoría").
3. **Congelación de contratos públicos** — se determinó qué cruza el límite de módulo (2 requests MediatR + 2 DTOs + 1 enum) y qué es extensión interna (Strategy contracts), corrigiendo el estilo de integración para que coincida con el ya usado por `ElectronicDocuments` (requests MediatR, no interfaces inyectadas).

## Decisión

Declarar el diseño de `Ride` v1.0 **congelado** bajo el contrato documentado en esta ADR. Ningún código existe todavía; esta ADR es la línea base contra la cual se implementará y contra la cual se evaluará cualquier desviación futura.

### 2. Objetivo del módulo

`Ride` tiene una única responsabilidad: **generar la Representación Impresa del Documento Electrónico (RIDE)** a partir del XML autorizado por el SRI.

`Ride` nunca:

- emite un comprobante electrónico,
- firma XML,
- se comunica con el SRI (SOAP, Recepción, Autorización),
- recalcula valores tributarios,
- consulta `Sales`, `Purchases`, `Inventory` ni `Accounting`.

Todas esas responsabilidades pertenecen a `ElectronicDocuments` (FROZEN, ADR-023) o a los módulos de negocio de origen. `Ride` es exclusivamente un traductor de XML autorizado a PDF.

### 3. Fuente oficial de datos

**El XML autorizado es la única fuente de verdad del RIDE.** Decisión arquitectónica, no una preferencia de implementación.

- El PDF nunca se reconstruye desde entidades de negocio (`SalesInvoice`, `Item`, `Customer`, etc.).
- Ningún valor tributario (IVA, ICE, totales) se recalcula dentro de `Ride` — se lee tal cual aparece en el XML autorizado.
- Si el XML autorizado no está disponible para un documento, `Ride` no genera un PDF con datos alternativos — reporta un estado explícito (`RideOutcome.PendingSource`, ver §14 más abajo) y espera a que el XML exista.

### 4. Alcance

**Lo que el módulo hace:**

- Recibe la referencia de un documento de origen (`SourceModule`, `SourceEntityId`) y su `TenantId`/`CompanyId`.
- Obtiene el XML autorizado correspondiente vía el contrato público de lectura de `ElectronicDocuments`.
- Parsea el XML a un modelo interno neutro (`RideModel`).
- Resuelve una plantilla, resuelve branding, genera un código QR desde la clave de acceso.
- Renderiza un PDF y lo persiste vía `IFileStorage`.
- Cachea el resultado y lo reutiliza mientras el XML, la plantilla, el branding y el motor de render no cambien.
- Expone dos operaciones públicas: obtener-o-generar, y regenerar explícitamente.

**Lo que el módulo NO hace:**

- No decide si un documento debe emitirse, firmarse o reenviarse — eso es `ElectronicDocuments`.
- No calcula impuestos, secuencias documentales ni claves de acceso — los consume ya calculados, dentro del XML.
- No conoce formularios, controladores de otros módulos, ni EF Core de otros dominios.
- No implementa hoy más que un tipo de comprobante (`Invoice`) y una plantilla — el resto es extensión futura habilitada por diseño, no implementada en v1.0.

### 5. Arquitectura — capas

| Capa | Responsabilidad |
|---|---|
| `ERP.Domain.Modules.Ride` | `RidePdfDocument` (entidad de metadatos), `RideModel` y sus VOs (`RideHeader`, `RideParty`, `RideLine`, `RideTaxSummary`, `RidePaymentInfo`, `RideAdditionalInfo`, `RideAccessKey`, `RideBranding`, `RideContentHash`), enums (`RideDocumentType` interno, `RidePdfState`), eventos de dominio. Sin dependencias externas. |
| `ERP.Application.Modules.Ride` | Contratos públicos (2 requests MediatR + DTOs), contratos internos (parsers, templates, renderer, branding, QR, storage, cache), `RidePipeline` (orquestador interno), UseCases. Sin QuestPDF, sin XDocument en firmas públicas, sin `Sales`/`Purchases`. |
| `ERP.Infrastructure.Ride` | Implementaciones concretas: parsers XML por tipo de comprobante, `QuestPdfRideRenderer`, `OrgSettingsRideBrandingProvider`, generador de QR, `RidePdfStorageService` sobre `IFileStorage`, `ElectronicDocumentRideSourceXmlProvider` (único adaptador hacia `ElectronicDocuments`), persistencia EF de `RidePdfDocument`. |
| `ERP.API` | `RideController` — expone los dos requests públicos vía HTTP, sin lógica de negocio. |

Dirección de dependencias: `API → Application → Domain`, `Infrastructure → Application` (implementa sus interfaces). Ningún ciclo, ninguna referencia de `Application`/`Domain` hacia `Infrastructure`.

### 6. Pipeline congelado

```
XML autorizado
   ↓
IRideXmlParser (Strategy, resuelto por tipo de comprobante)
   ↓
RideModel (modelo neutro)
   ↓
IRideTemplate (Strategy, resuelto por selector)
   ↓
IRideRenderer (QuestPDF es la única implementación v1.0)
   ↓
IRidePdfStorageService (vía IFileStorage)
   ↓
RideGenerationResultDto
```

Antes del parseo, `IRideCacheStrategy` decide si el pipeline se ejecuta o si se reutiliza un PDF ya almacenado (ver §14). Este pipeline queda **congelado**: agregar un tipo de comprobante nuevo no modifica su secuencia de pasos (ver §10).

### 7. Contratos públicos congelados

```csharp
GetOrGenerateRideQuery(string SourceModule, Guid SourceEntityId)
    : IRequest<Result<RideGenerationResultDto>>, ICompanyScopedRequest

RegenerateRideCommand(string SourceModule, Guid SourceEntityId)
    : IRequest<Result<RideGenerationResultDto>>, ICompanyScopedRequest

RideGenerationResultDto(RideOutcome Outcome, string? StoragePath, RidePdfMetadataDto? Metadata, string? ReasonCode)

RidePdfMetadataDto(
    string TemplateId,
    string TemplateVersion,
    string BrandingVersion,
    string RendererVersion,
    string SourceXmlHash,
    DateTime GeneratedAtUtc,
    bool WasCached)

enum RideOutcome { Generated, Cached, PendingSource, NotApplicable, Failed }
```

Estos nombres, firmas y semántica de `RideOutcome` no cambian sin una nueva ADR. Cualquier módulo consumidor (Sales hoy; Purchases, CRM, o el propio `RideController` mañana) integra exclusivamente contra estos dos requests, siguiendo la misma convención de integración cross-módulo ya establecida por `ElectronicDocuments` (requests MediatR, no interfaces inyectadas).

### 8. Contratos internos

`IRideXmlParser` / `IRideXmlParserResolver`, `IRideTemplate` / `IRideTemplateResolver` / `RideTemplateSelector`, `IRideRenderer`, `IRideBrandingProvider`, `IRideQrCodeGenerator`, `IRidePdfStorageService` / `IRidePdfStorageNamingStrategy`, `IRideCacheStrategy`, `IRideContentHasher`, `IRideSourceXmlProvider`, `IRideDocumentService` (facade interna consumida solo por los handlers de §7).

Ninguno de estos es alcanzable desde otro módulo del ERP. Pueden evolucionar libremente (nuevas implementaciones, cambios internos de firma, optimizaciones) mientras no rompan los contratos públicos de §7. `RideDocumentType` (el enum interno que clasifica comprobantes dentro de `Ride`) nunca aparece en un contrato público — la traducción desde el `DocumentType` de `ElectronicDocuments` es responsabilidad exclusiva de `ElectronicDocumentRideSourceXmlProvider`.

### 9. Strategy Pattern

`IRideXmlParser`, `IRideTemplate` e `IRideRenderer` (este último con una sola implementación hoy, pero con el mismo contrato de intercambio) usan Strategy + Resolver: el resolver construye un diccionario en memoria a partir de `IEnumerable<T>` inyectado por DI (mismo patrón verificado en `ElectronicDocumentXmlBuilderResolver` de `ElectronicDocuments`). **Prohibido en todo el módulo**: `switch(documentType)` o `if (documentType == ...)` para seleccionar comportamiento por tipo de comprobante, en cualquier capa.

### 10. Extensibilidad — nuevo tipo de comprobante

Agregar un comprobante nuevo (ej. Nota de Crédito) requiere únicamente:

1. Nuevo `IRideXmlParser` para ese tipo.
2. Nueva `IRideTemplate` para ese tipo.
3. Registro DI de ambos.

Sin modificar `RidePipeline`, sin modificar los resolvers, sin modificar los contratos públicos de §7.

### 11. Plantillas

La arquitectura soporta, sin cambio de contrato, una jerarquía futura de resolución:

```
Empresa → Sucursal → Punto de Emisión → Tipo de comprobante → Plantilla
```

`RideTemplateSelector` ya lleva los campos `CompanyId`, `BranchId?`, `EmissionPointId?`, `RideDocumentType` desde el diseño v1.0. **v1.0 implementa únicamente una plantilla activa** (`DefaultInvoiceRideTemplate`, resuelta solo por tipo de comprobante) — la resolución jerárquica completa es trabajo futuro que cambia la implementación interna de `IRideTemplateResolver`, no su firma.

### 12. Branding

`IRideBrandingProvider` es un contrato desacoplado: no depende del renderer, no depende del parser, no depende del XML. Resuelve `RideBranding` (logo, colores, pie de página) desde `org_settings` con el mismo mecanismo jerárquico ya usado por "Valores por Defecto de Facturación" — nunca lee archivos de disco directamente, siempre vía `IFileStorage` a partir de una ruta configurada.

### 13. Renderer

QuestPDF es únicamente una implementación de `IRideRenderer` (`QuestPdfRideRenderer`, en `ERP.Infrastructure.Ride`). No forma parte de ningún contrato público ni de ningún contrato interno de `Application` — `Application` solo conoce `IRideRenderer.RenderAsync(IRideDocumentLayout, ct) → byte[]`. Puede reemplazarse por otro motor sin afectar `Application`, `Domain`, ni los contratos públicos de §7.

### 14. Cache

La validez de un PDF ya generado depende de la coincidencia simultánea de:

- `SourceXmlHash` (hash del XML autorizado),
- `TemplateVersion`,
- `BrandingVersion`,
- `RendererVersion`,
- `RideSpecificationVersion` (versión del propio contrato `RideModel`/pipeline, para el caso en que el modelo neutro mismo cambie).

Un cambio en cualquiera de estos cinco valores invalida el PDF cacheado y dispara una regeneración en la siguiente solicitud. Estos cinco valores se exponen en `RidePdfMetadataDto` (§7) para que cualquier consumidor pueda auditar exactamente con qué versión se generó un RIDE ya emitido, sin necesidad de regenerarlo para saberlo.

### 15. Storage

El PDF nunca se almacena en base de datos. `RidePdfDocument` persiste únicamente metadatos (ruta, hash, versiones, estado, fecha). El archivo físico se persiste exclusivamente vía `IFileStorage`, con convención de ruta centralizada en `IRidePdfStorageNamingStrategy` — nunca una ruta construida ad-hoc fuera de esa clase.

### 16. Multi-tenant

Todo el módulo es Company Scoped: ambos requests públicos implementan `ICompanyScopedRequest`. No existe branding compartido entre tenants, no existen plantillas compartidas entre tenants (la resolución jerárquica de §11 nunca cruza `TenantId`), no existen archivos compartidos (la convención de ruta de `IFileStorage` segmenta por `TenantId` primero, igual que `ElectronicDocumentStorageNamingStrategy`).

### 17. Seguridad

- El XML autorizado nunca se modifica — `Ride` solo lo lee.
- `Ride` nunca firma ni re-firma nada — la firma es responsabilidad exclusiva y ya cerrada de `ElectronicDocuments` (ADR-023).
- Ningún valor tributario se recalcula dentro de `Ride`.
- El contenido autorizado no se altera bajo ninguna circunstancia — el PDF es únicamente una representación visual de lo que el XML ya dice.

### 18. Performance

El sistema reutiliza PDFs existentes vía la estrategia de cache de §14; no regenera un PDF cuyo XML, plantilla, branding y motor de render no hayan cambiado desde la última generación exitosa.

### Decisiones derivadas de la auditoría (H1–H5)

La ronda de auditoría (segunda ronda del proceso) encontró 5 hallazgos reales contra el código de `ElectronicDocuments`, todos resueltos en el diseño congelado por esta ADR:

| Hallazgo | Resolución incorporada |
|---|---|
| H1 — un `ElectronicDocument` puede llegar a `Authorized` sin `AuthorizedXmlPath` persistido (`ElectronicDocumentIssuer.AuthorizeAsync`, fallo de storage no bloquea la autorización) | `RideOutcome.PendingSource` como estado de negocio explícito, distinto de `Failed` — nunca tratado como error |
| H2 — el contrato real de lectura de `ElectronicDocuments` se identifica por `(SourceModule, SourceEntityId)`, no por `ElectronicDocument.Id` | Los dos requests públicos de §7 usan `(SourceModule, SourceEntityId)` como clave primaria |
| H3 — el cache no invalidaba por cambio de branding | `BrandingVersion` incorporado explícitamente a la clave de cache (§14) |
| H4 — sin guarda de concurrencia para generación simultánea del mismo RIDE | Índice único `(TenantId, ElectronicDocumentId, SourceXmlHash, TemplateVersion)` sobre `RidePdfDocument` — detalle de implementación, no de contrato público |
| H5 — sin dueño explícito de la traducción `ElectronicDocumentType` → `RideDocumentType` | Responsabilidad asignada explícitamente a `ElectronicDocumentRideSourceXmlProvider` (§8) |

## Cambios permitidos (sin nueva ADR)

- Nuevos parsers de comprobante (`IRideXmlParser`).
- Nuevas plantillas (`IRideTemplate`).
- Optimización interna del renderer o de cualquier implementación de un contrato interno.
- Nuevos proveedores de branding (`IRideBrandingProvider`), incluida la resolución jerárquica completa de §11.
- Corrección de bugs demostrados, con reproducción y test de regresión.
- Mejoras internas que no crucen los contratos públicos de §7.

## Cambios que requieren nueva ADR

- Modificar la firma o semántica de `GetOrGenerateRideQuery`, `RegenerateRideCommand`, `RideGenerationResultDto`, `RidePdfMetadataDto` o `RideOutcome`.
- Cambiar el pipeline congelado de §6 (agregar, quitar o reordenar etapas).
- Cambiar la fuente de verdad (dejar de usar el XML autorizado como único origen de datos).
- Eliminar el patrón Strategy + Resolver para parsers, plantillas o renderer.
- Modificar `RideModel` de forma que deje de ser un modelo neutro (p. ej. acoplarlo a una entidad de negocio o al XML directamente).
- Modificar el flujo de generación de forma que el PDF deje de representar exclusivamente el XML autorizado.

## Deuda técnica aceptada

Ninguna. El diseño no tiene implementación todavía, por lo que no existe deuda técnica de código — solo dos puntos de diseño declarados explícitamente como **trabajo futuro, no como deuda**:

1. La resolución jerárquica completa de plantillas (Empresa → Sucursal → Punto de Emisión) está habilitada por el diseño de `RideTemplateSelector` pero no implementada en v1.0 (§11) — extensión aditiva declarada, no un defecto.
2. Los tipos de comprobante distintos de `Invoice` (CreditNote, DebitNote, Retention, ShippingGuide, PurchaseSettlement) no tienen parser ni plantilla en v1.0 — mismo estado que sus builders en `ElectronicDocuments` (ADR-023, "Límites"): trabajo nuevo con su propio roadmap, no mantenimiento.

## Estado de implementación

- **Arquitectura:** COMPLETA.
- **Diseño:** CONGELADO.
- **Implementación:** NO INICIADA.

## Checklist final

| Criterio | Verificado |
|---|---|
| Clean Architecture | Sí — dependencias unidireccionales `API → Application → Domain`, `Infrastructure` implementa `Application` |
| DDD | Sí — `RideModel` y VOs como modelo de dominio neutro, `RidePdfDocument` como entidad con factory |
| SOLID | Sí — cada contrato con una sola responsabilidad, verificado uno por uno en la ronda de congelación de contratos |
| OCP | Sí — nuevo comprobante es aditivo (§10), nunca modifica el pipeline ni los resolvers |
| DIP | Sí — `Application` depende de interfaces (`IRideRenderer`, `IRideXmlParser`, etc.), nunca de QuestPDF/EF Core directamente |
| Strategy | Sí — parser, template y renderer resueltos por diccionario en memoria vía DI, cero `switch`/`if` por tipo |
| Multi-tenant | Sí — `ICompanyScopedRequest` en ambos contratos públicos, sin branding/plantillas/archivos compartidos |
| Reutilización | Sí — `RideModel` y sus VOs compartidos entre todos los parsers/plantillas presentes y futuros |
| Seguridad | Sí — XML nunca modificado, sin re-firma, sin recálculo tributario |
| Sin hardcode | Sí — branding vía proveedor, storage vía convención centralizada, sin rutas ni valores literales |
| Sin duplicidad | Sí — `RideRequest` como DTO redundante fue eliminado en la ronda de congelación de contratos; el request MediatR es el único DTO de entrada |
| Fuente única de verdad | Sí — el XML autorizado, con `RideOutcome.PendingSource` como manejo explícito de su ausencia temporal (H1) |

## Veredicto

**Design Frozen — Ready for Implementation.**

Justificación: las tres rondas previas (arquitectura, auditoría contra código real, congelación de contratos) no dejaron hallazgos abiertos — los 5 encontrados en la auditoría (H1–H5) fueron resueltos dentro del propio diseño, no diferidos. El diseño fue verificado contra el contrato real de `ElectronicDocuments` v1.0 (no en abstracto), lo cual replica el mismo criterio de cierre que se exigió a `ElectronicDocuments` antes de su propia ADR de cierre (evidencia real, no solo consistencia interna del diseño). La superficie pública es mínima (2 requests, 2 DTOs, 1 enum) y pasó la prueba de estabilidad a 5 años punto por punto. No hay deuda técnica de código porque no hay código — solo dos extensiones futuras explícitamente declaradas como fuera de alcance de v1.0, no como defectos.

## Alternativas consideradas

- **Congelar el diseño sin la ronda de auditoría contra código real**: descartada — hubiera dejado pasar H1 (la suposición no verificada de que `Authorized` implica XML disponible), que de haberse descubierto durante la implementación habría forzado a rediseñar la firma de `RideOutcome` a mitad de código, el mismo tipo de costo evitado en `ElectronicDocuments` por las pruebas reales contra el SRI antes de su cierre.
- **Exponer `IRideDocumentService` como interfaz pública inyectable en lugar de requests MediatR**: descartada en la ronda de congelación de contratos — habría introducido un segundo estilo de integración cross-módulo distinto al ya establecido por `ElectronicDocuments`, sin ningún beneficio que lo justifique.
