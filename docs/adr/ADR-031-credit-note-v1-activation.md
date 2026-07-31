# ADR-031: Activación de Nota de Crédito V1.1.0 (extensión controlada de ADR-023)

## Status

**Accepted.** 2026-07-31. Extiende de forma aditiva y controlada el núcleo FROZEN de `ElectronicDocuments` (ADR-023) bajo la causa 1 de "Cambios permitidos" no aplica aquí directamente — esta ADR es la habilitación explícita que ADR-023 exige para agregar un builder/provider/validador de un tipo de comprobante nuevo ("es una funcionalidad nueva, requiere su propia fase con roadmap explícito"). Fase 11 de `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md`.

## Contexto

El módulo `SalesReturn` (devolución de venta) quedó completo hasta el gate de gobernanza en fases previas de la misma sesión:

- `SalesReturn`/`SalesReturnDetail`/`SalesReturnRefundAllocation` (Domain), persistencia, `AuthorizeSalesReturnHandler` (advisory lock + reversión de inventario + Caja/CxC + Contabilidad + Auditoría).
- `CreditNoteXmlBuilder` — genera el XML de Nota de Crédito y ya fue validado contra `NotaCredito_V1.1.0.xsd` **directamente** (invocando `EmbeddedXmlSchemaProvider` desde el test, sin pasar por el pipeline productivo) en `CreditNoteXmlBuilderTests`.
- `SalesReturnCreditNoteDataProvider` — resuelve los datos de la Nota de Crédito desde `SalesReturn` + la factura original.
- Registro DI de ambos como `IElectronicDocumentDataProvider`/`IElectronicDocumentXmlBuilder` (defecto de wiring encontrado y corregido en la ronda de validación E2E anterior de esta misma sesión).
- Captura del secuencial SRI `"04"` vía `IDocumentSequenceRepository.CaptureNextAsync` (infraestructura FROZEN, ADR-019), congelado una única vez en `SalesReturn.SetCreditNoteDocumentNumber`.
- `AuthorizeSalesReturnHandler` llama a `IElectronicDocumentIssuer.RegisterAsync(..., ElectronicDocumentType.CreditNote, ...)` después del commit de la devolución — un fallo aquí es informativo, nunca revierte la autorización ya persistida (mismo criterio que Factura).
- `SalesReturnEndToEndTests`: 23/23 en verde, incluyendo el Escenario 18 (registro del `ElectronicDocument` de tipo CreditNote).
- `DocumentSequenceRepository.CaptureNextAsync` corregido para participar de una transacción ambiente ya abierta por el caller (defecto real encontrado y corregido en la ronda anterior de esta misma sesión, sin tocar la API pública ni la estrategia de locking).

El estado deliberado que quedaba pendiente, documentado explícitamente en el propio ADR-023 ("Límites" y "Cambios prohibidos") y en el plan P0-01 (Fase 11, "gate de gobernanza"):

```json
"CreditNote": { "activeVersion": null, ... }
```

### Hallazgo previo a modificar código: dónde está realmente el gate

Antes de tocar `manifest.json` se inspeccionó `EmbeddedXmlSchemaProvider.LoadSchemaSet` (el componente que efectivamente lee el manifiesto) y se confirmó que **no lee `activeVersion` en absoluto** — resuelve el `XmlSchemaSet` únicamente por la clave `(documentType, schemaVersion)` que le pasa el validador (`typeEntry.Versions.FirstOrDefault(v => v.Version == schemaVersion)`). `activeVersion` es un campo de registro/documentación en el propio manifiesto (qué versión es "la vigente" para ese tipo), no una condición evaluada en tiempo de ejecución.

El gate real, verificado en `ElectronicDocumentIssuer.RunPipelineAsync`, es la resolución del validador de esquema:

```csharp
var schemaValidator = _schemaValidatorResolver.Resolve(request.DocumentType);
if (schemaValidator is null)
    return await FailAsync(document, request.UserId, ct,
        $"No hay un validador de esquema registrado para el tipo de documento '{request.DocumentType}'.");
```

`ElectronicDocumentSchemaValidatorResolver` se construye por DI a partir de `IEnumerable<IElectronicDocumentSchemaValidator>` — hoy solo `InvoiceXmlSchemaValidator` estaba registrado. Sin un `IElectronicDocumentSchemaValidator` para `CreditNote`, el pipeline fallaba en esta etapa **antes** de siquiera intentar resolver el XSD — independientemente del valor de `activeVersion`. Esto corrige una imprecisión del plan P0-01 (que asumía que el fallo ocurría "en el paso de validación XSD por falta de `activeVersion`"): el fallo real ocurre un paso antes, por ausencia de validador registrado. Se documenta aquí explícitamente en vez de ocultarlo, según la instrucción de esta fase.

## Decisión

### 1. Activar `CreditNote.activeVersion` en `manifest.json` (cambio de una línea)

```diff
     "CreditNote": {
-      "activeVersion": null,
+      "activeVersion": "1.1.0",
```

Ningún otro tipo de documento (`Invoice`, `DebitNote`, `ShippingGuide`, `Retention`, `PurchaseSettlement`) se modifica. Aunque `EmbeddedXmlSchemaProvider` no evalúa este campo hoy, se activa como parte de esta ADR porque es el registro de verdad documental de qué versión está en uso productivo para cada tipo — mantenerlo en `null` mientras el tipo ya tiene builder/provider/validador reales sería una inconsistencia entre el manifiesto y el comportamiento real del sistema.

### 2. Pieza aditiva estrictamente necesaria: `CreditNoteXmlSchemaValidator`

Nueva clase `ERP.Application/Modules/ElectronicDocuments/SchemaValidation/CreditNoteXmlSchemaValidator.cs` — copia estructural exacta de `InvoiceXmlSchemaValidator` (mismo contrato `IElectronicDocumentSchemaValidator`, misma lógica de validación XSD vía `IXmlSchemaProvider`), cambiando únicamente `DocumentType => ElectronicDocumentType.CreditNote` y la versión fija `"1.1.0"` (misma convención documentada en el XML doc de `InvoiceXmlSchemaValidator`: la versión es identidad del validador, no un dato leído de ninguna parte).

Registrada en DI en el punto que el propio código ya reservaba explícitamente para esto (`ERP.Infrastructure/DependencyInjection.cs`, comentario preexistente: *"Los demás tipos de comprobante añadirán su propio `IElectronicDocumentSchemaValidator` aquí — nunca tocar el resolver ni `IXmlSchemaProvider`"*). Ni `ElectronicDocumentSchemaValidatorResolver` ni `IXmlSchemaProvider`/`EmbeddedXmlSchemaProvider` se modifican — el mecanismo de extensión por `IEnumerable<T>` ya soportaba esto sin cambios, confirmando que el diseño de ADR-023 anticipó correctamente este punto de extensión.

### 3. Nada más se activa ni se modifica

- `DebitNote`, `ShippingGuide`, `Retention`, `PurchaseSettlement` siguen con `activeVersion: null` y sin validador — fuera de alcance explícito de esta ADR.
- `RunPipelineAsync`, la máquina de estados de `ElectronicDocument`, `IElectronicDocumentIssuer`, `XadesBesSigner`, los clientes SOAP (`SriSoapClient`/`SriReceptionClient`/`SriAuthorizationClient`) no se tocan.
- El dominio `SalesReturn`, Inventario, Caja, CxC, Contabilidad, Auditoría, API y Frontend de SalesReturn no se tocan.
- RIDE de Nota de Crédito queda fuera de esta fase (Fase 12 del plan P0-01, no implementada).

## Validación de la activación

- `CreditNoteXmlSchemaValidatorTests` (nuevo, 5 tests) — mismo patrón que `InvoiceXmlSchemaValidatorTests`: esquema ausente, XML válido, nodo requerido faltante, tipo incorrecto, XML mal formado.
- `CreditNoteXmlBuilderTests` (existente, sin cambios de lógica) — sigue validando el XML generado contra `NotaCredito_V1.1.0.xsd` real, embebido, con `EmbeddedXmlSchemaProvider` invocado directamente.
- `SalesReturnEndToEndTests.Escenario18_...` (existente) — reejecutado tras la activación: el `ElectronicDocument` de tipo `CreditNote` ahora **supera la etapa de validación XSD** (antes fallaba en "no hay validador registrado"); como esta suite no configura un certificado SRI real para el tenant de prueba, el pipeline se detiene en la etapa de firma — sigue sin llegar nunca a `Authorized`, que es exactamente lo que la aserción existente (`CurrentState.Should().NotBe(Authorized)`) ya verificaba, sin necesidad de cambiar la aserción. Se actualizó únicamente el comentario explicativo para reflejar la etapa real donde se detiene ahora.
- No se ejecutó una prueba real contra el ambiente de Pruebas del SRI (`celcer.sri.gob.ec`) en esta fase: este entorno de desarrollo/test no tiene un certificado `.p12` de prueba configurado para ningún tenant, y generar/gestionar uno está fuera del alcance autorizado de esta tarea (fase de gate de gobernanza + activación de manifiesto, no de configuración de certificados). Queda como paso de validación operativa posterior, con el mismo protocolo que se usó para cerrar ADR-023 con Factura (8 comprobantes reales, un rechazo real confirmado).

## Consecuencias

**Positivas:** el pipeline de Nota de Crédito para `SalesReturn` ya no falla trivialmente por ausencia de validador — la brecha real que quedaba entre "XML se genera y valida en tests" y "el pipeline productivo lo intenta emitir" queda cerrada. `manifest.json` vuelve a ser consistente con el estado real de implementación (builder + provider + validador + secuencial, todos activos para CreditNote). El punto de extensión de `ElectronicDocumentSchemaValidatorResolver` diseñado en ADR-023 se usó exactamente como estaba previsto, sin ninguna modificación a componentes FROZEN.

**Negativas / deuda aceptada conscientemente:**
- Sin prueba real contra el SRI todavía — la Nota de Crédito nunca ha sido enviada/autorizada por el servicio real. Es un riesgo conocido y acotado: la estructura XML ya fue validada contra el XSD oficial (estructuralmente correcta), pero el SRI podría rechazarla por una regla de negocio no capturable por XSD (p. ej. referencia a comprobante modificado inexistente en su propio sistema). Condición de remediación: ejecutar el protocolo de prueba real (mismo que Factura) antes de considerar CreditNote apta para un cliente productivo real.
- `manifest.json.activeVersion` sigue sin ser leído por ningún código — es documentación de intención, no un guard funcional. Si en el futuro se decide que `activeVersion` debería bloquear el pipeline activamente (en vez de que la sola presencia de un validador registrado sea el gate), es un cambio de comportamiento de `EmbeddedXmlSchemaProvider`/`ElectronicDocumentSchemaValidatorResolver` (componentes FROZEN de ADR-023) y requeriría su propia ADR — no se implementa aquí por estar fuera del alcance de "activación mínima".

## Alternativas consideradas

| Alternativa | Razón de descarte |
|---|---|
| Dejar `activeVersion: null` indefinidamente, solo registrar el validador | Deja el manifiesto documentalmente inconsistente con el estado real del código — el manifiesto es, por diseño (ver doc de `EmbeddedXmlSchemaProvider`), la única fuente de verdad de qué versión corresponde a cada tipo; no reflejar la activación ahí sería confuso para cualquier lectura futura del archivo. |
| Hacer que `EmbeddedXmlSchemaProvider` valide `activeVersion` antes de resolver el esquema | Modificaría un componente FROZEN de ADR-023 sin bug demostrado ni necesidad real — el gate ya existe de forma efectiva en el resolver de validadores (ausencia de un `IElectronicDocumentSchemaValidator` registrado). Agregar una segunda capa de gate redundante no aporta valor y viola "no modificar por consistencia sin bug demostrado" (ADR-023, Cambios prohibidos). |
| Activar también DebitNote/ShippingGuide/Retention/PurchaseSettlement en la misma ADR | Fuera de alcance — cada uno requiere su propio builder/provider/validador y su propia justificación de negocio (ninguno tiene consumidor real hoy), exactamente el tipo de scope creep que ADR-023 prohíbe explícitamente. |

## Cierre

**Estado: Accepted.** **Alcance: activación de Nota de Crédito V1.1.0 únicamente.** **Fecha: 2026-07-31.** **Responsable: Sebastian Zhinin (Lead/Architect del proyecto).**

Detalle de la implementación funcional completa de SalesReturn/CreditNote (Fases 1-10): ver `P0-01_SALES_RETURN_IMPLEMENTATION_PLAN.md` y `P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md` en la raíz del repositorio. RIDE de Nota de Crédito (Fase 12) y prueba real contra el SRI quedan como trabajo posterior, no bloqueante para este cierre.
