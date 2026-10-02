# SRI — Arquitectura de catálogos globales (ZH-SRI-GLOBAL-CATALOG-ARCHITECTURE-01)

> **Naturaleza:** auditoría + diseño (2026-10-02, commit `787971ab`). La decisión vinculante está en
> [ADR-037](../decisions/ADR-037-sri-global-catalog-governance.md); este documento aporta la evidencia y el
> diseño detallado. Base: [SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md](SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md).
> No se modificó código, seeds, tablas ni migraciones.
>
> **Corrección de nomenclatura respecto a la auditoría 2.34:** los nombres reales de tabla son **singulares** y
> viven en el esquema `global` (`global.sri_retention_code`, `global.sri_doc_type`, `global.sri_id_type`,
> `global.sri_payment_method`…). La auditoría anterior los citó en plural; el significado de sus hallazgos no cambia.

## A. Inventario de catálogos `sri_*`

Todas las entidades viven en `ERP.Domain/Modules/SriCatalogs/Entities/` y su configuración en
`ERP.Infrastructure/Persistence/Configurations/SriCatalogs/`. **Ninguna** tiene `TenantId` ni `CompanyId`
(verificado en las 15 entidades); todas usan `ToTable(..., schema: "global")`.

| Catálogo (tabla `global.*`) | Clave | Columnas | Seed (`HasData`) | Vigencia temporal | FK entrantes desde tablas de tenant | Consumidores en runtime |
|---|---|---|---|---|---|---|
| `sri_country` | `Code` (+ `Iso2` como clave alterna) | Code, Name, PhoneCode, IsActive | ~29 | No | 3 (Branch/Geography) | `GeographyReadRepository`, `SalesBootstrapStep` |
| `sri_doc_type` | `Code` | Code, Name, ShortName, IsElectronic, IsActive | 10 | No | 2 (`DocumentSequence`, `DocTypeSriMap`) | `SriCatalogLookupRepository`, `SriDocTypeCatalogResolver`, `ElectronicDocumentsBootstrapStep` |
| `sri_emission_type` | `Code` (short) | Code, Name | 2 | No | 0 | **ninguno** |
| `sri_error_code` | `Code` | Code, ErrorType, Name, Description, IsActive | 33 | No | 0 | **ninguno** (solo seed) |
| `sri_ice_rate` | `Code` | Code, Name, Percentage, UnitValue, CalculationType, IsActive | 14 | **No** (la tarifa cambia por normativa) | 0 (referencia por string) | `SriCatalogResolver`, `SriCatalogLookupRepository`, `SriTaxResolver`, `SriGlobalRateReader` |
| `sri_id_type` | `Code` | Code, Name, Digits | 6 | No | 1 (`sri_id_type_usage`) | `SriCatalogLookupRepository` |
| `sri_id_type_usage` | `Id` (Guid) | Id, IdTypeCode, UsageType, IsActive | 13 | No | — | `SriCatalogLookupRepository`, `IdentificationUsageValidator` |
| `sri_irbpnr_rate` | `Code` | igual que ICE | 1 | No | 0 | `SriTaxResolver` |
| `sri_payment_method` | `Code` | Code, Name, IsActive | 8 | **No** (Tabla 24 publica *fecha inicio/fin*) | 0 (string en `PaymentMethod.SriPaymentMethodCode`) | `SriCatalogLookupRepository`, `SriTaxResolver`, `PaymentMethodSriMappingBackfillService` |
| `sri_retention_code` | `Id` (Guid determinístico) + único `(TaxType, Code)` (`uq_sri_ret_code`) | Id, TaxType, Code, Name, Percentage, AppliesTo, IsActive | 20 | **No** | 1 (`SupplierRetentionDefault.SriRetentionCodeId`, Restrict) | ver §D/§E |
| `sri_supplier_type` | `Code` | Code, Name, IsActive | 2 | No | 0 | `SriCatalogLookupRepository` |
| `sri_tax_regime` | `Code` | Code, Name, Abbrev, IsActive | 4 | No | 1 (`Company.TaxRegimeCode`) | `SriCatalogLookupRepository`, 3 providers XML (texto RIMPE) |
| `sri_tax_support` | `Code` | Code, Name, IsActive | 20 | No | 0 | `SriCatalogLookupRepository` |
| `sri_uom` | `Code` | Code, Name, Abbrev, IsActive | 22 | No | 0 | `SriCatalogResolver`, `InvoiceItemSearchRepository`, `SriCatalogLookupRepository` |
| `sri_vat_rate` | `Code` | Code, Name, Percentage, IsActive, **ValidFrom, ValidUntil** | 9 | **Sí, pero no se usa** | 0 (string en ítems/líneas) | `SriCatalogResolver`, `SriCatalogLookupRepository`, `SriTaxResolver`, `SriGlobalRateReader` |

Fuera de `SriCatalogs` pero relacionados: `DocTypeSriMap` (mapa tipo interno → `sri_doc_type`), `SriSettings`
(tenant/empresa — **no** es catálogo global), `SystemProviderSettings` (instancia).

## B. Mecanismo actual de carga

| Mecanismo | Uso hoy para `sri_*` | Evidencia |
|---|---|---|
| `HasData` en `IEntityTypeConfiguration` | **Única fuente** de las filas de los 15 catálogos | `SriCatalogs/*Configuration.cs` |
| Migraciones EF | Materializan el `HasData`: la baseline `20260929143218_InitialEnterpriseBaseline` crea y siembra todo (25 bloques `InsertData`). Las 3 migraciones posteriores no tocan filas `sri_*` | `Migrations/*.cs` |
| Evolución previa a la baseline | Editar `HasData` → EF genera `InsertData`/`UpdateData` en la siguiente migración (p. ej. IRBPNR + `CalculationType`, commit `ef6b7d86`) | git history |
| Seeding steps (`Seeding/Steps/*`) | **No escriben** `sri_*`; solo leen (`ElectronicDocumentsBootstrapStep`, `SalesBootstrapStep`) | `Seeding/Steps/` |
| `PaymentMethodSriMappingBackfillService` (startup) | No escribe catálogo; rellena `PaymentMethod.SriPaymentMethodCode` de tenants contra el catálogo | `Program.cs:480-490` |
| JSON / SQL / CSV | **No existen** para catálogos (`Resources/SRI/Catalogs/` está vacío; su README prohíbe datos no oficiales) | `Resources/SRI/Catalogs/README.md` |
| Constantes C# | Solo códigos estructurales: `SriDocumentTypeCodes` (01/04/07), `SriRetentionTaxTypeCodes` (1/2), `SriTaxCategoryCodes` (2/3/5), `SriEnvironmentCodes` (1/2), `TaxIdentification.SriRuc…SriPlaca` (04–09) | Domain |
| Constantes frontend | `sriIdentificationCodes.ts` (04–09) y `sriEnvironmentCodes.ts` (1/2), documentadas como espejo de validación permitido por `architecture.md § Catálogos y datos configurables` | frontend |

**Consecuencias:** (1) instalación nueva y upgrade **ya** usan el mismo camino (migraciones), lo que es una
propiedad valiosa a conservar; (2) un cambio de valor en `HasData` produce un `UpdateData` **destructivo** sin
ninguna salvaguarda; (3) no hay trazabilidad normativa por fila.

## C. Duplicaciones y SSOT actuales

| Tema | Estado | Comentario |
|---|---|---|
| Datos de catálogo | ✅ una sola fuente (`HasData` → migración) | Conservar (ADR-037 D3) |
| Constantes estructurales C#/TS | ✅ legítimas | Códigos invariantes de la ficha (tipo de comprobante, categoría de impuesto, ambiente, tipo de identificación); el TS es espejo documentado |
| Lectores de IVA/ICE | 🟠 **4 vías** sobre las mismas tablas: `SriCatalogResolver`, `SriTaxResolver`, `SriGlobalRateReader`, `SriCatalogLookupRepository` | Ninguna resuelve por fecha; consolidar es trabajo posterior, no de este ticket |
| `ISriTaxResolver` en Purchases | ✅ alias (`interface ISriTaxResolver : Common.Services.ISriTaxResolver;`) | No es duplicación |
| Lectores de retención | 🟠 `RetentionCodeResolver` + `SriGlobalRateReader.GetRetentionPercentageAsync` + `SriCatalogLookupRepository` | Mismo patrón |
| Texto RIMPE | 🟠 3 copias de `ResolveContribuyenteRimpeText` | Ver auditoría 2.34 §L |
| Vigencia de IVA | ⚠️ columnas `ValidFrom/ValidUntil` sembradas pero **sin consumidor**; además el código `2` (12 %) cierra en 2016-05-31 aunque el 12 % volvió a regir 2017-06-01 → 2024-03-31 | Prueba de que "una fila por código" no puede representar historia (motiva D4/D5) |

## D. Consumidores por catálogo (detalle de `sri_retention_code`)

| Consumidor | Qué usa | Archivo |
|---|---|---|
| `SupplierRetentionDefault` (empresa) | **FK por `Id`** | `SupplierRetentionDefaultConfiguration.cs:52-57` |
| Add/Get/SetState SupplierRetentionDefault | `GetRetentionCodeByIdAsync` | `MasterData/UseCases/*` |
| `RetentionEligibilityService` | `IRetentionCodeResolver.GetRetentionCodeByIdAsync` | `Retentions/Services/RetentionEligibilityService.cs:89` |
| `RetentionCalculator` / `CalculateRetentionUseCases` | `RetentionCode` (string) + nombre + % | Domain/Purchases, Application/Purchases |
| `RetentionDocumentLine` | **snapshot** `RetentionCode` (string), `RetentionCodeDescription`, `RetentionRate`; **sin FK** | `RetentionDocumentLine.cs:24-37` |
| `RetentionElectronicDocumentDataProvider` | copia `line.RetentionCode` a `codigoRetencion` | `RetentionElectronicDocumentDataProvider.cs:233` |
| `RetentionXmlBuilder` | transcribe `RetentionCode` | `RetentionXmlBuilder.cs:264-286` |
| `SriGlobalRateReader.GetRetentionPercentageAsync` | % por código | `Infrastructure/Services/SriGlobalRateReader.cs:43` |
| `CatalogController` / `GetSriRetentionCodes` → frontend `useSriRetentionCodes` | listado activo por tipo | `CatalogController.cs:53` |
| Contabilidad | **no** usa el código (traductores por tipo de impuesto) | `RetentionDocumentIssuedPostingTranslator` |
| Exportación ATS | **no existe** en el ERP | `git grep Ats` |
| Tests | ~10 tests usan el literal `"725"` | `*.Tests` |

## E. Análisis de `sri_retention_code` y 721–728

Seed actual (IVA, `SriRetentionCodeConfiguration.cs:28-76`), con identidad estable ya existente:

| Id | Code | Name (seed) | % | Tabla 20 (XML) |
|---|---|---|---|---|
| `10000000-…-0001` | 721 | Ret. IVA 10% – Bienes | 10 | 9 |
| `10000000-…-0002` | 723 | Ret. IVA 20% – Servicios | 20 | 10 |
| `10000000-…-0003` | 725 | Ret. IVA 30% – Presuntivo bienes | 30 | 1 |
| `10000000-…-0004` | 726 | Ret. IVA 70% – Presuntivo servicios | 70 | 2 |
| `10000000-…-0005` | 727 | Ret. IVA 100% – Liq. compra / honorarios | 100 | 3 |
| `10000000-…-0006` | 728 | Ret. IVA 15% – Constructoras | 15 | **no existe en Tabla 20** |
| — | — | (falta) 50 % | 50 | 11 |
| — | — | (falta) retención en cero | 0 | 7 |
| — | — | (falta) no procede retención | 0 | 8 |

Hechos:
- **El significado de 721–728 no está documentado** en ninguna fuente del repo; existen sin cambios desde el commit inicial (`061e9b54`). Su forma sugiere casilleros de formulario o códigos de catálogo ATS, pero **NO SE CONFIRMA** sin fuente oficial → `AtsCode` queda **sin decidir**.
- Los nombres del seed ("Presuntivo bienes", "Liq. compra / honorarios") **no** coinciden con la Tabla 20, que solo define porcentaje → código.
- **Qué representan realmente hoy en el ERP:** la *clave de negocio visible* de un concepto de retención IVA (lo que el usuario elige, lo que guarda la línea como snapshot y lo que referencian tests). El ERP **no** los usa para ATS ni contabilidad. El único uso fiscal externo es el XML, donde son incorrectos.
- **No deben eliminarse:** hay FKs por `Id` (`SupplierRetentionDefault`), snapshots históricos en `retention_document_lines.retention_code` y XML ya firmados con esos valores (evidencia histórica, ADR-037 D12).
- Retenciones de **Renta** (303…344): la ficha 2.34 remite al catálogo ATS para códigos y porcentajes; el ejemplo oficial del Anexo 1 usa el mismo código en `codigoRetencion` (`323B1`). XML code = código ATS (fuente externa a la ficha); porcentajes **por confirmar** (p. ej. 312 = 1.00 %).
- ISD 4580 está en la misma tabla con `TaxType = "ISD"`; la Tabla 19 le asigna impuesto `6`, que el ERP no soporta en XML.

## F. Modelo temporal propuesto

Clasificación de catálogos (no todos necesitan vigencia):

| Clase | Catálogos | Modelo |
|---|---|---|
| **Enumeraciones normativas estables** | `sri_doc_type`, `sri_id_type`, `sri_id_type_usage`, `sri_emission_type`, `sri_country`, `sri_supplier_type`, `sri_tax_support`, `sri_uom`, `sri_error_code`, `sri_tax_regime` | Fila por código (+ `IsEnabled` según §F.2, + fuente, ver K). Retirar del uso = `IsEnabled=false`, nunca borrar |
| **Reglas con vigencia** | `sri_vat_rate`, `sri_ice_rate`, `sri_irbpnr_rate`, `sri_retention_code`, `sri_payment_method` (Tabla 24 con fecha inicio/fin) | **Concepto** (identidad estable, tabla existente) + **versión** tipada hija con vigencia |

Versión tipada por catálogo (ejemplo para retenciones; mismo patrón, tabla propia por catálogo — **no** mega tabla):

```
global.sri_retention_code            (existente — concepto/identidad)
  id (Guid estable) · tax_type · code (clave de negocio, p.ej. 725) · name · applies_to
  is_enabled (hoy is_active; renombre en el slice — ver §F.1)

global.sri_retention_code_version     (nueva, en el slice)
  id · retention_code_id (FK) · valid_from (date) · valid_until (date, null = vigente, inclusivo)
  percentage · xml_code (codigoRetencion; null = no emisible) · ats_code (null hasta fuente oficial)
  normative_source_id (FK)
  UNIQUE (retention_code_id, valid_from)
```

- **Vocabulario:** `ValidFrom`/`ValidUntil` inclusivos, porque es el precedente existente (`SriVatRate`, `PriceList`); no se introduce `EffectiveFrom/To`.
- **Fecha de resolución:** fecha de negocio del documento (`DateOnly`, ADR-034), nunca "hoy" ni `UpdatedAt`. Un reintento de un borrador/fallido después de un cambio normativo resuelve la regla de su fecha de emisión.
- **Cambio normativo:** se cierra `valid_until` de la versión anterior (único UPDATE permitido: `null → fecha`) y se inserta una versión nueva. Porcentaje, código y fuente de una versión existente nunca cambian.
- **No solapamiento:** invariante del catálogo, verificado por (1) `UNIQUE (concepto, valid_from)` en BD, (2) `SriCatalogComplianceTests` y (3) el resolver (más de una versión vigente = error). Un `EXCLUDE USING gist` requeriría la extensión `btree_gist`, sin precedente en el repo → **decisión diferida**, no necesaria para el slice. Precedente del patrón invariante + test: `AccountingPeriod` (ADR-026 §6.1).
- **Sin clase base artificial:** cada versión es una entidad tipada; lo común se impone por convención y por la suite de compliance, no por herencia.

### F.1 Habilitación operativa (`IsEnabled`) — dimensión independiente de la vigencia

Hay dos dimensiones que no se sustituyen entre sí:

| Dimensión | Campo | Pregunta que responde | Quién la usa |
|---|---|---|---|
| Vigencia normativa | `ValidFrom` / `ValidUntil` (en la versión) | ¿Qué dice la norma para un documento emitido en la fecha X? | Resolución fiscal |
| Habilitación operativa | `IsEnabled` (en el concepto, default `true`) | ¿Se puede elegir este valor en una operación o configuración **nueva**? | Selectores y configuración |

**Estado actual (evidencia):**
- 13 de 15 catálogos ya tienen `IsActive` (todos salvo `sri_emission_type` y `sri_id_type`), y su uso real es operativo: filtran listados y validan selección.
- Ningún catálogo `sri_*` tiene `HasQueryFilter`: los históricos no están ocultos globalmente. Conservar.
- **Conflictos con la regla nueva:** varios métodos exigen `IsActive` incluso al resolver por Id o código para documentos existentes. Si hoy se deshabilitara un valor, se romperían el recálculo o la regeneración de documentos históricos:

| Método | Problema |
|---|---|
| `RetentionCodeResolver.GetRetentionCodeByIdAsync` (`RetentionCodeResolver.cs:35`) | Por Id **con** filtro `IsActive` |
| `RetentionCodeResolver.GetRetentionCodeAsync` (`:21`) | Por código con filtro |
| `SriTaxResolver.GetVatRate*/GetIceRate*/GetIrbpnr*/GetPaymentMethodName` (`SriTaxResolver.cs:18-111`) | Resolución por código con filtro |
| `SriGlobalRateReader` (`:24, :37, :50`) | Idem |
| `SriCatalogResolver.ResolveVatRates/ResolveIceRates` (`:36, :50`) | Idem |
| `SriDocTypeCatalogResolver.IsActiveElectronicDocTypeAsync`, usado por `RetentionElectronicDocumentDataProvider.cs:142` al **generar el XML** | Deshabilitar un tipo de comprobante impediría regenerar un borrador ya emitido |
| `SupplierRetentionDefaultDto` (`:38`) | Expone `IsActive` del catálogo (lectura correcta para mostrar el estado; no filtra) |

- `SriCatalogLookupRepository.GetRetentionCodeByIdAsync` **no** filtra: ya cumple "by Id including disabled".
- Los filtros `c.IsActive && c.SriTaxCategoryCode …` de Ítems/Compras pertenecen a la configuración tributaria del ítem (entidad de tenant), **no** al catálogo SRI.

**Decisión de implementación (ver ADR-037 D13):** `IsEnabled` es **el mismo concepto** que el `IsActive` actual
de los catálogos globales. No se agrega un segundo booleano. Cada catálogo renombra `IsActive → IsEnabled`
(propiedad y columna) dentro de su propio slice, junto con la separación de métodos. Los dos nombres nunca
coexisten en la misma tabla.

**Contrato de lectura por catálogo** (en el repositorio o resolver existente de ese catálogo; los módulos
funcionales nunca filtran `IsEnabled` por su cuenta):

| Método | Filtra `IsEnabled` | Filtra vigencia | Uso |
|---|---|---|---|
| `GetSelectable…(referenceDate?)` | **Sí** | Sí, si el catálogo es temporal (versión vigente a la fecha de referencia) | Selectores, alta y edición de configuraciones (p. ej. `SupplierRetentionDefault`) |
| `ResolveForDate…(key, documentIssueDate)` | **No** | **Sí** | Resolución fiscal (XML, cálculo de documentos existentes); fail-closed según §H |
| `GetByIdIncludingDisabled…(id)` | **No** | No | Mostrar referencias históricas y configuraciones existentes |

Reglas complementarias:
- Una configuración existente que apunta a un valor deshabilitado se **muestra** (`GetByIdIncludingDisabled`) con su estado. El servicio de elegibilidad la reporta como "configuración no seleccionable" y no la aplica en silencio a un documento nuevo; tampoco la borra.
- Deshabilitar un valor no modifica documentos, snapshots, XML ni versiones.
- Nunca se usa `HasQueryFilter` para `IsEnabled`.
- No se borran filas de catálogos oficiales por obsolescencia.
- Las tablas de versión (`*_version`) y `sri_normative_source` **no** llevan `IsEnabled`: la versión se gobierna por vigencia; la fuente es un registro de auditoría.

### F.1.1 Datos internos del sistema (ADR-037 D14)

Los catálogos globales SRI son **INTERNAL SYSTEM DATA**:

| Aspecto | Regla | Evidencia del estado actual |
|---|---|---|
| CRUD / UI de edición | No existe ni se crea en este alcance | Ningún `Add/Update/Remove` sobre `DbSet<Sri*>` fuera de migraciones |
| Edición por tenant/empresa | No | Tablas en `global`, sin `TenantId`/`CompanyId` |
| Edición por `PlatformAdmin` | No | El menú "Proveedor SRI" de admin-core es `SystemProviderSettings`, no un catálogo |
| Activar/desactivar por usuario | No | `IsEnabled` solo cambia por migración en un release de ZH Technologies |
| Lectura | Sí, solo lectura | `CatalogController`: 12 `GET api/v1/catalog/sri-*` / afines → semántica `GetSelectable…` |
| Modificación manual futura | **OUT OF SCOPE** — requiere diseño, ADR y autorización propios | — |

`IsEnabled` = disponibilidad operativa actual (interna) · `ValidFrom`/`ValidUntil` = vigencia normativa. Las
operaciones nuevas exigen ambas condiciones; los históricos leen también los deshabilitados.

### F.2 Clasificación de los 15 catálogos

A = requiere `IsEnabled` · B = requiere además temporalidad (concepto + versión) · C = no requiere `IsEnabled`

| Catálogo | Clase | `IsActive` hoy | Justificación |
|---|---|---|---|
| `sri_country` | **A** | Sí | Seleccionable en direcciones; FK desde Branch/Geography; un país no "expira" normativamente |
| `sri_doc_type` | **A** | Sí | Seleccionable para secuencias/mapeos (FK); la Tabla 3 es estable. Su uso en la generación de XML debe pasar a `ResolveForDate`/existencia, no a `IsEnabled` |
| `sri_emission_type` | **C** | No | Solo existe emisión normal (`1`) en offline (Tabla 2); no seleccionable; sin consumidores |
| `sri_error_code` | **C** | Sí (sin consumidor) | Catálogo de referencia/diagnóstico; un mensaje histórico del SRI siempre debe poder interpretarse; no seleccionable. El `IsActive` existente se documenta como sin efecto operativo |
| `sri_ice_rate` | **A + B** | Sí | Seleccionable en ítems; tarifas cambian por norma (Tabla 18) |
| `sri_id_type` | **C** | No | Códigos estructurales (Tabla 6) con constantes en Domain/TS; su disponibilidad por contexto ya la gobierna `sri_id_type_usage` |
| `sri_id_type_usage` | **A** | Sí | Es precisamente la habilitación de un tipo de identificación por contexto de uso |
| `sri_irbpnr_rate` | **A + B** | Sí | Tarifa específica sujeta a cambio normativo |
| `sri_payment_method` | **A + B** | Sí | Seleccionable (mapeo de formas de pago del tenant); la Tabla 24 publica fecha inicio/fin |
| `sri_retention_code` | **A + B** | Sí | Seleccionable (defaults de proveedor, líneas); porcentajes y representación cambian (Tabla 20 / ATS) |
| `sri_supplier_type` | **A** | Sí | Seleccionable en el rol proveedor |
| `sri_tax_regime` | **A** | Sí | Seleccionable en Company (FK). La vigencia del régimen es un dato de la empresa, no del catálogo; si el texto legal RIMPE cambia por norma, se evaluará temporalidad en `ZH-SRI-ISSUER-FISCAL-PROFILE-01` |
| `sri_tax_support` | **A** | Sí | Seleccionable (sustento tributario de compras); catálogo ATS cuyos códigos pueden retirarse |
| `sri_uom` | **A** | Sí | Seleccionable en ítems |
| `sri_vat_rate` | **A + B** | Sí (+ `ValidFrom/ValidUntil` sin uso) | Seleccionable en ítems; tarifas cambian (Tabla 17); requiere corregir la historia del 12 % |

Resumen: **A** = 13 (de los cuales **B** = 5: IVA, ICE, IRBPNR, formas de pago, retenciones) · **C** = 2 sin `IsEnabled`
(`sri_emission_type`, `sri_id_type`) + `sri_error_code` sin efecto operativo. Tablas nuevas de versión y de fuentes
normativas: sin `IsEnabled`.

## G. Identidad vs representación

| Concepto | Dónde vive | Ejemplo (IVA 30 %) |
|---|---|---|
| Identidad interna estable | `sri_retention_code.id` | `10000000-…-0003` |
| Clave de negocio visible / snapshot | `sri_retention_code.code` | `725` (se conserva) |
| Tipo de impuesto | `sri_retention_code.tax_type` → código XML vía `SriRetentionTaxTypeCodes` | IVA → `2` |
| Porcentaje vigente | `sri_retention_code_version.percentage` | 30 |
| Código XML (`codigoRetencion`) | `sri_retention_code_version.xml_code` | `1` (Tabla 20) |
| Código ATS | `sri_retention_code_version.ats_code` | **null — sin fuente** |
| Fuente | `normative_source_id` | Ficha 2.34, Tabla 20 |

`sri_retention_code.percentage` actual queda como valor heredado mientras haya consumidores; en el diseño la fuente
de verdad del porcentaje vigente es la versión. Los módulos de negocio (Compras/Gastos/Retenciones) siguen
trabajando con el concepto (`Id`/`Code` + %); **solo** la capa fiscal traduce a `xml_code`.

## H. Resolvers

Extender el resolver existente (`IRetentionCodeResolver`, impl. `Infrastructure/Persistence/Services/RetentionCodeResolver.cs`), no crear uno paralelo:

```csharp
// Contrato conceptual — no implementado.
Task<Result<SriRetentionRepresentation>> ResolveForElectronicDocumentAsync(
    RetentionTaxType taxType, string businessCode, decimal appliedRate, DateOnly documentIssueDate, CancellationToken ct);

record SriRetentionRepresentation(string TaxTypeXmlCode, string XmlCode, decimal Percentage, Guid NormativeSourceId);
```

Este método es el `ResolveForDate…` del contrato de §F.1: **no** filtra `IsEnabled`. Un concepto deshabilitado
después de crear el documento sigue resolviendo según su vigencia. Junto a él, el resolver expone
`GetSelectable…` (selección y defaults nuevos) y `GetByIdIncludingDisabled…` (lectura histórica). Los actuales
`GetRetentionCodeAsync`/`GetRetentionCodeByIdAsync`, que filtran `IsActive`, se reasignan a la semántica
"seleccionable" o se reemplazan por la variante correcta en el slice.

Reglas (fail-closed, código de error dedicado de configuración fiscal):
- 0 versiones vigentes a la fecha → error de configuración fiscal.
- más de 1 → error de configuración fiscal (catálogo corrupto).
- `xml_code` nulo → error ("concepto sin representación oficial", p. ej. 15 %).
- `appliedRate` de la línea ≠ `percentage` de la versión → error (nunca emitir un código de 30 % para una línea al 20 %).
- Nunca fallback a `Code`, nunca diccionario en código.

Punto de llamada: `RetentionElectronicDocumentDataProvider` (Application, con acceso legítimo a datos), que ya
resuelve `SriTaxTypeCode`. `RetentionXmlBuilder` **no cambia**: sigue transcribiendo el valor recibido.
Clave de resolución en el slice: `(TaxType, Code, fecha)`, porque es único (`uq_sri_ret_code`) y funciona para las
líneas existentes sin cambiar el esquema de `retention_document_lines`. Agregar `SriRetentionCodeId` a la línea es
una mejora posterior opcional.

## I. Instalación nueva

`dotnet ef database update` aplica la baseline (catálogo inicial) y luego, en orden, las migraciones incrementales
de catálogo. No hay un camino distinto para instalaciones nuevas: **misma secuencia, mismo resultado** que un
upgrade. Prohibido: seeding steps que escriban `sri_*`, scripts SQL manuales, JSON leído en startup.

## J. Upgrades

- Toda evolución normativa = **migración incremental nueva** (nunca editar `InitialEnterpriseBaseline`).
- `HasData` sigue siendo la fuente que alimenta el model snapshot. Regla de edición: **solo agregar** filas
  (conceptos/versiones/fuentes) y cerrar `ValidUntil` (`null → fecha`). Cualquier otro cambio sobre una fila
  existente es una violación, y la suite de compliance la detecta comparando contra un manifiesto aprobado
  (golden file con la huella de cada versión).
- La migración generada se revisa en el PR: debe contener solo `InsertData` y `UpdateData` de `valid_until`.
- Idempotencia: la garantiza el historial de migraciones de EF (`__EFMigrationsHistory`); un test verifica que
  "BD vacía + todas las migraciones" produce el mismo catálogo que el `HasData` del modelo.

## K. Trazabilidad normativa

Tabla tipada pequeña, referenciada por versión (no repetir textos en cada fila):

```
global.sri_normative_source
  id · document (p.ej. "FICHA_TECNICA_OFFLINE", "RESOLUCION", "CATALOGO_ATS")
  version ("2.34") · section ("Tabla 20") · published_on · reference_url · document_sha256 (nullable)
  introduced_in_migration (nombre de la migración EF que la incorporó) · notes
```

Responde: de dónde salió el código (`section`), qué ficha/resolución (`document`+`version`), desde/hasta cuándo
(`valid_from/valid_until` de la versión), qué release lo incorporó (`introduced_in_migration` ↔ `__EFMigrationsHistory`
y git tag). Para la ficha 2.34 auditada: SHA-256 `7333aebf…13c9`.
Los catálogos sin vigencia (clase "estable") pueden adoptar `normative_source_id` de forma incremental, catálogo por catálogo.

## L. Estrategia de Compliance Tests (`SriCatalogComplianceTests`)

Ubicación: `ERP.Infrastructure.Tests/Persistence/SriCatalogs/`, extendiendo el patrón ya existente de
`SriDocTypeSeedAlignmentTests` / `SriIceAndIrbpnrSeedAlignmentTests` (leen el `HasData` desde un `ModelBuilder`
aislado, sin BD).

| Familia | Verifica | Tipo |
|---|---|---|
| Globalidad | Ninguna entidad de `SriCatalogs` implementa `ITenantScopedEntity`/`ICompanyScopedEntity` ni tiene `TenantId`/`CompanyId`; esquema `global` | Reflexión |
| No solapamiento | Por concepto, las versiones no se solapan; como máximo una con `ValidUntil = null` | Seed |
| Cobertura obligatoria | Tabla 20 IVA completa (9, 10, 1, 11, 2, 3, 7, 8) con porcentaje coherente; tablas 3/6/16/17/24 presentes | Seed vs. golden normativo |
| Unicidad de resolución | Para cada fecha frontera (inicio/fin de vigencias) exactamente una versión por concepto | Seed |
| Consistencia | `percentage` en rango, `xml_code` con formato del XSD (≤5), `CalculationType` coherente con `Percentage/UnitValue` | Seed |
| Fail-closed | Resolver: 0 / >1 / `xml_code` nulo / tasa distinta → error | Unit |
| Datos internos (D14) | Ningún controller expone `POST/PUT/PATCH/DELETE` sobre catálogos `sri_*`; Application/API no escriben `DbSet<Sri*>` (solo migraciones) | Arquitectura (`ERP.Architecture.Tests`) |
| Habilitación operativa | `GetSelectable` excluye deshabilitados; `ResolveForDate` y `GetByIdIncludingDisabled` los incluyen; ninguna entidad `SriCatalogs` tiene `HasQueryFilter`; los catálogos de clase C y las tablas de versión/fuente no tienen `IsEnabled`; ninguna tabla tiene `IsActive` e `IsEnabled` a la vez | Unit + reflexión |
| Inmutabilidad | Huella de versiones existentes = manifiesto aprobado; solo se admiten altas y cierre de `ValidUntil` | Golden file |
| Equivalencia install/upgrade | BD vacía + migraciones == `HasData` del modelo | Integración PostgreSQL |
| Trazabilidad | Toda versión tiene `normative_source_id` válido | Seed |

## M. Componentes

| Componente | Decisión |
|---|---|
| Esquema `global` + 15 entidades tipadas | **KEEP** |
| `HasData` + migraciones como único canal | **KEEP** (+ reglas de edición de J) |
| `sri_retention_code` (Id estable, Code, `uq_sri_ret_code`) | **KEEP / EXTEND** (versión hija) |
| `sri_vat_rate.ValidFrom/ValidUntil` | **EXTEND** (modelo concepto+versión cuando se aborde IVA; corregir historia del 12 %) |
| `sri_ice_rate`, `sri_irbpnr_rate`, `sri_payment_method` | **EXTEND** (vigencia, en slices propios) |
| `IRetentionCodeResolver` / `RetentionCodeResolver` | **EXTEND** (resolución temporal fail-closed) |
| `SriCatalogResolver`, `SriTaxResolver`, `SriGlobalRateReader`, `SriCatalogLookupRepository` | **KEEP** ahora; consolidar la resolución temporal por catálogo después (sin 5.º lector genérico) |
| Constantes estructurales C#/TS | **KEEP** |
| Seed alignment tests | **EXTEND** → `SriCatalogComplianceTests` |
| Uso de `Code` como `codigoRetencion` | **REPLACE** (por `xml_code` resuelto) |
| `IsActive` de los catálogos clase A | **REPLACE** por `IsEnabled` (mismo concepto, renombre por slice; nunca dos flags) |
| Filtros `IsActive` dentro de métodos de resolución por Id/código (§F.1) | **REPLACE** por el contrato `GetSelectable` / `ResolveForDate` / `GetByIdIncludingDisabled` |
| Ausencia de `HasQueryFilter` en `SriCatalogs` | **DO NOT TOUCH** |
| Endpoints `GET api/v1/catalog/sri-*` (solo lectura) | **KEEP** (semántica `GetSelectable`); no se agregan endpoints de escritura (D14) |
| `RetentionXmlBuilder`, XSD, firma, SOAP, ConsultaComprobante | **DO NOT TOUCH** |
| `InitialEnterpriseBaseline` | **DO NOT TOUCH** |
| XML firmados/autorizados almacenados | **DO NOT TOUCH** (evidencia histórica) |

## O. Impacto previsto del primer vertical slice — `ZH-SRI-RETENTION-CATALOG-SSOT-01`

**Alcance**
1. Migración incremental: `global.sri_normative_source` + `global.sri_retention_code_version`.
2. Datos:
   - Fuentes: Ficha 2.34 Tabla 20 (IVA); Catálogo ATS (Renta, externo a la ficha).
   - Versiones de los 5 conceptos IVA con `xml_code` de la Tabla 20 (721→9, 723→10, 725→1, 726→2, 727→3).
   - Conceptos nuevos IVA 50 % (11), 0 % (7) y no procede (8), con `Id`/`Code` a decidir.
   - `728` (15 %): versión **sin** `xml_code` → no emisible hasta tener fuente (decisión: desactivar o documentar).
   - Renta: versiones con `xml_code = Code` y porcentaje actual, marcadas con fuente ATS **pendiente de verificación**.
3. `RetentionCodeResolver`: método de resolución temporal fail-closed (`ResolveForDate`), más `GetSelectable` y
   `GetByIdIncludingDisabled`; `sri_retention_code.is_active → is_enabled`. `RetentionEligibilityService` y los
   use cases de `SupplierRetentionDefault` consumen la variante que les corresponde (sin filtros propios).
4. `RetentionElectronicDocumentDataProvider`: usa el resolver con la fecha de emisión de la retención.
5. `SriCatalogComplianceTests` (familias de retención) + tests del resolver + tests del provider.

**Fuera del slice:** endpoints o UI para editar catálogos (D14).

**Sin cambios:** `RetentionXmlBuilder`, XSD, firma, SOAP, ConsultaComprobante, `RetentionDocumentLine` (esquema),
`SupplierRetentionDefault`, Compras/Gastos, contabilidad, frontend (sigue mostrando `Code`/`Name` del catálogo).

**Riesgos y verificaciones previas**
- Retenciones ya emitidas con `725`: las autorizadas conservan su XML como evidencia (no se regeneran); las que estén en `Draft/Failed` se regenerarán con el código correcto. Ejecutar la Q9 de la auditoría 2.34 en el piloto.
- El RIDE de retención muestra el `codigoRetencion` del XML autorizado: pasará a mostrar el código de la Tabla 20.
- Toca el módulo Retenciones (ADR-036) y la salida XML de la infraestructura CLOSED ElectronicDocuments → requiere nota de extensión controlada (ADR-023, causa "cambio obligatorio SRI").
- Cierre real del P0: autorización de una retención Renta + IVA en `celcer` (certificado de pruebas).

## P. Decisiones abiertas

| Id | Decisión | Dueño |
|---|---|---|
| DR-1 | Significado oficial de 721–728 y si existe `AtsCode` para IVA | Fiscal/contador + fuente SRI |
| DR-2 | Concepto 15 % (`728`): desactivar o fuente oficial | Fiscal |
| DR-3 | Códigos internos para IVA 50 %/0 %/no procede | Producto + fiscal |
| DR-4 | Porcentajes vigentes de Renta (catálogo ATS) | Fiscal |
| DR-5 | Constraint `EXCLUDE` con `btree_gist` (además de tests) | Arquitectura |
| DR-6 | Si `sri_tax_regime` necesita temporalidad (texto legal RIMPE) — se decide en `ZH-SRI-ISSUER-FISCAL-PROFILE-01` | Fiscal + arquitectura |
