# Preparación operativa de empresa (Company Readiness)

**Estado:** IN REVIEW (Fase 2B-1 backend y contrato; Fase 2B-2 interfaz guiada; Fase 2B-3 habilitación preventiva de cajas; F-1..F-5 política fiscal de emisión física). No CLOSED hasta revisión y CI.

SSOT funcional del diagnóstico de preparación operativa de la empresa activa en la sucursal activa: qué revisa, de qué fuente única sale cada dato, qué validador decide y qué capacidades condiciona. Las decisiones de emisión electrónica se fundamentan en [ADR-023](../decisions/ADR-023-electronic-documents-v1-closure.md) y [ADR-038](../decisions/ADR-038-sri-electronic-compliance-architecture.md); el modo contable, en [accounting.md](./accounting.md).

## Principios

1. **Solo lectura.** Readiness consulta y orienta; nunca persiste ni modifica configuración. Se recalcula en cada consulta.
2. **La operación propietaria es la autoridad.** Una capacidad solo depende de lo que su operación real exige (ver [matriz de operaciones](#matriz-operación--precondiciones-reales--capacidad)); Readiness no añade restricciones propias ni bloquea lo que el backend permite.
3. **Un dato, un ítem.** Cada requisito aparece en un único ítem; las cajas lo referencian por código.
4. **El validador propietario decide.** Readiness proyecta el resultado del validador o la política dueña del dato; no reimplementa reglas.
5. **Preparación operativa ≠ validación fiscal.** Que una caja pueda vender no certifica que su emisión cumpla la normativa: cada caja informa ambos estados por separado.
6. **Sin texto en el contrato.** El backend devuelve códigos, estados y un `actionTarget` semántico; el frontend resuelve textos vía i18n (`operationalReadiness.items.{code}.{label|ready|warning|missing|notApplicable}`) y traduce `actionTarget` a la ruta del formulario propietario (`operationalReadinessActionRoutes.ts`).
7. **Fail-closed.** Sin empresa válida en el scope no se consulta nada más con ese scope; sin sucursal activa no se evalúa ninguna caja ni bodega; un fallo técnico del validador nunca se presenta como `Ready`.

## Ámbito: empresa y sucursal activa

`GET /api/v1/companies/operational-readiness` → `CompanyOperationalReadinessDto` (implementación única: `CompanyOperationalReadinessResolver`).

La consulta es `IBranchScopedRequest`: la sucursal activa es el header `X-Branch-Id` (`CurrentBranchService`) y `BranchScopeBehavior` la valida con `IBranchAccessGuard` (existe, activa, de la empresa, accesible para el usuario). Sin sucursal la consulta se rechaza (`branch_context_required`); nunca se recurre a otra sucursal. La pantalla no consulta mientras `activeBranchStore` no tenga sucursal y muestra `operationalReadiness.branchRequired`; el selector de sucursal existente (`useBranchGate`) es el único que la define.

| Ámbito | Requisitos |
|---|---|
| Empresa (una vez) | Identidad, establecimiento matriz, configuración SRI y certificado, modo contable, catálogo SRI, defaults de venta de empresa, marca y documentos, ítems activos |
| Sucursal activa | Cajas activas de la sucursal, bodegas activas de la sucursal, bodega por defecto de venta de la sucursal (`IInvoiceDefaultsResolver` con la sucursal activa) |
| Caja y punto | Punto de emisión asignado y activo, modalidad, resultado del validador de esa modalidad para ese punto (electrónico: dirección del establecimiento, certificado, etc.; físico: [política fiscal física](#política-fiscal-de-emisión-física-f-1f-5)) |

Las cajas y bodegas se filtran por empresa **y** sucursal activa, porque `OpenCashSession` exige `register.BranchId == sucursal activa`, el punto de una caja debe pertenecer a un establecimiento de su sucursal, y ventas, ajustes y compras validan que la bodega sea de la sucursal activa.

## Contrato

| Campo | Semántica |
|---|---|
| `branchId` | Sucursal activa a la que corresponde el diagnóstico |
| `status` (ítem) | `Ready` cumplido · `Missing` incumplido y `Blocking` · `Warning` incumplido pero recomendable, o cumplido con advertencia · `NotApplicable` la capacidad que lo exige no está activa |
| `severity` | `Blocking` (incumplido ⇒ `Missing`) · `Warning` (incumplido ⇒ `Warning`) · `Info` |
| `blockingAreas` | Capacidades de la sucursal que este ítem deja sin ningún camino mientras esté `Missing` o `Warning`. Vacío si no condiciona ninguna (aunque esté pendiente en alguna caja) |
| `actionTarget` | Pantalla propietaria donde se corrige el dato (nunca una ruta) |
| `status` (sección) y `overallStatus` | `Missing` > `Warning` > `Ready`; `NotApplicable` solo si ningún ítem aplica. Los ítems `NotApplicable` no cuentan |
| `canSell`, `canUseInventory`, `canUseCashRegister` | En la sucursal activa: `true` si ningún ítem que lista el área está `Missing` o `Warning` |
| `canIssueElectronicInvoices` | Igual, y además alguna caja de la sucursal tiene un punto de emisión electrónico |
| `cashRegisters[]` | Una entrada por caja activa de la sucursal: `cashRegisterId`, `code`, `name`, `emissionPointId`, `establishmentCode`, `emissionPointCode`, `emissionType`, `operationalStatus`, `fiscalStatus`, `requirementCodes` |

**Por caja:**

| Campo | Valores |
|---|---|
| `operationalStatus` | `Ready` si la caja puede vender con su punto (punto asignado y activo, sin requisitos obligatorios del validador de su modalidad); `Missing` si no |
| `fiscalStatus` | `Verified` punto electrónico sin requisitos obligatorios · `Pending` punto (electrónico o físico) con requisitos obligatorios o verificación fallida · `NotVerified` punto físico que cumple los requisitos verificables de la [política fiscal física](#política-fiscal-de-emisión-física-f-1f-5); la autorización del SRI no se certifica y nunca se presenta como autorizado · `NotApplicable` caja sin punto activo |
| `requirementCodes` | `code` de los ítems de `sections` que la caja incumple (referencia, no copia). Los requisitos que adopta un ítem base se referencian con el código de ese ítem (p. ej. `identity.taxId`) |

Cada punto distinto se valida una sola vez con el validador de su modalidad aunque lo compartan varias cajas.

### Ejemplos

Sucursal Matriz con caja física `CAJA-01` (001-002) que cumpliera la política física (hoy ningún régimen lo permite: ver BLOCKED) y caja electrónica `CAJA-02` (001-003) sin certificado:

```json
{
  "branchId": "…matriz…",
  "canSell": true,
  "canIssueElectronicInvoices": false,
  "cashRegisters": [
    { "code": "CAJA-01", "establishmentCode": "001", "emissionPointCode": "002", "emissionType": "Physical",
      "operationalStatus": "Ready", "fiscalStatus": "NotVerified", "requirementCodes": [] },
    { "code": "CAJA-02", "establishmentCode": "001", "emissionPointCode": "003", "emissionType": "Electronic",
      "operationalStatus": "Missing", "fiscalStatus": "Pending", "requirementCodes": ["electronicEmission.certificate"] }
  ]
}
```

El ítem `electronicEmission.certificate` aparece una vez, `Missing`, con `blockingAreas: ["ElectronicInvoicing"]` (la caja física mantiene la venta).

Dos cajas electrónicas, una completa (001-002) y otra cuyo establecimiento no tiene dirección (002-001): `canIssueElectronicInvoices: true`, pero `electronicEmission.establishmentAddress` queda `Missing` con `blockingAreas: []` y referenciado solo por la segunda caja; `overallStatus` es `Missing`. Un punto correcto nunca oculta el incumplimiento de otro.

Sucursal Norte sin cajas propias (aunque Matriz tenga cajas listas): `cashRegisters: []`, `cashRegister.withEmissionPoint` `Missing` con `blockingAreas: ["Sales", "CashRegister"]`, `canSell: false`.

## Matriz operación → precondiciones reales → capacidad

Precondiciones verificadas en el código de cada operación. Solo las que dependen de configuración se reflejan en Readiness.

| Operación (propietaria) | Precondiciones de configuración reales | Capacidad |
|---|---|---|
| Abrir caja (`OpenCashSessionCommand`, branch-scoped) | Caja activa de la sucursal activa con punto de emisión asignado, existente y activo; los requisitos que la estrategia de emisión de ese punto exige para la factura (01): `IPhysicalEmissionRequirementsValidator` si es físico, `IElectronicEmissionRequirementsValidator` si es electrónico ([habilitación preventiva](#habilitación-preventiva-de-cajas-apertura-de-jornada)). No contabiliza | Sales, CashRegister |
| Crear borrador de venta (`CreateSalesDraftCommand`, branch-scoped) | Caja abierta; punto de emisión **de la caja** activo en la empresa; bodegas de línea de la sucursal activa. Precio: lista del cliente → lista por defecto → `Item.BaseSalePrice`. Tipo de documento: el enviado (activo en `sri_doc_types`) o 01. Condición de pago: explícita → del cliente → Contado del sistema (o crédito con condición de empresa/vencimiento manual) | Sales |
| Autorizar venta (`AuthorizeSalesInvoiceCommand`) | Punto y establecimiento del borrador; con emisión **electrónica** (snapshot del borrador), `IElectronicEmissionRequirementsValidator` antes del secuencial; con emisión física, la [política fiscal física](#política-fiscal-de-emisión-física-f-1f-5) antes del secuencial (`PhysicalSalesInvoiceEmissionStrategy`). Contabiliza en la misma transacción: sin modo contable se rechaza | Sales, ElectronicInvoicing |
| Confirmar compra / ajuste de stock (branch-scoped) | Bodega de la sucursal activa. Contabiliza en la misma transacción: sin modo contable se rechaza. Las transferencias no contabilizan | Inventory |
| Movimiento o depósito de caja, cierre con diferencia | Caja abierta. Contabiliza en la misma transacción: sin modo contable se rechaza. Cerrar sin diferencia no contabiliza | CashRegister |

El modo contable se rechaza dentro de la transacción de la operación porque `AccountingPostingRecorder` lanza en los handlers in-process de `ErpDbContext.SaveChangesAsync` ([accounting.md](./accounting.md): "sin ninguna versión la decisión no puede tomarse y la operación que generaría el hecho se rechaza").

El punto por defecto de la empresa (`EmissionPoint.IsDefault`) solo alimenta los valores por defecto de la pantalla de venta; ninguna operación lo usa, por eso no condiciona ninguna capacidad.

## Matriz dato → propietario → validador → capacidades

| Ítem (`code`) | Ámbito | Dato y propietario | Regla / validador | Severidad | Capacidades | Pantalla (`actionTarget`) |
|---|---|---|---|---|---|---|
| `identity.taxId` | Empresa | `Company.TaxIdentificationNumber` | No vacío y no provisional (`IsTemporaryTaxIdentification`); con cajas electrónicas, el veredicto del validador (`company`, `issuerTaxId`) | Warning; Blocking si el validador lo rechaza | Las del [requisito electrónico](#requisitos-de-emisión-electrónica) | `CompanyProfile` |
| `identity.legalName` | Empresa | `Company.LegalName` | No vacío; con cajas electrónicas, `issuerLegalName` | Ídem | Ídem | `CompanyProfile` |
| `identity.tradeName` | Empresa | `Company.TradeName` | No vacío | Warning | — | `CompanyProfile` |
| `identity.corporateEmail` | Empresa | `Company.CorporateEmail` | No vacío | Warning | — | `CompanyProfile` |
| `structure.mainEstablishment` | Empresa | `Establishment.IsMain` activo | Existe; con cajas electrónicas, `mainEstablishment` | Ídem `identity.taxId` | Ídem | `Establishments` |
| `accounting.operationalMode` | Empresa | `AccountingOperationalPolicy` | `AccountingOperationalDecision` ≠ `NotConfigured` (`Disabled` es una decisión válida) | Blocking | Sales, ElectronicInvoicing, Inventory, CashRegister | `AccountingSettings` |
| `cashRegister.withEmissionPoint` | Sucursal / caja | `CashRegister` activas de la sucursal con `EmissionPointId` de un punto activo | Toda caja activa de la sucursal tiene punto activo, y hay al menos una | Blocking | Sales, CashRegister solo si ninguna caja de la sucursal tiene punto | `CashRegisters` |
| `electronicInvoicing.activation` | Sucursal | `EmissionPoint.EmissionType` de los puntos de caja | Alguno `Electronic` ⇒ `Ready`; si no ⇒ `NotApplicable` | Info | — | `EmissionPoints` |
| `electronicEmission.*` | Empresa o punto | Ver [requisitos electrónicos](#requisitos-de-emisión-electrónica) | `IElectronicEmissionRequirementsValidator` | Ver abajo | Ver abajo | Indicado por el validador |
| `electronicInvoicing.verificationFailed` | Punto | — | El validador lanzó un error técnico (se registra en log); el punto no puede emitir | Blocking | Ver abajo | `ElectronicInvoicingSettings` |
| `physicalEmission.activation` | Sucursal | `EmissionPoint.EmissionType` de los puntos de caja | Alguno `Physical` ⇒ `Ready`; si no ⇒ `NotApplicable` | Info | — | `EmissionPoints` |
| `physicalEmission.*` | Empresa o punto | Ver [política fiscal física](#política-fiscal-de-emisión-física-f-1f-5) | `IPhysicalEmissionRequirementsValidator` | Blocking | Sales solo si ninguna caja de la sucursal puede vender | Indicado por la política |
| `sales.defaultDocType` | Empresa | `invoice.default_doc_type_code` vía `IInvoiceDefaultsResolver` | No vacío (sin él la venta usa 01) | Warning | — | `CompanySalesSettings` |
| `sales.defaultPaymentMethod` | Empresa | `invoice.default_payment_method_code` | No vacío (la autorización sincroniza el código del método de pago usado) | Warning | — | `CompanySalesSettings` |
| `sales.defaultPaymentTerm` | Empresa | `invoice.default_payment_term_id` | Existe (sin él, el crédito exige condición o vencimiento manual) | Warning | — | `CompanySalesSettings` |
| `sales.defaultPriceList` | Empresa | `PriceList.IsDefault` | Existe (sin ella el precio cae a `Item.BaseSalePrice`) | Warning | — | `PriceLists` |
| `sales.defaultWarehouse` | Sucursal | Bodega de venta resuelta por `IInvoiceDefaultsResolver` para la sucursal activa (BranchSetting → `Warehouse.IsMain`) | Resuelta, o selección manual no requerida | Warning | — | `Branches` |
| `sales.consumerFinalPolicy` | Empresa | `ISalesFiscalPolicyResolver` (con fallback de dominio) | Siempre resuelve | Info | — | `CompanyFiscalSettings` |
| `inventory.activeWarehouse` | Sucursal | `Warehouse` activas de la empresa en la sucursal activa | Al menos una | Blocking | Inventory | `Warehouses` |
| `inventory.categoryDepth` | Empresa | `ICatalogConfigurationResolver` (con SystemDefault) | Siempre resuelve | Info | — | `Items` |
| `inventory.activeItems` | Empresa | `Item` activos | Al menos uno | Warning | — | `Items` |
| `documents.branding` | Empresa | Logo vía `ICompanyBrandingResolver` | Configurado | Warning | — | `CompanyBranding` |
| `documents.footerText` | Empresa | Pie de página vía `ICompanyBrandingResolver` | Configurado | Warning | — | `CompanyBranding` |
| `documents.decimalPrecision` | Empresa | Política de precisión ([data-standards.md](./data-standards.md#estándar-de-precisión-numérica)) | Siempre resuelve (SystemDefault) | Info | — | `DecimalSettings` |

### Requisitos de emisión electrónica

Readiness invoca `IElectronicEmissionRequirementsValidator` (el mismo gate de `AuthorizeSales`, notas de crédito y retenciones) una vez por cada punto electrónico distinto de las cajas de la sucursal, con el tenant y la empresa del contexto y el tipo Factura (`SriDocumentTypeCodes.Invoice`), que es la capacidad diagnosticada. Cada requisito incumplido se publica una sola vez con el **mismo código y pantalla** que devuelve el validador (`ElectronicEmissionRequirementCodes`) y las cajas afectadas lo referencian en `requirementCodes`; Readiness no añade reglas fiscales ni contacta al SRI.

Un requisito obligatorio incumplido en **cualquier** caja queda `Missing` (un punto correcto nunca oculta otro). Solo condiciona una capacidad si la deja sin ningún camino en la sucursal:

| Situación en la sucursal | `blockingAreas` del requisito |
|---|---|
| Ninguna caja puede vender | Sales, ElectronicInvoicing |
| Hay caja física que puede vender, ninguna electrónica puede emitir | ElectronicInvoicing |
| Otra caja electrónica sí puede emitir | — (pendiente solo para las cajas que lo referencian) |
| Requisito de advertencia (`certificateExpiring`) | — (`Warning`) |

Los códigos que revisan un dato con ítem base no se publican aparte: el ítem base adopta el veredicto del validador (estado, severidad y capacidades) y las cajas lo referencian con el código base.

| Código del validador | Ítem base |
|---|---|
| `electronicEmission.company`, `electronicEmission.issuerTaxId` | `identity.taxId` |
| `electronicEmission.issuerLegalName` | `identity.legalName` |
| `electronicEmission.mainEstablishment` | `structure.mainEstablishment` |
| `physicalEmission.issuerTaxId` | `identity.taxId` |
| `physicalEmission.issuerLegalName` | `identity.legalName` |

El resto (`matrixAddress`, `emissionPoint`, `establishmentAddress`, `sriSettings`, `sriEnvironment`, `sriWsdlUrl`, `certificate`, `certificateUnusable`, `certificateExpired`, `certificateExpiring`, `docType`) solo los publica el validador.

## Habilitación preventiva de cajas (apertura de jornada)

Una caja que no puede vender con su propio punto de emisión no inicia la jornada: el rechazo ocurre al abrir la caja, antes de que el cajero comience a vender, y no al autorizar la factura con el cliente esperando.

- **Regla única.** `ICashRegisterOpeningReadinessChecker` (Caja, Application) evalúa la caja con **su** punto de emisión, nunca con el punto por defecto de la empresa: sin punto activo ⇒ `cashRegister.withEmissionPoint`; con punto activo, delega en `ISalesInvoiceEmissionStrategyResolver` para el tipo del punto y el documento 01 — la estrategia física invoca la [política fiscal física](#política-fiscal-de-emisión-física-f-1f-5); la electrónica, `IElectronicEmissionRequirementsValidator`. No contiene reglas fiscales propias. Un fallo técnico produce `electronicInvoicing.verificationFailed` o `physicalEmission.verificationFailed` según la modalidad y nunca habilita la caja.
- **Autoridad en el backend.** `OpenCashSessionHandler` aplica la regla antes de crear la sesión (sin ningún efecto si rechaza): punto desactivado o fallo de verificación ⇒ `VALIDATION_ERROR`; requisitos electrónicos obligatorios ⇒ `SRI_EMISSION_REQUIREMENTS_MISSING`, físicos ⇒ `PHYSICAL_EMISSION_REQUIREMENTS_MISSING`, con el detalle del validador. Los requisitos de advertencia (`certificateExpiring`) no impiden abrir.
- **Diagnóstico previo.** `GET /api/v1/cash-registers/{id}/opening-readiness` (branch-scoped, permiso `caja.open`, el mismo de la apertura; no exige permisos de configuración) devuelve `CashRegisterOpeningReadinessDto`: `canOpen`, `emissionType`, `fiscalStatus` y `requirements[]` (`code`, `severity` `Required`/`Warning`, `actionTarget`). Una caja de otra sucursal es `NotFound`. Solo lectura y recalculado en cada consulta; la apertura lo vuelve a evaluar.
- **Mismos códigos que Readiness.** `OperationalReadinessCodes` (Companies, Application) es la única fuente de los códigos de ítem compartidos, del mapeo de requisitos con ítem base (`electronicEmission.company`/`issuerTaxId` y `physicalEmission.issuerTaxId` ⇒ `identity.taxId`, etc.) y del estado fiscal por modalidad; la usan el resolver de empresa y la caja.
- **Interfaz.** En la apertura de caja (`CajaPage`) se consulta el diagnóstico de la caja seleccionada en la sucursal activa (al cambiar de caja o de sucursal, y tras un rechazo de la apertura); una respuesta de otra caja o sucursal no se muestra. Si `canOpen` es `false` se deshabilita la apertura y cada requisito se presenta con el mismo componente que Preparación de Datos (`ReadinessRequirementRow`, vía `configuracion/facades/operationalReadinessGuidanceFacade`): qué falta, por qué importa y «Se corrige en» según el menú autenticado, o la indicación de pedir asistencia si el usuario no tiene acceso a esa pantalla. Si el diagnóstico no puede consultarse, se avisa y la apertura queda a decisión del backend. Las demás cajas no se ven afectadas.
- **Física.** Una caja física sin régimen que lo permita, sin autorización de impresión vigente o con el siguiente secuencial fuera del rango no abre; si cumple la política, abre y se muestra `NotVerified` con su aclaración (no se certifica ante el SRI).
- **Después de abrir.** Si la configuración cambia con la caja ya abierta, el gate de `AuthorizeSalesInvoiceCommand` sigue rechazando antes del secuencial y de cualquier efecto (validación definitiva sin cambios).

## Política fiscal de emisión física (F-1..F-5)

Propietario: módulo Company (régimen tributario, punto de emisión, autorización de impresión y secuencia). Implementación única: `IPhysicalEmissionRequirementsValidator` (`ERP.Application/Modules/Company/PhysicalEmission`). La consume `PhysicalSalesInvoiceEmissionStrategy` (Ventas), y con ella la habilitación preventiva de cajas, el diagnóstico de Readiness y el gate de `AuthorizeSalesInvoiceCommand` antes de `CaptureNextAsync`. Readiness y Caja no contienen reglas fiscales físicas: proyectan el resultado. Nunca contacta al SRI.

### Fuentes oficiales

| Fuente | Contenido usado |
|---|---|
| Reglamento de Comprobantes de Venta, Retención y Documentos Complementarios, D.E. 430 (R.O. 247, 30-VII-2010; texto consolidado gob.ec) | Art. 5: autorización de impresión por establecimientos gráficos autorizados; art. 6: vigencia de un año (tres meses si hay obligaciones pendientes, ampliable hasta doce); art. 7: comprobantes emitidos durante la suspensión no sustentan crédito ni gasto; art. 18: preimpresos — n.º y fecha de autorización (num. 1), RUC del emisor (num. 2), razón social (num. 3), numeración establecimiento-punto-secuencial de 9 dígitos (num. 5), fecha de caducidad (num. 7); art. 49: baja al vencer la vigencia |
| [sri.gob.ec/facturacion-fisica](https://www.sri.gob.ec/facturacion-fisica) | Notas de venta emitidas exclusivamente por Negocios Populares del Régimen RIMPE |
| [sri.gob.ec/rimpe](https://www.sri.gob.ec/rimpe) (Res. NAC-DGERCGC21-00000060 y NAC-DGERCGC24-00000027) | Negocio Popular: notas de venta o facturas electrónicas a su elección; al perder la condición, anula las notas de venta autorizadas. Emprendedor: facturas bajo el esquema de comprobantes electrónicos |
| Res. NAC-DGERCGC22-00000024 (resumen de terceros; texto oficial no consultado) | Desde el 30-XI-2022 los obligados solo pueden solicitar preimpresos si ya tienen autorización de emisión electrónica en producción, y su uso es excepcional (fuerza mayor o caso fortuito). Para Negocios Populares la obligación electrónica rige donde no corresponde nota de venta |

### Modalidades

| Modalidad | Tratamiento en el ERP |
|---|---|
| Factura electrónica (01) | Sin cambios: `IElectronicEmissionRequirementsValidator` (ElectronicDocuments FROZEN, no modificado) |
| Representación impresa de la factura electrónica (RIDE / tirilla) | No es un comprobante físico: pertenece al flujo electrónico |
| Factura física preimpresa (01) | **No habilitada para ningún régimen.** Régimen General: una autorización registrada no basta — los preimpresos de un emisor obligado solo proceden por fuerza mayor o caso fortuito (ver BLOCKED); RIMPE Negocio Popular y Emprendedor: no previstos |
| Nota de venta física RIMPE Negocio Popular (02) | Permitida por el catálogo para el régimen 03, pero **no implementada** en Ventas: `sri_doc_type` 02 sigue inactivo y la política la rechaza (`physicalEmission.docType`). No se adapta el flujo de la factura 01 |
| Documentación física excepcional o de contingencia | Es el único supuesto en que hoy procede un preimpreso para un emisor obligado; no está implementado (sin mecanismo verificable de contingencia) y queda BLOCKED |

### Requisitos que verifica (todos `Required`)

| Código | Brecha | Regla | Pantalla |
|---|---|---|---|
| `physicalEmission.issuerTaxId` (ítem base `identity.taxId`) | F-3 | Empresa del scope con RUC de 13 dígitos no provisional | `CompanyProfile` |
| `physicalEmission.issuerLegalName` (ítem base `identity.legalName`) | F-3 | Razón social | `CompanyProfile` |
| `physicalEmission.emissionPoint` | — | Punto de la empresa existente y activo (la modalidad la fija quien invoca: tipo del punto en la apertura, snapshot del borrador al autorizar) | `EmissionPoints` |
| `physicalEmission.taxRegime` | F-4 | `Company.TaxRegimeCode` configurado | `CompanyFiscalSettings` |
| `physicalEmission.docTypeNotPermitted` | F-4, F-5 | Fila activa en `global.sri_tax_regime_physical_doc_type` para (régimen activo, tipo) | `EmissionPoints` |
| `physicalEmission.docType` | F-5 | Tipo activo en `sri_doc_type` (habilitado en el ERP; no depende de `is_electronic`) | — |
| `physicalEmission.authorization` | F-1 | Existe una autorización activa del punto y tipo, en vigor hoy (día de empresa, `ICompanyClock`) | `PhysicalDocumentAuthorizations` |
| `physicalEmission.authorizationExpired` | F-1 | La autorización que cubre el siguiente secuencial está caducada | `PhysicalDocumentAuthorizations` |
| `physicalEmission.sequentialOutOfRange` | F-1 | El siguiente secuencial (`DocumentSequence.CurrentSeq`, 1 si no hay fila; solo lectura) no está en ningún rango activo. Al autorizar, la comprobación definitiva ocurre bajo el lock de la secuencia ([captura acotada](#captura-acotada-al-rango-autorizado)) | `PhysicalDocumentAuthorizations` |
| `physicalEmission.verificationFailed` | — | Fallo técnico de la política (registrado en log); nunca habilita la caja | `PhysicalDocumentAuthorizations` |

El rechazo usa `PHYSICAL_EMISSION_REQUIREMENTS_MISSING` con el detalle `[código] mensaje` (`PhysicalEmissionRequirementCodes`). `fiscalStatus` de una caja física: `Pending` si hay requisitos; `NotVerified` si cumple los verificables — nunca `Verified`. Los requisitos físicos se publican en la sección `physicalEmission` (ítem `physicalEmission.activation`, `Info`) y solo bloquean `Sales` si ninguna caja de la sucursal puede vender.

### Datos persistentes

- `physical_document_authorization` (empresa): punto, tipo, número de autorización (solo dígitos), fecha de autorización, fecha de caducidad (máximo un año, art. 6), primer y último secuencial (1..999.999.999). Inmutable; renovación o corrección = nuevo registro y baja lógica del anterior. Sin solapamiento de rangos activos del mismo punto y tipo. Se administra en Secuencias documentales (`GET/POST /api/v1/settings/physical-document-authorizations`, `PATCH /{id}/disable`, permiso `settings.document-sequences.manage`).
- `global.sri_tax_regime_physical_doc_type` (catálogo): `03 Negocio Popular → 02 Nota de venta` activa; `01 General → 01 Factura` **inactiva** (documenta el bloqueo en `legal_basis`). Solo una fila activa permite; lo no listado o inactivo no se emite en físico (fail-closed).
- Migración incremental `PhysicalDocumentAuthorization` (2 tablas, 2 filas de catálogo); su consolidación en la baseline V1 queda a decisión del propietario.

### Captura acotada al rango autorizado

`AuthorizeSalesInvoiceCommand` pide a la estrategia de emisión los rangos de la modalidad (`ResolveAuthorizedSequentialRangesAsync`: la física devuelve los de las autorizaciones activas en vigor hoy, vía `IPhysicalEmissionRequirementsValidator.GetAuthorizedSequentialRangesAsync`; la electrónica, ninguno) y los pasa a `IDocumentSequenceRepository.CaptureNextAsync(…, authorizedRanges, ct)`. El propietario de Secuencias Documentales comprueba el número bajo el mismo advisory lock y en la misma transacción que la reserva: fuera de rango no escribe nada (la transacción propia se revierte; la ambiente queda intacta) y la venta se rechaza con `physicalEmission.sequentialOutOfRange`, en Draft y sin efectos. Con dos o más cajas disputando el último número autorizado, solo una lo obtiene y nunca se entrega uno superior al límite. La numeración electrónica conserva la captura sin límite. Extensión registrada en [frozen-infrastructure.md § Secuencias Documentales](./frozen-infrastructure.md#secuencias-documentales).

### BLOCKED (sin fundamento oficial suficiente; fail-closed)

| Caso | Motivo |
|---|---|
| Factura física ordinaria del Régimen General (01) | Una autorización registrada habilitaba la factura física sin más condición. Según copias de terceros de la Res. NAC-DGERCGC22-00000024 (texto oficial no consultado), desde el 30-XI-2022 los preimpresos de un emisor obligado solo se usan excepcionalmente por fuerza mayor o caso fortuito; esa circunstancia no es verificable en el ERP y no existe un mecanismo de contingencia. Fila del catálogo inactiva |
| Factura física para RIMPE Emprendedor (02) | La ficha RIMPE exige facturas electrónicas y no menciona preimpresos; no se verificó si la excepción de contingencia de la Res. NAC-DGERCGC22-00000024 lo alcanza. Sin fila en el catálogo |
| Cupo transitorio de preimpresos (Res. NAC-DGERCGC24-00000035, 5 % de los emitidos el año anterior, emergencia eléctrica) | No se verificó su vigencia actual; no se implementa ningún cupo |

### Limitaciones (impiden declarar la emisión física legalmente habilitada)

- La autorización se registra tal como la declara el usuario: el ERP no comprueba su autenticidad ni su suspensión (art. 7) ante el SRI.
- La circunstancia excepcional (fuerza mayor o caso fortuito) que justifica el preimpreso para un emisor electrónico no es verificable localmente.
- La nota de venta 02 no está implementada.
- Con la fila General → 01 inactiva, hoy ningún punto físico puede abrir caja ni emitir: la emisión física queda deshabilitada hasta que el propietario disponga de fundamento oficial y de un flujo de contingencia verificable.

## Interfaz (pestaña Preparación de Datos de la empresa)

`OperationalReadinessSection.tsx` solo presenta el contrato: no calcula capacidades, no evalúa reglas fiscales y no contiene formularios de configuración.

- **Sucursal.** La cabecera muestra la sucursal activa (`activeBranchStore`). Una respuesta cuyo `branchId` no es la sucursal activa nunca se muestra (se trata como carga en curso). Se vuelve a consultar al cambiar de sucursal, en cada montaje (al volver de la pantalla donde se corrigió un dato) y con «Volver a verificar» (`useAsync.refetch`); el estado vive solo en `useAsync`.
- **Capacidades.** Las cuatro capacidades son las del backend. Si alguna está disponible y `overallStatus ≠ Ready`, se aclara que «disponible» significa que existe un camino, no que la configuración esté completa.
- **Cajas.** Tabla de `cashRegisters[]`: caja, establecimiento-punto, modalidad, estado operativo, estado fiscal y requisitos pendientes (enlaces al ítem, `#readiness-item-{code}`). Contadores de cajas listas y pendientes (`operationalStatus`), y aviso cuando unas pueden operar y otras no.
- **Estados fiscales.** `Verified` éxito, `Pending` advertencia, `NotVerified` informativo (punto físico que cumple lo verificable; la autorización del SRI y su uso excepcional no se verifican en línea — nunca éxito ni bloqueo), `NotApplicable` neutro (sin punto).
- **Requisitos.** Cada ítem aparece una vez con: qué falta (`label`), por qué importa (mensaje por estado), operaciones afectadas (`blockingAreas`, con la etiqueta de la capacidad), cajas afectadas (inversa de `requirementCodes[]`) y dónde se corrige.
- **Destinos y permisos.** `operationalReadinessActionRoutes.ts` traduce `actionTarget` a ruta; la disponibilidad sale del menú autenticado (`GET /api/v1/me/menu`, ya filtrado por el backend) vía `useNavigationAccess()` (`components/navigationAccess.ts`), que `RouteAccessGuard` provee con el mismo menú ya cargado: sin otra consulta ni mapa de permisos. Coincide por ruta completa (sin query), no por primer segmento. Garantía real: un ítem del menú implica el permiso de **vista** de la pantalla (`NavItem.Permission`), no el de edición; por eso la acción se presenta como «Se corrige en: {pantalla}», con el nombre tomado del menú, y la pantalla destino sigue decidiendo qué puede editarse. Sin acceso, se pide asistencia a un administrador; con el menú aún sin resolver, no se ofrece acción.

## Pruebas

- `ERP.Infrastructure.Tests/Services/CompanyOperationalReadinessResolverTests.cs` (resolver).
- `ERP.Application.Tests/Companies/GetCompanyOperationalReadinessQueryHandlerTests.cs` (proyección al DTO, scope del contexto autenticado y exigencia de sucursal).
- `ERP.Application.Tests/Caja/OpenCashSessionHandlerTests.cs` (habilitación preventiva en la apertura: caja electrónica preparada con su propio punto y tipo 01, sin certificado, punto desactivado, fallo técnico, advertencia que no bloquea, física que cumple y física sin autorización, vencida, fuera de rango o régimen que no lo permite; rechazo sin crear la sesión) y `GetCashRegisterOpeningReadinessHandlerTests.cs` (dos cajas de una sucursal con resultados distintos, otra sucursal, códigos base, configuración que cambia, `NotVerified` físico, física vencida junto a otra lista, fallo técnico físico).
- `ERP.API.Tests/Integration/CompanyOperationalReadinessBranchHttpTests.cs` (HTTP real con PostgreSQL: cajas por sucursal, caja física del Régimen General con autorización vigente `Pending` por `docTypeNotPermitted` y otra además sin autorización, aislamiento entre empresas, rechazo sin sucursal, sucursal no autorizada o de otra empresa; solo puntos físicos, sin SRI).
- `frontend/src/modules/configuracion/empresa/sections/OperationalReadinessSection.test.tsx` (interfaz) y `frontend/src/components/navigationAccess.test.ts` (destinos según el menú).
- `ERP.Application.Tests/Companies/PhysicalEmissionRequirementsValidatorTests.cs` (política física: régimen que permite y que no, nota de venta no implementada, sin régimen, sin autorización, vencida, aún no vigente, último día, rango agotado, bloques sucesivos, otro punto, punto inactivo o ajeno, RUC provisional, scope inválido, mensaje del rechazo) y `PhysicalDocumentAuthorizationUseCasesTests.cs` (registro, punto no físico/inactivo/ajeno, tipo no habilitado, solapamiento, vigencia máxima, baja lógica).
- `ERP.Infrastructure.Tests/Accounting/SalesInvoiceAuthorizedPostingIntegrationTests.ReadinessGap.cs` (PostgreSQL real: Régimen General con autorización vigente rechazado; factura física sin autorización o vencida rechazada sin secuencial ni efectos; rango agotado entre la validación y la captura rechazado bajo el lock sin consumir número; estrategia física sin documento electrónico; catálogo GLOBAL régimen × tipo y aislamiento entre empresas) y `ERP.Infrastructure.Tests/Persistence/DocumentSequenceAmbientTransactionTests.cs` (concurrencia real por el último número autorizado, con y sin transacción ambiente; captura concurrente dentro del rango; fuera de rango sin crear fila ni alterar la transacción ambiente).
- `frontend/src/modules/documentSequences/components/PhysicalDocumentAuthorizationsSection.test.tsx` (administración de autorizaciones) y `frontend/src/modules/configuracion/empresa/operationalReadinessActionRoutes.test.ts`.
- `frontend/src/modules/caja/pages/CajaPage.openingReadiness.test.tsx` (apertura: caja preparada, sin certificado, sin punto, sin acceso a la configuración, dos cajas, cambio de sucursal, respuesta ajena descartada, física `NotVerified`, rechazo que vuelve a diagnosticar, fallo de consulta).
