# SRI — Arquitectura de cumplimiento electrónico (ZH-SRI-ARCHITECTURE-DESIGN-01)

> **Naturaleza:** auditoría + diseño (2026-10-02, commit `2b8b5a70`). La decisión vinculante está en
> [ADR-038](../decisions/ADR-038-sri-electronic-compliance-architecture.md); este documento aporta la evidencia y el
> diseño detallado. Bases: [SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md](SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md),
> [SRI-GLOBAL-CATALOG-ARCHITECTURE.md](SRI-GLOBAL-CATALOG-ARCHITECTURE.md) / ADR-037, ADR-023, ADR-024, ADR-025, ADR-031,
> ADR-034, ADR-036. **No se modificó código, tablas, migraciones, XML, catálogos ni frontend.** Los contratos C#
> de este documento son **conceptuales**: no existen en el código hasta que el ticket de su fase los implemente.

**Pregunta de entrada para todo cambio SRI futuro:**

| Pregunta | Destino | Sección |
|---|---|---|
| ¿Es un catálogo (código, tarifa, porcentaje, representación)? | ADR-037 (resolver del catálogo) | G |
| ¿Es un dato fiscal común del emisor? | Issuer Fiscal Profile | H |
| ¿Es un `campoAdicional`? | AdditionalInfo Composer (contributor) | I |
| ¿Es estructura XML de un tipo concreto? | Typed body + builder del tipo | D, K |
| ¿Es una versión de ficha o de XSD? | Document Specification | F |
| ¿Es transporte / protocolo SRI? | SRI Gateway | L |
| ¿Es estado fiscal / semántica de respuesta? | `ElectronicDocument` + ConsultaComprobante + interpretación tipada | M |
| ¿Es recuperar algo varado? | Recovery capability | N |
| ¿Es entregar al cliente? | Communications | O |
| ¿Es un requisito de un sector? | Extensión tipada / contributor sectorial | P |

---

## A. Mapa de la arquitectura actual (verificado en código)

### A.1 Pipeline común (único — `ElectronicDocumentIssuer`)

```
Módulo origen ──post-commit──► IElectronicDocumentIssuer.RegisterAsync         (crea ED en Draft y persiste)
                                └─ RunPipelineAsync
                                   ├─ [guard de origen, si existe]             IElectronicDocumentSourceLifecycleGuard (hoy solo Retentions)
                                   ├─ IElectronicDocumentXmlSupplierResolver.Resolve(tipo)
                                   │    ├─ supplier explícito (Retention)      RetentionElectronicDocumentXmlSupplier
                                   │    └─ fallback comercial (01/04)           CommercialElectronicDocumentXmlSupplier(provider, builder)
                                   │         → ElectronicDocumentXml(Xml, Version, Environment, AccessKey, ...)
                                   ├─ document.SetEnvironment(xml.Environment)
                                   ├─ IElectronicDocumentSchemaValidator       (XSD, fail-closed; sin XSD = inválido)
                                   ├─ IElectronicDocumentSigningService        → IElectronicDocumentSigner → XadesBesSigner
                                   ├─ IElectronicDocumentXmlStorageService     (draft + signed)
                                   ├─ sin guard: MarkXmlGenerated(xmlVersion, schemaVersion) + MarkSigned → SaveChanges
                                   │  con guard: ClaimDispatchAsync → XmlGenerated→Signed→Dispatching bajo lock del origen
                                   ├─ TrySendToReceptionAsync                  IElectronicDocumentReceptionService → ISriReceptionClient → SriSoapClient.SendAsync
                                   │    RECIBIDA → Received · [70]/[43]/[45] → Received · resto → Rejected
                                   └─ AuthorizeAsync                           IElectronicDocumentAuthorizationService → ISriAuthorizationClient → SriSoapClient.CheckAuthorizationAsync
                                        AUTORIZADO → StoreAuthorized + Authorized · NO AUTORIZADO → Rejected · TIMEOUT → sigue Received
Estado fiscal posterior:  ISriDocumentStatusQuery → SriDocumentStatusQuery → SriSoapClient.QueryDocumentStatusAsync (ConsultaComprobante)
RIDE:                     RideDocumentService → ElectronicDocumentRideSourceXmlProvider (XML autorizado) → IRideXmlParser por tipo → QuestPdfRideRenderer
Monitor/diagnóstico:      ElectronicDocumentsController (list/dashboard/detail/by-source/timeline/xml/retry/register), ElectronicDocumentAudit (timeline)
```

### A.2 Por tipo documental

| Etapa | Factura (01) | Nota de crédito (04) | Retención (07) |
|---|---|---|---|
| Disparo | `AuthorizeSalesUseCases` post-commit | `AuthorizeSalesReturnUseCases` post-commit | `IRetentionElectronicTransmission` post-commit de Compra/Gasto |
| Supplier | `CommercialElectronicDocumentXmlSupplier` (fallback del resolver) | ídem | `RetentionElectronicDocumentXmlSupplier` → `IRetentionElectronicDocumentXmlService` (módulo Retentions) |
| Provider | `SalesInvoiceElectronicDocumentDataProvider` | `SalesReturnCreditNoteDataProvider` | `RetentionElectronicDocumentDataProvider` (+ `IRetentionCodeResolver`, ADR-037) |
| Modelo | `ElectronicDocumentData` | `ElectronicDocumentData` (+ `Reason`, `ModifiedDocument`) | `RetentionElectronicDocumentData` (modelo hermano) |
| Builder | `InvoiceXmlBuilder` 1.1.0 (`ISriTaxCategoryCodeResolver`) | `CreditNoteXmlBuilder` 1.1.0 | `RetentionXmlBuilder` 1.0.0 (`IRetentionXmlBuilder`) |
| XSD | `InvoiceXmlSchemaValidator` 1.1.0 | `CreditNoteXmlSchemaValidator` 1.1.0 | `RetentionXmlSchemaValidator` 1.0.0 |
| Guard / `Dispatching` | No (pipeline v1.0) | No | Sí (ADR-036) |
| `infoAdicional` | "Observación" si `invoice.Notes` | `[]` | `[]` |
| Reintento | `RetryAsync` (reenvío de `Signed` permitido) | ídem | `RetryGuardedAsync` (solo consulta) |
| Recovery automático | `ElectronicDocumentRetryJob` (**inoperante**: 0 candidatos por filtro de tenant, auditoría §G) | ídem | `RetentionElectronicRecoveryJob` (origen sin ED / ED Draft; verificación de anulaciones) |
| ConsultaComprobante | No | No | Sí (anulación, `RetentionAnnulmentService`) |
| Anulación SRI | No aplica (flujo comercial) | No | ADR-036 (`AnnulmentPending` → `Cancelled` con evidencia) |
| RIDE | `InvoiceRideXmlParser` + `DefaultInvoiceRideTemplate` | `CreditNoteRideXmlParser` + `CreditNoteRideTemplate` | `RetentionRideXmlParser` + `RetentionRideTemplate`; vista previa al vuelo vía `GenerateRetentionRidePdfUseCases` → `RetentionRidePdfService` |
| Correo | `SalesInvoiceAuthorizedCommunicationHandler` (Communications, XML + RIDE) | No | No |

### A.3 Diferencias legítimas (no se igualan)

- **Forma del cuerpo:** la retención no tiene detalle comercial ni totales de venta. El modelo hermano
  (`RetentionElectronicDocumentData`) es correcto y se conserva.
- **Guard / `Dispatching` / reintento solo-consulta:** propio de orígenes anulables con efectos fiscales (ADR-036).
  Ventas/NC mantienen el pipeline v1.0 hasta que un ticket demuestre la necesidad.
- **Anulación oficial SRI:** normativa propia de retenciones (ADR-036 §2).
- **Plantillas RIDE:** cada tipo tiene la suya (ADR-025).
- **Versión XML:** 1.1.0 / 1.1.0 / 1.0.0. No tienen por qué coincidir (§F).

### A.4 Duplicaciones demostradas (no legítimas)

| Lógica | Copias | Evidencia |
|---|---|---|
| Clave de acceso + código numérico + módulo 11 | 3 | `BuildAccessKeyDigits`/`ComputeNumericCode`/`ComputeCheckDigit` en los 3 builders (los comentarios lo justifican como "duplicación puntual de un algoritmo cerrado") |
| `infoAdicional` (`BuildInfoAdicional` + `MaxAdditionalFields = 15`) | 3 | los 3 builders |
| `infoTributaria` (incluida la ubicación de `contribuyenteRimpe`) | 3 | los 3 builders |
| Texto RIMPE (`ResolveContribuyenteRimpeText`) | 3 | los 3 providers; además, desactualizado para Negocio Popular (auditoría §L) |
| Proyección del emisor (Company + matriz + SriSettings → `ElectronicDocumentIssuerData`, ambiente, tipo de emisión) | 3 | los 3 providers |
| Versión activa por tipo | 3 fuentes | constante `XmlVersionValue` del builder, constante `SchemaVersionValue` del validador y `manifest.json` `activeVersion` (Retention = `null` aunque está activa) |
| Resolución de `WsdlUrl` por empresa | 3 | `ElectronicDocumentReceptionService`, `ElectronicDocumentAuthorizationService`, `RetentionAnnulmentService` |
| `Utf8StringWriter` | 3 | trivial (P3) |

> Corrección a la auditoría 2.34 §K: en `manifest.json`, `CreditNote.activeVersion` **ya es `"1.1.0"`**; solo
> `Retention` sigue en `null`.

---

## B. Arquitectura objetivo (con nombres existentes)

```
                    Módulos de negocio (Sales, Retentions, futuros)
                                     │  agregado + snapshots
                                     ▼
            Fiscal Source Adapter = provider existente del tipo
            IElectronicDocumentDataProvider / IRetentionElectronicDocumentDataProvider / futuros
                                     │  usa ▼ (no reimplementa)
        ┌────────────────────────────┼─────────────────────────────┐
        ▼                            ▼                             ▼
 Issuer Fiscal Profile        SRI Catalog resolvers          (campos propios del documento:
 (NUEVO, fase 2)              (ADR-037, existentes)           "Observación", etc.)
 Company → ElectronicDocument-
 IssuerData + emisión
        └────────────────────────────┼─────────────────────────────┘
                                     ▼
            Modelo fiscal canónico = sobre común + cuerpo tipado
            (EmissionContext + IssuerData + AdditionalInfo) + ElectronicDocumentData | RetentionElectronicDocumentData | ...
                                     │
                                     ▼
            Orquestador de ensamblado fiscal (existente: CommercialElectronicDocumentXmlSupplier,
            RetentionElectronicDocumentXmlService)  ◄── AdditionalInfo Composer (NUEVO, fase 1)
                                     │                    contributors: RUC Proveedor (SystemProviderSettings),
                                     │                    Gran Contribuyente, sectoriales...
                                     ▼
            Document Specification (versión XML del builder + versión de ficha; fase 3)
                                     ▼
            Builder específico del tipo (Invoice/CreditNote/RetentionXmlBuilder) + writer común mínimo (fase 2/7)
                                     ▼
            XSD (IElectronicDocumentSchemaValidator + EmbeddedXmlSchemaProvider)         ── KEEP
                                     ▼
            Firma (ElectronicDocumentSigningService → XadesBesSigner)                      ── DO NOT TOUCH
                                     ▼
            SRI Gateway  ISriReceptionClient · ISriAuthorizationClient · ISriDocumentStatusQuery (→ SriSoapClient)
                         + servicios por empresa: Reception / Authorization / (NUEVO) Status
                                     ▼
            ElectronicDocument (agregado, máquina de estados ADR-023/036) + interpretación tipada de respuestas
                                     ▼
            Recovery (jobs + Monitor; capacidad transversal, ZH-EDOC-RECOVERY-ARCHITECTURE-01)
                                     ▼
            Evento ElectronicDocumentAuthorizedEvent ──► Communications (correo, adjuntos, reintentos)
```

No se crea una capa nueva donde ya existe una pieza: el "Fiscal Source Adapter" es el provider, el
"Canonical Fiscal Model" son los records actuales, el "SRI Gateway" son las tres interfaces de
`Application/Common/Interfaces/SRI`. Las únicas piezas nuevas son el **AdditionalInfo Composer** (fase 1), el
**Issuer Fiscal Profile** (fase 2), la **versión de ficha** en la especificación (fase 3) y el **servicio de estado
por empresa** (fase 4/5).

---

## C. Componentes reutilizados

| Componente | Papel en la arquitectura objetivo |
|---|---|
| `ElectronicDocumentIssuer` (`RegisterAsync`/`RunPipelineAsync`/`RetryAsync`/`RetryGuardedAsync`) | Único pipeline y único dueño de transiciones |
| `ElectronicDocument` + `ElectronicDocumentState` + eventos + `ElectronicDocumentAudit` | Estado y evidencia de cada etapa (timeline = SSOT de timestamps) |
| `IElectronicDocumentXmlSupplier` + resolver | Costura genérica por tipo (agnóstica del cuerpo) |
| `CommercialElectronicDocumentXmlSupplier` / `RetentionElectronicDocumentXmlService` | Orquestadores de ensamblado fiscal: punto único donde el modelo llega al builder |
| `IElectronicDocumentDataProvider` / `IRetentionElectronicDocumentDataProvider` | Fiscal Source Adapters |
| `ElectronicDocumentData` / `RetentionElectronicDocumentData` + records compartidos | Modelo fiscal canónico (sobre + cuerpo) |
| Builders (3) | Estructura XML por tipo |
| Validadores XSD + `EmbeddedXmlSchemaProvider` + `manifest.json` | Preflight fail-closed |
| `XadesBesSigner` / `XadesSignedXml` | Firma |
| `SriSoapClient` + `ISriReceptionClient` / `ISriAuthorizationClient` / `ISriDocumentStatusQuery` | SRI Gateway |
| `SriFiscalStatus` / `SriStatusQueryOutcome` | Modelo de semántica tipada a replicar en recepción/autorización |
| `IElectronicDocumentSourceLifecycleGuard` (+ resolver) | Política por origen (variabilidad legítima) |
| `ISourceDocumentSummaryProvider` (+ resolver) | Precedente de "contributor por módulo de origen" para Monitor; patrón a reutilizar en recovery y communications |
| Catálogos `global.sri_*` + resolvers ADR-037 | Representaciones oficiales |
| `SystemProviderSettings` + `ISystemProviderSettingsRepository` | SSOT del RUC Proveedor (instancia) |
| RIDE (parsers genéricos de `campoAdicional` + `AdditionalInfoSection`) | Muestra cualquier campo adicional sin cambios |
| `ElectronicDocumentAuthorizedEvent` + `CommunicationQueue` | Frontera ED → Communications |

---

## D. Modelo fiscal canónico

### D.1 Decisión: evolucionar, no reescribir

`ElectronicDocumentData` **ya es** el sobre común + el cuerpo comercial, y `RetentionElectronicDocumentData` ya
reutiliza los records genéricos (`ElectronicDocumentEmissionContext`, `ElectronicDocumentIssuerData`,
`ElectronicDocumentCounterpartyData`, `ElectronicDocumentAdditionalField`). Reestructurarlo hoy en
`Envelope<TBody>` cambiaría 3 providers y 3 builders FROZEN sin ninguna ganancia fiscal. Se formaliza lo que ya
existe:

| Parte | Records actuales | Contenido |
|---|---|---|
| **Sobre común** | `ElectronicDocumentEmissionContext` | ambiente, tipo de emisión, `codDoc`, estab, dirEstablecimiento, ptoEmi, secuencial, `IssueDate` (`DateOnly`, ADR-034) |
| | `ElectronicDocumentIssuerData` | identidad + perfil fiscal del emisor (se extiende en fase 2, §H) |
| | `IReadOnlyList<ElectronicDocumentAdditionalField>` | `infoAdicional` **ya compuesta** (§I) |
| | `ElectronicDocumentXml.AccessKey` | la clave se calcula al construir y viaja en el resultado (no en el modelo de entrada) |
| | (fase 3) versión de especificación | §F |
| **Cuerpo tipado** | `ElectronicDocumentData` (Details, TaxSummary, Totals, Payments, Reason, ModifiedDocument) | Factura y NC; candidato natural para Liquidación de compra (03) |
| | `RetentionElectronicDocumentData` (RetentionInfo, SubjectWithheld, SourceDocument, Lines, Totals) | Retención |
| | futuros: Nota de débito (motivos), Guía de remisión (transportista, destinatarios, sin totales) | modelos hermanos propios |

Reglas:
- **Nada sectorial ni de un solo tipo en el sobre.** Lo que no aplica a todos los tipos vive en el cuerpo tipado
  o en una extensión (§P).
- **Sin modelo gigante:** no se agregan propiedades opcionales "por si acaso" (precedente: se eliminó
  `Counterparty.Phone` por especulativo).
- `RetentionBody ≠ InvoiceBody`: se conserva el fork documentado en RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A.
- El receptor (`Counterparty` / `SubjectWithheld`) es del cuerpo, porque su forma y su obligatoriedad cambian por
  tipo (la Guía tiene varios destinatarios).

### D.2 Contrato del sobre (fase 7, solo con disparador)

Cuando se incorpore un cuarto tipo (03/05/06), y no antes, se introduce un contrato mínimo que los records
implementan, para que **un único** orquestador genérico haga provider → perfil → composer → builder:

```csharp
// Conceptual — no implementado.
public interface IElectronicDocumentFiscalModel<TSelf> where TSelf : IElectronicDocumentFiscalModel<TSelf>
{
    ElectronicDocumentEmissionContext Emission { get; }
    ElectronicDocumentIssuerData Issuer { get; }
    IReadOnlyList<ElectronicDocumentAdditionalField> AdditionalInfo { get; }
    TSelf WithAdditionalInfo(IReadOnlyList<ElectronicDocumentAdditionalField> composed);
}
```

Hasta entonces, los dos orquestadores existentes llaman al mismo composer (§I.4) y logran el mismo efecto sin
tocar los records.

---

## E. Source providers (Fiscal Source Adapters)

**Responsabilidad oficial:** *agregado de negocio → modelo fiscal.* Un provider por tipo.

| Responsabilidad | Hoy | Objetivo |
|---|---|---|
| Cargar el agregado y sus snapshots, por tenant/empresa | ✅ | **Permanece** |
| Validar completitud de datos de negocio (estado, punto de emisión, SKU, formas de pago...) y fallar con `ValidationFailure` | ✅ | **Permanece** |
| Mapear snapshots (montos, líneas, impuestos ya calculados) sin recalcular | ✅ | **Permanece** |
| Extraer el secuencial del número ya capturado (nunca `CaptureNextAsync`) | ✅ | **Permanece** |
| Resolver representaciones de catálogo vía resolvers ADR-037 (p. ej. `IRetentionCodeResolver.ResolveForDateAsync`, forma de pago) | ✅ parcial | **Permanece** (siempre por resolver, con la fecha del documento) |
| Aportar campos adicionales **propios del documento** ("Observación") | ✅ | **Permanece**, como `sourceFields` del composer |
| Proyectar el emisor (Company + matriz + RIMPE + SriSettings) | 🟠 ×3 | **Sale** → Issuer Fiscal Profile (fase 2) |
| Texto RIMPE | 🟠 ×3 | **Sale** → perfil del emisor, desde catálogo (fase 2) |
| Campos adicionales normativos (RUC Proveedor, Gran Contribuyente) | — | **Nunca entran**: los aporta el composer (fase 1) |
| Validar `codDoc` con `IsActiveElectronicDocTypeAsync` (filtra `IsActive`) | ⚠️ | Pasa a la semántica `ResolveForDate`/existencia de ADR-037 §F.1 en el slice de `sri_doc_type` |

**Prohibido en providers:** generar XML (`System.Xml.*`), firmar, decidir SOAP o reintentos, interpretar
respuestas SRI, tablas "valor → código" en código, y depender de `ISystemProviderSettingsRepository` (frontera
vigente en `frozen-infrastructure.md`, protegida por
`SalesInvoiceElectronicDocumentDataProviderTests.GetDataAsync_Issuer_proviene_de_Company_y_SriSettings_nunca_de_SystemProviderSettings`).

---

## F. Especificación SRI y versionado

### F.1 Dos versiones distintas

| Concepto | Ejemplo | Naturaleza | Fuente única objetivo |
|---|---|---|---|
| **Versión de la Ficha Técnica** (especificación normativa) | `2.34` | Qué norma implementa este release del ERP | Constante única en Application (`SriTechnicalSpecification.Current`), respaldada por una fila de `global.sri_normative_source` (documento `FICHA_TECNICA_OFFLINE`, versión `2.34`, SHA-256 `7333aebf…13c9`) — ADR-037 D10 |
| **Versión del esquema XML** por tipo | Factura `1.1.0`, NC `1.1.0`, Retención `1.0.0` | Estructura que produce cada builder | **El builder** (la estructura está en su código) → viaja en `ElectronicDocumentXml.Version` |
| Normativa aplicable a un dato | Tabla 20, Anexo 26, Res. NAC-DGERCGC26-00000027 | Trazabilidad por dato | `sri_normative_source` (catálogos) / citas en contributors y ADRs |

No se asume que coincidan: una ficha nueva puede no cambiar ningún XSD, y un XSD puede cambiar sin ficha nueva.

### F.2 Hoy

- `ElectronicDocument.XmlVersion` (desde `ElectronicDocumentXml.Version`, constante del builder) y
  `ElectronicDocument.SchemaVersion` (constante del validador) **ya se persisten** en `MarkXmlGenerated`.
- La versión de ficha **no** se registra.
- La versión activa está en tres fuentes (A.4) y `manifest.json` desalineado para Retención.
- Timestamps de etapa: `CreatedAt`, `LastAttemptUtc`, `AuthorizationDate` en la entidad, y el timeline completo
  (XmlGenerated, Signed, Dispatching, Sent, Received, Authorized...) en `ElectronicDocumentAudit` (ADR-022).

### F.3 Objetivo (fase 3 — no implementado)

1. **SSOT de la versión XML = builder.** El validador valida contra el XSD de `(DocumentType, xml.Version)` en
   lugar de su propia constante. `SchemaVersion` persistido = versión del XSD efectivamente usado.
2. **`manifest.json` = índice de XSD disponibles.** `activeVersion` deja de ser una tercera fuente: un compliance
   test exige `activeVersion == versión del builder` para cada tipo activo (corrige Retención `null`).
3. **`ElectronicDocument.SpecificationVersion`** (string, `VersionMaxLen`, columna aditiva nullable): se fija en
   `MarkXmlGenerated` junto a `XmlVersion`/`SchemaVersion`, desde `SriTechnicalSpecification.Current`. Los
   documentos históricos quedan `null` (no se infiere ni se rellena).
4. **Timestamps:** no se agregan columnas. El timeline de auditoría es la SSOT de generación/firma/envío; la
   entidad conserva los campos operativos que ya tiene.
5. **Evidencia definitiva:** el XML firmado y el autorizado almacenados. Las versiones registradas son metadatos
   de trazabilidad; si discrepan del XML, manda el XML (ADR-037 D12).
6. Un documento `Draft`/`Failed` regenerado tras un upgrade queda con la versión del release que lo generó; un
   documento firmado o autorizado nunca se regenera.

---

## G. Integración con ADR-037 (catálogos globales)

ADR-037 es la SSOT de catálogos. Esta arquitectura **no** duplica IVA, ICE, IRBPNR, retenciones, formas de pago,
tipos de identificación, tipos de comprobante ni errores.

```
Concepto de negocio (snapshot del documento: código interno, tasa, fecha de emisión)
   → resolver del catálogo (ADR-037: ResolveForDate…, fail-closed, sin filtrar IsEnabled)
   → representación oficial vigente a la fecha de emisión (xml_code, porcentaje, código ATS)
   → modelo fiscal (string ya resuelto)
   → builder (solo transcribe)
```

| Límite | Regla |
|---|---|
| Módulos de negocio | Trabajan con el concepto (Id / código de negocio / tasa). Nunca con `xml_code` |
| Provider | Único llamador del resolver al construir el modelo fiscal; usa la fecha de negocio del documento |
| Builder | Transcribe. Excepción heredada: `InvoiceXmlBuilder`/`CreditNoteXmlBuilder` traducen la etiqueta `VAT/ICE/IRBPNR` con `ISriTaxCategoryCodeResolver` (constantes estructurales `SriTaxCategoryCodes`, clase "estable" de ADR-037). **KEEP**; moverlo al provider es opcional (P3) |
| Composer / perfil | Pueden leer catálogos solo vía resolvers ADR-037 (p. ej. leyenda RIMPE si DR-6 la convierte en dato de catálogo) |
| Error | Representación ausente o ambigua → `SRI_FISCAL_CATALOG_CONFIGURATION_ERROR`, ED `Failed`, sin XML |

---

## H. Issuer Fiscal Profile

### H.1 Fuente

`Company` **es** la fuente natural (ya tiene `TaxRegimeCode`/`TaxRegime`, `SpecialTaxpayerNo`, `IsAccountingReq`,
`WithholdsRenta`, `WithholdsVat`). No se crea una entidad paralela. Los datos faltantes (agente de retención con
resolución, Gran Contribuyente con resolución) se agregan a `Company` en el ticket de la fase 2, previa
**DECISION REQUIRED** con el perfil real del piloto (auditoría R21/R22).

### H.2 Proyección única

```
Company (+ matriz, + SriSettings, + catálogo sri_tax_regime vía resolver ADR-037)
   → IElectronicDocumentIssuerProfileResolver.ResolveAsync(tenantId, companyId, issueDate)   (Application, ElectronicDocuments)
   → ElectronicDocumentIssuerProfile { Issuer: ElectronicDocumentIssuerData, Environment, EmissionType }
   → provider lo coloca en el modelo; builders y composer solo leen ElectronicDocumentIssuerData
```

```csharp
// Conceptual — fase 2, no implementado. Extensión aditiva del record existente.
public sealed record ElectronicDocumentIssuerData(
    string TaxId, string LegalName, string? TradeName, string MatrixAddress,
    string? TaxRegime,                      // leyenda contribuyenteRimpe ya resuelta (o null)
    bool IsAccountingRequired,
    string? SpecialTaxpayerNumber = null,   // contribuyenteEspecial (hoy solo en RetentionInfo)
    string? RetentionAgentResolution = null,// agenteRetencion (Anexo 21; XSD: [0-9]+, ≤ 8)
    string? LargeTaxpayerResolution = null  // Gran Contribuyente (Anexo 24) → lo emite un contributor de infoAdicional
);
```

Reglas:
- Una sola implementación de la leyenda RIMPE (corrige el texto de Negocio Popular). Antes de emitirlo, verificar
  el patrón contra el XSD oficial vigente: los XSD embebidos solo admiten `CONTRIBUYENTE RÉGIMEN RIMPE`
  (auditoría §K). La temporalidad de la leyenda es ADR-037 DR-6.
- Los builders no consultan `Company` ni conocen códigos de régimen; el composer no consulta `Company` (recibe el
  `Issuer` ya proyectado).
- `RetentionElectronicDocumentInfo.SpecialTaxpayerNumber` pasa a leerse del perfil (MOVE LATER, sin cambiar el
  XML).
- El perfil se resuelve en cada generación con datos actuales de `Company`: igual que hoy. Lo histórico está en
  el XML firmado/autorizado.

---

## I. AdditionalInfo — arquitectura (P0 siguiente)

### I.1 Hoy

| Tipo | Quién compone | Contenido | Validación |
|---|---|---|---|
| Factura | `SalesInvoiceElectronicDocumentDataProvider` | `"Observación" = invoice.Notes` (si no vacío) | builder: ≤ 15; XSD: nombre y valor 1..300 |
| NC | `SalesReturnCreditNoteDataProvider` | `[]` | builder ≤ 15 |
| Retención | `RetentionElectronicDocumentDataProvider` | `[]` | builder ≤ 15 (XSD 1.0.0: si existe `infoAdicional`, exige ≥ 1 campo; el builder la omite vacía) |
| RIDE | parsers de los 3 tipos | renderiza todo `campoAdicional` | — |

No existe ningún punto donde un requisito normativo pueda agregar un campo sin tocar 3 providers.

### I.2 Diseño: un composer, contributors explícitos

```
                     IElectronicDocumentAdditionalInfoComposer   (Application/ElectronicDocuments/AdditionalInfo)
                                     ▲
     ┌───────────────────────────────┼─────────────────────────────────┬───────────────────────────┐
 contributor normativo         contributor normativo              contributor sectorial      campos del documento
 SystemProviderRuc (Anexo 26)  LargeTaxpayer (Anexo 24, fase 2)   (futuros, §P)              (sourceFields del provider)
 ← ISystemProviderSettingsRepository  ← Issuer.LargeTaxpayerResolution  ← política sectorial    ← "Observación"
```

```csharp
// Conceptual — se implementa en ZH-SRI-ANEXO26-PROVIDER-RUC-01.
public sealed record AdditionalInfoCompositionContext(
    ElectronicDocumentType DocumentType,
    Guid TenantId,
    Guid CompanyId,
    DateOnly IssueDate,                     // fecha de negocio (ADR-034), nunca "hoy"
    ElectronicDocumentIssuerData Issuer);

public interface IElectronicDocumentAdditionalInfoContributor
{
    string Id { get; }                      // trazabilidad y logs, p. ej. "sri.anexo26.provider-ruc"
    int Order { get; }                      // orden determinístico entre contributors
    bool AppliesTo(ElectronicDocumentType documentType);
    Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ContributeAsync(
        AdditionalInfoCompositionContext context, CancellationToken ct);
}

public interface IElectronicDocumentAdditionalInfoComposer
{
    Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ComposeAsync(
        AdditionalInfoCompositionContext context,
        IReadOnlyList<ElectronicDocumentAdditionalField> sourceFields,
        CancellationToken ct);
}
```

### I.3 Reglas de composición (única implementación)

| # | Regla | Comportamiento |
|---|---|---|
| R1 | Una sola composición | Solo el composer produce la lista final; providers aportan `sourceFields`, builders transcriben |
| R2 | Orden determinístico | Contributors normativos/sectoriales por `Order` ascendente (empate → `Id` ordinal), luego `sourceFields` en el orden del provider |
| R3 | Nombres únicos | Comparación sin distinguir mayúsculas y con `Trim`. Un nombre producido por un contributor es **reservado**: si un `sourceField` lo repite → fallo (un campo de usuario nunca suplanta un campo normativo). Duplicados entre contributors → fallo |
| R4 | Longitud | Nombre y valor: 1..300 (XSD `nombre`/`campoAdicional`) tras `Trim`. Sin truncado silencioso: fuera de rango → fallo. Si "Observación" debe truncarse, lo decide Ventas en su provider, no el composer |
| R5 | Límite | ≤ 15 campos en total (incluye los normativos). Exceso → fallo. Los builders conservan su chequeo como defensa |
| R6 | Fail-closed | Un contributor que determina que su campo es **obligatorio** y no puede producirlo devuelve `Failure` → no hay XML → ED `Failed` (pre-firma, reintentable) con código propio |
| R7 | Trazabilidad | El composer registra (log estructurado) `Id` del contributor por campo emitido. No se persiste: la evidencia es el XML firmado/autorizado (D11) |
| R8 | Pureza | Mismo contexto → mismo resultado (sin reloj, sin aleatoriedad). Vista previa y pipeline producen el mismo `infoAdicional` |
| R9 | Errores | Códigos estables nuevos (p. ej. `ELECTRONIC_DOCUMENT_ADDITIONAL_INFO_INVALID`, `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED`), mapeados como validación (422) por ADR-027 |

### I.4 Punto de invocación

Los únicos caminos por los que un modelo fiscal llega a un builder son dos (verificado):

| Orquestador | Tipos | Consumidores |
|---|---|---|
| `CommercialElectronicDocumentXmlSupplier.BuildXmlAsync` | 01, 04 (y futuros con forma comercial) | pipeline |
| `RetentionElectronicDocumentXmlService.GenerateXmlAsync` | 07 | pipeline (vía supplier), XML de vista previa (`GenerateRetentionXmlUseCases`), RIDE de vista previa (`GenerateRetentionRidePdfUseCases`) |

Ambos hacen, en este orden: `provider.GetDataAsync` → `composer.ComposeAsync(contexto desde data.Emission/Issuer +
referencia, data.AdditionalInfo)` → `data with { AdditionalInfo = compuesta }` → `builder.Build`. Un test de
arquitectura impide un tercer camino (ningún otro tipo invoca `IElectronicDocumentXmlBuilder.Build` /
`IRetentionXmlBuilder.Build`). En la fase 7 ambos colapsan en un orquestador genérico (§D.2).

El builder recibe la colección final y no sabe de `SystemProviderSettings`.

---

## J. Integración con SystemProviderSettings

- **Confirmado:** `SystemProviderSettings` es la SSOT global del proveedor del sistema (singleton `Id=1`, esquema
  sin `TenantId`/`CompanyId`, editable solo con la policy `PlatformAdmin` desde admin-core). **No se mueve a
  Tenant/Company.** Es configuración de la instancia del ERP Core, no de ZH Platform: funciona sin Platform.
- **Consumo:** solo por `SystemProviderRucAdditionalInfoContributor` (Application/ElectronicDocuments), vía
  `ISystemProviderSettingsRepository.GetAsync`. Ningún provider, builder, módulo de negocio ni RIDE lo lee.
- La frontera emisor ↔ proveedor de `frozen-infrastructure.md` sigue vigente: `ruc`/`razonSocial` de
  `infoTributaria` vienen solo de `Company`.

**Base normativa (DA-1, confirmada 2026-10-02 por el responsable funcional):**

| Dato | Valor |
|---|---|
| Resolución | NAC-DGERCGC26-00000027 |
| Publicación | 28/07/2026, Quinto Suplemento del Registro Oficial No. 335 |
| Obligación | Los emisores que usan sistemas de facturación suministrados por terceros incorporan el `RUC Proveedor` en sus comprobantes |
| Plazo | El SRI indica **60 días hábiles** desde la publicación |
| Formato | Ficha Técnica 2.34, Anexo 26 (p. 135): `<campoAdicional nombre="RUC Proveedor">RUC</campoAdicional>`, alfanumérico ≤ 300 |

Siguen pendientes solo dos datos administrativos, que no cambian el diseño:
- la confirmación de que ZH Technologies completó su registro como proveedor y figura en el listado
  correspondiente;
- la fecha legal exacta que se registrará en `EffectiveDate`. **No** se calcula en código ni se hardcodea: los
  60 días hábiles dependen del calendario oficial y la fecha se configura cuando esté confirmada.

**Regla definitiva (DA-2): `EffectiveDate` es la fecha de aplicabilidad fiscal del requisito.**

`EffectiveDate` no es informativa: decide si el requisito aplica a un documento. Antes de esa fecha el campo no
se emite nunca; desde esa fecha es obligatorio y `Enabled` pasa a ser una precondición de emisión, no un
interruptor de omisión. No existe "incorporación anticipada" en la semántica productiva.

| # | `EffectiveDate` | `IssueDate` | `Enabled` | `Ruc` | Resultado |
|---|---|---|---|---|---|
| 1 | `null` (o sin fila) | — | `false` (o sin fila) | — | Sin campo: requisito aún no activado en esta instalación |
| 2 | `null` | — | `true` | — | **Fallo** `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED`: configuración incompleta |
| 3 | configurada | `IssueDate < EffectiveDate` | cualquiera | — | Sin campo (no se evalúan `Enabled` ni `Ruc`) |
| 4a | configurada | `IssueDate ≥ EffectiveDate` | `true` | válido | Campo `RUC Proveedor = Ruc` |
| 4b | configurada | `IssueDate ≥ EffectiveDate` | `false` | — | **Fallo** `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED` |
| 4c | configurada | `IssueDate ≥ EffectiveDate` | `true` | ausente o inválido | **Fallo** `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED` |

Un fallo es fail-closed: el composer devuelve `Failure`, **no se genera XML**, el ED queda `Failed` (pre-firma,
reintentable cuando se corrija la configuración) y el Monitor muestra el código y el motivo. Nunca se emite un
comprobante exigible sin el campo.

Reglas complementarias:
- La comparación usa exclusivamente la `IssueDate` de negocio del documento (ADR-034), nunca `DateTime.Now`,
  `DateTime.UtcNow` ni el reloj de la empresa. El mismo documento produce siempre el mismo resultado: vista
  previa, pipeline y regeneración de un `Draft`/`Failed` coinciden.
- Los documentos `Signed`/`Authorized` (y los ya transmitidos) **nunca se regeneran** para agregar el campo
  (ADR-037 D12 / ADR-038 D11). Solo `Draft`/`Failed` aplican la regla al regenerarse, según su `IssueDate`.
- `EffectiveDate` es configuración de instancia (`SystemProviderSettings`). Se carga con la fecha legal
  confirmada; **nunca** se calcula (p. ej. sumando 60 días hábiles) ni se hardcodea en código.
- `Ruc` válido = no vacío, 13 dígitos numéricos, igual que el invariante de `SystemProviderSettings.Configure`. El
  contributor lo verifica igual, sin confiar en el invariante.
- `Configure` exige además razón social y CIIU para habilitar. Si en la fecha exigible faltan, no se puede
  habilitar y la emisión falla cerrado (4b): es el comportamiento buscado.
- **Pruebas en `celcer`:** se configura en la instancia de pruebas una `EffectiveDate` igual o anterior a la
  `IssueDate` de los comprobantes de prueba, con `Enabled = true` y el RUC de ZH. No se agregan flags, ambientes
  especiales ni lógica de "emisión anticipada".
- **Impacto de la fila 2 (verificación previa al despliegue):** hoy `Configure` permite `Enabled = true` con
  `EffectiveDate = null`. Con esta regla, esa configuración bloquea la emisión electrónica de **todas** las
  empresas de la instancia. Antes de desplegar el P0 se consulta `system_provider_settings` en cada instalación
  (Q11 de la auditoría 2.34) y se corrige si está en ese estado. Que la UI de admin-core o `Configure` exijan
  `EffectiveDate` al habilitar sería coherente, pero es una decisión del ticket P0; no se asume aquí.
- **Riesgo residual (operativo, no de código):** en la fila 1 el campo se omite, porque el sistema no conoce la
  fecha legal y no puede inventarla. Mitigación: registrar `EffectiveDate` y `Enabled = true` en cuanto se
  confirme la fecha oficial, como parte del despliegue del P0.
- Nombre exacto: `RUC Proveedor` (Ficha 2.34, Anexo 26, p. 135). Constante estructural citada en Domain
  (`SriCatalogs/Constants`), no un literal en el contributor.

---

## K. XML builders

- **KEEP:** `InvoiceXmlBuilder`, `CreditNoteXmlBuilder`, `RetentionXmlBuilder`, uno por tipo y versión. No hay
  builder gigante.
- **Writer común mínimo, solo con beneficio demostrado y cuando una fase necesite tocar esa parte:**

| Parte compartida | Disparador | Destino conceptual | Garantía |
|---|---|---|---|
| `infoAdicional` | No hace falta para el P0 (el composer entrega la lista; los builders ya transcriben igual) | `SriAdditionalInfoXmlWriter` (Application/ElectronicDocuments/XmlBuilders) cuando se toque el bloque | XML byte a byte idéntico antes/después |
| `infoTributaria` (incl. orden `agenteRetencion` → `contribuyenteRimpe`) | Fase 2: agregar `agenteRetencion` en 3 builders | `SriInfoTributariaXmlWriter` | Golden XML de los 3 tipos |
| Clave de acceso (49 dígitos, código numérico determinístico, módulo 11) | Fase 7: primer tipo nuevo (evita una 4.ª copia) | `AccessKey.Generate(...)` en Domain (algoritmo puro, sin frameworks) | Vectores de prueba: mismas claves que hoy para los mismos datos (auditoría §W) |
| `Utf8StringWriter` | Junto con cualquiera de los anteriores | helper interno | trivial |

- Cada cuerpo (`infoFactura`, `infoNotaCredito`, `infoCompRetencion`, detalles, impuestos) sigue en su builder.
- Los builders siguen sin XSD, firma, SOAP, almacenamiento ni repositorios.

---

## L. SRI Gateway

| Operación | Interfaz Application (técnica, recibe `wsdlUrl`) | Servicio por empresa (resuelve `SriSettings`) | Implementación |
|---|---|---|---|
| Recepción (`validarComprobante`) | `ISriReceptionClient` | `IElectronicDocumentReceptionService` | `SriReceptionClient` → `SriSoapClient.SendAsync` |
| Autorización (`autorizacionComprobante`, polling) | `ISriAuthorizationClient` | `IElectronicDocumentAuthorizationService` | `SriAuthorizationClient` → `SriSoapClient.CheckAuthorizationAsync` |
| Estado (`consultarEstadoAutorizacionComprobante`) | `ISriDocumentStatusQuery` | **Falta** | `SriDocumentStatusQuery` → `SriSoapClient.QueryDocumentStatusAsync` |

- `SriSoapClient` es el único cliente SOAP: **KEEP**. No se crean clientes paralelos.
- **Gap:** no hay servicio de estado por empresa; `RetentionAnnulmentService` (módulo Retentions) lee
  `SriSettings.WsdlUrl` y llama a `ISriDocumentStatusQuery` directamente. Objetivo (fase 4/5):
  `IElectronicDocumentStatusService.QueryAsync(companyId, accessKey)` en ElectronicDocuments, simétrico a los otros
  dos; Retenciones y recovery lo consumen.
- **Regla:** los módulos de negocio nunca referencian `ISriReceptionClient`, `ISriAuthorizationClient`,
  `ISriDocumentStatusQuery` ni `SriSoapClient`. La violación actual (`RetentionAnnulmentService`) queda en el
  allowlist del test de arquitectura (ratchet) hasta la fase 4/5.

---

## M. Semántica de estado y de errores SRI

### M.1 Dónde vive hoy

| Respuesta | Interpretación | Forma |
|---|---|---|
| ConsultaComprobante | `SriSoapClient` → `SriStatusQueryOutcome` (técnico) + `SriFiscalStatus` (fiscal) | ✅ tipada; modelo a replicar |
| Recepción | `SriReceptionResult.Received` = `Status == "RECIBIDA"`; Issuer: `AlreadyExistsErrorPrefixes = ["[70]","[43]","[45]"]` con `StartsWith` sobre strings formateados | 🟠 strings |
| Autorización | `ElectronicDocumentAuthorizationService.TerminalOutcomeStatuses = ["AUTORIZADO","NO AUTORIZADO","TIMEOUT"]`; Issuer compara `auth.Status` con literales; `SriSoapClient` sintetiza `TIMEOUT`/`SIN_RESPUESTA` | 🟠 strings |
| Mensajes | `SriMessage(Code, MessageType, Message, AdditionalInfo)` estructurado (ADR-024) + `LastError` texto | ✅ estructura disponible, no usada para decidir |
| Catálogo `sri_error_code` (33 códigos) | sin consumidor | ⚪ |

### M.2 Objetivo (diseño de ZH-EDOC-REJECTION-RECOVERY-01, no implementado aquí)

1. **Protocolo ≠ semántica.** `SriSoapClient` parsea y entrega estructura (`SriMessage.Code`), nunca decide.
2. **Resultados tipados** para recepción y autorización, simétricos a la consulta:
   `technical outcome` (Success / Timeout / Unavailable / Unknown) separado de `fiscal disposition`
   (Received / AlreadyExists / ReturnedDefinitive / ReturnedTransient / Authorized / NotAuthorized / Pending).
3. **Una sola clasificación de códigos** (70/43/45 = ya existe; 50 = transitorio; resto = definitivo), basada en
   `SriMessage.Code`, no en `StartsWith`/`Contains`. Por ser conocimiento normativo, se evalúa como atributo del
   catálogo `sri_error_code` (ADR-037, slice propio) con un único intérprete en Application.
4. **Invariantes que no cambian:** 70/43/45 → consultar, nunca regenerar clave; timeout nunca es rechazo; una
   consulta no exitosa nunca trae estado fiscal; `RECHAZADA` (99) nunca es `NotAuthorized`.
5. **ConsultaComprobante = SSOT transversal del estado fiscal** de cualquier tipo (el contrato no depende del
   tipo; rango de fechas limitado por el SRI, error 99).
6. Golden tests de la tabla de decisión actual **antes** de migrar a tipos (sin cambio de comportamiento salvo
   los gaps aprobados R12–R14).

---

## N. Frontera de recovery

| Es transversal (una sola implementación) | Puede variar por tipo/origen (contribuido, no copiado) | Nunca se duplica |
|---|---|---|
| Descubrimiento de candidatos cross-tenant (consulta con `AsPlatformQuery` que devuelve solo ids → un scope `JobExecutionContext` por candidato; patrón de `RetentionElectronicRecoveryJob`) | Si se permite reenviar desde `Signed`/`Dispatching` (guard de origen: hoy Retenciones = solo consulta) | Transiciones de estado (solo `ElectronicDocument` vía `ElectronicDocumentIssuer`) |
| Acción por estado del ED: generar / enviar / consultar / DeadLetter | Detección de "origen sin ED" (Retención `Issued` sin ED hoy; Factura/NC autorizada sin ED = gap auditoría §H) mediante un contributor por módulo (patrón `ISourceDocumentSummaryProvider`) | Tratamiento 70/43/45 |
| Política de ventana/backoff (`ElectronicDocumentRetryPolicy`, a alinear con las 24 h del SRI) | Flujos normativos propios (anulación de retenciones, ADR-036) | Consulta de estado (Gateway §L) |
| Reintento manual y register manual del Monitor | Preferencia por empresa (`electronic_documents.auto_retry_enabled`) | Regla "timeout/ambiguo nunca autoriza reenvío a ciegas" |

Hoy: el job genérico es inoperante (bug de tenant) y `RetentionElectronicRecoveryJob` cubre lo específico de
retenciones. Al corregir el genérico (ZH-EDOC-RETRY-JOB-TENANT-01) no deben competir: el genérico se ocupa del
ciclo del **ED**; los jobs de origen solo de "origen sin ED" y de flujos normativos propios. El diseño detallado es
**ZH-EDOC-RECOVERY-ARCHITECTURE-01**; este documento solo fija estos límites.

---

## O. Frontera de Communications

```
ElectronicDocuments  ──publica──►  ElectronicDocumentAuthorizedEvent (hecho fiscal: ED autorizado; tipo, ids)
Communications       ──decide───►  si se envía, a quién, plantilla, adjuntos (XML autorizado + RIDE), cola, reintentos, SMTP
```

- ElectronicDocuments **no** implementa correo, SMTP ni reintentos de entrega.
- Hoy solo existe `SalesInvoiceAuthorizedCommunicationHandler` (Communications), que conoce Ventas para obtener el
  destinatario. Para NC y Retención (auditoría R30) no se copian handlers: un handler genérico en Communications
  + un contributor de destinatario por módulo de origen (`email`, nombre, número visible), mismo patrón que
  `ISourceDocumentSummaryProvider`. Ticket propio (fase 6).
- El RIDE se obtiene por su caso de uso existente (`GetOrGenerateRideQuery`); Communications no genera XML.

---

## P. Extensiones sectoriales

Regla: **nada sectorial obligatorio en el sobre ni en un cuerpo** si no aplica universalmente al tipo.

| Canal | Cuándo | Ejemplo | Mecanismo |
|---|---|---|---|
| Contributor de `infoAdicional` | El requisito se expresa como `campoAdicional` | Gran Contribuyente (Anexo 24), leyendas sectoriales | `IElectronicDocumentAdditionalInfoContributor` + política de aplicabilidad |
| Dato de línea ya estándar en el XSD | El XSD lo define para todos (no es sectorial en sí) | `codigoAuxiliar` (Anexos 23/25), SKU `ICE-FPN-01` de fundas (Anexo 18) | Campo opcional del detalle del cuerpo comercial; el dato lo aporta el ítem |
| Bloque tipado opcional del cuerpo | Requisito estructural de un sector | exportación (Anexo 4), reembolsos (Anexo 5), placa transporte/combustibles (Anexos 12, 25) | Record tipado opcional del cuerpo (p. ej. `InvoiceExportInfo?`), escrito por el builder solo si está presente; puede exigir otra versión de XSD (→ §F) |

- La aplicabilidad la decide una **política sectorial** explícita (configuración de empresa / dato normativo), nunca
  un `if` disperso en un módulo de negocio.
- Ninguna extensión se implementa en esta fase. Ninguna contamina el flujo Retail.

---

## Q. Incorporación de tipos documentales futuros (03, 05, 06)

Sin cambiar Ventas ni Retenciones existentes. Requiere fase propia con roadmap (ADR-023 lo prohíbe como
"mantenimiento").

| Paso | Pieza | Reutiliza |
|---|---|---|
| 1 | Provider del módulo dueño (Compras para 03; Ventas para 05; Inventario/Logística para 06) | perfil del emisor, resolvers ADR-037 |
| 2 | Cuerpo tipado: 03 puede reutilizar `ElectronicDocumentData` (forma comercial con proveedor como contraparte); 05 y 06 modelos hermanos | sobre común |
| 3 | Builder del tipo + writer común (aquí se mueve la clave de acceso a Domain, fase 7) | `infoTributaria`/`infoAdicional` comunes |
| 4 | Validador/XSD: versión del builder; `manifest.json` `activeVersion` alineado por compliance test | `EmbeddedXmlSchemaProvider` (XSD ya embebidos) |
| 5 | Supplier: fallback comercial si usa `ElectronicDocumentData`; si no, orquestador genérico (fase 7) | composer, pipeline |
| 6 | RIDE: parser + plantilla registrados en los resolvers existentes | ADR-025 |
| 7 | Políticas del tipo: guard de origen si el origen se anula; contributor de recovery; contributor de destinatario | §N, §O |
| 8 | Compliance: golden XML + XSD + matriz de requisitos del tipo | §V |

`ElectronicDocumentType` ya declara los seis tipos; el pipeline, la firma, el gateway, el estado y el Monitor no
cambian.

---

## R. Reglas de dependencia

| Capa | Permitido | Prohibido |
|---|---|---|
| Domain | Conceptos fiscales puros: `AccessKey`, `SriFiscalStatus`, `SriStatusQueryOutcome`, `SriMessage`, constantes estructurales (`SriDocumentTypeCodes`, nombre `RUC Proveedor`), entidades | EF, `System.Xml*`, SOAP, HTTP |
| Application | Contratos (gateway, specification, composer, perfil), modelo fiscal, orquestación (`ElectronicDocumentIssuer`, suppliers), builders (`System.Xml.Linq`, hoy en Application/XmlBuilders — **KEEP**), políticas y resolvers | SOAP/HTTP, XAdES, `System.Security.Cryptography.Xml`, EF directo |
| Infrastructure | `SriSoapClient`, `XadesBesSigner`, XSD embebidos, storage, repositorios, resolvers de catálogo | Decisiones de estado o de negocio |
| API | Endpoints finos, permisos, Monitor, jobs Hangfire (descubrimiento + scope) | Lógica fiscal |
| Módulos de negocio (Sales, Retentions, Purchases, Expenses...) | Providers (Fiscal Source Adapters), guards de origen, contributors de recovery/destinatario, `IElectronicDocumentIssuer` | `ISriReceptionClient`, `ISriAuthorizationClient`, `ISriDocumentStatusQuery`, `SriSoapClient`, signer, `IXmlSchemaProvider`, `ISystemProviderSettingsRepository`; invocar builders fuera de un orquestador registrado |

Tests de arquitectura propuestos (`ERP.Architecture.Tests`, ratchet sobre la deuda actual):
- Ningún tipo fuera de ElectronicDocuments/Infrastructure.Sri referencia las interfaces del gateway (allowlist:
  `RetentionAnnulmentService` hasta la fase 4/5).
- Ningún provider ni builder referencia `ISystemProviderSettingsRepository`.
- Solo los orquestadores registrados invocan `Build` de un builder.
- Domain no referencia `System.Xml`.

---

## S. Matriz SSOT

| Concern | SSOT actual | SSOT futura | Owner | Consumers | Acción |
|---|---|---|---|---|---|
| Identidad del emisor (RUC, razón social, nombre comercial, matriz) | `Company` + `Establishment` matriz, proyectado ×3 en providers | `Company` → `IElectronicDocumentIssuerProfileResolver` → `ElectronicDocumentIssuerData` | ElectronicDocuments (proyección) / Company (dato) | providers, builders, composer | EXTEND (fase 2) |
| Clave de acceso | builder (×3) | `AccessKey.Generate` en Domain | ElectronicDocuments | builders → `ElectronicDocumentXml.AccessKey` → `ElectronicDocument.AccessKey` | MOVE LATER (fase 7) |
| Secuencial | `IDocumentSequenceRepository.CaptureNextAsync` en el origen; provider lo extrae del número | sin cambio | Document Sequences (ADR-019) | providers | KEEP |
| Tipo de comprobante | `sri_doc_type` + `SriDocumentTypeCodes` | sin cambio (lectura por `ResolveForDate`/existencia, ADR-037 §F.1) | SriCatalogs | providers | KEEP / EXTEND |
| Ambiente / tipo de emisión | `SriSettings` (empresa), leído ×3 en providers | `SriSettings` vía perfil del emisor | ElectronicInvoicing (dato) / ElectronicDocuments (proyección) | providers → `ElectronicDocumentXml.Environment` → ED | EXTEND (fase 2) |
| Versión XML por tipo | constante del builder + constante del validador + `manifest.json` | **builder** (→ `ElectronicDocumentXml.Version`); validador selecciona XSD por ella; manifest verificado por test | ElectronicDocuments | validador, ED (`XmlVersion`/`SchemaVersion`) | EXTEND (fase 3) |
| Versión de especificación (ficha) | no existe | `SriTechnicalSpecification.Current` + fila `sri_normative_source` | ElectronicDocuments + SriCatalogs | ED (`SpecificationVersion`), compliance | EXTEND (fase 3) |
| Catálogos fiscales | `global.sri_*` + resolvers | sin cambio (ADR-037) | SriCatalogs | providers, perfil, composer | KEEP (ADR-037) |
| Perfil fiscal del emisor (RIMPE, especial, agente, Gran Contribuyente, contabilidad) | `Company` parcial; RIMPE ×3 | `Company` (+ campos fase 2) → proyección única | Company / ElectronicDocuments | builders (`infoTributaria`), composer | EXTEND (fase 2) |
| Información adicional | provider de cada tipo | `IElectronicDocumentAdditionalInfoComposer` | ElectronicDocuments | orquestadores → builders → RIDE | EXTEND (fase 1) |
| SystemProviderSettings | `system_provider_settings` (instancia) | sin cambio; consumido solo por su contributor | Configuration (instancia) | `SystemProviderRucAdditionalInfoContributor` | KEEP / EXTEND (fase 1) |
| Generación XML | builders por tipo | builders + writer común mínimo | ElectronicDocuments | orquestadores | KEEP / EXTEND |
| Validación XSD | validadores + `EmbeddedXmlSchemaProvider` | sin cambio de semántica (fail-closed); versión desde el XML | ElectronicDocuments / Infrastructure | Issuer | KEEP |
| Firma | `XadesBesSigner` | sin cambio | Infrastructure.Sri | `ElectronicDocumentSigningService` | DO NOT TOUCH |
| Recepción | `SriSoapClient.SendAsync` vía `ISriReceptionClient` + servicio por empresa | sin cambio | Infrastructure.Sri / ElectronicDocuments | Issuer | KEEP |
| Autorización | `SriSoapClient.CheckAuthorizationAsync` vía `ISriAuthorizationClient` + servicio por empresa | sin cambio (selección entre varias `<autorizacion>`: R14) | Infrastructure.Sri / ElectronicDocuments | Issuer | KEEP / EXTEND |
| Consulta de estado | `ISriDocumentStatusQuery` (sin servicio por empresa) | + `IElectronicDocumentStatusService` | ElectronicDocuments | Retenciones, recovery | EXTEND (fase 4/5) |
| Interpretación de errores SRI | strings en Issuer / AuthorizationService / SriSoapClient | intérprete tipado único sobre `SriMessage.Code` (+ clasificación en `sri_error_code`) | ElectronicDocuments | Issuer, recovery, Monitor | EXTEND (fase 5) |
| Storage XML | `ElectronicDocumentXmlStorageService` (draft/signed/authorized) | sin cambio | ElectronicDocuments | Issuer, RIDE, Communications | KEEP |
| RIDE | `RideDocumentService` + parsers/plantillas por tipo (ADR-025) | sin cambio | Ride | API, Communications | KEEP |
| Recovery | `ElectronicDocumentRetryJob` (inoperante) + `RetentionElectronicRecoveryJob` + manual | capacidad transversal + contributors por origen | ElectronicDocuments | jobs, Monitor | EXTEND (fase 4) |
| Frontera Communications | `ElectronicDocumentAuthorizedEvent` → handler de Factura | mismo evento → handler genérico + contributor de destinatario | Communications | — | EXTEND (fase 6) |

---

## T. Inventario KEEP / EXTEND / MOVE LATER / REMOVE EVENTUALLY / DO NOT TOUCH

| Componente | Categoría | Nota |
|---|---|---|
| `XadesBesSigner`, `XadesSignedXml` | **DO NOT TOUCH** | Validado contra comprobantes autorizados reales (SHA-256 + RSA-SHA1) |
| `SriSoapClient` (recepción, autorización, consulta), derivación de endpoints desde `WsdlUrl` | **DO NOT TOUCH** (salvo R14, EXTEND puntual con ticket) | Contrato 2.34 correcto |
| Tratamiento 70/43/45 | **DO NOT TOUCH** (comportamiento) | Se tipará en fase 5 sin cambiar la decisión |
| `SriFiscalStatus` / `SriStatusQueryOutcome` | **DO NOT TOUCH** | Modelo a replicar |
| Preflight XSD fail-closed | **DO NOT TOUCH** | — |
| ADR-036: `Dispatching`, `Discarded`, `AnnulmentPending`, guard, anulación | **DO NOT TOUCH** | — |
| `ElectronicDocument` (agregado) | **KEEP / EXTEND** | + `SpecificationVersion` (fase 3); salida de `Rejected` (R12, ticket propio) |
| `ElectronicDocumentIssuer` | **KEEP / EXTEND** | Interpretación tipada (fase 5); sin conocer composer ni perfil |
| `IElectronicDocumentXmlSupplier` + resolver | **KEEP** | Costura por tipo |
| `CommercialElectronicDocumentXmlSupplier` | **EXTEND** (fase 1) | Invoca el composer |
| `RetentionElectronicDocumentXmlService` | **EXTEND** (fase 1) → **MOVE LATER** (fase 7) | De `Modules/Retentions/Services` → orquestador genérico de ElectronicDocuments; motivo: es orquestación fiscal, no lógica de retenciones |
| `ElectronicDocumentData` / `RetentionElectronicDocumentData` | **KEEP / EXTEND** | Sobre común formalizado en fase 7 |
| `ElectronicDocumentIssuerData` | **EXTEND** (fase 2) | Campos fiscales del emisor |
| Providers (3) | **KEEP / EXTEND** | Delegan emisor (fase 2); aportan solo campos propios |
| `ResolveContribuyenteRimpeText` (×3) | **MOVE LATER** (fase 2) | Providers → perfil del emisor (texto desde catálogo); motivo: triplicado y desactualizado |
| Proyección emisor/ambiente en providers (×3) | **MOVE LATER** (fase 2) | Providers → `IElectronicDocumentIssuerProfileResolver` |
| `RetentionElectronicDocumentInfo.SpecialTaxpayerNumber` | **MOVE LATER** (fase 2) | → `ElectronicDocumentIssuerData.SpecialTaxpayerNumber`; XML igual |
| Clave de acceso en builders (×3) | **MOVE LATER** (fase 7) | Builders → `AccessKey.Generate` (Domain); motivo: evitar 4.ª copia; mismas claves |
| `BuildInfoAdicional` / `MaxAdditionalFields` (×3) | **MOVE LATER** (cuando se toque) | Builders → writer común; el límite real pasa al composer (fase 1) |
| `infoTributaria` en builders (×3) | **MOVE LATER** (fase 2) | Builders → writer común; motivo: `agenteRetencion` en 3 sitios |
| `Utf8StringWriter` (×3) | **MOVE LATER** (P3) | helper común |
| Constante `SchemaVersionValue` en validadores | **REMOVE EVENTUALLY** (fase 3) | Versión desde `ElectronicDocumentXml.Version` |
| `manifest.json` `activeVersion` | **KEEP** como índice, verificado por test (fase 3) | Corregir `Retention: null` |
| `AlreadyExistsErrorPrefixes`, `TerminalOutcomeStatuses`, comparaciones de `Status` | **REMOVE EVENTUALLY** (fase 5) | Reemplazados por resultados tipados |
| `ElectronicDocumentReceptionService` / `AuthorizationService` | **KEEP** | + servicio de estado simétrico (fase 4/5) |
| Uso directo de `ISriDocumentStatusQuery` en `RetentionAnnulmentService` | **MOVE LATER** (fase 4/5) | → `IElectronicDocumentStatusService` |
| `ISriTaxCategoryCodeResolver` en builders comerciales | **KEEP** (MOVE opcional P3) | Constantes estructurales |
| `ElectronicDocumentRetryPolicy` | **EXTEND** (fase 4) | Ventana 24 h |
| `ElectronicDocumentRetryJob` | **EXTEND (fix)** (fase 4) | Patrón cross-tenant de `RetentionElectronicRecoveryJob` |
| `RetentionElectronicRecoveryJob` | **KEEP** | Patrón de referencia |
| `IElectronicDocumentSourceLifecycleGuard` | **KEEP** | Variabilidad por origen |
| `SystemProviderSettings` | **KEEP / EXTEND** (consumo) | Global de instancia |
| Catálogos `sri_*` + resolvers | **KEEP** (ADR-037) | — |
| `EmbeddedXmlSchemaProvider` + XSD | **KEEP** | Verificar patrón RIMPE contra XSD oficial (fase 2) |
| RIDE (parsers, plantillas, renderer) | **KEEP** | Muestra `campoAdicional` sin cambios |
| `SalesInvoiceAuthorizedCommunicationHandler` | **KEEP** → **REMOVE EVENTUALLY** (fase 6) | Reemplazado por handler genérico + contributor de Ventas, sin cambiar el correo de factura |
| `docs/FICHA … Versio232.pdf` | **LEGACY** | Referencia 2.34 en `docs/sri/` |

No se clasifica nada como REWRITE.

---

## U. Migración incremental

Cada fase deja build y suites verdes, no duplica comportamiento, se puede revertir con el commit de la fase y
mantiene emitiendo los documentos actuales. El orden 1 → 2 → 3 es firme; 4–6 pueden reordenarse por prioridad
operativa; 7 tiene disparador.

| Fase | Ticket | Alcance | Cambia XML | Rollback |
|---|---|---|---|---|
| 0 | **ZH-SRI-ARCHITECTURE-DESIGN-01** (este) | ADR-038 + este documento | No | — |
| 1 | **ZH-SRI-ANEXO26-PROVIDER-RUC-01** (P0) | Composer + contributor RUC Proveedor; invocado en los 2 orquestadores; tests (§X) | Sí, solo `infoAdicional` | Revertir el commit; o `Enabled=false` (sin campo, comportamiento actual) |
| 2 | ZH-SRI-ISSUER-FISCAL-PROFILE-01 | Perfil del emisor; campos de `Company` (agente, Gran Contribuyente); leyenda RIMPE única; `contribuyenteEspecial` en 01/04 si aplica; writer `infoTributaria`; contributor Gran Contribuyente | Sí (`infoTributaria`, `infoAdicional`) | Commit; los campos nuevos de `Company` son nullable |
| 3 | ZH-EDOC-SPECIFICATION-VERSIONING-01 | `SpecificationVersion` (columna aditiva); validador por `xml.Version`; manifest verificado | No | Columna nullable ignorada |
| 4 | ZH-EDOC-RETRY-JOB-TENANT-01 + ZH-EDOC-RECOVERY-ARCHITECTURE-01 | Job genérico cross-tenant, ventana 24 h, `IElectronicDocumentStatusService`, detector de origen sin ED | No | Preferencia `auto_retry_enabled` |
| 5 | ZH-EDOC-REJECTION-RECOVERY-01 | Interpretación tipada (recepción/autorización), código 50, R14, corrección de NAT con misma clave | No (comportamiento de pipeline) | Golden tests de decisión |
| 6 | ZH-EDOC-COMMUNICATIONS-01 | Handler genérico + contributors de destinatario; NC y Retención por correo | No | Preferencia `email_on_authorization` |
| 7 | (con el primer tipo nuevo 03/05/06) | Contrato del sobre, orquestador genérico, clave de acceso en Domain, mover `RetentionElectronicDocumentXmlService` | No (byte a byte) | Commit |
| transversal | SriComplianceTests | Se construye incrementalmente desde la fase 1 | — | — |

Cada fase que cambie salida XML de la infraestructura CLOSED registra un addendum en ADR-023 (causa 1: cambio
obligatorio del SRI) o (causa 2: bug demostrado).

---

## V. Estrategia de compliance (`SriComplianceTests`)

| Familia | Verifica | Dónde | Estado |
|---|---|---|---|
| **Catalog compliance** | Globalidad, vigencias, cobertura de tablas, fail-closed, inmutabilidad | `ERP.Infrastructure.Tests/Persistence/SriCatalogs/SriCatalogComplianceTests` | ✅ existe (ADR-037); se extiende por catálogo |
| **XML compliance** | Golden XML por tipo × escenario validado contra el XSD oficial; reglas no cubiertas por el XSD: `infoAdicional` ≤ 15, `RUC Proveedor` presente cuando aplica, orden de `infoTributaria`, leyendas RIMPE, vectores de clave de acceso (módulo 11) | `ERP.Application.Tests/ElectronicDocuments/Compliance` (+ integración XSD en `ERP.Infrastructure.Tests`) | Parcial (tests de builder + XSD); se crea la carpeta en la fase 1 |
| **Protocol compliance** | Parsing contra los ejemplos oficiales (recepción, autorización, ConsultaComprobante); tabla de decisión 70/43/45, TIMEOUT, NO AUTORIZADO, RECHAZADA/99 | `ERP.Infrastructure.Tests/Services/Sri` (`SriSoapClientTests`, `SriSoapClientConsultaComprobanteTests`) + `ElectronicDocumentIssuerReceptionTests` | ✅ existe; se agrega la tabla de decisión como golden en la fase 5 |
| **Specification compatibility** | Matriz de requisitos de la ficha (IDs estables, p. ej. R01–R35 de la auditoría 2.34) → test(s) que cubren cada requisito; `SriTechnicalSpecification.Current` tiene fila en `sri_normative_source`; `manifest.activeVersion == builder` | `ERP.Application.Tests/ElectronicDocuments/Compliance/SriSpecificationMatrix` | Nuevo (fase 3) |
| **Architecture** | Reglas de §R | `ERP.Architecture.Tests` | Nuevo (fase 1, ratchet) |

**Cuando aparezca la ficha 2.35:**
1. Registrar la especificación: fila en `sri_normative_source` (documento, versión, SHA-256, URL).
2. Comparar solo el historial de cambios de la ficha contra 2.34 y actualizar la matriz de requisitos (nuevos
   IDs; los existentes conservan su ID).
3. Actualizar catálogos (ADR-037, migración nueva), reglas o contributors afectados.
4. Correr compliance: lo que falle es un gap real.
5. Abrir tickets solo para esos gaps. **No se reaudita el ERP desde cero.**
6. Cambiar `SriTechnicalSpecification.Current` a `2.35` en el mismo release que cierra los gaps obligatorios.

---

## W. ADR

[ADR-038 — SRI Electronic Compliance Architecture](../decisions/ADR-038-sri-electronic-compliance-architecture.md)
(D1–D17).

---

## X. Diseño exacto del próximo P0 — ZH-SRI-ANEXO26-PROVIDER-RUC-01

```
SystemProviderSettings (global, Id=1)
        │  ISystemProviderSettingsRepository.GetAsync
        ▼
SystemProviderRucAdditionalInfoContributor   (Order = 100, AppliesTo = todos, regla definitiva §J:
        │                                     IssueDate < EffectiveDate ⇒ sin campo;
        │                                     IssueDate ≥ EffectiveDate ⇒ Enabled + Ruc válido, o falla cerrado;
        │                                     EffectiveDate null + Enabled ⇒ falla cerrado)
        │
        ▼
IElectronicDocumentAdditionalInfoComposer   (reglas R1–R9, §I.3)
        ▲                         ▲
 CommercialElectronicDocument-   RetentionElectronicDocument-
 XmlSupplier (01, 04)            XmlService (07; pipeline + vista previa XML/RIDE)
        │  data with { AdditionalInfo = compuesta }
        ▼
InvoiceXmlBuilder / CreditNoteXmlBuilder / RetentionXmlBuilder   (sin cambios: ya transcriben AdditionalInfo)
        ▼
XSD → firma → SRI → XML autorizado
        ▼
RIDE genérico existente (Invoice/CreditNote/RetentionRideXmlParser → AdditionalInfoSection)  (sin cambios)
```

**Archivos previstos (implementación del P0, no de este ticket):**

| Archivo | Cambio |
|---|---|
| `ERP.Domain/Modules/SriCatalogs/Constants/SriAdditionalInfoFieldNames.cs` (nuevo) | `SystemProviderRuc = "RUC Proveedor"` con cita Ficha 2.34 Anexo 26 |
| `ERP.Application/Modules/ElectronicDocuments/AdditionalInfo/*` (nuevo) | contexto, interfaces, `ElectronicDocumentAdditionalInfoComposer`, `SystemProviderRucAdditionalInfoContributor` |
| `ERP.Application/Modules/ElectronicDocuments/Services/CommercialElectronicDocumentXmlSupplier.cs` | Invocar composer entre provider y builder (el resolver lo instancia al vuelo: pasarle el composer) |
| `ERP.Application/Modules/ElectronicDocuments/Services/ElectronicDocumentXmlSupplierResolver.cs` | Inyectar el composer para construir el supplier comercial |
| `ERP.Application/Modules/Retentions/Services/RetentionElectronicDocumentXmlService.cs` | Invocar composer entre provider y builder |
| `ERP.Application/DependencyInjection.cs` / `ERP.Infrastructure/DependencyInjection.cs` | Registro del composer y del contributor |
| Códigos de error (`ApiResponseCodes.ElectronicDocuments`) | `ELECTRONIC_DOCUMENT_ADDITIONAL_INFO_INVALID`, `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED` |
| `SystemProviderSettings.cs` | Solo actualizar el comentario "NO se inyecta todavía en ningún XML" |
| `docs/architecture/frozen-infrastructure.md` | Frontera emisor ↔ proveedor: campo confirmado (Anexo 26, `campoAdicional`) |
| `docs/decisions/ADR-023-…md` | Addendum causa 1 (Ficha 2.34 Anexo 26) |

**Sin cambios:** builders, providers, validadores, XSD, firma, SOAP, Issuer, jobs, RIDE, frontend,
`SystemProviderSettings` (entidad/tabla/endpoint), migraciones.

**Tests requeridos:**

| Nivel | Casos |
|---|---|
| Composer (unit) | orden normativo → origen; reservado vs. "Observación" con el mismo nombre; duplicados; longitud 0/300/301; 15 vs 16; contributor que falla → `Failure`; resultado determinístico |
| Contributor (unit) | Una prueba por fila de la tabla de §J: (1) sin fila, y `EffectiveDate=null`+`Enabled=false` → sin campo; (2) `EffectiveDate=null`+`Enabled=true` → fallo; (3) `IssueDate` anterior a `EffectiveDate` con `Enabled` true y false, y con RUC inválido → sin campo y sin fallo; (4a) `IssueDate` igual y posterior a `EffectiveDate`, `Enabled=true`, RUC válido → campo; (4b) `Enabled=false` → fallo; (4c) RUC vacío o ≠ 13 dígitos → fallo; el resultado no depende del reloj (mismo `IssueDate`, distinto "hoy" → mismo resultado); aplica a 01/04/07 |
| Fecha límite | Con la misma configuración: `IssueDate = EffectiveDate − 1 día` → sin campo; `IssueDate = EffectiveDate` → campo |
| Pipeline (fail-closed) | Factura/NC/Retención en los casos 2, 4b y 4c → ED `Failed` con `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED`, sin XML almacenado, sin firma ni envío; corregir la configuración y reintentar → XML con el campo |
| Históricos | ED `Signed`/`Authorized` no se regeneran al cambiar `SystemProviderSettings`; `Draft`/`Failed` aplican la regla por su `IssueDate` |
| Orquestadores | Factura con "Observación" + RUC (orden y 2 campos); NC y Retención solo RUC; deshabilitado → XML idéntico al actual (golden) |
| XSD (integración) | XML de los 3 tipos con `RUC Proveedor` pasa el XSD oficial |
| Retención vista previa | `GenerateRetentionXmlUseCases` y RIDE de vista previa incluyen el campo |
| RIDE | Los 3 parsers exponen `RUC Proveedor` en `AdditionalInfo` |
| Regresión | `GetDataAsync_Issuer_proviene_de_Company_y_SriSettings_nunca_de_SystemProviderSettings` sigue verde; `infoTributaria` no cambia |
| Arquitectura | Providers y builders no referencian `ISystemProviderSettingsRepository`; solo los 2 orquestadores invocan `Build` |

**Prerrequisitos de cierre:**
- Implementación y tests: no dependen de ningún dato pendiente. La regla de §J es definitiva.
- Antes del despliegue: verificar en cada instalación que `system_provider_settings` no esté en la fila 2
  (`Enabled = true` sin `EffectiveDate`), porque bloquearía toda la emisión electrónica.
- Despliegue: (1) confirmación administrativa de que ZH Technologies figura en el listado de proveedores;
  (2) fecha legal exacta de exigibilidad registrada en `SystemProviderSettings.EffectiveDate` (no calculada ni
  hardcodeada); (3) `Enabled = true` con el RUC de ZH. Mientras falten, la instalación queda en la fila 1 y el
  campo se omite (riesgo residual de §J).
- Prueba real en `celcer` de al menos una factura con el campo, configurando en la instancia de pruebas una
  `EffectiveDate` igual o anterior a la fecha de emisión (sin flags ni lógica especial).
- Documentos ya firmados o autorizados no se regeneran; `Draft`/`Failed` regenerados aplican la regla por su
  `IssueDate`.

---

## Decisiones abiertas

| Id | Decisión | Dueño |
|---|---|---|
| DA-1 | **Resuelta en lo normativo** (§J): Res. NAC-DGERCGC26-00000027, RO No. 335 (Quinto Suplemento) del 28/07/2026, 60 días hábiles. **Pendiente solo lo administrativo:** confirmación del registro de ZH Technologies en el listado de proveedores y fecha legal exacta para `EffectiveDate` | ZH (administrativo) |
| DA-2 | **Resuelta** (§J): `EffectiveDate` es la fecha de aplicabilidad; antes de ella nunca se emite el campo (sin emisión anticipada); desde ella, `Enabled=false` o un RUC inválido fallan cerrado; `Enabled=true` sin `EffectiveDate` falla cerrado | — |
| DA-3 | Perfil fiscal real del piloto (agente de retención, Gran Contribuyente, RIMPE NP, contribuyente especial en 01/04) | Fiscal |
| DA-4 | Leyenda RIMPE como dato de catálogo con vigencia (= ADR-037 DR-6) y verificación de XSD oficiales | Fiscal + arquitectura |
| DA-5 | Clasificación de códigos SRI como atributo de `sri_error_code` (fase 5) | Arquitectura |
| DA-6 | Retención 1.0.0 → 2.0.0 (auditoría R25): solo cambia builder + versión, no la arquitectura | Fiscal |
