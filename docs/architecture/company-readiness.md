# Preparación operativa de empresa (Company Readiness)

**Estado:** IN REVIEW (Fase 2B-1 backend y contrato; Fase 2B-2 interfaz guiada). No CLOSED hasta revisión y CI.

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
| Caja y punto | Punto de emisión asignado y activo, modalidad, resultado del validador electrónico para ese punto (dirección del establecimiento del punto, etc.) |

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
| `operationalStatus` | `Ready` si la caja puede vender con su punto (punto asignado y activo; si es electrónico, sin requisitos obligatorios del validador); `Missing` si no |
| `fiscalStatus` | `Verified` punto electrónico sin requisitos obligatorios · `Pending` punto electrónico con requisitos obligatorios o verificación fallida · `NotVerified` punto físico: no existe política fiscal propietaria que verificar ([brechas F-1..F-5](#brecha-de-política-fiscal-física-f-1f-5)); nunca se presenta como autorizado · `NotApplicable` caja sin punto activo |
| `requirementCodes` | `code` de los ítems de `sections` que la caja incumple (referencia, no copia). Los requisitos que adopta un ítem base se referencian con el código de ese ítem (p. ej. `identity.taxId`) |

Cada punto electrónico distinto se valida una sola vez aunque lo compartan varias cajas.

### Ejemplos

Sucursal Matriz con caja física `CAJA-01` (001-002) y caja electrónica `CAJA-02` (001-003) sin certificado:

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
| Abrir caja (`OpenCashSessionCommand`, branch-scoped) | Caja activa de la sucursal activa con punto de emisión asignado y existente. No contabiliza | Sales, CashRegister |
| Crear borrador de venta (`CreateSalesDraftCommand`, branch-scoped) | Caja abierta; punto de emisión **de la caja** activo en la empresa; bodegas de línea de la sucursal activa. Precio: lista del cliente → lista por defecto → `Item.BaseSalePrice`. Tipo de documento: el enviado (activo en `sri_doc_types`) o 01. Condición de pago: explícita → del cliente → Contado del sistema (o crédito con condición de empresa/vencimiento manual) | Sales |
| Autorizar venta (`AuthorizeSalesInvoiceCommand`) | Punto y establecimiento del borrador; con emisión **electrónica** (snapshot del borrador), `IElectronicEmissionRequirementsValidator` antes del secuencial; con emisión física, ningún requisito fiscal del emisor (`PhysicalSalesInvoiceEmissionStrategy`). Contabiliza en la misma transacción: sin modo contable se rechaza | Sales, ElectronicInvoicing |
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

El resto (`matrixAddress`, `emissionPoint`, `establishmentAddress`, `sriSettings`, `sriEnvironment`, `sriWsdlUrl`, `certificate`, `certificateUnusable`, `certificateExpired`, `certificateExpiring`, `docType`) solo los publica el validador.

## Brecha de política fiscal física (F-1..F-5)

La venta física es operativamente posible hoy (el backend la permite y Readiness no la bloquea), pero **no existe una política fiscal propietaria que la valide**. Por eso toda caja con punto físico se informa `fiscalStatus: NotVerified`: Readiness no le atribuye una autorización inexistente. Antes de certificar esta modalidad, el módulo propietario (Ventas / configuración documental) debe implementar su política; Readiness solo la proyectará cuando exista.

| ID | Brecha comprobada | Estado actual en el código |
|---|---|---|
| F-1 | Sin datos de autorización de impresión, vigencia ni rango de secuenciales autorizado | `EmissionPoint`, `DocumentSequence` y `Establishment` no los modelan; `CaptureNextAsync` solo limita a 999.999.999 |
| F-2 | Sin política de modalidad por tipo documental | Un punto `Physical` basta para emitir una factura física; `DocumentFlowPolicy` gobierna el flujo, no la modalidad, y Ventas no la consulta |
| F-3 | La venta física no exige el RUC oficial del emisor | `PhysicalSalesInvoiceEmissionStrategy.CheckRequirementsAsync` devuelve `NotApplicable`; puede emitirse con RUC provisional `TMP-EC-…` |
| F-4 | El régimen tributario no condiciona el tipo documental | El régimen (p. ej. RIMPE Negocio Popular) solo afecta el monto máximo de Consumidor Final |
| F-5 | El catálogo mezcla "apto electrónico" con "permitido" | `sri_doc_types` tiene un único flag `IsElectronic`; la factura física pasa porque 01 es electrónica. La nota de venta (02) está inactiva y no electrónica, por lo que se rechaza en cualquier modalidad: no está habilitada por defecto |

## Interfaz (pestaña Preparación de Datos de la empresa)

`OperationalReadinessSection.tsx` solo presenta el contrato: no calcula capacidades, no evalúa reglas fiscales y no contiene formularios de configuración.

- **Sucursal.** La cabecera muestra la sucursal activa (`activeBranchStore`). Una respuesta cuyo `branchId` no es la sucursal activa nunca se muestra (se trata como carga en curso). Se vuelve a consultar al cambiar de sucursal, en cada montaje (al volver de la pantalla donde se corrigió un dato) y con «Volver a verificar» (`useAsync.refetch`); el estado vive solo en `useAsync`.
- **Capacidades.** Las cuatro capacidades son las del backend. Si alguna está disponible y `overallStatus ≠ Ready`, se aclara que «disponible» significa que existe un camino, no que la configuración esté completa.
- **Cajas.** Tabla de `cashRegisters[]`: caja, establecimiento-punto, modalidad, estado operativo, estado fiscal y requisitos pendientes (enlaces al ítem, `#readiness-item-{code}`). Contadores de cajas listas y pendientes (`operationalStatus`), y aviso cuando unas pueden operar y otras no.
- **Estados fiscales.** `Verified` éxito, `Pending` advertencia, `NotVerified` informativo («la autorización fiscal física no se verifica todavía», nunca éxito ni bloqueo), `NotApplicable` neutro (sin punto).
- **Requisitos.** Cada ítem aparece una vez con: qué falta (`label`), por qué importa (mensaje por estado), operaciones afectadas (`blockingAreas`, con la etiqueta de la capacidad), cajas afectadas (inversa de `requirementCodes[]`) y dónde se corrige.
- **Destinos y permisos.** `operationalReadinessActionRoutes.ts` traduce `actionTarget` a ruta; la disponibilidad sale del menú autenticado (`GET /api/v1/me/menu`, ya filtrado por el backend) vía `useNavigationAccess()` (`components/navigationAccess.ts`), que `RouteAccessGuard` provee con el mismo menú ya cargado: sin otra consulta ni mapa de permisos. Coincide por ruta completa (sin query), no por primer segmento. Garantía real: un ítem del menú implica el permiso de **vista** de la pantalla (`NavItem.Permission`), no el de edición; por eso la acción se presenta como «Se corrige en: {pantalla}», con el nombre tomado del menú, y la pantalla destino sigue decidiendo qué puede editarse. Sin acceso, se pide asistencia a un administrador; con el menú aún sin resolver, no se ofrece acción.

## Pruebas

- `ERP.Infrastructure.Tests/Services/CompanyOperationalReadinessResolverTests.cs` (resolver).
- `ERP.Application.Tests/Companies/GetCompanyOperationalReadinessQueryHandlerTests.cs` (proyección al DTO, scope del contexto autenticado y exigencia de sucursal).
- `ERP.API.Tests/Integration/CompanyOperationalReadinessBranchHttpTests.cs` (HTTP real con PostgreSQL: cajas por sucursal, aislamiento entre empresas, rechazo sin sucursal, sucursal no autorizada o de otra empresa; solo puntos físicos, sin SRI).
- `frontend/src/modules/configuracion/empresa/sections/OperationalReadinessSection.test.tsx` (interfaz) y `frontend/src/components/navigationAccess.test.ts` (destinos según el menú).
