# RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B — Diseño del wiring mínimo de autorización SRI para Retención

## Estado

**Aprobado como decisión técnica.** 2026-09-04.

Este documento **no implementa cambios por sí mismo**. Continúa [RETENTIONS-SRI-AUTHORIZATION-INTEGRATION-AUDIT-04A](../architecture/) (auditoría sin cambios de código) y fija el contrato exacto de la extensión mínima antes de tocar `ElectronicDocumentIssuer` (infraestructura sensible, adyacente a ADR-023 FROZEN). Cualquier implementación futura (`RETENTIONS-SRI-SCHEMA-VALIDATOR-04C`, `RETENTIONS-SRI-AUTHORIZATION-WIRING-04D`, `RETENTIONS-SRI-AUTHORIZATION-TRIGGER-04E`) requiere su propia entrega, con sus propios tests, respetando lo decidido aquí.

## Contexto

- 04A confirmó que firma (`XadesBesSigner`), envío/consulta SOAP (`SriSoapClient`), storage de XML (`ElectronicDocumentXmlStorageService`), estado (`ElectronicDocument`), reintentos, dead-letter, Monitor y RIDE-desde-XML-autorizado (`ElectronicDocumentRideSourceXmlProvider`, que **ya traduce** `"Retention" → RideDocumentType.Retention`) son genéricos por `ElectronicDocumentType` — nunca conocen Factura/Nota de Crédito por nombre.
- El único acoplamiento real está en el primer tramo de `ElectronicDocumentIssuer.RunPipelineAsync`: `IElectronicDocumentDataProviderResolver` → `IElectronicDocumentDataProvider` → `ElectronicDocumentData` → `IElectronicDocumentXmlBuilderResolver` → `IElectronicDocumentXmlBuilder` → `ElectronicDocumentXml`. Ambos resolutores están fijos a `ElectronicDocumentData`, la forma comercial de Factura/Nota de Crédito.
- Retención ya tiene su propio camino completo y probado hasta `ElectronicDocumentXml`: `RetentionElectronicDocumentDataProvider` → `RetentionXmlBuilder`, orquestados por `IRetentionElectronicDocumentXmlService.GenerateXmlAsync(ElectronicDocumentSourceReference, ct) : Task<Result<ElectronicDocumentXml>>` (03E) — la firma de este método coincide, dato por dato, con lo que el pipeline necesita.

## Decisiones aprobadas

### B. Nombre final de la abstracción

**`IElectronicDocumentXmlSupplier`** (y su resolver, **`IElectronicDocumentXmlSupplierResolver`**).

Se descartan las otras dos opciones del ticket:
- `IElectronicDocumentXmlSource` — colisiona semánticamente con `ElectronicDocumentSourceReference` (que ya usa "Source" para el documento de origen del negocio, no para el XML). Habría dos conceptos distintos ambos llamados "Source" en la misma firma de método — confuso.
- `IElectronicDocumentXmlGenerator` — "Generator" ya se usa en el código para generadores de códigos/QR/barras (`IQrCodeGenerator`, `IRideQrCodeGenerator`, `IRideBarcodeGenerator`) con un significado distinto (transformación pura sin I/O). Reutilizar la palabra para "orquesta provider+builder o el servicio de Retención" sería inconsistente con ese uso ya establecido.

"Supplier" no colisiona con ningún término ya usado (`Provider` = paso 1 del camino comercial; `Builder` = paso 2 del camino comercial; `Source` = referencia al documento de origen de negocio) y describe con precisión el rol: "quien me entrega el `ElectronicDocumentXml`, sin que me importe cómo lo hizo por dentro".

### C. Contrato propuesto

```csharp
namespace ERP.Application.Modules.ElectronicDocuments.Services;

public interface IElectronicDocumentXmlSupplier
{
    ElectronicDocumentType DocumentType { get; }

    Task<Result<ElectronicDocumentXml>> BuildXmlAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    );
}

public interface IElectronicDocumentXmlSupplierResolver
{
    IElectronicDocumentXmlSupplier? Resolve(ElectronicDocumentType documentType);
}
```

- Input `ElectronicDocumentSourceReference` (ya existe, sin cambios — `TenantId`/`CompanyId`/`SourceEntityId`, mismo tipo que ya consumen `IElectronicDocumentDataProvider.GetDataAsync` y `IRetentionElectronicDocumentXmlService.GenerateXmlAsync`).
- Output `Result<ElectronicDocumentXml>` (ya existe, sin cambios).
- `CancellationToken ct = default` (mismo default que el resto de la interfaz pública de ElectronicDocuments).
- `Resolve` devuelve `IElectronicDocumentXmlSupplier?` (nullable) — **mismo patrón exacto** que `IElectronicDocumentXmlBuilderResolver`/`IElectronicDocumentDataProviderResolver`/`IRideXmlParserResolver`: nunca lanza, el llamador decide qué significa "no hay supplier registrado" (hoy: `FailAsync` con el mismo mensaje que ya usan los dos `if (... is null)` que este supplier reemplaza).

**Ajuste adicional necesario, detectado en este diseño (no en el ticket original):** `RunPipelineAsync` hoy llama `document.SetEnvironment(dataResult.Value!.Emission.Environment)` usando el `ElectronicDocumentData` intermedio, que con esta abstracción deja de estar disponible en `RunPipelineAsync`. `ElectronicDocumentXml` (DTO en `ERP.Application/Modules/ElectronicDocuments/DTOs/ElectronicDocumentData.cs` — sí, ambos tipos comparten archivo hoy) **no** expone `Environment`. Se aprueba agregar un campo `string Environment` a `ElectronicDocumentXml` — cambio aditivo, sin romper ningún consumidor existente (todo constructor con parámetros nombrados ya usado en tests/producción seguiría compilando si se agrega al final con posición explícita, o se actualiza por nombre). Todo productor de `ElectronicDocumentXml` (`InvoiceXmlBuilder`, `CreditNoteXmlBuilder`, `RetentionXmlBuilder`) ya conoce `Emission.Environment` en el momento de construir el XML — no es un dato nuevo a obtener, solo un campo nuevo a poblar. Esto se implementa en `RETENTIONS-SRI-AUTHORIZATION-WIRING-04D` junto con el resto del wiring, no antes.

### D. Ubicación propuesta

`ERP.Application/Modules/ElectronicDocuments/Services/` (junto a `IElectronicDocumentIssuer.cs`, `IElectronicDocumentSigningService.cs`, `IElectronicDocumentXmlStorageService.cs`, `IElectronicDocumentDataProviderResolver.cs`, `IElectronicDocumentXmlBuilderResolver.cs`).

Se descarta `XmlBuilders/`: esa carpeta es específicamente para `IElectronicDocumentXmlBuilder`, cuyo contrato de entrada es `ElectronicDocumentData` — un supplier no es un builder (no construye XML él mismo, orquesta a quien lo construya), y mezclarlo ahí sugeriría erróneamente que tiene la misma forma de entrada.

### E. Diseño del resolver

`ElectronicDocumentXmlSupplierResolver` **no** usa un `switch`/`if (documentType == Retention)`. Regla única, genérica: **un supplier explícitamente registrado tiene prioridad; si no hay uno, se cae automáticamente al camino comercial (provider+builder) para ese tipo, si existe.**

```csharp
public sealed class ElectronicDocumentXmlSupplierResolver : IElectronicDocumentXmlSupplierResolver
{
    private readonly IReadOnlyDictionary<ElectronicDocumentType, IElectronicDocumentXmlSupplier> _explicitSuppliers;
    private readonly IElectronicDocumentDataProviderResolver _dataProviderResolver;
    private readonly IElectronicDocumentXmlBuilderResolver _xmlBuilderResolver;

    public ElectronicDocumentXmlSupplierResolver(
        IEnumerable<IElectronicDocumentXmlSupplier> explicitSuppliers,
        IElectronicDocumentDataProviderResolver dataProviderResolver,
        IElectronicDocumentXmlBuilderResolver xmlBuilderResolver)
    {
        _explicitSuppliers = explicitSuppliers.ToDictionary(s => s.DocumentType);
        _dataProviderResolver = dataProviderResolver;
        _xmlBuilderResolver = xmlBuilderResolver;
    }

    public IElectronicDocumentXmlSupplier? Resolve(ElectronicDocumentType documentType)
    {
        if (_explicitSuppliers.TryGetValue(documentType, out var explicitSupplier))
            return explicitSupplier;

        var dataProvider = _dataProviderResolver.Resolve(documentType);
        var xmlBuilder = _xmlBuilderResolver.Resolve(documentType);
        return dataProvider is null || xmlBuilder is null
            ? null
            : new CommercialElectronicDocumentXmlSupplier(documentType, dataProvider, xmlBuilder);
    }
}
```

Esta regla es **general, no específica de Retención**: cualquier tipo documental futuro que necesite su propia forma de datos (p. ej. si Guía de Remisión algún día también necesitara un modelo propio) se resuelve de la misma manera, registrando su propio `IElectronicDocumentXmlSupplier` — sin volver a tocar este resolver.

### F. Diseño de supplier comercial

`CommercialElectronicDocumentXmlSupplier` — **no se registra explícitamente en DI**. Es un adaptador delgado que el resolver instancia al vuelo (ver E), parametrizado con el `IElectronicDocumentDataProvider`/`IElectronicDocumentXmlBuilder` ya resueltos para ese tipo. Reproduce exactamente las dos líneas que hoy hace `RunPipelineAsync`:

```csharp
public sealed class CommercialElectronicDocumentXmlSupplier : IElectronicDocumentXmlSupplier
{
    private readonly IElectronicDocumentDataProvider _dataProvider;
    private readonly IElectronicDocumentXmlBuilder _xmlBuilder;

    public ElectronicDocumentType DocumentType { get; }

    public CommercialElectronicDocumentXmlSupplier(
        ElectronicDocumentType documentType,
        IElectronicDocumentDataProvider dataProvider,
        IElectronicDocumentXmlBuilder xmlBuilder)
    {
        DocumentType = documentType;
        _dataProvider = dataProvider;
        _xmlBuilder = xmlBuilder;
    }

    public async Task<Result<ElectronicDocumentXml>> BuildXmlAsync(
        ElectronicDocumentSourceReference reference, CancellationToken ct = default)
    {
        var dataResult = await _dataProvider.GetDataAsync(reference, ct);
        if (!dataResult.IsSuccess)
            return Result<ElectronicDocumentXml>.Failure(
                dataResult.Error ?? "No se pudo obtener el modelo común del documento de origen.",
                dataResult.Code);

        return _xmlBuilder.Build(dataResult.Value!) with
        {
            /* Environment poblado aquí desde dataResult.Value!.Emission.Environment — ver sección C */
        };
    }
}
```

Ni `InvoiceXmlBuilder`, ni `CreditNoteXmlBuilder`, ni `IElectronicDocumentDataProvider` de ningún tipo comercial cambian. El registro DI de ambos resolutores comerciales (`IElectronicDocumentDataProviderResolver`, `IElectronicDocumentXmlBuilderResolver`) tampoco cambia — el nuevo resolver los consume tal cual, sin re-registrarlos.

### G. Diseño de supplier de Retention

`RetentionElectronicDocumentXmlSupplier` — delegado de una sola línea, sin lógica propia:

```csharp
public sealed class RetentionElectronicDocumentXmlSupplier : IElectronicDocumentXmlSupplier
{
    private readonly IRetentionElectronicDocumentXmlService _retentionXmlService;

    public RetentionElectronicDocumentXmlSupplier(IRetentionElectronicDocumentXmlService retentionXmlService)
        => _retentionXmlService = retentionXmlService;

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.Retention;

    public Task<Result<ElectronicDocumentXml>> BuildXmlAsync(
        ElectronicDocumentSourceReference reference, CancellationToken ct = default)
        => _retentionXmlService.GenerateXmlAsync(reference, ct);
}
```

Se registra **explícitamente** en DI (a diferencia del comercial, que nace del fallback del resolver) — es la única forma de que el resolver lo encuentre antes de intentar el camino comercial (que para `Retention` de todas formas no encontraría nada, porque no hay `IElectronicDocumentDataProvider`/`IElectronicDocumentXmlBuilder` de tipo comercial registrados para ese valor de enum).

`IRetentionElectronicDocumentXmlService` no cambia — ya tiene exactamente la firma que este supplier necesita desde 03E.

### H. Cambio futuro esperado en `ElectronicDocumentIssuer` (no implementado aquí)

En `RunPipelineAsync`, reemplazar:

```csharp
var provider = _providerResolver.Resolve(request.DocumentType);
if (provider is null) return await FailAsync(document, request.UserId, ct,
    $"No hay un proveedor de datos registrado para el tipo de documento '{request.DocumentType}'.");
var dataResult = await provider.GetDataAsync(reference, ct);
if (!dataResult.IsSuccess) { /* ... FailAsync ... */ }
document.SetEnvironment(dataResult.Value!.Emission.Environment);
var xmlBuilder = _xmlBuilderResolver.Resolve(request.DocumentType);
if (xmlBuilder is null) return await FailAsync(document, request.UserId, ct,
    $"No hay un generador de XML registrado para el tipo de documento '{request.DocumentType}'.");
var xmlResult = xmlBuilder.Build(dataResult.Value!);
if (!xmlResult.IsSuccess) { /* ... FailAsync ... */ }
```

por:

```csharp
var supplier = _xmlSupplierResolver.Resolve(request.DocumentType);
if (supplier is null) return await FailAsync(document, request.UserId, ct,
    $"No hay un generador de XML registrado para el tipo de documento '{request.DocumentType}'.");
var xmlResult = await supplier.BuildXmlAsync(reference, ct);
if (!xmlResult.IsSuccess) { /* ... FailAsync, igual que hoy ... */ }
document.SetEnvironment(xmlResult.Value!.Environment);
```

- `_providerResolver`/`_xmlBuilderResolver` dejan de ser dependencias directas de `ElectronicDocumentIssuer` — se sustituyen por `_xmlSupplierResolver` en el constructor.
- Desde `xmlResult.Value` en adelante (validación XSD, firma, storage, envío, autorización, reintentos, dead-letter, Monitor) **no cambia ni una línea** — todo ese código ya consume solo `ElectronicDocumentXml`/`ElectronicDocumentType`/`document`, nunca `ElectronicDocumentData`.
- El mensaje de error "no hay generador registrado" se unifica en uno solo (hoy son dos mensajes distintos para provider/builder) — cambio de texto aceptable porque ambos eran, en la práctica, la misma situación ("este tipo de documento no está conectado todavía").
- **Tests de regresión obligatorios en 04D**: los tests actuales de `ElectronicDocumentIssuer` (registro, reintentos, dead-letter, recepción, timeline) para Factura/Nota de Crédito deben seguir pasando sin modificación de sus asserts — solo el arreglo de constructor/mocks cambia (mockear `IElectronicDocumentXmlSupplierResolver` en vez de los dos resolutores, o construir el resolver real con los dos mocks de siempre — a decidir en 04D según qué sea menos invasivo para los tests existentes).

### I. Decisión sobre `RetentionXmlSchemaValidator`

**Confirmado: debe ir antes del wiring principal.** `RunPipelineAsync` llama a `_schemaValidatorResolver.Resolve(request.DocumentType)` inmediatamente después de obtener el XML, **incondicionalmente** — sin importar de dónde vino ese XML (camino comercial o supplier de Retención). Si `RetentionXmlSchemaValidator` no existe/no está registrado, el pipeline fallaría ahí con "No hay un validador de esquema registrado", sin importar qué tan bien esté hecho el wiring de 04D.

**Fase recomendada: `RETENTIONS-SRI-SCHEMA-VALIDATOR-04C`, antes de `RETENTIONS-SRI-AUTHORIZATION-WIRING-04D`.** Es de bajo riesgo y cero dependencia de 04D — se puede implementar y probar de forma aislada (mismo patrón que `InvoiceXmlSchemaValidator`, contra `ComprobanteRetencion_V1.0.0.xsd`, ya embebido y resoluble desde 03B/03D). No requiere que exista todavía ningún supplier ni ningún cambio en `ElectronicDocumentIssuer`.

### J. Decisión sobre activación de manifest

**No se activa en 04C ni en 04D.** `manifest.json.Retention.activeVersion` permanece `null` hasta que se cumplan las tres condiciones simultáneamente:

1. `RetentionXmlSchemaValidator` existe y está registrado (04C).
2. El wiring completo (`IElectronicDocumentXmlSupplier`/Resolver + cambio en `ElectronicDocumentIssuer`) existe y pasa sus tests de regresión (04D).
3. Un `RegisterAsync` real para una retención `Issued` fue ejercitado de punta a punta **contra el ambiente de Pruebas del SRI** (no solo tests unitarios/mocks) y llegó a un estado terminal coherente (`Authorized` o `Rejected` con motivo real del SRI, no un error de wiring).

La activación es responsabilidad de **`RETENTIONS-SRI-AUTHORIZATION-TRIGGER-04E`** (o una fase aún más pequeña e independiente inmediatamente después, si se prefiere aislar el cambio de un solo archivo/línea de las demás decisiones de esa fase) — nunca antes, porque `activeVersion` es, por diseño (ver README de `Resources/SRI/`), documentación de "qué usa hoy el validador activo del ERP" — activarlo antes de una autorización real verificada sería una afirmación falsa en ese documento.

### K. Decisión sobre disparo de `RegisterAsync` para Retention

**Opción B — manual/endpoint, para reducir riesgo inicial.** Se confirma la recomendación del ticket.

Razón: permite ejercitar el flujo completo contra Pruebas de forma controlada (QA dispara la emisión electrónica explícitamente, retención por retención) antes de comprometerse a que **toda** retención `Issued` dispare automáticamente firma+envío+autorización SRI de inmediato — un fallo de diseño en la automatización (p. ej. disparar antes de que el usuario confirme que los datos del proveedor están completos) sería mucho más caro de revertir una vez en producción con emisión automática.

La idempotencia no requiere ningún mecanismo nuevo: `ElectronicDocumentIssuer.RegisterAsync` ya es idempotente por diseño (`GetBySourceAsync` + reanudación si Draft/Failed, `Conflict` si ya existe en cualquier otro estado, e índice único `uq_electronic_document_source` como barrera atómica de concurrencia) — el endpoint/botón manual solo necesita invocar `RegisterAsync` con `SourceModule="Retentions"`/`SourceEntityId=RetentionDocument.Id`, sin lógica de idempotencia propia.

Automatización (Opción A) queda como evolución futura explícita, condicionada a que la Opción B haya validado el flujo en producción real durante un período razonable — no se fija una fecha aquí, es una decisión de negocio, no técnica.

### L. Decisión sobre endpoints preview vs. autorizados

**Confirmado: se mantienen separados, sin mezclar.**

- Los endpoints de 03F (`GET /api/v1/retentions/{id}/electronic/xml`, `GET /api/v1/retentions/{id}/ride/pdf`) siguen existiendo tal cual, para QA/diagnóstico/vista previa — nunca firman, nunca persisten, nunca dependen de que exista un `ElectronicDocument`.
- El acceso al XML/PDF **autorizado** se hace, cuando exista un `ElectronicDocument` de tipo `Retention`, a través del mecanismo genérico ya existente (`ElectronicDocumentsController.GetXml`/`GetElectronicDocumentXmlQuery` con `ElectronicDocumentXmlVariant.Authorized`, y el `RidePipeline` — este último requiere resolver primero la incompatibilidad de `RideModel`/`RetentionRideModel`, ya documentada como pendiente desde 03C/03E) — **nunca** agregando una rama "si está autorizado, devuelve otra cosa" dentro de los endpoints de 03F.
- Fallback si todavía no está autorizado: mismo criterio que Ride ya usa para Factura/Nota de Crédito (`PendingSource`/`NotApplicable`, nunca inventar un XML/PDF "autorizado" que no existe).

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Colar un `if (documentType == Retention)` dentro de `ElectronicDocumentIssuer` "por simplicidad" en 04D | El diseño del resolver (sección E) ya no necesita esa rama — la regla es genérica (explícito > fallback comercial). Revisar en code review de 04D que no se reintroduzca. |
| Refactor masivo hacia interfaces genéricas (`IElectronicDocumentXmlBuilder<TData>`) | Explícitamente descartado — el contrato de esta fase no usa genéricos; `IElectronicDocumentXmlSupplier` es una interfaz concreta sobre tipos ya concretos (`ElectronicDocumentSourceReference`/`ElectronicDocumentXml`). |
| Duplicar firma/SOAP/storage en un pipeline paralelo | Explícitamente descartado desde 04A — la extensión toca solo el tramo antes de la firma; todo lo demás se reutiliza sin cambios. |
| Activar `manifest.json` antes de tener validador+wiring+prueba real | Fijado en la sección J: tres condiciones simultáneas, nunca antes. |
| Cambiar el XML/comportamiento de Factura/Nota de Crédito al introducir el supplier | El supplier comercial (sección F) reproduce exactamente las mismas dos llamadas que ya existen, en el mismo orden, con los mismos resolutores ya registrados — 04D debe incluir tests de regresión explícitos que confirmen bytes/estructura idénticos. |
| El campo nuevo `Environment` en `ElectronicDocumentXml` rompe algún consumidor existente | Es un campo aditivo; todos los constructores existentes de ese record en código de producción usan argumentos con nombre (ver `InvoiceXmlBuilder`/`CreditNoteXmlBuilder`/`RetentionXmlBuilder`) — agregarlo exige tocar esos tres sitios en 04D (poblarlo), pero no rompe compilación en ningún otro consumidor si se agrega con valor por defecto o se actualizan los tres builders en el mismo commit. Confirmar en 04D. |

## Fases siguientes recomendadas

1. **`RETENTIONS-SRI-SCHEMA-VALIDATOR-04C`** — `RetentionXmlSchemaValidator` contra `ComprobanteRetencion_V1.0.0.xsd`, registrado en DI junto a `InvoiceXmlSchemaValidator`/`CreditNoteXmlSchemaValidator`. Sin dependencia de 04D.
2. **`RETENTIONS-SRI-AUTHORIZATION-WIRING-04D`** — implementar `IElectronicDocumentXmlSupplier`/`IElectronicDocumentXmlSupplierResolver`/`CommercialElectronicDocumentXmlSupplier`/`RetentionElectronicDocumentXmlSupplier` (secciones C, E, F, G), el campo `Environment` en `ElectronicDocumentXml`, el cambio en `RunPipelineAsync` (sección H), registro DI, y tests de regresión completos de Factura/Nota de Crédito + tests nuevos de Retención.
3. **`RETENTIONS-SRI-AUTHORIZATION-TRIGGER-04E`** — disparo manual/endpoint de `RegisterAsync` para retenciones `Issued` (sección K), prueba end-to-end contra Pruebas SRI, y activación de `manifest.json.Retention.activeVersion` (sección J) una vez verificado.
4. Posterior — resolver XML/PDF autorizado de Retención (sección L) y, si se decide, automatizar el disparo (Opción A de la sección K).

## Confirmación explícita

No se implementó ningún cambio de código en esta fase. No se creó ninguna migración. No se modificó `ElectronicDocumentIssuer` ni ningún registro de DI. No se modificó `manifest.json`. No se firmó ningún XML. No se envió nada al SRI. No se tocó SaaS/Platform. Este documento es exclusivamente de diseño y decisión técnica.
