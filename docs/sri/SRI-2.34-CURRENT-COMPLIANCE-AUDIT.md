# SRI 2.34 — Auditoría de cumplimiento actual (ZH-SRI-2.34-CURRENT-COMPLIANCE-AUDIT-01)

> **Naturaleza del documento:** auditoría (fotografía al 2026-10-02, commit `787971ab`). **No es normativo**: no
> define reglas (las reglas viven en `docs/architecture/*`) ni reemplaza ADRs. Sirve de base para
> `ZH-SRI-ARCHITECTURE-DESIGN-01`. No se modificó código productivo.

## A. Fuente auditada

| Campo | Valor verificado en el PDF |
|---|---|
| Título | "FICHA TÉCNICA DE COMPROBANTES ELECTRÓNICOS OFFLINE — Emisión de comprobantes electrónicos — Método de automatización off-line" |
| Versión / fecha | **Versión 2.34 — ACTUALIZADO JULIO 2026** (historial: 2.34 = 27/07/2026) |
| URL oficial | `https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/f8d9bb36-…/FICHA TÉCNICA … Versión 2.34.pdf` |
| SHA-256 del PDF descargado | `7333aebfbdf2cb3ba83f9fc67a7a7f0346ca59506480a260cc42f96dbdfc13c9` (3 825 039 bytes) |
| Cambio 2.33 (13/07/2026) | Anexo 25: campo `<placa>` obligatorio para operadoras de transporte comercial (Tabla 33) |
| Cambio 2.34 (27/07/2026) | **Anexo 26: RUC de proveedor de sistemas / servicios de facturación electrónica** |
| Cambio 2.32 (08/10/2025) | Anexo 25: `codigoAuxiliar` transporte comercial |
| Cambio 2.31 (27/03/2025) | §8: WS ConsultaComprobante y ConsultaFactura (FCN) |

El repo contiene además `docs/FICHA TECNICA … Versio232.pdf` (v2.32, desactualizada respecto a Anexos 25-placa y 26).

### Requisitos extraídos (con cita)

| Requisito | Sección/anexo | Tipo doc. | Aplicabilidad | Impacto ERP |
|---|---|---|---|---|
| Clave de acceso 49 dígitos, módulo 11 (11→0, 10→1) | §5.2, Tabla 1 | Todos | General | Builders |
| Tipos de comprobante 01/03/04/05/06/07 | §5.4, Tabla 3 | Todos | General | Catálogo `sri_doc_types` |
| Ambiente 1/2, emisión normal = 1 | Tablas 2 y 4 | Todos | General | `SriSettings` |
| Tipos de identificación 04–08 | §5.7, Tabla 6 | Todos | General | Catálogo `sri_id_types` |
| Estados PPR/AUT/NAT; SRI procesa en máx. 24 h | §5.12 | Todos | General | Pipeline/retry |
| NAT → corregir y reenviar **con la misma clave y secuencial** | §4.8, §5.10, §11 nota 1 | Todos | General | Flujo de rechazo |
| Firma XAdES-BES 1.3.2, ENVELOPED, UTF-8, RSA-SHA1, 2048 bits, PKCS12, `ds:KeyInfo` firmado | §6.1–6.8, Anexo 14 | Todos | General | Firma |
| WS Recepción/Autorización (celcer/cel), consumo asíncrono con espera parametrizable | §7.2, §7.4 | Todos | General | Cliente SOAP |
| No quemar URLs ni certificados SSL del SRI | §7.1.4, §8.1.4 | Todos | General | Configuración |
| Tamaño máx. **320 KB** individual; lote **500 KB / ~50** comprobantes | §7.5 | Todos | General | Preflight |
| WS ConsultaComprobante: `AUTORIZADO`, `NO AUTORIZADO`, `PENDIENTE DE ANULAR`, `ANULADO`; error `estadoConsulta=RECHAZADA` id 99 | §8.2.3, §8.4 | Todos | General | Estado fiscal |
| Código 70: no reenviar ni generar otra clave/secuencial hasta respuesta (máx. 24 h) | §11 nota 2 | Todos | General | Pipeline/retry |
| Comprobantes DEVUELTOS no se guardan en la BD del SRI | §11 nota 1 | Todos | General | Evidencia "no existe" |
| Enviar el comprobante al receptor por correo | §4.7 | Todos | General | Comunicaciones |
| Tablas IVA (17), ICE (18), impuesto a retener (19), retención IVA/ISD (20), formas de pago (24) | §9, Tablas 16–24 | Todos | General | Catálogos |
| Retención ATS v2.0.0 | Anexo 10 | 07 | General (coexiste v1.0.0 en Anexo 1) | Retenciones |
| Fundas plásticas (ICE-FPN-01/FPR-02/FPE-03) | Anexo 18 | 01 | ≥3 establecimientos o franquicia | Retail |
| Agente de retención `<agenteRetencion>` | Anexo 21 | Todos | Designados por resolución | `infoTributaria` |
| RIMPE `<contribuyenteRimpe>` (2 textos) | Anexo 22 | Todos | RIMPE | `infoTributaria` |
| Gran Contribuyente (`campoAdicional`) | Anexo 24 | 01/04/05 | Calificados | `infoAdicional` |
| **RUC Proveedor** (`campoAdicional nombre="RUC Proveedor"`, ≤300) | **Anexo 26** | Todos | Emisores que usan sistemas de terceros | `infoAdicional` + RIDE |

Afirmaciones **NO CONFIRMADAS POR FICHA 2.34**: fecha exacta de exigibilidad del Anexo 26 (la ficha dice "60 días desde la publicación en el RO", sin fecha); vigencia/obsolescencia de la retención v1.0.0; porcentajes de retención de Renta (la ficha remite al catálogo ATS); algoritmo de validación de RUC.

## B. Resumen ejecutivo

El núcleo técnico (clave de acceso, firma XAdES-BES, cliente SOAP, manejo de 70/43/45, ConsultaComprobante y separación estado fiscal / estado de consulta) **está bien alineado con la 2.34 y no debe tocarse**. Factura está validada con comprobantes reales en `celcer`.

Los gaps reales se concentran en **datos fiscales que no llegan al XML** y en **robustez operativa**:

1. **P0 — Retención: `codigoRetencion` de IVA usa códigos del catálogo ATS/formulario (`721…728`)** en vez de la Tabla 20 (`9,10,1,11,2,3,7,8`). Además Retención nunca se probó contra el SRI real.
2. **P0 — Anexo 26 (RUC Proveedor):** el dato existe (`SystemProviderSettings`) pero **ningún XML lo emite**. Lo que lo bloqueaba (no había texto normativo) ya lo resuelve la ficha.
3. **P1 — Retry job genérico apagado de hecho** (0 candidatos por filtro fail-closed de tenant); además su ventana (~31 min) contradice las 24 h del SRI.
4. **P1 — Perfil fiscal del emisor incompleto:** sin `agenteRetencion` (Anexo 21) ni Gran Contribuyente (Anexo 24); texto RIMPE Negocio Popular desactualizado.
5. **P1 — Rechazo sin camino de corrección** (§4.8/§5.10) y DEVUELTA transitoria (código 50) tratada como rechazo definitivo.

## C. Mapa actual de ElectronicDocuments

```
Origen (Sales / Sales-NC / Retentions)
 └─ IElectronicDocumentDataProvider / IElectronicDocumentXmlSupplier
     ├─ SalesInvoiceElectronicDocumentDataProvider ─┐
     ├─ SalesReturnCreditNoteDataProvider ──────────┤→ CommercialElectronicDocumentXmlSupplier → Invoice/CreditNoteXmlBuilder
     └─ RetentionElectronicDocumentDataProvider ────→ RetentionElectronicDocumentXmlSupplier → RetentionXmlBuilder (v1.0.0)
 └─ ElectronicDocumentIssuer (RegisterAsync / RunPipelineAsync / RetryAsync / RetryGuardedAsync)
     → IElectronicDocumentSchemaValidator (Invoice 1.1.0, CreditNote 1.1.0, Retention 1.0.0; EmbeddedXmlSchemaProvider + manifest.json)
     → ElectronicDocumentSigningService → XadesBesSigner (única implementación)
     → ElectronicDocumentXmlStorageService (draft/signed/authorized)
     → [solo orígenes con guard: ClaimDispatchAsync → Dispatching]
     → ElectronicDocumentReceptionService → SriReceptionClient → SriSoapClient.SendAsync (validarComprobante)
     → ElectronicDocumentAuthorizationService → SriAuthorizationClient → SriSoapClient.CheckAuthorizationAsync
     → ElectronicDocument (Authorized / Rejected / …)
 └─ Estado fiscal posterior: SriDocumentStatusQuery → SriSoapClient.QueryDocumentStatusAsync (ConsultaComprobante) — hoy solo lo usa Retenciones (anulación)
 └─ RIDE: ElectronicDocumentRideSourceXmlProvider → Invoice/CreditNote/RetentionRideXmlParser → QuestPdfRideRenderer
```

| Capa | Piezas (archivo) |
|---|---|
| Domain | `ElectronicDocument` (13 estados, `ElectronicDocumentState.cs`), `ElectronicDocumentType` (6 tipos, 3 activos), `AccessKey`, `AuthorizationNumber`, `SriFiscalStatus`, `SriStatusQueryOutcome`, `ExternalAnnulmentEvidence`, 16 eventos, `ElectronicDocumentSriMessage` |
| Application | `ElectronicDocumentIssuer`, `ElectronicDocumentRetryPolicy` (5 intentos, 1→16 min), Reception/Authorization/Signing services, 3 builders, 3 validadores XSD, `IElectronicDocumentSourceLifecycleGuard` (solo Retenciones), Monitor/Dashboard/Timeline/Diagnostic queries |
| Infrastructure | `SriSoapClient` (recepción, autorización, consulta), `XadesBesSigner`/`XadesSignedXml`, `SriCertificateInspector`, `EmbeddedXmlSchemaProvider` + 13 XSD, `ElectronicDocumentRepository`, catálogos `sri_*`, RIDE QuestPDF |
| API | `ElectronicDocumentsController` (list/dashboard/detail/by-source/timeline/xml/**retry**/**register**), `RetentionsController` (xml, ride, register, anulación), `RideController`, `ElectronicInvoicingController`, `SystemProviderSettingsController` (PlatformAdmin) |
| Jobs | `ElectronicDocumentRetryJob` (cada minuto), `RetentionElectronicRecoveryJob` (cada minuto) |
| Frontend | Monitor (`modules/electronicDocuments/monitor`), panel de diagnóstico/timeline/XML, badge de estado de retención, panel de anulación, configuración SRI y certificado, UI admin de proveedor de sistema (`admin-core/api/systemProviderSettingsService.ts`) |

## D. RUC Proveedor (Anexo 26) — estado exacto: 🟡 PARCIAL

| Pregunta | Respuesta | Evidencia |
|---|---|---|
| A. ¿Existe implementación? | Solo la **configuración**; no la emisión | `ERP.Domain/Modules/Configuration/Entities/SystemProviderSettings.cs` (comentario: "NO se inyecta todavía en ningún XML") |
| B. ¿Dónde se configura? | `system_provider_settings`, API `api/v1/system/provider-settings` (policy `PlatformAdmin`), UI admin-core | `SystemProviderSettingsController.cs:31-32` |
| C. Alcance | **Global de instancia** (singleton Id=1, sin TenantId/CompanyId) — correcto: el proveedor es ZH, no cada empresa | idem |
| D. ¿SSOT única? | Sí (una entidad, un repositorio) | `ISystemProviderSettingsRepository` |
| E. Documentos que lo incluyen | **Ninguno** | Ningún consumidor fuera de los use cases de configuración (`git grep SystemProviderSettings`) |
| F/G/H. Formato 2.34 | No emitido. La ficha exige `<infoAdicional><campoAdicional nombre="RUC Proveedor">RUC</campoAdicional>` (alfanumérico ≤300) | Ficha p.135 |
| I. RIDE | El RIDE ya renderiza **todo** `campoAdicional` genéricamente (Invoice/CreditNote/Retention parsers + `AdditionalInfoSection`) → al emitirlo en XML aparecería en el RIDE sin cambios | `InvoiceRideXmlParser.cs:94`, `RetentionRideXmlParser.cs:95` |
| J. Tests | Solo de configuración (`SystemProviderSettingsTests`, `SystemProviderSettingsHandlerTests`, auth HTTP) | — |
| K. Duplicación | No aplica todavía; **riesgo** al implementar: hay 3 providers con `AdditionalInfo` propio (Factura usa "Observación"; NC y Retención `[]`) | `SalesInvoiceElectronicDocumentDataProvider.cs:222`, `SalesReturnCreditNoteDataProvider.cs:201`, `RetentionElectronicDocumentDataProvider.cs:213` |

Observaciones: (1) `Enabled` exige RUC + razón social + CIIU; el Anexo 26 solo pide el RUC. (2) **DECISION REQUIRED (aplicabilidad):** el Anexo 26 obliga a "contribuyentes … que utilicen sistemas de facturación electrónicos de terceros (proveedores detallados en la resolución 2718)", Res. NAC-DGERCGC26-00000027; la ficha no da fecha de exigibilidad.
**Acción:** extender (no reimplementar) — inyectar el campo una sola vez en el modelo común antes de los builders; contar el campo en el máximo de 15.

## E. ConsultaComprobante — estado exacto: ✅ IMPLEMENTADO Y CORRECTO (uso parcial)

| Contrato 2.34 §8 | ERP | Evidencia |
|---|---|---|
| Endpoints `…/ConsultaComprobante?wsdl` celcer/cel | Derivado del `WsdlUrl` configurado (no quemado); `null` si no se puede derivar | `SriSoapClient.cs:751-763` |
| Operación `consultarEstadoAutorizacionComprobante(claveAcceso)`, ns `http://ec.gob.sri.ws.consultas` | Idéntico | `SriSoapClient.cs:32, 342-352` |
| `estadoAutorizacion` ∈ {AUTORIZADO, NO AUTORIZADO, PENDIENTE DE ANULAR, ANULADO} | Mapeo exacto; otro literal → `Unknown` | `SriSoapClient.cs:698-706`, `SriFiscalStatus.cs` |
| Error `estadoConsulta=RECHAZADA` id 99 | `SriStatusQueryOutcome.Rejected`, **nunca** estado fiscal (cubre también la prosa que lo llama "estadoAutorizacion") | `SriSoapClient.cs:630-649` |
| Timeout / red / SOAP Fault | `Timeout` / `Unavailable` separados del estado fiscal | `SriSoapClient.cs:250-275` |
| Respuesta para otra clave | Descartada (`Unknown`) | `SriSoapClient.cs:651-665` |

Separación estado fiscal ↔ estado de consulta: **correcta, sin mezcla**. Tests: `SriSoapClientConsultaComprobanteTests` (verde).
**Uso:** solo lo consume Retenciones (anulación, `RetentionAnnulmentService`, recovery job). Es apto como **SSOT transversal** del estado fiscal para Factura/NC/ND/Guía (el contrato no depende del tipo). Restricción: rango de fechas permitido por el SRI (error 99 "fuera del rango"), no cuantificado por la ficha.

## F. Recepción / autorización — estado exacto: ✅ con 4 gaps puntuales

| Caso | Comportamiento ERP | Veredicto |
|---|---|---|
| RECIBIDA | `Sent→Received` y consulta autorización | ✅ |
| DEVUELTA con 70/43/45 | Tratado como "ya existe" → `Received` → consultar, sin regenerar clave | ✅ (`ElectronicDocumentIssuer.cs:768, 808-822`) |
| DEVUELTA con cualquier otro código | `Rejected` terminal, **incluido 50 "Error interno general"** (falla del SRI, no del comprobante) | ⚠️ (`ElectronicDocumentIssuer.cs:824-833`) |
| Rejected / NAT | Estado terminal; **no hay camino de corrección y reenvío con la misma clave** (§4.8, §5.10) | 🔴 |
| AUTORIZADO | `Authorized`; número = clave de acceso (§5.9) | ✅ |
| NO AUTORIZADO | `Rejected` con mensajes estructurados | ✅ |
| Literal `RECHAZADO` (ejemplo §7.2.3) | No reconocido → polling → `TIMEOUT` → queda `Received` (seguro, pero no concluye) | 🟡 (real SRI usa "NO AUTORIZADO", EST-01v2) |
| Varias `<autorizacion>` en la respuesta | Toma la **primera** (`FirstOrDefault`); la FAQ dice que puede haber varias NAT y una AUT | ⚠️ (`SriSoapClient.cs:567-569`) |
| PPR / sin nodo `<autorizacion>` | Polling interno 5 intentos (2-16 s) → `TIMEOUT` → queda `Received` | ✅ |
| Timeout / red en recepción | Reintento HTTP del mismo XML (misma clave) ×3; si falla queda `Signed` | ✅ (permitido: misma clave → 43/70) |
| Respuesta no XML | `ERROR_RESPUESTA_INVALIDA`, sin transición | ✅ |

¿Algún camino reenvía cuando debería consultar? **No** en estados `Received`/`Dispatching` (guarded: solo consulta). `Signed` no guardado (Factura/NC) reenvía el **mismo** XML/clave — compatible con la ficha. ¿Timeout = rechazo? **No.** ¿Se genera otra clave/secuencial? **No** (clave determinística por documento, secuencial congelado en origen).

## G. Retry + bug tenant — estado exacto: 🔴 job inoperante (P1 técnico con exposición fiscal)

- **Bug confirmado:** `ElectronicDocumentRetryJob.cs:55` consulta `GetRetryCandidatesAsync` **antes** de `JobExecutionContext.Begin` (`:96`). `ElectronicDocument` es `ICompanyOperationalEntity` → filtro global fail-closed (tenant **y** empresa) → 0 filas. Caracterizado con PostgreSQL real por `PurchaseRetentionConfirmIntegrationTests.ElectronicCancellation.cs:512` (`Hallazgo_GetRetryCandidatesAsync_sin_contexto_de_tenant_no_devuelve_candidatos`, **verde**). El patrón correcto ya existe en `RetentionElectronicRecoveryJob` (consulta ignorando filtros → solo ids → un scope por candidato).
- **Tomaría si se arreglara:** estados `Draft(1)`, `Signed(3)`, `Received(5)`, `Failed(10)` de **todos** los tipos (`ElectronicDocumentRepository.cs:209-219`, sin paginación); excluye `Dispatching`, `DeadLetter`, `Rejected`.
- **Riesgos de encenderlo tal cual:**
  - Facturas/NC `Signed` históricas → **reenvío** del mismo XML (seguro por la ficha: 43/45/70).
  - Facturas/NC `Draft/Failed` → regeneran XML/firma con datos **actuales** del origen (mismo secuencial y clave).
  - Retenciones → pasan por `RetryGuardedAsync` (solo consulta) — seguro.
  - **`Received` → DeadLetter tras ~31 min** (5 intentos, backoff 1/2/4/8/16 min) aunque el SRI tiene **hasta 24 h** (§5.12, §7.5, §11 nota 2). DeadLetter es recuperable (`Reactivate` restaura el estado previo) pero ensucia el Monitor y deja de consultar.
  - Preferencia `electronic_documents.auto_retry_enabled` por empresa puede apagarlo.
- **Clasificación: P1 técnico** — no genera documentos fiscalmente inconsistentes (la transmisión inicial es inmediata y existe reintento manual), pero deja comprobantes sin resolver hasta acción humana. Corregir junto con la ventana de 24 h.
- Query read-only: §U Q3/Q4.

## H. Transmisión inmediata por documento

| Documento | Modo | Después del commit | Recovery | ¿Puede quedar sin transmitir indefinidamente? |
|---|---|---|---|---|
| Factura (01) | **AUTOMÁTICO** síncrono post-commit (`AuthorizeSalesUseCases.cs:728-736`) | Sí, fuera de la transacción | Retry job (inoperante) + `POST /electronic-documents/{id}/retry` + `POST /electronic-documents/register` | **Sí**: caída entre commit y `RegisterAsync` → factura autorizada sin ED; ningún job lo detecta |
| Nota de crédito (04) | **AUTOMÁTICO** síncrono post-commit (`AuthorizeSalesReturnUseCases.cs:336-346`) | Sí | Igual que Factura | **Sí**, mismo caso |
| Retención (07) | **AUTOMÁTICO** post-commit (Compras/Gastos → `RetentionElectronicTransmission`) | Sí | `RetentionElectronicRecoveryJob` (Issued sin ED o ED en Draft, gracia 2 min) + register manual | Parcial: `Dispatching`/`Signed` sin respuesta concluyente quedan para intervención manual (ADR-036 O-10/P-2) |
| Nota de débito (05) | NO IMPLEMENTADO (solo XSD) | — | — | — |
| Guía de remisión (06) | NO IMPLEMENTADO (solo XSD) | — | — | — |
| Liquidación de compra (03) | NO IMPLEMENTADO (solo XSD) | — | — | — |

## I. Retención vs Anexo 10 (ATS 2.0.0) y Anexo 1 (1.0.0)

ERP emite **`comprobanteRetencion version="1.0.0"`** (`RetentionXmlBuilder.cs:40`), elección documentada (no inventar `parteRel`, `pagoLocExt`, `impuestosDocSustento`). La ficha 2.34 **sigue publicando ambas** (Anexo 1 p.~51 y Anexo 10 p.104); no declara obsoleta la 1.0.0 → vigencia **NO CONFIRMADA POR FICHA 2.34**.

| Elemento | ERP (1.0.0) | Ficha 2.34 | Diferencia |
|---|---|---|---|
| `infoTributaria` | completo + `contribuyenteRimpe` | + `agenteRetencion` (Anexo 21) | 🔴 sin `agenteRetencion` |
| `infoCompRetencion` | fechaEmision, dirEstablecimiento, contribuyenteEspecial, obligadoContabilidad, tipo/razón/identificación sujeto, periodoFiscal | 2.0.0 añade `tipoSujetoRetenido`, `parteRel` | solo en 2.0.0 |
| `impuesto/codigo` | 1 Renta, 2 IVA (`SriRetentionTaxTypeCodes`) | Tabla 19: 1/2/6 | ✅ (ISD 6 no soportado) |
| **`impuesto/codigoRetencion` IVA** | **`721…728`** (catálogo `sri_retention_codes`) | **Tabla 20: 10%→9, 20%→10, 30%→1, 50%→11, 70%→2, 100%→3, 0%→7, no procede→8**; ejemplo Anexo 1: `<codigo>2</codigo><codigoRetencion>1</codigoRetencion>` para 30% | 🔴 **P0** |
| `codigoRetencion` Renta | `303, 304, 307…344` | catálogo ATS (externo) | ⚠️ porcentajes a verificar (p.ej. 312 = 1.00% en seed) |
| `codDocSustento`/`numDocSustento`/`fechaEmisionDocSustento` | opcionales; `numDocSustento` se omite si no da 15 dígitos | Ejemplo Anexo 1 los muestra; XSD 1.0.0 los admite opcionales | 🟡 |
| `numAutDocSustento`, `codSustento`, `pagoLocExt`, totales | No | Solo 2.0.0 | ⚠️ decisión de versión |
| `infoAdicional` | **siempre vacía** | RUC Proveedor obligatorio (Anexo 26) | 🔴 |
| XSD | `ComprobanteRetencion_V1.0.0.xsd` (preflight activo) | — | ✅ (`manifest.json` aún dice `activeVersion: null`: doc desactualizada) |
| RIDE | Implementado (`RetentionRideTemplate`) | "corresponderá al publicado para la versión 1.0.0" (Anexo 10) | ✅ |
| Prueba real SRI | **Sin evidencia de retención autorizada en `celcer`** | — | ⚠️ |

El XSD 1.0.0 acepta `codigoRetencion` como texto libre ≤5 → el preflight **no** detecta el error de códigos IVA; solo lo detectaría el SRI.

## J. Firma — ✅ CUMPLE (DO NOT TOUCH)

| Ficha §6 / Anexo 14 | ERP (`XadesBesSigner.cs`) | Estado |
|---|---|---|
| XAdES-BES, ns `http://uri.etsi.org/01903/v1.3.2#` | Igual (`:18`) | ✅ |
| ENVELOPED | `XmlDsigEnvelopedSignatureTransform` (`:105`) | ✅ |
| C14N `REC-xml-c14n-20010315` | `XmlDsigC14NTransformUrl` (`:82`) | ✅ |
| RSA-SHA1 | `XmlDsigRSASHA1Url` (`:83`) | ✅ |
| Digest | **SHA-256** en 3 referencias y CertDigest; el ejemplo del Anexo 14 usa SHA-1, pero §6.8 no prescribe digest y se verificó byte a byte contra factura **AUTORIZADA** real | ✅ (no "modernizar" ni "corregir" a SHA-1) |
| KeyInfo con X509 firmado | Referencia `#Certificate` firmada (`:117-128`) | ✅ |
| PKCS#12 | `X509CertificateLoader.LoadPkcs12*` | ✅ |
| Vigencia del certificado | Validada antes de firmar (`:53`) | ✅ |
| Longitud de clave 2048 | **No validada** (es "recomendación técnica" en §6.8) | 🟡 P3 |
| UTF-8 | `Utf8StringWriter`, `UTF8Encoding(false)` | ✅ |
| Implementación única | Sí (`IElectronicDocumentSigner` → adapter → `XadesBesSigner`) | ✅ |

## K. XSD / preflight / tamaño

| Punto | Estado |
|---|---|
| XSD embebidos con `manifest.json` como índice; selección por tipo+versión | ✅ (`EmbeddedXmlSchemaProvider`) |
| Validación previa a firma, obligatoria y fail-closed (sin XSD → inválido) | ✅ (`ElectronicDocumentIssuer.cs:217-246`) |
| Errores XSD visibles en Monitor (`Failed` + `LastError`) | ✅ |
| Validación del XML **firmado** | No (se valida el XML sin firma) — aceptable, la firma no altera el comprobante | 🟡 |
| **Límite 320 KB individual (§7.5, error 26)** | **No validado** antes de transmitir | 🔴 P1 |
| Lote 500 KB/50 | ⚪ no se usa envío por lote |
| Patrón `contribuyenteRimpe` en XSD embebidos | Solo `CONTRIBUYENTE RÉGIMEN RIMPE` en los 13 XSD → no admite el texto de Negocio Popular del Anexo 22 (vigente desde 2.22) | ⚠️ XSD posiblemente desactualizados; **verificar contra XSD oficiales actuales** |
| `manifest.json` `activeVersion` de Retention/CreditNote | `null` aunque hay validador activo | 🟨 doc |

## L. Perfil fiscal del emisor

| Dato | Dónde vive | Consumo | Estado |
|---|---|---|---|
| Régimen (General/RIMPE_ME/RIMPE_NP/ESP) | `Company.TaxRegimeCode` → catálogo `sri_tax_regime` | 3 providers (`ResolveContribuyenteRimpeText` **triplicado**) | 🟠 duplicado + ⚠️ NP emite "CONTRIBUYENTE RÉGIMEN RIMPE" en vez de "CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE" (Anexo 22) |
| Contribuyente especial | `Company.SpecialTaxpayerNo` | Solo Retención (`contribuyenteEspecial`) | 🟡 Factura/NC no lo emiten |
| Obligado a llevar contabilidad | Company | 3 builders | ✅ |
| Agente de retención (resolución) | **No existe** | — | 🔴 Anexo 21 |
| Gran Contribuyente (resolución) | **No existe** | — | 🔴 Anexo 24 |
| RUC Proveedor | `SystemProviderSettings` (instancia) | — | 🟡 (ver D) |

No existe hoy un `SriIssuerFiscalProfile`. **Pieza a evolucionar:** `Company` (+ catálogo `sri_tax_regime`) como fuente, y el `Issuer` del modelo común (`ElectronicDocumentData.Issuer`) como único punto de traducción a XML — no crear una entidad paralela sin pasar por `ZH-SRI-ARCHITECTURE-DESIGN-01`.

## M. Catálogos

| Catálogo | Fuente ficha | ERP | Estado |
|---|---|---|---|
| Tipos de comprobante | Tabla 3 | `sri_doc_types` + `SriDocumentTypeCodes` | ✅ |
| Tipos de identificación | Tabla 6 | `sri_id_types` (04–09) | ✅ |
| Impuesto (IVA 2, ICE 3, IRBPNR 5) | Tabla 16 | `SriTaxCategoryCodes` + `SriTaxCategoryCodeResolver` | ✅ |
| Tarifas IVA (0,2,3,4,5,6,7,8,10) | Tabla 17 | `sri_vat_rate` (mismos 9 códigos) | ✅ |
| ICE | Tabla 18 | `sri_ice_rate` (14 códigos) | 🟡 parcial; **sin 3740 fundas plásticas** |
| Impuesto a retener (1,2,6) | Tabla 19 | `SriRetentionTaxTypeCodes` (1,2) | 🟡 ISD no soportado |
| Retención IVA (XML) | Tabla 20 | `sri_retention_codes` usa 721–728 | 🔴 (ver I) |
| Retención ISD 4580 / 4586 | Tabla 20 | 4580 (5%) | 🟡 sectorial |
| Formas de pago | Tabla 24 | `sri_payment_methods` (01,15–21) | ✅ |
| Errores SRI | §11 | `sri_error_code` (33) incl. 43/45/70/82/92 | ✅ |
| UOM / sustento tributario | ATS | `sri_uom` (22), `sri_tax_support` (20) | ✅ (fuera de ficha) |

Hardcodes: no se encontraron literales SRI sueltos en builders/providers; los códigos legítimamente constantes están centralizados en Domain (`SriDocumentTypeCodes`, `SriRetentionTaxTypeCodes`, `SriTaxCategoryCodes`). La excepción es el texto RIMPE (triplicado).

## N. RucValidator — FUENTE EXTERNA A FICHA 2.34

La ficha 2.34 **no define** el algoritmo del RUC. Solo lista la advertencia **62 "Identificación incorrecta"** (cédulas que no pasan el dígito verificador) como **ADVERTENCIA**, no como error (§11).

`ERP.Domain/Common/Validators/RucValidator.cs`: provincia 01–24 o 30; 3.er dígito 0–5 → módulo 10 (persona natural); 6 → módulo 11 con 8 coeficientes (pública); 9 → módulo 11 con 9 coeficientes (privada); 7/8 → inválido; establecimiento ≠ 000/0000. Se usa en `TaxIdentification` (maestro de terceros) y `CompanyIdentityRules` (empresa), **sin override**.

Hallazgos:
- **Inconsistencia interna demostrable:** `EcuadorIdValidator` acepta cédulas con 3.er dígito **6** (`tercerDigito > 6` es el límite), pero el RUC de esa persona (cédula + `001`) se valida como **entidad pública** y probablemente se rechaza.
- El ERP rechaza con error duro lo que el SRI trata como advertencia; un RUC real que no cumpla el algoritmo bloquearía el registro de un proveedor o cliente.
- **DECISION REQUIRED:** confirmar con fuente oficial del SRI (no la ficha) la vigencia del dígito verificador para RUC de sociedades y la política (bloquear vs. advertir). No se atribuye esta regla a la ficha.

## O. Fundas plásticas (Anexo 18)

- **Regla:** establecimientos de comercio con **3 o más establecimientos abiertos**, o franquiciador/franquiciados (sin importar el número), que entreguen fundas tipo acarreo/camiseta → agregar en `<detalles>` una línea por tipo: `ICE-FPN-01` (normal), `ICE-FPR-02` (rebaja 50%, biodegradables/compostables), `ICE-FPE-03` (exenta, ≥50% reciclado); cantidad = número de fundas; ICE = tarifa específica × cantidad (Tabla 18, código 3740); el ICE forma parte de la base del IVA.
- **Datos que necesitaría el ERP:** ítems con SKU exacto `ICE-FPN-01/02/03`, ICE específico 3740 en el catálogo, y la condición de aplicabilidad (número de establecimientos abiertos de la empresa).
- **Existe hoy:** motor de ICE por línea (`SalesInvoiceDetail.Taxes`, cálculo específico/ad valorem), `codigoPrincipal` = SKU → un ítem con SKU `ICE-FPN-01` saldría correcto.
- **Falta:** código **3740** en `sri_ice_rate`; validación/guía de configuración.
- **Aplicabilidad:** depende de que la empresa piloto tenga ≥3 establecimientos o sea franquicia → **P1 PILOTO si se cumple; P2 SECTORIAL si no** (DECISION REQUIRED: dato de negocio).

## P. Reglas sectoriales (inventario, sin implementar)

| Regla | Anexo | Sector | Dato requerido | Soporte ERP | Prioridad |
|---|---|---|---|---|---|
| Factura exportación | 4 | Exportadores | Incoterm, puertos, flete, seguro | No | P2 |
| Reembolso de gastos | 5 | Intermediación | Detalle de reembolsos | No | P2 |
| Subsidios | 6, 7 | Bienes subsidiados | Valor subsidio, RIDE especial | No | P2 |
| Rubros de terceros | 8 | Cobros por cuenta de terceros | Rubros | No | P2 |
| Factura sustitutiva de guía | 9 | Transporte propio | Destinatarios | No | P2 |
| Factura comercial negociable | 11, §8 ConsultaFactura | Financiamiento | Dirección comprador, forma pago 21 | No | P2 |
| Combustibles (placa, Tabla 30) | 12, 16 | Gasolineras | Placa, códigos | No | P2 |
| Máquina fiscal | 13 | Máquinas fiscales | Marca/tipo/serie | No | P2 |
| Liquidación de compra | 17 | Compras a no obligados | Documento 03 | Solo XSD | P2 (P1 si el negocio compra a informales) |
| Autorretenciones | 19 | Grandes contribuyentes | Códigos 350/3481 | No | P2 |
| Devolución IVA adultos mayores (DIG) | 20, §12, §13 | Beneficiarios | `valorDevolucionIva` + WS DIG | No | P2 |
| Materiales de construcción | 23 | Ferreterías/constructoras | `codigoAuxiliar` Tabla 31 | No | P2 |
| Transporte comercial (código + placa) | 25 | Operadoras | `codigoAuxiliar` Tabla 32, `<placa>` Tabla 33 | No | P2 |
| Retención combustibles/periódicos | §10 | Comercializadoras | IVA presuntivo | No | P2 |
| ISD 4586 2.5% | Tabla 20 | Pagos al exterior | Código 4586 | No | P2 |

Ninguna debe contaminar los flujos Retail.

## Q. Matriz principal 2.34

| ID | Requisito SRI | Fuente 2.34 | Documento | Estado ERP | SSOT actual | Evidencia código | Gap | Riesgo | Prioridad | Acción |
|---|---|---|---|---|---|---|---|---|---|---|
| R01 | Clave de acceso 49 díg., módulo 11 | §5.2 Tabla 1 | Todos | 🟠 | Builder (×3) | `InvoiceXmlBuilder.cs:175-227` (idéntico en CN/Ret) | Algoritmo triplicado | Bajo | P3 | KEEP comportamiento; centralizar luego |
| R02 | Código numérico estable en reintentos | §5.2 | Todos | ✅ | Builder | `ComputeNumericCode` (SHA-256 de identificadores) | — | — | — | NO ACTION |
| R03 | Tipo doc / ambiente / emisión | Tablas 2-4 | Todos | ✅ | `sri_doc_types`, `SriSettings` | `SriDocTypeConfiguration.cs` | — | — | — | NO ACTION |
| R04 | Tipos identificación 04–08 | Tabla 6 | Todos | ✅ | `sri_id_types` | `SriIdTypeConfiguration.cs` | — | — | — | NO ACTION |
| R05 | Firma XAdES-BES (RSA-SHA1, C14N, enveloped, KeyInfo firmado) | §6, Anexo 14 | Todos | ✅ | `XadesBesSigner` | `XadesBesSigner.cs:82-145` | — | — | — | DO NOT TOUCH |
| R06 | Clave 2048 bits | §6.8 (recomendación) | Todos | 🟡 | — | `SriCertificateInspector.cs` no mide `KeySize` | Sin advertencia | Bajo | P3 | EXTEND inspector |
| R07 | WS Recepción/Autorización y namespaces | §7.2.3 | Todos | ✅ | `SriSoapClient` | `SriSoapClient.cs:30-33, 318-340` | — | — | — | NO ACTION |
| R08 | No quemar URLs del SRI | §7.1.4 | Todos | ✅ | `SriSettings.WsdlUrl` | `SriSoapClient.cs:731-763` | — | — | — | NO ACTION |
| R09 | Espera parametrizable tras RECIBIDA | §7.4 | Todos | ✅ | `SriPollingOptions` | `SriSoapClient.cs:141-148` | — | — | — | NO ACTION |
| R10 | Código 70/43/45 → no regenerar; consultar | §11 nota 2 | Todos | ✅ | Issuer | `ElectronicDocumentIssuer.cs:768-822` | — | — | — | DO NOT TOUCH |
| R11 | Hasta 24 h de procesamiento | §5.12, §7.5, §11 n.2 | Todos | ⚠️ | `ElectronicDocumentRetryPolicy` | `ElectronicDocumentRetryPolicy.cs:10-19` (~31 min) | DeadLetter prematuro | Medio | P1 | EXTEND política |
| R12 | NAT → corregir y reenviar con misma clave | §4.8, §5.10, §11 n.1 | Todos | 🔴 | — | `Rejected` sin transición de salida (`ElectronicDocument.cs:349-385`) | Sin flujo | Medio | P1 | Diseño |
| R13 | DEVUELTA transitoria (50) no es rechazo del comprobante | §11 | Todos | ⚠️ | Issuer | `ElectronicDocumentIssuer.cs:824-833` | Todo no-70/43/45 → Rejected | Medio | P1 | EXTEND |
| R14 | Varias `<autorizacion>`; una AUT | §5.11, FAQ p.142 | Todos | ⚠️ | `SriSoapClient` | `SriSoapClient.cs:567-569` (`FirstOrDefault`) | Podría tomar NAT antigua | Medio | P1 | Caracterizar + fix |
| R15 | ConsultaComprobante (4 estados; RECHAZADA≠estado fiscal) | §8 | Todos | ✅ | `SriSoapClient.QueryDocumentStatusAsync` | `SriSoapClient.cs:229-291, 630-706` | Uso solo en Retenciones | — | P2 | EXTEND uso |
| R16 | Tamaño ≤320 KB | §7.5, error 26 | Todos | 🔴 | — | sin búsqueda positiva | Sin preflight | Bajo (retail) | P1 | EXTEND preflight |
| R17 | Validación XSD previa | §5.1, error 35 | 01/04/07 | ✅ | `IElectronicDocumentSchemaValidator` | `ElectronicDocumentIssuer.cs:217-246` | — | — | — | NO ACTION |
| R18 | `infoAdicional` ≤15 campos, ≤300 | XSD/Anexos | Todos | 🟠 | Builder (×3) | `MaxAdditionalFields = 15` ×3; largo vía XSD | Triplicado | Bajo | P3 | Centralizar |
| R19 | **RUC Proveedor** | **Anexo 26** | Todos | 🟡 | `SystemProviderSettings` | no consumido | No emitido | **Alto** | **P0** | EXTEND |
| R20 | RIMPE (2 textos) | Anexo 22 | Todos | ⚠️🟠 | `Company.TaxRegime` | 3× `ResolveContribuyenteRimpeText`; XSD solo admite 1 texto | NP incorrecto | Bajo-medio | P1 | Verificar XSD oficial + centralizar |
| R21 | Agente de retención | Anexo 21 | Todos | 🔴 | — | no modelado (`InvoiceXmlBuilder.cs:249-251`) | Falta dato y nodo | Medio (si aplica) | P1 | DECISION + EXTEND |
| R22 | Gran Contribuyente | Anexo 24 | 01/04/05 | 🔴 | — | no modelado | Falta dato | Medio (si aplica) | P1 | DECISION + EXTEND |
| R23 | Contribuyente especial en factura/NC | Anexos 1/3 | 01/04 | 🟡 | `Company.SpecialTaxpayerNo` | solo Retención lo emite | Incompleto | Bajo | P2 | EXTEND |
| R24 | Códigos de retención IVA en XML | **Tabla 20**, Anexo 1 | 07 | 🔴 | `sri_retention_codes` | seed `721-728` (`SriRetentionCodeConfiguration.cs:35-78`) → `RetentionXmlBuilder` | Código incorrecto | **Alto** | **P0** | Corregir mapeo |
| R25 | Retención versión 1.0.0 vs ATS 2.0.0 | Anexos 1 y 10 | 07 | ⚠️ | Builder | `RetentionXmlBuilder.cs:40` | Vigencia no confirmada | Medio | P1 | DECISION + prueba celcer |
| R26 | % Renta según catálogo ATS | Tabla 20 (remite a ATS) | 07 | ⚠️ | `sri_retention_codes` | `312 = 1.00%` | No confirmable con ficha | Medio | P1 | Verificar fuente ATS |
| R27 | IVA/ICE/IRBPNR, tarifas IVA | Tablas 16-17 | 01/04 | ✅ | catálogos | `SriVatRateConfiguration.cs` | — | — | — | NO ACTION |
| R28 | ICE fundas 3740 | Tabla 18, Anexo 18 | 01 | 🔴 | `sri_ice_rate` | no sembrado | Falta código | Depende | P1/P2 | DECISION aplicabilidad |
| R29 | Formas de pago | Tabla 24 | 01 | ✅ | `sri_payment_methods` | — | — | — | — | NO ACTION |
| R30 | Envío al receptor por correo | §4.7 | Todos | 🟡 | Communications | solo `SalesInvoiceAuthorizedCommunicationHandler.cs:63` (Invoice) | NC y Retención no se envían | Medio | P1 | EXTEND |
| R31 | Retry automático operativo | §5.12 (obligación de verificar AUT) | Todos | 🔴 | `ElectronicDocumentRetryJob` | `ElectronicDocumentRetryJob.cs:55` vs `:96` | 0 candidatos | Medio | P1 | Fix job |
| R32 | Evidencia "SRI no tiene el comprobante" (P-2) | §8 (99 "No existen datos"), §11 n.1 | Todos | 🟡 | ConsultaComprobante | ADR-036 P-2 abierto | Sin usar como evidencia | Medio | P1 | DECISION ADR-036 |
| R33 | ND / Guía / Liquidación | Anexos 1, 3, 17 | 05/06/03 | ⚪ | XSD | `manifest.json` `activeVersion: null` | — | — | P2 | Backlog |
| R34 | Envío por lote | §7.5 Tabla 8 | Todos | ⚪ | — | — | — | — | — | NO ACTION |
| R35 | Anexos sectoriales | 4-9, 11-13, 16, 19, 20, 23, 25 | Varios | ⚪ | — | — | — | — | P2 | Backlog |

## R. Matriz por tipo documental (solo tipos implementados)

| Aspecto | Factura (01) | Nota de crédito (04) | Retención (07) |
|---|---|---|---|
| XML / versión | ✅ 1.1.0 | ✅ 1.1.0 | ⚠️ 1.0.0 (ATS 2.0.0 disponible) |
| XSD preflight | ✅ | ✅ | ✅ |
| Firma | ✅ | ✅ | ✅ |
| RUC Proveedor | 🔴 | 🔴 | 🔴 |
| `infoAdicional` | "Observación" (notas) | vacío | vacío |
| Perfil emisor (RIMPE / agente / GC / especial) | RIMPE (⚠️ NP) / 🔴 / 🔴 / 🟡 | RIMPE (⚠️ NP) / 🔴 / 🔴 / 🟡 | RIMPE (⚠️ NP) / 🔴 / n.a. / ✅ |
| Transmisión | Automática post-commit | Automática post-commit | Automática post-commit + recovery job |
| Recepción / autorización | ✅ (real en celcer) | ✅ (sin prueba real, ADR-031) | ✅ código; **sin prueba real** |
| Consulta de estado (ConsultaComprobante) | No usada | No usada | ✅ (anulación) |
| Retry | Manual (job inoperante) | Manual (job inoperante) | Guarded: solo consulta |
| RIDE | ✅ | ✅ | ✅ |
| Cancelación / anulación | Flujo comercial (sin anulación SRI) | — | ✅ ADR-036 (Discarded / AnnulmentPending / ANULADO verificado) |
| Envío por correo | ✅ | 🔴 | 🔴 |
| Permisos | `electronic_documents.view/detail/retry`, `ride.view/regenerate` | igual | igual + permisos de origen (Compras/Gastos) |
| Tests | Builder, XSD, issuer, pipeline integración | Builder, XSD | Builder, XSD, issuer guarded, integración ADR-036 |

## S. Top riesgos (solo demostrados)

**P0 — puede generar incumplimiento / documento fiscal inconsistente**
1. **Códigos de retención IVA (R24):** `codigoRetencion` de IVA = 721–728; la Tabla 20 y el ejemplo oficial usan 9/10/1/11/2/3/7/8. Si el SRI acepta el XML, el comprobante queda con un dato fiscal incorrecto; si lo rechaza, ninguna retención con IVA se autoriza. Ninguna retención se ha probado en `celcer`.
2. **RUC Proveedor (R19):** requisito obligatorio de la 2.34 ausente en los 3 tipos (sujeto a confirmar aplicabilidad y fecha del RO).

**P1 — robustez necesaria antes de producción amplia**
3. Retry job inoperante (R31) + ventana de 31 min frente a 24 h (R11).
4. NAT/DEVUELTA sin camino de corrección; código 50 tratado como rechazo (R12, R13).
5. Agente de retención / Gran Contribuyente / RIMPE NP (R20–R22) — dependen del perfil real del emisor piloto.
6. Factura/NC autorizadas sin ED tras una caída post-commit: ningún detector (§H).
7. `FirstOrDefault` sobre `<autorizacion>` (R14).
8. Sin preflight de 320 KB (R16).
9. NC y Retención no se envían al receptor (R30).
10. RucValidator estricto e inconsistente con el validador de cédula (§N).

**P2** — ND/Guía/Liquidación, anexos sectoriales, contribuyente especial en factura, ConsultaComprobante transversal.
**P3** — centralizar clave de acceso / `infoAdicional` / RIMPE; validar 2048 bits; `manifest.json` desactualizado.

## T. Componentes actuales

| Componente | Clasificación | Nota |
|---|---|---|
| `XadesBesSigner`, `XadesSignedXml` | **DO NOT TOUCH** | Validado contra comprobantes autorizados reales |
| `SriSoapClient` (recepción, consulta) | **KEEP** | Contrato 2.34 correcto |
| `SriSoapClient.ParseAutorizacionResponse` | **EXTEND** | Selección entre varias `<autorizacion>` |
| `SriSoapClient.QueryDocumentStatusAsync` + `SriFiscalStatus`/`SriStatusQueryOutcome` | **KEEP → SSOT de estado fiscal** | Extender su uso a todos los tipos |
| `ElectronicDocument` (máquina de estados) | **KEEP / EXTEND** | Falta salida de `Rejected` (corrección) |
| `ElectronicDocumentIssuer` | **KEEP / EXTEND** | Reglas 70/43/45 correctas; tratar DEVUELTA transitoria |
| `ElectronicDocumentRetryPolicy` | **EXTEND** | Ventana alineada a 24 h |
| `ElectronicDocumentRetryJob` | **EXTEND (fix)** | Copiar patrón de `RetentionElectronicRecoveryJob` |
| `RetentionElectronicRecoveryJob` | **KEEP** | Patrón cross-tenant correcto |
| `IElectronicDocumentSourceLifecycleGuard` | **KEEP** | Base para extender D-4/D-10 (ADR-036 O-15) |
| Clave de acceso en builders (×3) | **DUPLICATED → MOVE LATER** | `AccessKey` VO solo valida formato |
| `BuildInfoAdicional` / `MaxAdditionalFields` (×3) | **DUPLICATED → MOVE LATER** | Punto natural para RUC Proveedor / Gran Contribuyente |
| `ResolveContribuyenteRimpeText` (×3 providers) | **DUPLICATED** | Además texto NP desactualizado |
| `Utf8StringWriter` (×3) | **DUPLICATED** (trivial) | P3 |
| `ElectronicDocumentData.Issuer` | **EXTEND** | Futuro perfil fiscal del emisor |
| `SystemProviderSettings` | **KEEP / EXTEND** | SSOT del RUC Proveedor |
| `EmbeddedXmlSchemaProvider` + `manifest.json` | **KEEP** | Revisar XSD oficiales vigentes (patrón RIMPE) |
| Catálogos `sri_*` | **KEEP / EXTEND** | Corregir códigos IVA de retención; agregar 3740 si aplica |
| RIDE (parsers + QuestPDF) | **KEEP** | Ya muestra `campoAdicional` genéricamente |
| `docs/FICHA … Versio232.pdf` | **LEGACY** | Referencia obsoleta |

No se recomienda un "SRI Core" paralelo: la infraestructura actual puede evolucionar.

## U. Consultas read-only

Script completo (PostgreSQL, envuelto en `BEGIN TRANSACTION READ ONLY … ROLLBACK`). Enums: `current_state` 1 Draft, 3 Signed, 4 Sent, 5 Received, 6 Authorized, 7 Rejected, 8 DeadLetter, 9 Cancelled, 10 Failed, 11 Dispatching, 12 Discarded, 13 AnnulmentPending; `document_type` 1 Invoice, 2 CreditNote, 4 Retention.

```sql
BEGIN TRANSACTION READ ONLY;
-- Q1 ED por tenant/empresa/tipo/estado + antigüedad
SELECT tenant_id, company_id, document_type, current_state, count(*) n,
       min(created_at) oldest, max(created_at) newest, max(retry_count) max_retry
FROM electronic_documents GROUP BY 1,2,3,4 ORDER BY 1,2,3,4;
-- Q2 Antigüedad por estado
SELECT current_state,
  count(*) FILTER (WHERE now()-coalesce(last_attempt_utc,created_at) < interval '1 hour') lt_1h,
  count(*) FILTER (WHERE now()-coalesce(last_attempt_utc,created_at) BETWEEN interval '1 hour' AND interval '24 hours') h1_24,
  count(*) FILTER (WHERE now()-coalesce(last_attempt_utc,created_at) > interval '24 hours') gt_24h
FROM electronic_documents GROUP BY 1 ORDER BY 1;
-- Q3 Candidatos del retry genérico (lo que vería el job con tenant)
SELECT document_type, current_state, count(*) n, count(*) FILTER (WHERE retry_count >= 5) exhausted
FROM electronic_documents WHERE current_state IN (1,3,5,10) GROUP BY 1,2 ORDER BY 1,2;
-- Q4 Signed/Sent/Received/Dispatching > 24 h
SELECT document_type, current_state, count(*) n, min(coalesce(last_attempt_utc,created_at)) oldest
FROM electronic_documents
WHERE current_state IN (3,4,5,11) AND now()-coalesce(last_attempt_utc,created_at) > interval '24 hours'
GROUP BY 1,2 ORDER BY 1,2;
-- Q5 Facturas autorizadas en punto electrónico sin ED
SELECT si.tenant_id, si.company_id, count(*) n, min(si.issue_date) oldest
FROM sales_invoices si
JOIN emission_point ep ON ep.id = si.emission_point_id AND ep.emission_type = 1
LEFT JOIN electronic_documents ed ON ed.source_module='Sales' AND ed.source_entity_id=si.id AND ed.document_type=1
WHERE si.status = 2 AND ed.id IS NULL GROUP BY 1,2;
-- Q6 Devoluciones autorizadas con NC numerada sin ED
SELECT sr.tenant_id, sr.company_id, count(*) n
FROM sales_returns sr
LEFT JOIN electronic_documents ed ON ed.source_module='Sales' AND ed.source_entity_id=sr.id AND ed.document_type=2
WHERE sr.status = 2 AND sr.credit_note_document_number IS NOT NULL AND ed.id IS NULL GROUP BY 1,2;
-- Q7 Retenciones Issued sin ED o con ED en Draft
SELECT rd.tenant_id, rd.company_id, coalesce(ed.current_state::text,'(sin ED)') ed_state, count(*) n
FROM retention_documents rd
LEFT JOIN electronic_documents ed ON ed.source_module='Retentions' AND ed.source_entity_id=rd.id
WHERE rd.status = 1 AND (ed.id IS NULL OR ed.current_state = 1) GROUP BY 1,2,3;
-- Q8 Retenciones canceladas con ED Authorized (conciliación ADR-036 §24.5)
SELECT rd.tenant_id, rd.company_id, count(*) n
FROM retention_documents rd
JOIN electronic_documents ed ON ed.source_module='Retentions' AND ed.source_entity_id=rd.id
WHERE rd.status = 2 AND ed.current_state = 6 GROUP BY 1,2;
-- Q9 Códigos de retención usados (IVA debería mapear a Tabla 20 en el XML)
SELECT tax_type, retention_code, count(*) n FROM retention_document_lines GROUP BY 1,2 ORDER BY 1,2;
-- Q10 Authorized / Cancelled / AnnulmentPending por tipo
SELECT document_type, current_state, count(*) FROM electronic_documents
WHERE current_state IN (6,9,13) GROUP BY 1,2 ORDER BY 1,2;
-- Q11 Configuración RUC Proveedor (presencia en XML no inferible por SQL: el XML vive en storage)
SELECT id, ruc IS NOT NULL has_ruc, enabled, effective_date FROM system_provider_settings;
ROLLBACK;
```

**Resultado DEV** (`dberpsaas` del connection string de Development, contenedor `postgreszh`, solo lectura): las 11 consultas ejecutan sin error; **0 filas** en todas (la base DEV tiene 1 tenant, 0 facturas, 0 retenciones, 0 `sri_settings`). **No se ejecutaron contra el piloto.**

## V. Tickets inmediatos recomendados (máx. 5)

| # | Ticket | Prio | Problema | Evidencia | Alcance | Riesgo | Dependencias |
|---|---|---|---|---|---|---|---|
| 1 | `ZH-SRI-RETENTION-IVA-CODES-01` | P0 | `codigoRetencion` IVA usa 721–728 en lugar de la Tabla 20 | §I, R24 | Mapeo explícito catálogo→código XML (Tabla 20) en el provider/catálogo, sin cambiar el código interno; tests; **prueba real en celcer** de retención Renta+IVA | Retenciones históricas emitidas con códigos viejos (consultar Q9) | Certificado de pruebas |
| 2 | `ZH-SRI-ANEXO26-PROVIDER-RUC-01` | P0 | RUC Proveedor no se emite | §D, R19 | Inyectar `campoAdicional nombre="RUC Proveedor"` desde `SystemProviderSettings` en el modelo común, 1 sola vez, para 01/04/07; respetar ≤15 campos; RIDE sin cambios; fail-closed si está habilitado sin RUC | Bajo; cambia XML de infraestructura CLOSED → requiere nota ADR-023 | Confirmar aplicabilidad (ZH en Res. 2718) y fecha RO |
| 3 | `ZH-EDOC-RETRY-JOB-TENANT-01` | P1 | Job sin candidatos; ventana 31 min frente a 24 h | §G, R11, R31 | Patrón de `RetentionElectronicRecoveryJob`; política alineada a 24 h para `Received`; ejecutar Q3/Q4 en el piloto antes de activar | Reenvíos masivos de `Signed` históricos (seguros por la ficha) | Q3/Q4 en piloto; decisión O-15 de ADR-036 |
| 4 | `ZH-SRI-ISSUER-FISCAL-PROFILE-01` | P1 | Sin `agenteRetencion`/Gran Contribuyente; texto RIMPE NP; lógica RIMPE triplicada | §L, R20–R22 | Datos en Company (no entidad paralela), traducción única al modelo común; verificar XSD oficiales vigentes | Bajo | Perfil fiscal real del piloto (DECISION) |
| 5 | `ZH-EDOC-REJECTION-RECOVERY-01` | P1 | NAT/DEVUELTA terminal sin corrección; código 50 como rechazo; `FirstOrDefault` en autorizaciones | §F, R12–R14 | Clasificar DEVUELTA transitoria vs definitiva; elegir AUT entre varias autorizaciones; diseñar corrección con misma clave | Medio (toca Issuer CLOSED) | ADR |

**Backlog:** decisión retención 1.0.0→2.0.0 (R25) · % Renta vs catálogo ATS (R26) · preflight 320 KB (R16) · correo de NC/Retención (R30) · detector de factura/NC sin ED (§H) · P-2 con ConsultaComprobante (R32) · RucValidator (§N) · fundas 3740 (R28, si aplica) · contribuyente especial en factura/NC (R23) · centralizar clave/infoAdicional (P3) · `manifest.json` · ND/Guía/Liquidación y anexos sectoriales.

## W. Qué NO debe tocarse

- `XadesBesSigner` / `XadesSignedXml` (incluido digest SHA-256 + RSA-SHA1).
- Algoritmo de clave de acceso y código numérico determinístico (solo moverlo, sin cambiar el resultado).
- Tratamiento 70/43/45 → consultar, nunca regenerar clave.
- `QueryDocumentStatusAsync` y la separación `SriFiscalStatus` / `SriStatusQueryOutcome`.
- Ciclo de vida de anulación de retenciones (ADR-036: Dispatching, Discarded, AnnulmentPending).
- Preflight XSD fail-closed.
- Derivación de endpoints desde `WsdlUrl` (sin URLs quemadas).
- Catálogos IVA, formas de pago, tipos de identificación y tipos de comprobante.

## X. Validaciones ejecutadas

| Validación | Resultado |
|---|---|
| `dotnet test ERP.Application.Tests --filter ERP.Application.Tests.ElectronicDocuments` | 148/148 ✅ |
| `dotnet test ERP.Infrastructure.Tests --filter SriSoapClient\|XadesBesSigner\|SriAuthorizationClient\|SriReceptionClient` | 43/43 ✅ |
| `Hallazgo_GetRetryCandidatesAsync_sin_contexto_de_tenant_no_devuelve_candidatos` (PostgreSQL real) | 1/1 ✅ (confirma el bug) |
| Consultas §U contra DEV en transacción READ ONLY | 11/11 sin error, 0 filas |
| `git diff --check` + búsqueda de espacios finales en este archivo | ✅ sin hallazgos |
