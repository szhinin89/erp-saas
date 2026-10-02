# ADR-038 — Arquitectura de cumplimiento electrónico SRI

**Status:** Accepted · **Fecha:** 2026-10-02 · **Ticket:** ZH-SRI-ARCHITECTURE-DESIGN-01
**Evidencia y diseño detallado:** [`docs/sri/SRI-ELECTRONIC-COMPLIANCE-ARCHITECTURE.md`](../sri/SRI-ELECTRONIC-COMPLIANCE-ARCHITECTURE.md)
**Base:** [`docs/sri/SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md`](../sri/SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md)
**Relacionado:** ADR-023 (ElectronicDocuments v1.0 CLOSED — este ADR **no** la reemplaza: fija cómo evoluciona),
ADR-024, ADR-025 (RIDE), ADR-031 (NC), ADR-034 (fecha de negocio), ADR-036 (retenciones), ADR-037 (catálogos).

## Contexto

ElectronicDocuments emite Factura (01), Nota de crédito (04) y Retención (07) por un pipeline único
(`ElectronicDocumentIssuer`) con firma, cliente SOAP, XSD y ConsultaComprobante alineados con la Ficha Técnica
2.34. Los gaps de la auditoría 2.34 no están en ese núcleo, sino en **datos fiscales comunes que no tienen un
lugar único**:

1. **Información adicional sin punto de composición.** Cada provider arma su `infoAdicional` (Factura
   "Observación", NC y Retención vacía). El Anexo 26 (RUC Proveedor) obligaría a tocar tres providers.
2. **Perfil del emisor proyectado tres veces.** Company + matriz + SriSettings + texto RIMPE (desactualizado
   para Negocio Popular) se repiten en los 3 providers. Faltan agente de retención y Gran Contribuyente.
3. **Versiones sin fuente única.** La versión XML activa está en el builder, en el validador y en
   `manifest.json` (Retención `null`). La versión de la ficha no se registra.
4. **Semántica SRI en strings.** Recepción y autorización se interpretan con `StartsWith("[70]")` y literales;
   solo ConsultaComprobante tiene resultados tipados.
5. **Fronteras implícitas.** Un módulo de negocio (`RetentionAnnulmentService`) usa el gateway SRI directamente;
   recovery y correo existen solo para algunos tipos, sin un límite escrito que evite versiones paralelas.

Existe el riesgo de "resolver" esto con un segundo "SRI Core" o una reescritura del módulo CLOSED. Este ADR lo
descarta y fija una única forma de evolucionar.

## Decisión

- **D1 — Se evoluciona ElectronicDocuments; no hay "SRI Core" paralelo.** `ElectronicDocumentIssuer`, el agregado
  `ElectronicDocument`, sus suppliers y servicios son la única capacidad fiscal/SRI. Toda pieza nueva se agrega
  dentro de ese módulo o en el módulo dueño del dato.
- **D2 — Los módulos producen datos fiscales; no usan infraestructura SRI.** Un módulo de negocio aporta un
  provider (agregado → modelo fiscal), guards de origen y contributors. Nunca conoce SOAP, endpoints, XAdES, XSD,
  códigos SRI, ConsultaComprobante, mappings de catálogo ni reglas de `infoAdicional`. Esto se protege con tests de
  arquitectura (ratchet; la excepción vigente, `RetentionAnnulmentService`, se elimina al crear el servicio de
  estado por empresa).
- **D3 — Modelo fiscal canónico = sobre común + cuerpo tipado.** El sobre está formado por los records existentes
  (`ElectronicDocumentEmissionContext`, `ElectronicDocumentIssuerData`, `infoAdicional` compuesta); la clave de
  acceso viaja en `ElectronicDocumentXml`. Los cuerpos son tipados por documento (`ElectronicDocumentData`
  comercial, `RetentionElectronicDocumentData`, futuros hermanos). No hay modelo gigante ni propiedades opcionales
  especulativas. Un contrato de sobre explícito y un orquestador genérico solo se introducen con el primer tipo
  nuevo (03/05/06).
- **D4 — Catálogos gobernados por ADR-037.** Concepto de negocio → resolver del catálogo (fecha de negocio,
  fail-closed) → representación oficial → modelo → builder que transcribe. Esta arquitectura no duplica ningún
  catálogo ni mapping.
- **D5 — Una sola proyección del perfil fiscal del emisor.** `Company` (sin entidad paralela) →
  `IElectronicDocumentIssuerProfileResolver` → `ElectronicDocumentIssuerData` (+ ambiente y tipo de emisión de
  `SriSettings`). Providers, builders y composer no consultan `Company` por su cuenta ni repiten lógica RIMPE.
- **D6 — `infoAdicional` tiene un solo composer y un solo punto de extensión.**
  `IElectronicDocumentAdditionalInfoComposer` combina contributors explícitos (normativos, sectoriales) con los
  campos propios del documento que entrega el provider. Reglas únicas: orden determinístico, nombres únicos
  (los normativos son reservados), nombre y valor 1..300 sin truncado, máximo 15, fail-closed si falta un campo
  obligatorio, sin reloj. Se invoca solo en los orquestadores de ensamblado fiscal (hoy
  `CommercialElectronicDocumentXmlSupplier` y `RetentionElectronicDocumentXmlService`); el builder recibe la lista
  final.
- **D7 — `SystemProviderSettings` sigue siendo global de instancia, y `EffectiveDate` es la fecha de
  aplicabilidad fiscal del `RUC Proveedor`.** No se mueve a Tenant/Company. Solo lo lee su contributor
  (`RUC Proveedor`, Ficha 2.34 Anexo 26; Res. NAC-DGERCGC26-00000027). Reglas:
  1. `EffectiveDate = null` y `Enabled = false` (o sin configuración): requisito aún no activado en la
     instalación; no se emite el campo.
  2. `EffectiveDate = null` y `Enabled = true`: configuración incompleta; falla cerrado con
     `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED`.
  3. `IssueDate < EffectiveDate`: no se emite el campo, sea cual sea `Enabled`. No existe incorporación
     anticipada en la semántica productiva.
  4. `IssueDate ≥ EffectiveDate`: con `Enabled = true` y RUC válido se emite el campo; con `Enabled = false`, o
     con el RUC ausente o inválido, falla cerrado con `SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED` y **no se genera
     XML**.
  5. La comparación usa exclusivamente la `IssueDate` de negocio (ADR-034).
  6. Los documentos `Signed`/`Authorized` nunca se regeneran (D11).
  7. `EffectiveDate` es configuración de instancia, se carga con la fecha legal confirmada y nunca se calcula ni
     se hardcodea en código.
  8. Las pruebas en `celcer` configuran una `EffectiveDate` propia en la instancia de pruebas; no hay flags ni
     lógica especial de emisión anticipada.

  Los datos del emisor (`ruc`, `razonSocial`) nunca salen de `SystemProviderSettings`.
- **D8 — Builders específicos por tipo.** Se conservan `InvoiceXmlBuilder`, `CreditNoteXmlBuilder` y
  `RetentionXmlBuilder`. Solo se extraen partes comunes (`infoTributaria`, `infoAdicional`, clave de acceso)
  cuando una fase necesita modificarlas, con salida byte a byte idéntica verificada por tests.
- **D9 — Firma, SOAP y XSD actuales se conservan.** `XadesBesSigner`/`XadesSignedXml`, `SriSoapClient` (con la
  derivación de endpoints desde `WsdlUrl`), el tratamiento 70/43/45 y el preflight XSD fail-closed no se
  reemplazan. El "SRI Gateway" son las interfaces existentes `ISriReceptionClient`, `ISriAuthorizationClient` e
  `ISriDocumentStatusQuery`, más servicios por empresa en ElectronicDocuments.
- **D10 — Ficha técnica y esquema XML se versionan por separado.** La versión XML por tipo la define el builder
  (`ElectronicDocumentXml.Version`); el validador elige el XSD por esa versión y `manifest.json` es un índice
  verificado por test. La versión de la ficha es una constante única del release, respaldada por
  `sri_normative_source`, y se registra en `ElectronicDocument.SpecificationVersion` al generar el XML.
- **D11 — El XML firmado/autorizado almacenado es la evidencia definitiva.** Las versiones registradas son
  trazabilidad; ningún cambio de arquitectura, catálogo o perfil regenera ni reinterpreta comprobantes firmados o
  autorizados. Solo `Draft`/`Failed` se regeneran, con las reglas aplicables a su fecha de emisión.
- **D12 — ConsultaComprobante es la SSOT transversal del estado fiscal cuando aplica.** Se mantiene la separación
  `SriStatusQueryOutcome` (técnico) / `SriFiscalStatus` (fiscal). Recepción y autorización migran a resultados
  tipados del mismo estilo, con una única clasificación de códigos SRI basada en `SriMessage.Code`, nunca en
  `StartsWith`/`Contains` sobre texto.
- **D13 — Recovery es transversal y se diseña aparte.** Una sola capacidad decide qué hacer con un ED no terminal
  (descubrimiento cross-tenant por ids + scope por candidato, acción por estado, ventana alineada a las 24 h del
  SRI). Lo que varía por origen entra por contributors o guards (detección de "origen sin ED", si se permite
  reenviar). Detalle: ZH-EDOC-RECOVERY-ARCHITECTURE-01.
- **D14 — Communications es una capacidad separada.** ElectronicDocuments publica
  `ElectronicDocumentAuthorizedEvent`; Communications decide entrega, adjuntos, cola y reintentos. ElectronicDocuments
  no implementa SMTP. NC y Retención se incorporan con un handler genérico y un contributor de destinatario por
  módulo, sin copiar el handler de Factura.
- **D15 — Las reglas sectoriales entran como extensiones.** Contributors de `infoAdicional`, campos de línea ya
  estándar en el XSD o bloques tipados opcionales del cuerpo, activados por una política sectorial explícita.
  Ninguna es obligatoria en el sobre ni en un cuerpo si no aplica universalmente.
- **D16 — Migración incremental, nunca big-bang.** Fases independientes (RUC Proveedor → perfil del emisor →
  versionado de especificación → recovery → semántica tipada → communications → generalización con el primer
  tipo nuevo). Cada una deja la suite verde, no duplica comportamiento, se revierte con su commit y registra un
  addendum en ADR-023 si cambia la salida XML (causa 1 o 2).
- **D17 — Los compliance tests gobiernan los upgrades de ficha.** `SriComplianceTests` en cuatro familias:
  catálogo (ADR-037), XML, protocolo y compatibilidad de especificación (matriz de requisitos con IDs estables).
  Con una ficha nueva se registra la especificación, se compara el historial de cambios, se actualizan
  catálogos/reglas, se corre compliance y solo se abren tickets por los gaps reales; no se reaudita el ERP desde
  cero.

## Consecuencias

- **El siguiente P0, `ZH-SRI-ANEXO26-PROVIDER-RUC-01`, ya no requiere decisiones de arquitectura:**
  composer + contributor `RUC Proveedor` invocados en los dos orquestadores, sin cambios en builders, providers,
  XSD, firma, SOAP, RIDE ni frontend (diseño exacto en el documento, §X). La regla de exigibilidad (D7) es
  definitiva. Para el despliegue solo faltan dos datos administrativos: la confirmación del registro de ZH
  Technologies como proveedor y la fecha legal exacta que se cargará en `EffectiveDate`.
- Base normativa confirmada (DA-1): Resolución NAC-DGERCGC26-00000027, publicada el 28/07/2026 en el Quinto
  Suplemento del Registro Oficial No. 335. El SRI indica 60 días hábiles desde la publicación para incorporar el
  RUC Proveedor en los comprobantes de emisores que usan sistemas suministrados por terceros.
- Riesgo residual operativo: mientras `EffectiveDate` no esté configurada y `Enabled = false`, el campo se omite,
  porque el sistema no puede inventar la fecha legal. Se mitiga registrando la fecha en el despliegue del P0.
- Verificación previa al despliegue del P0: hoy `SystemProviderSettings.Configure` permite `Enabled = true` sin
  `EffectiveDate`; con la regla 2 esa configuración bloquea la emisión electrónica de todas las empresas de la
  instalación. Se revisa y corrige en cada instalación antes de desplegar.
- Cambios en el código CLOSED de ElectronicDocuments: se limitan a los orquestadores, un servicio de estado por
  empresa, una columna aditiva (`SpecificationVersion`) y la tipificación de respuestas, cada uno en su ticket y
  bajo las causas de ADR-023.
- Los providers se adelgazan en la fase 2 (dejan de proyectar el emisor) y conservan su responsabilidad de
  negocio.
- Los nuevos tipos (03/05/06) reutilizan el pipeline completo y solo aportan provider, cuerpo, builder,
  XSD/versión, RIDE y políticas propias.
- Se agregan tests de arquitectura con allowlist (ratchet) para las violaciones actuales.

## Alternativas consideradas

| Alternativa | Motivo de descarte |
|---|---|
| Crear un "SRI Core" / "Fiscal Engine" nuevo junto a ElectronicDocuments | Duplica pipeline, estado y gateway ya validados contra el SRI real; dos fuentes de verdad del ciclo de vida |
| Reescribir `ElectronicDocumentData` como `Envelope<TBody>` ahora | Toca 3 providers y 3 builders FROZEN sin ganancia fiscal; se difiere al primer tipo nuevo (D3) |
| Agregar "RUC Proveedor" en cada provider | Triplica una regla normativa y hace que los providers dependan de configuración de instancia, contra la frontera vigente |
| Inyectar `SystemProviderSettings` en los builders | Los builders pasarían a conocer configuración y reglas de aplicabilidad (contradice D6/D8 y ADR-037 D6) |
| Componer `infoAdicional` dentro de `ElectronicDocumentIssuer` | El Issuer no ve el modelo fiscal (solo `ElectronicDocumentXml`) y la vista previa de retención quedaría sin el campo |
| Entidad `SriIssuerFiscalProfile` separada de `Company` | Segunda fuente de verdad del emisor; la auditoría ya identificó `Company` como fuente natural |
| Builder único genérico para todos los tipos | Mezcla estructuras XSD distintas; los cuerpos tienen formas legítimamente diferentes |
| Mover `SystemProviderSettings` a Tenant/Company | El proveedor del sistema es ZH (hecho de instancia); repetirlo por empresa crea divergencia |
| Un job y un handler de correo por tipo documental | Repite descubrimiento, ventanas y entrega; contributors por origen cubren la variabilidad real |

## Decisiones abiertas

DA-1 resuelta en lo normativo; pendiente solo lo administrativo (registro de ZH en el listado de proveedores y
fecha legal exacta para `EffectiveDate`) · DA-2 **resuelta** (D7) · DA-3 perfil
fiscal real del piloto · DA-4 leyenda RIMPE como catálogo con vigencia (= ADR-037 DR-6) · DA-5 clasificación de
códigos en `sri_error_code` · DA-6 retención 1.0.0 → 2.0.0. Detalle en el documento de arquitectura.
