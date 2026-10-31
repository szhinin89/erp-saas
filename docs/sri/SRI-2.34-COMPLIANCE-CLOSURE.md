# SRI 2.34 — Cierre de conformidad (ZH-SRI-2.34-COMPLIANCE-CLOSURE-01)

> **Naturaleza:** informe de cierre (2026-10-31, base `main@8e5bfde66`). **No es normativo**: las reglas viven en
> [backend.md § Límites y finalización](../architecture/backend.md#límites-y-finalización),
> [frozen-infrastructure.md § ElectronicDocuments](../architecture/frozen-infrastructure.md#electronicdocuments-v10-facturación-electrónica-sri)
> y el addendum 2026-10-31 de [ADR-023](../decisions/ADR-023-electronic-documents-v1-closure.md). Parte de la
> [auditoría 2.34](SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md) (IDs R01–R35 conservados) y no la repite.
> Fuente textual consultada: `docs/FICHA TECNICA … Versio232.pdf` (tabla de errores y notas 1–2, §7.5,
> Anexos 21, 22 y 24, sin cambios en la 2.34 para esos puntos según el historial de la ficha 2.34).

## 1. Matriz de requisitos (estado en `main` + este cambio)

Leyenda: **CONFORME** sin acción · **RESUELTO** por entregas posteriores a la auditoría · **CORREGIDO** en este
cambio · **BLOCKED** requiere decisión o dato que no puede inventarse · **FUERA DE ALCANCE** FUTURE/sectorial.

| ID | Requisito (fuente) | Estado | Evidencia / motivo |
|---|---|---|---|
| R01, R18 | Clave de acceso 49 díg. módulo 11; `infoAdicional` ≤ 15 (§5.2, XSD) | CONFORME | Algoritmo correcto; triplicación P3 sin efecto fiscal. HC: protocolo; secuencial solo del origen (`CaptureNextAsync`) |
| R02–R04 | Código numérico estable, tablas 2–4 y 6 | CONFORME | Sin cambios |
| R05 | XAdES-BES (§6, Anexo 14) | CONFORME | `XadesBesSigner` sin cambios (DO NOT TOUCH) |
| R06 | Clave 2048 bits (§6.8, recomendación) | CONFORME | No es obligación; P3 |
| R07–R09 | WS Recepción/Autorización, sin URLs quemadas, espera parametrizable (§7) | CONFORME | `SriSoapClientTests`. HC: namespaces y nombres de servicio = protocolo; endpoints derivados de `SriSettings.WsdlUrl` (configuración por empresa); los presets celcer/cel del frontend son sugerencias editables. Coherencia ambiente ↔ host oficial (§7.1) validada por `SriWsdlUrl` al guardar y en el gate de emisión; hosts no oficiales (simuladores) no se juzgan ([backend.md](../architecture/backend.md#diagnóstico-de-configuración-sri)). Polling interno 5 × 2–16 s y 3 reintentos HTTP = política del ERP; retardo inicial en `SriPolling` |
| R10 | 70/43/45 → consultar, nunca otra clave (nota 2) | CONFORME | Sin cambios |
| R11 | Procesamiento SRI hasta 24 h desde RECIBIDA (§7.5, nota 2) | **CORREGIDO** | §3 |
| R12 | NAT → corregir y reenviar con la misma clave (nota 1) | **BLOCKED** | Ver §5.2 |
| R13 | DEVUELTA 50 "Error interno general" no es defecto del comprobante | **CORREGIDO** | §3 |
| R14 | Varias `<autorizacion>` por clave; vale la AUTORIZADO (nota 1) | **CORREGIDO** | §3 |
| R15 | ConsultaComprobante (§8) | CONFORME | Usada por Retenciones; 01/04 se concilian por Autorización (Caso #32) |
| R16 | Máx. "320 Kb" por comprobante individual (§7.5, error 26) | **CORREGIDO** con límite provisional (unidad no definida por la ficha) | §3; riesgo residual §5.6 |
| R17 | XSD previo fail-closed | CONFORME | Sin cambios. HC: versiones 1.1.0/1.1.0/1.0.0 en builder, validador y `manifest.json` coinciden; triple fuente = deuda ADR-038 fase 3, sin efecto en el comportamiento |
| R19 | RUC Proveedor (Anexo 26) | RESUELTO | Composer + contributor (ADR-023 addendum 2026-10-02); pendiente solo lo administrativo (§5) |
| R20 | Leyenda RIMPE (Anexo 22) | CONFORME Emprendedor · **BLOCKED** Negocio Popular | Ver §5.3. HC: leyenda = protocolo, repetida idéntica en 3 providers (deuda ADR-038 fase 2, sin efecto en el comportamiento); régimen resuelto por catálogo `sri_tax_regime` |
| R21 | `agenteRetencion` (Anexo 21) | **BLOCKED** | Ver §5.4 |
| R22 | Gran Contribuyente (Anexo 24) | **BLOCKED** | Ver §5.4 |
| R23 | `contribuyenteEspecial` en 01/04 ("obligatorio cuando corresponda") | **CORREGIDO** | §3 |
| R24 | Códigos IVA de retención Tabla 20 | RESUELTO | `SriRetentionCodeVersion.XmlCode` (ADR-037); 15 % sin código → no emitible |
| R25 | Retención 1.0.0 vs 2.0.0 (Anexos 1 y 10) | CONFORME (1.0.0) | La ficha publica ambas; no hay evidencia normativa que prohíba 1.0.0 |
| R26 | % Renta según catálogo ATS | Limitación externa | Catálogo gobernado por ADR-037; la ficha remite al ATS |
| R27, R29 | Tarifas IVA/ICE/IRBPNR; formas de pago | CONFORME | Sin cambios. HC: tarifas, porcentajes y códigos de retención salen de catálogos GLOBAL/snapshots (ADR-037); solo los códigos estructurales (Tablas 3, 16 y 19, ambiente, emisión normal, consumidor final) son constantes de Domain |
| R28 | Fundas ICE 3680 (Anexo 18) | FUERA DE ALCANCE | Depende de ≥ 3 establecimientos o franquicia (dato de negocio) |
| R30 | Envío al receptor (§4.7) | RESUELTO | ZH-EDOC-COMMUNICATIONS-01 (01/04/07) |
| R31 | Reintento automático operativo | RESUELTO | Descubrimiento cross-tenant + scope por candidato (`ElectronicDocumentRetryJob`) |
| R32 | Estado incierto / Caso #32 | RESUELTO (consulta segura 24 h + conciliación) · **BLOCKED** reenvío de lo nunca registrado | Ver §5.1 |
| R33–R35 | 03/05/06, lote, anexos sectoriales | FUERA DE ALCANCE | FUTURE |
| §H | Factura/NC autorizadas sin ED tras caída | RESUELTO | `SalesElectronicDocumentRecovery` (facturas y NC) |
| §K | `manifest.json` `Retention.activeVersion = null` | CONFORME | Sin consumidor funcional; marca deliberada de "sin autorización real verificada" (ver §5) |
| §N | `RucValidator` vs advertencia 62 | **BLOCKED** | Fuera de la ficha; requiere fuente oficial y decisión bloquear/advertir |

### Servicios SRI (C)

| Respuesta | Estado resultante | Fuente |
|---|---|---|
| RECIBIDA | `Received` → consulta autorización | Recepción |
| DEVUELTA 70/43/45 | `Received` → consulta (nunca otra clave) | nota 2 |
| DEVUELTA solo 50 | **sin transición** (`Signed`/`Dispatching`), requiere conciliación, solo consulta | tabla de errores, nota 1 |
| DEVUELTA otra | `Rejected` (no se guarda en el SRI) | nota 1 |
| AUTORIZADO (aunque haya NAT previos) | `Authorized`, número = clave | §5.9, nota 1 |
| NO AUTORIZADO | `Rejected` | §5.12 |
| Sin `<autorizacion>` / PPR / TIMEOUT | sin transición; reintento solo consulta hasta 24 h desde el `Received` del timeline, luego DeadLetter con conciliación pendiente; sin evidencia de recepción, consulta sin DeadLetter por plazo | §7.4, §7.5 |
| Transporte, SOAP Fault, respuesta no XML | sin transición | — |
| ConsultaComprobante AUTORIZADO / NO AUTORIZADO / PENDIENTE DE ANULAR / ANULADO; RECHAZADA 99 | Retenciones (ADR-036); 99 nunca es estado fiscal | §8 |

**HC** = revisión de hardcoding (2026-10-31) sobre las rutas de producción de 01/04/07, SOAP, catálogos,
recuperación y configuración SRI: sin defectos reales; ver notas HC en las filas.

## 2. Observaciones históricas ya solucionadas (antes de este cambio)

R19, R24, R30, R31, §H y el Caso #32 (consultar antes de reenviar, conciliación con evidencia, sin
autorizar por declaración). Commits de referencia: `a965485e3`, `4cdf8cac1`, `351a0b0fa`, `c5cef5f15`,
`d0ed35c4f`; ADR-023 addenda 2026-10-02 y 2026-10-29.

## 3. Defectos corregidos en este cambio

| ID | Defecto demostrado | Corrección | RED → GREEN |
|---|---|---|---|
| R14 | `ParseAutorizacionResponse` tomaba la primera `<autorizacion>`; con un NAT previo y un AUT posterior marcaba `Rejected` un comprobante autorizado | Se elige la AUTORIZADO; si no hay, la primera | Teoría NAT-primero FAIL sin el fix |
| R13 | DEVUELTA 50 (falla interna del SRI) terminaba en `Rejected` (terminal) | Si todos los mensajes estructurados son 50, no hay `MarkSent`/`MarkRejected`: el documento conserva su estado como en un fallo de transporte | FAIL sin el fix |
| R16 | Sin preflight del límite de tamaño | XML firmado > 320 × 1024 bytes → `Failed` (validación) antes de almacenar, `Signed` o transmitir | Límite exacto transmite; 1 byte más falla |
| R11 | El máximo de la empresa (default 5, backoff 1–16 min) pasaba a DeadLetter `Signed`/`Received` a los ≈ 31 min y dejaba de consultar, aunque el SRI responde hasta 24 h después de RECIBIDA | Mismo job y política: `Signed`/`Received` siguen candidatos sobre el máximo con backoff ≤ 60 min; el issuer solo aplica DeadLetter cuando pasaron 24 h desde el primer `Received` del timeline (`ElectronicDocumentAudit`); sin evidencia de recepción, nunca por plazo (§5.7) | Unit + job + PostgreSQL (abajo) |
| C | Nada impedía combinar ambiente Pruebas con el host oficial de Producción (o al revés) | `SriWsdlUrl.IsValidFor` (regla única): rechazo al guardar y en el gate de emisión antes del secuencial; hosts no oficiales permitidos | Unit (regla, validador, gate, estado) |
| R23 | Factura y NC omitían `contribuyenteEspecial` aunque `Company.SpecialTaxpayerNo` existe | `ElectronicDocumentIssuerData.SpecialTaxpayerNumber` (fuente única 01/04/07); la retención deja de duplicarlo en `RetentionElectronicDocumentInfo` (XML idéntico) | XSD oficial 1.1.0/1.1.0/1.0.0 |

**R16 — fundamento e incertidumbre.** La ficha (2.32 y 2.34, §7.5) dice "para el envío individual, el tamaño
máximo por comprobante será de 320 Kb" y el error 26 "Tamaño del archivo supera lo establecido"; no define la
unidad y no se encontró evidencia oficial complementaria. Se mide el XML firmado en UTF-8 (lo que se envía; la
envoltura SOAP lo lleva en base64 y no es "el comprobante"). Límite provisional de 320 × 1024 bytes, la mayor
de las lecturas en bytes; no está garantizado por la norma (con 320 000 bytes, un XML entre ambos valores pasaría
el control local y el SRI lo devolvería con 26). Se valida antes de persistir `Signed` y del primer envío. El
secuencial ya está consumido: Ventas captura el número y confirma la venta antes de registrar el comprobante
(post-commit); el control no captura otro ni lo libera. Recuperación: regenerar da el mismo XML, así que no hay
recuperación automática (§5.6).

## 4. Pruebas ejecutadas

| Suite (filtrada) | Resultado |
|---|---|
| Application: ElectronicDocuments + Retentions + Ride + providers de Ventas | 458/458 PASS |
| Infrastructure: SriSoapClient, SriAuthorizationClient, SriReceptionClient, XadesBesSigner, SriDocumentStatusQuery, RIDE retención | 59/59 PASS |
| Infrastructure PostgreSQL real: `ElectronicDocumentUncertainSriPostgreSqlTests`, `SalesElectronicDocumentRecovery*`, `PurchaseRetentionConfirmIntegrationTests` | 99/99 PASS (entrega R13/R14/R16/R23) |
| R11 — Application (`ElectronicDocumentIssuerRetryTests`, `ElectronicDocumentRetryPolicyTests`) | incluidas en 469/469 (Application focal) |
| R11 — API job (`ElectronicDocumentRetryJobTenantContextTests`, scope por tenant/empresa) | 9/9 PASS |
| R11 — PostgreSQL real (`ElectronicDocumentUncertainSriPostgreSqlTests`, `ElectronicDocumentRepositoryDashboardPostgreSqlTests`) | 185/185 PASS (incluye SOAP/firma/RIDE, Caso #32 + R11, repositorio, recuperación Ventas/NC, retenciones) |
| Architecture.Tests | 143/143 PASS |
| `check-architecture-guardrails.ps1`, `npm run architecture:check` | PASS (0 nuevas) |
| RED controlado (fix revertido temporalmente) R14 y R13 | 1 FAIL cada uno, restaurado |
| Ajustes finales (R11 desde `Received`, C ambiente ↔ host): Application focal + ElectronicInvoicing | 528/528 PASS |
| Ajustes finales: PostgreSQL real R11 (`ElectronicDocumentUncertainSriPostgreSqlTests`, `ElectronicDocumentRepositoryDashboardPostgreSqlTests`) | 28/28 PASS |

Cobertura nueva: R13 (solo 50 / 50 + 35), R14 (orden AUT-NAT y NAT-AUT; solo NAT), R16 (límite y límite + 1),
R23 (builders 01/04/07 con XSD oficial, providers de Factura y NC), R11 (ventana abierta y cerrada, sin
registro de firma, AUTORIZADO y NO AUTORIZADO sobre el máximo, sin llamadas a Recepción, clave intacta, backoff
≤ 60 min traducido a SQL, contador del Monitor, job por tenant/empresa, dos consultas concurrentes, reinicio del
worker con nuevo contenedor de servicios, inicio de ventana aislado por tenant). No se ejecutaron suites completas ni se
contactó al SRI.

## 5. Limitaciones externas y puntos BLOCKED

Conformidad por contrato (XSD embebidos, respuestas SOAP representativas, PostgreSQL real) **no equivale** a
aceptación real del SRI. Pendientes externos: aceptación en `celcer` de NC (04) y Retención (07), y de 01/04 con
`contribuyenteEspecial`; registro de ZH Technologies como proveedor y fecha legal de `EffectiveDate` (Anexo 26);
porcentajes de Renta frente al catálogo ATS vigente.

1. **R32 — comprobantes que el SRI nunca registró.** La consulta automática ya cubre las 24 h (R11). Queda
   sin camino de reenvío un comprobante cuya ausencia consta (DEVUELTA 50, o sin registro tras 24 h). Causa:
   la política del Caso #32 prohíbe el reenvío automático. Acción mínima verificable: decisión del propietario
   sobre reenviar el **mismo** XML firmado y con la misma clave en esos dos casos (el SRI responde 43/45/70 si
   ya lo tiene); hasta entonces, fail-closed con conciliación.
2. **R12 — corrección de NO AUTORIZADO.** La ficha permite reenviar con la misma clave tras corregir; `Rejected`
   es terminal y la venta ya está comprometida fiscalmente. Causa: no existe una corrección demostrable que no
   edite la venta. Acción mínima verificable: ADR aprobado que defina qué correcciones son posibles (solo
   configuración del emisor o catálogos), la transición de salida de `Rejected` y su evidencia. Sin cambios.
3. **R20 Negocio Popular.** El Anexo 22 exige `CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE`, pero el patrón de
   los XSD embebidos (copia en `Resources/SRI`, sin fecha de publicación) solo admite `CONTRIBUYENTE RÉGIMEN
   RIMPE`. Causa: no se dispone de los XSD oficiales vigentes para contrastar (no se descargaron: sin contacto con
   el SRI en esta entrega). Acción mínima verificable: incorporar los XSD oficiales y comparar el patrón; no se
   altera la leyenda ni el esquema para forzar la validación. RIMPE Emprendedor: conforme.
4. **R21/R22 — agente de retención y Gran Contribuyente.** Exigen el número de resolución, que `Company` no
   modela (`WithholdsRenta`/`WithholdsVat` no son la designación). Son condicionales al perfil del contribuyente:
   no se emiten ni se inventan. Acción mínima verificable: perfil fiscal real del piloto (resoluciones) y
   aprobación de los campos en `Company` (migración). El XSD 1.1.0 ya admite `<agenteRetencion>`.
5. **§N — RucValidator.** La ficha no define el algoritmo y trata la identificación incorrecta como
   advertencia 62; requiere fuente oficial y decisión de política.
6. **R16 — comprobante que excede el tamaño.** Medido sobre el formato del builder: ≈ 450–730 bytes por línea
   de detalle, unos 420 ítems en el peor caso. Ventas no limita líneas; una venta mayor queda autorizada
   comercialmente con su comprobante `Failed` sin recuperación. Acción mínima: decisión del propietario sobre un
   límite preventivo de líneas antes de autorizar la venta; la unidad exacta solo se cierra con el SRI.
7. **R11 — `Signed` sin evidencia de recepción.** El plazo de §7.5 corre desde RECIBIDA; si el ERP nunca obtuvo
   respuesta de Recepción (fallo de transporte, DEVUELTA 50) no hay fecha de inicio persistida. Fail-closed: no
   hay DeadLetter por plazo y la consulta sigue cada 60 min hasta AUTORIZADO, NO AUTORIZADO o conciliación; no
   se reenvía (Caso #32). Limitación: consulta indefinida mientras el propietario no decida R32.
8. **C — host no oficial.** La coherencia ambiente ↔ endpoint solo se garantiza con los hosts oficiales de la
   ficha; una URL de otro host (proxy, simulador, cambio del SRI) es responsabilidad de la configuración.

## 6. Archivos modificados

Código: `ElectronicDocumentData.cs`, `RetentionElectronicDocumentData.cs`, `InvoiceXmlBuilder.cs`,
`CreditNoteXmlBuilder.cs`, `RetentionXmlBuilder.cs`, `SalesInvoiceElectronicDocumentDataProvider.cs`,
`SalesReturnCreditNoteDataProvider.cs`, `RetentionElectronicDocumentDataProvider.cs`,
`ElectronicDocumentIssuer.cs`, `SriSoapClient.cs`, `ElectronicDocumentRetryPolicy.cs`,
`IElectronicDocumentRepository.cs`, `ElectronicDocumentRepository.cs`.
Pruebas: `ElectronicDocumentIssuerReceptionTests`, `SriSoapClientTests`, `InvoiceXmlBuilderTests`,
`CreditNoteXmlBuilderTests`, `RetentionXmlBuilderTests`, `SalesInvoiceElectronicDocumentDataProviderTests`,
`SalesReturnCreditNoteDataProviderTests`, `RetentionElectronicDocumentDataProviderTests` y fixtures de retención
(campo movido), `ElectronicDocumentIssuerRetryTests`, `ElectronicDocumentRetryPolicyTests`,
`ElectronicDocumentRetryJobTenantContextTests`, `ElectronicDocumentUncertainSriPostgreSqlTests`,
`ElectronicDocumentRepositoryDashboardPostgreSqlTests`, `ElectronicEmissionRequirementsValidatorTests`,
`GetElectronicInvoicingStatusQueryHandlerTests`; código C: `SriWsdlUrl.cs`, `UpsertSriConfigurationCommandValidator.cs`,
`ElectronicEmissionRequirementsValidator.cs`, `GetElectronicInvoicingStatusQueryHandler.cs`,
`ValidateSriConfigurationQueryHandler.cs`.
Documentación: ADR-023 (addendum), `frozen-infrastructure.md`, `backend.md`, este informe, `STATUS.md`.
Sin migraciones, frontend, XSD, catálogos ni configuración.

## 7. Sin duplicación

Un solo pipeline (`ElectronicDocumentIssuer`), un solo cliente SOAP (`SriSoapClient`), un solo validador XSD por
tipo y una sola firma. `contribuyenteEspecial` pasa a tener **una** fuente (antes dos rutas: solo retención); la
clasificación del código 50 vive junto a la de 70/43/45 en el Issuer. La ventana de 24 h es parte de la misma
`ElectronicDocumentRetryPolicy` y del mismo job; su inicio sale del timeline existente. No se agregaron estados,
jobs, schedulers, clientes SOAP, servicios, catálogos, migraciones ni políticas de reintento paralelas.

## 8. Estado final

| Documento | Estado |
|---|---|
| Factura 01 | Conforme por contrato (XSD 1.1.0, R13/R14/R16/R23); validada antes en `celcer`; nueva salida con `contribuyenteEspecial` pendiente de aceptación real |
| Nota de crédito 04 | Conforme por contrato; sin aceptación real en `celcer` |
| Retención 07 | Conforme por contrato (1.0.0, Tabla 20, RUC Proveedor, XML sin cambios por R23); sin aceptación real |
| Caso #32 | Consulta segura durante las 24 h del SRI y conciliación con evidencia: conforme; reenvío de lo nunca registrado: **BLOCKED** (§5.1) |

**READY FOR REVIEW** para las correcciones de §3 (R11, R13, R14, R16, R23). Los puntos de §5 quedan BLOCKED de
forma individual; ninguno se declara CLOSED.
