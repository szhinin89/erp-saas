# Motor de Configuración Jerárquico — Arquitectura Objetivo

> **Estado:** Diseño técnico definitivo (no implementado). No se modificó código fuente, no se creó migración, no se hizo commit ni push. Sí se creó/modificó documentación de arquitectura (este archivo).
> **Revisión:** CONFIGURATION-ENGINE-TARGET-ARCHITECTURE-REVIEW-01 — ver [Registro de cambios de la revisión](#registro-de-cambios-de-la-revisión-review-01) al final del documento.
> **Alcance:** Reemplaza conceptualmente los mecanismos actuales dispersos (`OrgSetting`, `GeneralParameter`, `Company.BrandingConfiguration` JSON, flags `IsDefault`/`IsMain` sin constraint, `FiscalPrecision` hardcodeado, snapshots parciales) por una arquitectura única, tipada, auditable y con precedencia explícita.
> **Regla de uso:** este documento es la fuente de verdad para dónde vive cualquier configuración futura. Si un parámetro nuevo no encaja limpio en un scope de la tabla de Fase 2, **no se crea** hasta resolver la ambigüedad aquí, no en el código.

---

## 0. Diagnóstico del estado actual (línea base real, no hipotética)

Investigación de código confirma que hoy coexisten **cinco mecanismos de configuración no reconciliados**, cada uno con reglas propias:

| Mecanismo | Qué es | Problema |
|---|---|---|
| `OrgSetting` (`org_settings`) | Tabla scope+key+value tipado, con `OrgSettingKeys` como registro centralizado de keys, `OrgScope` enum (`Company,Branch,Establishment,EmissionPoint,Warehouse`) | Es el mecanismo más cercano al ideal, pero: unicidad de `(tenant,company,scope,scopeId,key)` no verificada como constraint DB; repositorio usa `IgnoreQueryFilters()` (bypass manual de tenant); sin auditoría; sin `RequiresSnapshot`/`IsSensitive`; scopes `User`, `Profile`, `CashRegister`, `Document` no existen en el enum |
| `GeneralParameter` | Bag key/value genérico, usado hoy solo por `decimal.*` (`DecimalConfigRepository`) | Segunda fuente de verdad key/value paralela a `OrgSetting`, sin relación documentada entre ambas |
| `Company.BrandingConfiguration` (JSON string) | Blob JSON de branding | Tercer mecanismo de branding, **coexiste** con `ride.branding.*` en `OrgSetting` — dos fuentes para el mismo concepto. Corrección de esta revisión: el naming `ride.branding.*` es en sí mismo un defecto de diseño — la marca (`Company Branding`) es propiedad de la Company, y RIDE/PDF es solo uno de sus consumidores; ver Fase 11 |
| Flags `IsDefault`/`IsMain` (`PriceList.IsDefault`, `Warehouse.IsMain`, `EmissionPoint.IsDefault`, `Branch.IsMainBranch`, `Establishment.IsMain`) | Booleano en la propia entidad de catálogo | **Ninguno tiene constraint único filtrado en DB** — nada impide dos `true` en el mismo scope hoy |
| `FiscalPrecision` (constantes) vs `decimal.*` en `GeneralParameter` | Precisión fiscal hardcodeada vs precisión de presentación configurable | Dos sistemas de decimales sin frontera documentada — riesgo de que alguien use el configurable donde debía ir el fiscal fijo |

Este diagnóstico es el punto de partida: la arquitectura objetivo no es "agregar una tabla más", es **consolidar estos cinco mecanismos en una sola jerarquía con reglas explícitas de quién gana**.

---

## FASE 1 — Modelo conceptual final

Siete capas, cada una con una responsabilidad exclusiva. Ninguna capa puede asumir el trabajo de otra.

### 1.1 `ConfigurationDefinition`

**Problema que resuelve:** hoy cualquiera puede escribir una key string en `OrgSetting` o `GeneralParameter` sin que exista un catálogo de qué keys son válidas, qué tipo tienen, ni qué scopes permiten. Esto es la causa raíz de key drift (`ride.branding.*` vs `BrandingConfiguration` JSON son la prueba viva del problema).

**Qué debería existir:** catálogo estático de definición de settings — ver Fase 4 para la decisión tabla-vs-código.

**Qué contiene — separado en tres bloques de metadata, no uno solo:**
- **Core definition** (vive en `ERP.Domain`, sin dependencia de nada de UI): `Key, DataType, AllowedScopes, DefaultScope, FallbackStrategy, IsSensitive, RequiresAudit, RequiresSnapshot, Validator`. Este bloque es lo único que un resolver de Infrastructure necesita para resolver un valor — es el contrato de negocio puro.
- **Presentation metadata** (vive en `ERP.Application` o en el propio frontend, nunca en `ERP.Domain`): `Name, Description, HelpText, Ordering, I18nKey`. Es texto de UI para pantallas de administración de configuración — cambiar una etiqueta o el orden de un formulario no debe tocar `ERP.Domain`.
- **Access metadata** (vive en `ERP.Application`, ya que depende de `PermissionRequired` que es un concepto de autorización de aplicación, no de dominio): `PermissionRequired, IsUserEditable, IsSensitiveOperation` (si escribir esta key requiere flujo de confirmación reforzado, ej. reautenticación).

Regla dura: **`ERP.Domain` solo conoce la Core definition.** Ningún tipo de Domain referencia `I18nKey`, `HelpText` ni `PermissionRequired` — esos son conceptos de presentación/autorización que pertenecen a capas superiores. Si un resolver de Domain/Infrastructure necesitara "el nombre visible" de un setting, es una señal de que ese resolver está mezclando responsabilidades.

**Qué NO debe contener:** el valor configurado (eso es `ConfigurationValue`), ni lógica de resolución (eso es `ConfigurationResolver`), ni metadata de presentación/acceso mezclada en la misma clase que la Core definition.

**Reglas que debe cumplir:**
- Toda key que se pueda escribir en `ConfigurationValue` **debe** tener una fila/entrada previa en `ConfigurationDefinition`. Escribir una key no registrada es un error de validación, no un "guardado silencioso".
- `AllowedScopes` es una lista cerrada — un `ConfigurationValue` con un scope fuera de esa lista es inválido y se rechaza en el resolver de escritura.

**Ejemplo aplicado:** `invoice.default_warehouse_id` (hoy `OrgSettingKeys.Invoice.DefaultWarehouseId`) pasa a tener una entrada `ConfigurationDefinition` con `DataType=Guid`, `AllowedScopes=[Branch, CashRegister, User]`, `RequiresSnapshot=false` (el warehouse resuelto sí se snapshotea en el documento, pero eso es aparte — la config en sí no es snapshot), `RequiresAudit=true`.

### 1.2 `ConfigurationValue` / `OrgSettings`

**Problema que resuelve:** almacenar el valor real configurado por scope, sin que cada módulo invente su propia tabla key/value (elimina la duplicación `OrgSetting` vs `GeneralParameter`).

**Qué debería existir:** una única tabla (`org_settings` reforzada — ver Fase 5), no dos.

**Qué contiene:** `TenantId, CompanyId, ScopeType, ScopeId, Key, Value, ValueType, CreatedAt/By, UpdatedAt/By, RowVersion`.

**Qué NO debe contener:** reglas de fallback ni de precedencia (eso vive en `ConfigurationPrecedence`, consumido por el resolver) — la tabla es solo almacenamiento de hechos ("en este scope, para esta key, el valor configurado es X"), nunca decide qué scope gana.

**Reglas:** unique constraint real `(TenantId, CompanyId, ScopeType, ScopeId, Key)`; `Key` debe existir en `ConfigurationDefinition` (FK lógica, validada en el resolver de escritura, no en DB por portabilidad de tipos); `GeneralParameter` se elimina como mecanismo paralelo (ver Fase 11).

**Ejemplo:** fila `(tenant=T1, company=C1, scope=Branch, scopeId=BranchMatriz, key='invoice.default_warehouse_id', value='guid-bodega-central')`.

### 1.3 `ConfigurationResolver`

**Problema que resuelve:** hoy el único resolver real de este tipo es `OrgConfigResolver` (genérico, sin conocer precedencia de negocio) y `SalesFiscalPolicyResolver` (conoce una sola regla). No hay un resolver por dominio que encapsule *toda* la cadena de fallback de ese dominio.

**Qué debería existir:** una interfaz tipada por dominio de negocio (no por key suelta) — ver Fase 6 para el catálogo completo.

**Qué contiene:** lógica de "walk the precedence chain" para un conjunto cohesivo de settings de un dominio, devolviendo un objeto tipado (no un `string`/`Dictionary<string,object>`).

**Qué NO debe contener:** acceso directo a `DbContext` — solo a través de `IOrgSettingsRepository`/repositorios de catálogo tipados; tampoco debe exponer el valor crudo sin resolver — el resolver **es** el punto donde termina la ambigüedad de "¿de qué scope vino esto?".

**Ejemplo:** `IInvoiceDefaultsResolver.ResolveDefaultWarehouseAsync(branchId, cashRegisterId, userId)` reemplaza cualquier lectura directa de `invoice.default_warehouse_id` desde un handler de Sales.

### 1.4 `ConfigurationPrecedence`

**Problema que resuelve:** hoy la precedencia (si existe) está implícita en el orden en que un handler llama a cosas — no está declarada en ningún lugar como regla de negocio explícita, lo cual es exactamente lo que el Principio 5 prohíbe ("ningún fallback crítico puede depender de orden de arrays").

**Qué debería existir:** no necesariamente una tabla — puede ser metadata declarativa dentro de `ConfigurationDefinition` (`AllowedScopes` ya ordenado de mayor a menor precedencia) + lógica explícita, con nombre, en cada resolver. La tabla de Fase 3 es la fuente de verdad legible por humanos; el código la implementa 1:1, nunca la reinterpreta.

**Qué contiene:** por cada `ConfigurationDefinition` crítica, la secuencia ordenada de scopes a consultar y qué pasa si ninguno responde (`FallbackStrategy`: `UseSeedValue | RequireManualSelection | Error`).

**Regla:** un cambio de precedencia es un cambio de arquitectura (requiere actualizar este documento), nunca un cambio silencioso de orden en un array de código.

### 1.5 `ModuleRuntimeContext`

**Problema que resuelve:** hoy el frontend arma sus propios defaults combinando varias respuestas de API sueltas (fallback en frontend = Principio 4 violado). Un `RuntimeContext` es el objeto único, resuelto en backend, que un módulo operativo consume para saber "qué defaults y qué reglas aplican ahora, para este usuario, en esta sesión".

**Qué contiene:** ver Fase 7, resultado ya resuelto (no keys crudas) de todos los resolvers relevantes al módulo.

**Qué NO debe contener:** lógica de fallback (ya fue resuelta antes de llegar aquí) ni acceso a `OrgSetting` — es un DTO de salida, no una fuente de datos.

**Ejemplo:** `SalesRuntimeContext` que el frontend de POS pide una sola vez al abrir turno de caja.

### 1.6 `ConfigurationChangeLog`

**Problema que resuelve:** hoy **no existe ningún registro** de quién cambió un setting crítico ni cuándo (confirmado: no hay llamada a `UserActivity` desde los handlers de OrgSettings). Esto viola directamente el requisito del usuario ("cada dato mostrado debe poder explicar... quién lo configuró, cuándo").

**Qué contiene:** ver Fase 8.

**Qué NO debe contener:** el propósito no es un audit log genérico de toda la app (`UserActivity` ya cubre eso) — es específico a cambios de configuración con diff old/new tipado.

### 1.7 `TransactionSnapshot`

**Problema que resuelve:** hoy el snapshot es parcial e inconsistente — `SalesInvoiceDetail` snapshotea impuestos e ítem, `SalesInvoice` snapshotea cliente y condición de pago, pero **`WarehouseId`, `EmissionPointId`, método de pago (sin FK), certificado/ambiente SRI usado, lista de precios — no se snapshotean**. Un cambio futuro en `Warehouse.Name` o en qué certificado está activo alteraría la lectura de un documento histórico.

**Qué contiene:** ver Fase 9 — el dato **interpretativo** que participó en la decisión del documento (código, nombre, número, tasa, ambiente, fingerprint), congelado en el momento de emisión. No es una copia ciega de cada fila relacionada.

**Regla:** si el dato pudo cambiar en el futuro y el documento ya fue emitido/autorizado, se snapshotea el valor interpretativo necesario para releer el documento correctamente sin consultar configuración viva — no se duplica automáticamente cada FK del documento. El FK vivo puede conservarse en paralelo para trazabilidad/navegación (ej. "ver bodega actual"), pero nunca es la fuente de lectura fiscal/financiera/legal del documento — esa fuente es siempre el snapshot. Sin excepciones para datos fiscales.

---

## FASE 2 — Scopes oficiales

Jerarquía real confirmada en código (no la jerarquía idealizada): `Tenant → Company → Branch → Establishment (FK opcional a Branch) → EmissionPoint (FK obligatoria a Establishment)`, con `Warehouse` y `CashRegister` colgando directamente de `Branch` (no de `Establishment`/`EmissionPoint`). El modelo de scopes de configuración debe reflejar esta topología real, no una idealizada de árbol estricto.

| Scope | Definición | Cuándo usarlo | Cuándo NO usarlo | Ejemplo actual | Riesgo de mal uso |
|---|---|---|---|---|---|
| **System** | Constante de plataforma, igual para todos los tenants, no configurable por nadie | Invariantes técnicas (ej. `FiscalPrecision.TaxAmount=2`) | Nunca para algo que un tenant pudiera legítimamente querer distinto | `FiscalPrecision` | Volverlo configurable "por si acaso" rompe consistencia fiscal entre tenants |
| **Tenant** | Aplica a todas las companies de un tenant | Políticas de plataforma/licenciamiento, no de negocio | Reglas de negocio de una company específica | (no hay ejemplo de negocio hoy; es el nivel de aislamiento SaaS) | Meter reglas de negocio aquí las hace invisibles al admin de la company |
| **Company** | Aplica a una empresa dentro del tenant | Política que la empresa decide para sí misma y **no varía por sucursal** | Cualquier cosa que dependa de sucursal, caja, usuario o documento (Principio 14) | `sales.consumer_final.max_amount`, `SriSettings` (1 por company) | Volverse "basurero" (Principio 11) metiendo ahí reglas de Branch/Warehouse por comodidad |
| **Branch** | Sucursal física/operativa de la company | Default operativo que varía por sucursal (bodega default, política de caja) | Reglas fiscales de punto de emisión (eso es EmissionPoint) | `Branch.IsMainBranch` | Confundir Branch con Establishment (son conceptualmente distintos: Branch=operativo, Establishment=fiscal SRI) |
| **Establishment** | Establecimiento fiscal SRI (código de 3 dígitos) | Reglas que el SRI ata al establecimiento | Reglas puramente operativas sin relevancia fiscal | `Establishment.IsMain`, `EmissionPoint.EstablishmentId` (FK) | Tratar Establishment como sinónimo de Branch pierde el hecho de que hoy el FK Establishment→Branch es opcional |
| **EmissionPoint** | Punto de emisión SRI (secuencial de comprobantes) | Configuración atada a la secuencia documental / certificado usado | Defaults que no son de emisión fiscal | `EmissionPoint.IsDefault` | Resolver secuencias fuera de `CaptureNextAsync` (infraestructura CLOSED) — prohibido sin importar el scope |
| **Warehouse** | Bodega física | Reglas de inventario/valoración por bodega | Reglas de venta que no son de inventario | `Warehouse.IsMain` | Nada — es scope legítimo, pero recordar que hoy cuelga de Branch, no de Establishment |
| **CashRegister** | Caja/punto de venta físico | Default de venta atado a una caja concreta (ej. bodega de despacho de esa caja) | Reglas de todo el Branch | `CashRegister.DefaultWarehouseId`, `CashRegister.DefaultCustomerId`, `CashRegister.EmissionPointId` | Confundir con Branch: una caja puede querer un default distinto al resto de la sucursal |
| **Module** | Módulo funcional del ERP (Sales, Purchases, Inventory, Cash) | Configuración de comportamiento de un módulo entero, no de una entidad específica | Cuando el dato realmente pertenece a Branch/Company | (nuevo — no existe hoy formalmente) | Usarlo como cajón de sastre para todo lo que "no encaja en otro lado" — necesita `Owner` claro igual que cualquier otro scope |
| **Form** | Pantalla/formulario específico del frontend | Preferencias de UI (columnas visibles, orden) — nunca reglas de negocio | Cualquier regla que afecte un documento o un cálculo | (nuevo) | Meter una regla crítica aquí viola Principio 4 (nada crítico se resuelve en frontend) — Form es exclusivamente preferencia de presentación |
| **User** | Usuario individual autenticado | Preferencia personal opcional (si `IsUserEditable=true` en la Definition) | Cualquier default que deba ser igual para todo el equipo de una caja/sucursal | (nuevo) | Permitir que un User override silencie un control de Branch/Company sin dejar rastro de auditoría |
| **Profile/Role** | Perfil o rol de permisos | Default que aplica a todos los usuarios de ese rol | Cuando en realidad es un permiso (eso es `PermissionRequired` en la Definition, no un scope de valor) | (nuevo) | Confundir "quién puede cambiar la config" con "qué valor tiene la config" |
| **BusinessPartner** | Cliente o proveedor específico | Condición comercial pactada con ese BP (lista de precios, término de pago) | Reglas generales de ventas de la empresa | `CompanyBpTradingSettings` | Hoy `CompanyBpTradingSettings` no tiene `PriceListId` — el motor debe permitir la precedencia BusinessPartner → Company de todos modos (Fase 3); el campo en sí es mejora funcional de P1, no requisito estructural bloqueante (ver Fase 11) |
| **Item** | Ítem/producto específico | Override de precio/impuesto a nivel de ítem (ya cubierto por Items Module FROZEN) | No usar para nada fuera del dominio de pricing/fiscal del ítem | `Item.BaseSalePrice`, `PricingRule` | Ninguno adicional — este scope ya está bien delimitado en el módulo Items congelado |
| **Document** | Un documento transaccional puntual | Nunca para configuración — es el *consumidor final* de la resolución, materializado como snapshot | Jamás usarlo como fuente de configuración hacia adelante | `SalesInvoiceDetail.VatRate` (snapshot) | Tratar un valor de documento como si fuera reconfigurable retroactivamente rompe la integridad histórica |
| **RuntimeOnly** | Valor calculado en el momento, nunca persistido como config | Estado de sesión (turno de caja abierto/cerrado), no una preferencia | Nunca para algo que deba sobrevivir el request | `CashRegister` session status en `SalesRuntimeContext` | Persistirlo por error como si fuera configuración duplica la fuente de verdad (la sesión de caja ya vive en su propia entidad) |

### 2.1 Business Configuration vs User/Form Preferences — frontera obligatoria

Los scopes `Form` y `User` son estructuralmente distintos del resto: **no configuran el negocio, configuran la experiencia de una persona sobre el negocio.** Esta frontera debe quedar explícita para que nadie use `Form`/`User` como atajo para no modelar bien un setting de `Branch`/`CashRegister`/`Company`:

- `Form` **solo** puede usarse para preferencia de UI no crítica: columnas visibles, orden de columnas, densidad de tabla, colapso de paneles. Nunca para una regla que afecte un documento, un cálculo, una validación fiscal o cualquier decisión operativa — eso es, sin excepción, Principio 4 ("ningún fallback crítico puede depender de orden de arrays" aplica igual a "ninguna regla crítica puede vivir en preferencia de formulario").
- `User` participa en la cadena de precedencia de un setting de negocio (ej. bodega default de venta, Fase 3) **únicamente** cuando la `ConfigurationDefinition` de esa key lo declara explícitamente en `AllowedScopes` y el perfil del usuario tiene el permiso — nunca por defecto. Fuera de esos casos declarados, todo lo que un usuario individual pueda personalizar es preferencia de UI, no configuración de negocio.
- Si el volumen de preferencias `Form`/`User` crece (paneles, layouts, favoritos, atajos), **no es obligatorio** que sigan viviendo en `org_settings` — pueden tener un almacenamiento propio y más liviano (ej. `UserPreferences` key/value simple, sin `ConfigurationDefinition`/auditoría/precedencia, porque no son configuración de negocio y no necesitan ese aparato). La condición para migrarlas ahí es que nunca contengan un dato que un resolver de negocio (Fase 6) consulte — si lo hacen, dejaron de ser preferencia y son, por definición, configuración de negocio mal clasificada.

---

## FASE 3 — Reglas de precedencia

> Regla general de lectura de la tabla: se evalúa de izquierda a derecha; el primer scope con valor configurado gana. "Fallback final" es lo que ocurre si **ningún** scope de la cadena tiene valor.

| Setting | Scopes permitidos | Precedencia | Fallback final | Si no existe | Resolver dueño |
|---|---|---|---|---|---|
| Bodega default de venta | User\*, CashRegister, Branch, Warehouse.IsMain | User (solo si `Profile` lo permite) → CashRegister.DefaultWarehouseId → Branch (`invoice.default_warehouse_id`) → Warehouse.IsMain de esa Branch → null | Selección manual obligatoria, venta bloqueada hasta elegir | Bloquea emisión, no asume ninguna bodega | `IInvoiceDefaultsResolver` |
| Punto de emisión | CashRegister, EmissionPoint | CashRegister.EmissionPointId → EmissionPoint.IsDefault del Establishment de esa Branch | null, bloquear emisión | Bloquea emisión — nunca se infiere un punto de emisión | `ISriSigningContextResolver` |
| Lista de precios | BusinessPartner, Company | `CompanyBpTradingSettings.PriceListId` del cliente (**campo no existe hoy — mejora funcional P1, no bloqueante del motor base; ver Fase 11**) → PriceList.IsDefault de la Company | Error explícito, no hay lista de precios implícita | Bloquea cotización/venta, error de configuración visible al operador | `IPricingDefaultsResolver` |
| Condición de pago | BusinessPartner, Company (seed) | `CompanyBpTradingSettings.PaymentTermId` → default de ventas de Company → seed "CONTADO" | Error si ni siquiera existe el seed CONTADO (fallo de instalación) | Bloquea, no asume crédito ni contado por defecto | `ISalesFiscalPolicyResolver` (o nuevo `IPaymentDefaultsResolver`) |
| Método de pago | CashRegister/Profile, Module(Sales) | Caja/perfil si aplica → default de ventas/cobro de Company → método "efectivo" seed | Selección manual | No autoriza el documento sin método de pago explícito | `IPricingDefaultsResolver` / `ICashOperationPolicyResolver` |
| Política Consumidor Final | Company, System | Company (`sales.consumer_final.max_amount`) → default por régimen tributario del System catalog → default conservador del System | Valor conservador documentado en System (nunca 0 implícito silente) | Ya implementado hoy en `SalesFiscalPolicyResolver` — patrón correcto, mantener | `ISalesFiscalPolicyResolver` |
| Decimales (presentación) | Company, System | Company (`decimal.*` migrado desde `GeneralParameter` a `OrgSetting`) → System default | System default fijo | Nunca afecta cálculo fiscal, solo UI | `ICompanySettingsResolver` |
| Decimales (precisión fiscal real) | System únicamente | No hay cadena — es constante | `FiscalPrecision` hardcodeado, **no configurable por ningún scope** | N/A — es invariante de plataforma | Ninguno — acceso directo a constante, explícitamente fuera del motor de configuración |
| Reglas de caja (ej. permitir venta sin sesión abierta) | Branch/Company (si se decide configurable) | Branch → Company | Invariante fija documentada (no se permite venta sin sesión) | Bloquea, comportamiento seguro por defecto | `ICashOperationPolicyResolver` |

\* User solo participa en la cadena si `ConfigurationDefinition.AllowedScopes` para esa key incluye `User` **y** el perfil del usuario tiene el permiso — de lo contrario el scope User se salta automáticamente, nunca se evalúa "por accidente".

---

## FASE 4 — Diseño de ConfigurationDefinition

**Decisión: metadata en código (no tabla de base de datos), versionada junto con el código que la consume.**

Justificación: las 12 reglas del enunciado exigen que "ningún handler lea settings críticos por key string" y que "todo setting crítico pase por resolver tipado" — eso solo se puede *forzar en tiempo de compilación* si la definición vive en código (clases `sealed record` estáticas, análogas a `OrgSettingKeys` pero enriquecidas), no en una tabla que se puede alterar en runtime sin tocar el resolver que la interpreta. Una tabla DB para esto solo añade una capa de indirección sin beneficio: nadie va a crear settings críticos "desde la UI" sin que exista antes el resolver tipado que los consume, y el diseño explícitamente prohíbe eso (Principio 6/7).

```
ERP.Domain/Modules/Configuration/Definitions/
  ConfigurationDefinition.cs        // record — SOLO Core definition:
                                      //   Key, Module, DataType, AllowedScopes[], DefaultScope,
                                      //   DefaultValue, FallbackStrategy, IsSensitive,
                                      //   RequiresAudit, RequiresSnapshot, Validator,
                                      //   RuntimeContextOwner
  ConfigurationCatalog.cs           // static registry: IReadOnlyDictionary<string, ConfigurationDefinition>
  Domains/
    InvoiceDefaultsDefinitions.cs
    PricingDefinitions.cs
    CashPolicyDefinitions.cs
    ...

ERP.Application/Modules/Configuration/Metadata/
  ConfigurationPresentationMetadata.cs  // Name, Description, HelpText, Ordering, I18nKey
                                          // — keyed 1:1 por Key contra ConfigurationCatalog,
                                          //   pero es una tabla/registro separado, nunca el mismo tipo
  ConfigurationAccessMetadata.cs        // PermissionRequired, IsUserEditable, IsSensitiveOperation
```

Respuestas puntuales:
- **Dónde vive:** la Core definition en `ERP.Domain/Modules/Configuration/Definitions/`, particionada por dominio de negocio (no un solo archivo gigante) pero registrada en un único `ConfigurationCatalog` estático al arrancar. La Presentation metadata y la Access metadata viven en `ERP.Application` (o se resuelven en el propio frontend para lo puramente cosmético, ej. `I18nKey`) — nunca en `ERP.Domain`. Un endpoint de "listar configuración editable" hace join entre `ConfigurationCatalog` (Domain) y `ConfigurationPresentationMetadata`/`ConfigurationAccessMetadata` (Application) al momento de responder, no antes.
- **Cómo evitar keys no registradas:** `IOrgSettingsRepository.UpsertAsync` (o su reemplazo) valida `ConfigurationCatalog.TryGet(key)` antes de escribir; si no existe, excepción de dominio (`UnknownConfigurationKeyException`), nunca un insert silencioso.
- **Cómo validar tipo:** `ConfigurationDefinition.DataType` + `Validator` (delegate/Func tipado) se ejecutan en el mismo punto de escritura, antes de persistir el `string` serializado.
- **Cómo validar scope permitido:** el resolver de escritura compara el `ScopeType` solicitado contra `AllowedScopes`; fuera de esa lista = rechazo, no "se guarda pero nadie lo lee".
- **Cómo evitar configuración basura:** al ser código, un nuevo setting requiere un PR con su propia `ConfigurationDefinition` — no hay forma de crear una key "al vuelo" desde un endpoint genérico; se elimina el endpoint genérico `PUT /org-settings/{key}` sin definición asociada.
- **Exposición a UI:** solo lo que tenga `IsUserEditable=true` se expone en un endpoint de "configuración editable"; el resto es de solo lectura vía resolver/RuntimeContext.

---

## FASE 5 — Diseño de ConfigurationValue / OrgSettings

**Decisión: reforzar `org_settings`, no reemplazarla.** La tabla y el modelo de scope (`OrgScope`) son sólidos; el problema no es el diseño de la tabla, es (a) que compite con `GeneralParameter`/`BrandingConfiguration` JSON, y (b) que le faltan columnas de auditoría/concurrencia y scopes.

Columnas objetivo (delta contra lo que existe hoy):

| Columna | Estado actual | Acción |
|---|---|---|
| `TenantId, CompanyId, Scope, ScopeId, Key, Value, DataType` | Existen | Mantener |
| `CreatedAt/By, UpdatedAt/By` | Existen vía `AuditableEntity` | Mantener |
| `RowVersion` | No confirmado como concurrency token real | **Agregar** — control de concurrencia optimista obligatorio para settings críticos editados por más de un admin |
| Unique constraint `(TenantId, CompanyId, Scope, ScopeId, Key)` | Documentado en comentario, no confirmado como índice DB real | **Agregar como índice único real** — P0 |
| `OrgScope` enum ampliado | Solo `Company, Branch, Establishment, EmissionPoint, Warehouse` | **Agregar solo bajo demanda real** — ver regla de "scopes persistidos vs scopes oficiales" debajo de esta tabla. No se agregan `CashRegister, User, Profile, BusinessPartner, Module` de una sola vez por anticipación; cada uno se agrega en el mismo cambio que introduce la primera `ConfigurationDefinition` real que lo necesita |
| Validación contra `ConfigurationDefinition` | No existe | **Agregar** en el repositorio/servicio de escritura (Fase 4) |
| Manejo de valor corrupto | No confirmado | Ver regla de fail-closed vs fail-open inmediatamente debajo de esta tabla — **no es un fallback uniforme**, depende de si la `ConfigurationDefinition` de esa key es crítica |
| `IgnoreQueryFilters()` en el repositorio | Presente, bypass manual de tenant | **Mantener el bypass pero auditarlo**: es necesario porque `OrgSetting` puede resolverse para el scope Company completo sin filtro de branch — pero debe quedar documentado como decisión intencional, con test que confirme que el filtrado manual por `TenantId`/`CompanyId` nunca se omite |

**Regla de manejo de valor corrupto (fail-closed vs fail-open) — corrección obligatoria a la versión anterior de este documento:**

La versión previa proponía que un `Value` corrupto "cae al siguiente scope" de forma uniforme. Eso es incorrecto para settings críticos: silenciar un valor corrupto de bodega, punto de emisión, certificado SRI, política fiscal, caja, pricing o cualquier dato que participe en un documento **oculta un error de configuración detrás de un comportamiento aparentemente normal**, que es exactamente lo que el usuario pidió evitar ("el ERP debe ser confiable para tomar decisiones").

Regla definitiva, determinada por `ConfigurationDefinition.IsSensitive`, `RequiresAudit` o `RequiresSnapshot` (cualquiera de las tres en `true` marca la key como crítica):

- **Setting crítico, sensible, auditable o que afecta operación/documentos** (bodega, punto de emisión, certificado/ambiente SRI, política fiscal, caja, pricing, cualquier dato que un documento vaya a snapshotear): un `Value` que falla de-serialización o validación es **fail-closed** — el resolver lanza una excepción de configuración tipada (ej. `CorruptConfigurationValueException`), la operación se bloquea, y se muestra al operador un error claro de configuración ("el valor configurado para X en el scope Y es inválido — corríjalo antes de continuar"). **Nunca cae al siguiente scope de la cadena de fallback** — eso disfrazaría un dato corrupto como si fuera "no configurado", que es un estado distinto y más peligroso de asumir en silencio.
- **Setting visual/no crítico** (presentación, decimales de UI, branding cosmético, preferencias de usuario): puede caer a un default seguro documentado, con **warning/log obligatorio** del hallazgo (vía `ConfigurationChangeLog` o el logging estructural existente), pero sin bloquear al usuario.

Respuestas puntuales:
- **Mantener `org_settings`:** sí.
- **Reforzar:** sí — constraint único real, `RowVersion`, scopes ampliados, validación contra `ConfigurationDefinition`.
- **Reemplazar parcialmente:** no la tabla en sí; sí sus *competidores* (ver Fase 11).
- **Qué tabla eliminar:** `GeneralParameter` (migrar sus 5 keys `decimal.*` a `org_settings` con scope `Company`, key `presentation.decimal.*`, registradas en `ConfigurationDefinition`).
- **Migración necesaria (solo enumerada, no ejecutada por este documento):** (1) agregar índice único filtrado + `RowVersion` a `org_settings`; (2) ampliar `OrgScope` enum con el/los scope(s) que la primera `ConfigurationDefinition` real de P1 necesite (no todos de golpe); (3) script de datos: mover filas de `GeneralParameter` (`decimal.*`) a `org_settings`; (4) deprecar `GeneralParameter` tras la migración de datos.

**Regla de scopes persistidos vs scopes oficiales:** la Fase 2 define 16 scopes **conceptuales** — es el vocabulario completo que el motor reconoce y que gobierna dónde *puede* vivir una configuración futura. El enum `OrgScope`, en cambio, es una lista **persistida**, y solo crece cuando existe una `ConfigurationDefinition` real que declara ese scope en su `AllowedScopes`. No se amplía el enum "porque probablemente lo vamos a necesitar" — un valor de enum sin ninguna `ConfigurationDefinition` que lo use es, por definición, configuración basura potencial (Principio 11) esperando a que alguien lo llene sin pasar por el catálogo. `Item` y `Document` son scopes oficiales que **no** se modelan como filas de `OrgScope`/`org_settings`: `Item` ya tiene su propio mecanismo (`PricingRule`, Items Module FROZEN) y `Document` nunca es un scope de configuración viva (es el destino de un snapshot, Fase 9) — ambos quedan fuera del enum por diseño, no por omisión.

---

## FASE 6 — Resolvers tipados

Regla dura: **prohibido inyectar `IOrgSettingsRepository` directamente en un `CommandHandler`/`QueryHandler` de Application.** Solo los resolvers (Infrastructure, detrás de interfaz de Domain/Application) pueden hablar con `IOrgSettingsRepository`. Esto ya es, en parte, el patrón real de `SalesFiscalPolicyResolver` (que compone `IOrgConfigResolver` + `ICompanyRepository`) — se generaliza a todos los dominios.

| Resolver | Qué resuelve | Scopes que consulta | Fallback | Errores que devuelve | Módulo consumidor | Qué NO debe hacer |
|---|---|---|---|---|---|---|
| `ICompanySettingsResolver` | Config general de empresa (branding único, decimales de presentación, política consumidor final) | Company → System | Default de plataforma | `ConfigurationNotFoundException` solo si `FallbackStrategy=Error` | Company Settings UI, RIDE | Resolver nada de Branch/Warehouse — Principio 14 |
| `IBranchSettingsResolver` | Config operativa de sucursal | Branch → Company | Sube a Company solo si la Definition lo permite | igual | Sales, Inventory | Resolver reglas fiscales de Establishment (scope distinto) |
| `IInvoiceDefaultsResolver` | Bodega default, doc type default, payment term default para facturación | User → CashRegister → Branch → Warehouse.IsMain | `RequireManualSelection` | `NoDefaultResolvedException` (no excepción genérica — el frontend debe poder distinguir "no hay default, elige tú" de "error real") | Sales (creación de factura) | No decide método de pago (eso es otro resolver) ni datos fiscales SRI |
| `ISalesFiscalPolicyResolver` | Política consumidor final, umbral, régimen tributario aplicable | Company → System | Default conservador de System | — | Sales | No decide bodega/pago — solo política fiscal de venta |
| `IPricingDefaultsResolver` | Lista de precios default, método de pago default | BusinessPartner → Company → seed | Error si ni el seed existe | `PricingConfigurationMissingException` | Sales, Pricing | No calcula el precio final (eso es `PricingResolver`, ya existe) — solo resuelve *qué lista/política aplica* |
| `ICashOperationPolicyResolver` | Reglas de apertura/cierre de caja, si se permite venta sin sesión | Branch → Company → invariante fija | Invariante segura (bloquear) | `CashSessionRequiredException` | Cash/POS | No reemplaza la propia entidad `CashSession` — solo resuelve políticas, no estado |
| `IInventoryOperationPolicyResolver` | Reglas de bodega (permitir stock negativo, bodega de ajuste default) | Warehouse → Branch → Company | Invariante conservadora | `InventoryPolicyViolationException` | Inventory | No resuelve bodega default de venta (eso es `IInvoiceDefaultsResolver`) |
| `IPurchasesOperationPolicyResolver` | Bodega default de recepción, condición de pago default a proveedor | BusinessPartner(proveedor) → Company | Selección manual | igual patrón | Purchases | No reutiliza ciegamente la config de Sales — proveedor ≠ cliente aunque comparta tabla `CompanyBpTradingSettings` |
| `ISriSigningContextResolver` | Certificado activo, ambiente SRI, punto de emisión, WSDL, **y valida que el `PaymentMethod` usado en el documento tenga un `SriPaymentMethodCode` resoluble** (fail-closed si no lo tiene y el documento requiere emisión electrónica) | EmissionPoint → Establishment → Company (`SriSettings`, 1 por company) | Bloquea emisión, nunca asume certificado ni asume un código SRI por defecto | `SriCertificateNotConfiguredException`, `SriPaymentMethodNotResolvableException` | Electronic Documents | No debe cachear el certificado más allá de lo que la infraestructura CLOSED de ElectronicDocuments ya define |
| `IUserPreferenceResolver` | Preferencias de UI puramente cosméticas (si `IsUserEditable`) | User → Profile → Module default | Default del módulo | — | Frontend (Form scope) | Nunca resolver nada con `RequiresAudit=true` o `IsSensitive=true` — esas quedan fuera de alcance de este resolver por diseño |

---

## FASE 7 — RuntimeContext por módulo

Un `RuntimeContext` es la única superficie que el frontend/handler de negocio debería necesitar tocar — sustituye a que el frontend combine 4-5 respuestas de API y decida fallback por su cuenta (lo cual hoy probablemente ocurre de forma implícita, y es exactamente el Principio 4 que se busca cerrar).

### 7.1 `SalesRuntimeContext`
**Incluye:** `FiscalPolicy` (de `ISalesFiscalPolicyResolver`), `InvoiceDefaults` (doc type, payment term), `DefaultWarehouseId` (resuelto, ya sin ambigüedad de scope), `DefaultPriceListId`, `DefaultPaymentTermId`, `DefaultPaymentMethodId`, `CashSessionStatus` (abierta/cerrada, requerida sí/no), `AllowedPaymentMethods[]`, `CustomerDefaults` (si hay cliente seleccionado, sus condiciones de `CompanyBpTradingSettings`), `BranchContext` (branch/establishment/emission point activos).

**No incluye:** el catálogo completo de clientes/ítems (eso son queries normales, no config), ni el cálculo de precio final de una línea (eso es `PricingResolver` en tiempo de transacción, no runtime context de apertura).

**Qué endpoints reemplaza:** los actuales, si existen, de "traer defaults de factura" + "traer política fiscal" + "traer estado de caja" por separado — se consolidan en `GET /sales/runtime-context`.

**Fallback frontend que debe eliminarse:** cualquier `if (!warehouseId) warehouseId = warehouses[0].id` en el cliente — si el backend no resolvió bodega, el contexto debe traer `DefaultWarehouseId: null` + `RequiresManualWarehouseSelection: true`, y el frontend bloquea, no adivina.

**Bloqueos que debe comunicar:** `CashSessionRequired`, `NoEmissionPointConfigured`, `NoDefaultPriceList` — como campos explícitos boolean/enum, no como ausencia silenciosa de un campo.

### 7.2 `PurchasesRuntimeContext`
**Incluye:** bodega default de recepción, condición de pago default a proveedor, política de aprobación de OC si aplica.
**No incluye:** nada de ventas.
**Reemplaza:** combinaciones sueltas de defaults de compras hoy resueltas ad-hoc en frontend/handler.

### 7.3 `InventoryRuntimeContext`
**Incluye:** política de stock negativo, bodega default de ajuste, bodega principal de la sucursal activa.
**No incluye:** reglas de venta/compra — solo movimiento de inventario.

### 7.4 `CashRuntimeContext`
**Incluye:** estado de sesión de caja actual, caja default del usuario si aplica, métodos de pago permitidos en esa caja, política de apertura/cierre.
**No incluye:** defaults de venta que no sean estrictamente de caja (eso vive en `SalesRuntimeContext`, que puede componer `CashRuntimeContext` internamente).

### 7.5 `CompanySettingsContext`
**Incluye:** branding resuelto (fuente única `company.branding.*`, con Company como owner y RIDE como uno de sus consumidores — ver Fase 11), decimales de presentación, política consumidor final vigente.
**No incluye:** nada operativo de sucursal/caja.
**Reemplaza:** lectura directa de `Company.BrandingConfiguration` JSON desde cualquier consumidor que no sea el propio módulo de administración de branding.

---

## FASE 8 — Auditoría/histórico

### `ConfigurationChangeLog`

**Aplica a:** OrgSettings (todo lo que pase por `ConfigurationValue`) **y** a los flags de columna crítica que hoy no tienen historial: `PriceList.IsDefault`, `Warehouse.IsMain`, `EmissionPoint.IsDefault`, `Branch.IsMainBranch`, `Establishment.IsMain`, cambios de `SriSettings` (certificado/ambiente), cambios de `sales.consumer_final.max_amount`. La tabla es genérica (no una tabla por entidad) usando `EntityType + EntityId + FieldName` para cubrir tanto `ConfigurationValue` como columnas sueltas.

**Schema:**
```
ConfigurationChangeLog
  Id, TenantId, CompanyId
  EntityType (enum: OrgSetting | PriceListDefault | WarehouseMain | EmissionPointDefault
                     | BranchMain | EstablishmentMain | SriSettings | ... )
  EntityId (Guid, la fila/entidad afectada — para OrgSetting: la key+scope compuesto)
  FieldName (string, ej. "IsDefault", "CertP12Path", o el Key de OrgSetting)
  OldValue (string, serializado)
  NewValue (string, serializado)
  ChangedBy (UserId)
  ChangedAtUtc
  Reason (string, opcional pero obligatorio si RequiresAudit + IsSensitive)
  Source (enum: AdminUI | Api | Migration | System)
```

**Reglas:**
- `oldValue/newValue`: siempre capturados, incluso si `OldValue` es null (primera configuración).
- **UI ahora o solo persistencia:** solo persistencia + endpoint de solo lectura (`GET /config-audit?entityType=&entityId=`) en esta fase — una pantalla dedicada de "historial de configuración" es P2/P3, no P0.
- **Cambios obligatorios de auditar:** todo lo marcado `RequiresAudit=true` en `ConfigurationDefinition`, más los 5 flags `IsDefault/IsMain` (obligatorio por default, no opt-in, dado que hoy no tienen ni siquiera constraint de unicidad — el log es la única red de seguridad hasta que exista el constraint).

---

## FASE 9 — Snapshots

Regla madre: **si el dato pudo cambiar después de emitido el documento y el documento ya es fiscal/histórico, se snapshotea el dato interpretativo, no cada FK.** El patrón ya validado en el código (VOs `CustomerSnapshot`, `PaymentTermSnapshot`, campos `Snapshot*` en `SalesInvoiceDetail`, flag `IsFrozen`) se generaliza.

**Precisión obligatoria (corrección a la versión anterior de este documento):** no se trata de "snapshotear todo lo que hoy es FK". Se trata de identificar, por cada FK relevante a un documento, cuál es el **dato interpretativo mínimo** que garantiza una lectura histórica correcta — típicamente código, nombre, número/secuencial, tasa, ambiente, fingerprint — y congelar solo eso. La FK viva se mantiene si aporta trazabilidad (navegar al registro actual), pero deja de ser la fuente de verdad de lectura del documento en el momento en que existe su snapshot. Esta distinción evita que "agregar snapshot" se lea como "duplicar el modelo entero de cada entidad relacionada dentro de cada documento".

| Módulo | Ya snapshoteado hoy | Falta snapshotear | No debe snapshotearse |
|---|---|---|---|
| **Ventas** | Cliente (`CustomerSnapshot`), condición de pago (`PaymentTermSnapshot`), impuestos por línea (`VatRate/IceRate` + nombres), SKU/nombre de ítem, totales autorizados | **Código/nombre de bodega usada** (hoy solo `WarehouseId` como live FK, sin snapshot interpretativo), **código de punto de emisión usado** (hoy solo `EmissionPointId` como live FK), **nombre/código de método de pago interno usado** (hoy solo `SriPaymentMethodCode` string suelto — falta el nombre del `PaymentMethod` interno, no todo el registro), **código/nombre de lista de precios usada**. Los FKs vivos (`WarehouseId`, `EmissionPointId`) pueden conservarse para trazabilidad, pero no bastan como fuente de lectura histórica | El estado actual del cliente/ítem hacia adelante — el snapshot es de lectura histórica, nunca fuente para nueva lógica |
| **Compras** | Parcial (a confirmar contra `PurchaseInvoice`/`PurchasePayable`, que ya usan `FiscalPrecision` fijo) | Condición de pago pactada con proveedor al momento de la OC/factura, bodega de recepción usada | — |
| **Inventario** | — | Costo unitario usado en el movimiento (si no está ya, dado que `FiscalPrecision.UnitCost` es fijo, el *valor* debe congelarse en el movimiento, no recalcularse desde el ítem actual) | Configuración de política de stock negativo — eso es una regla operativa, no un dato del movimiento |
| **Caja** | — | Método(s) de pago permitidos vigentes al momento del cierre de turno (para que un cierre histórico no cambie de interpretación si luego se reconfiguran los métodos permitidos) | Estado de sesión en curso — eso vive en `CashSession`, no en un snapshot de config |
| **Documentos electrónicos** | Ambiente y certificado usados **no confirmados como snapshot explícito** — `SriSettings` es mutable y 1-por-company; si se sube un certificado nuevo, no hay evidencia de que el XML/documento ya autorizado quede vinculado al fingerprint del certificado que realmente lo firmó | **Obligatorio agregar:** `SriEnvironmentUsed`, `CertificateFingerprintUsed`, `EmissionPointUsed`, `WarehouseUsed`, `PaymentMethodUsed`, `PriceListUsed`, `PaymentTermUsed`, `TaxRatesUsed` (ya existe), `DecimalPrecisionUsed` (aunque sea la constante fija, versionarla igual por si `FiscalPrecision` cambiara alguna vez en una versión futura de plataforma) | — |

Cómo evitar que cambios futuros alteren lectura histórica: el snapshot se materializa en el propio documento (columnas o VO embebido, como ya hace `CustomerSnapshot`), **nunca** como un FK a una fila de `ConfigurationChangeLog` que el lector tendría que "resolver" — el snapshot debe ser autocontenido y legible sin joins a configuración viva.

---

## FASE 10 — Constraints de integridad

| Constraint | Clasificación |
|---|---|
| Unique `org_settings` por `(TenantId, CompanyId, Scope, ScopeId, Key)` | **Obligatorio ahora (P0)** — hoy es solo un comentario de intención, no un índice real |
| Unique filtrado: un solo `PriceList.IsDefault=true` por `(TenantId, CompanyId)` | **Obligatorio ahora (P0)** — hoy no existe ninguna protección |
| Unique filtrado: un solo `Branch.IsMainBranch=true` por `CompanyId` | **Obligatorio ahora (P0)** |
| Unique filtrado: un solo `Establishment.IsMain=true` por `CompanyId` | **Obligatorio ahora (P0)** |
| Unique filtrado: un solo `EmissionPoint.IsDefault=true` por `EstablishmentId` | **Obligatorio ahora (P0)** |
| Unique filtrado: un solo `Warehouse.IsMain=true` por `BranchId` | **Obligatorio ahora (P0)** |
| Unique `CompanyBpTradingSettings` por `(TenantId, CompanyId, BusinessPartnerId)` | Antes de producción (P1/P2) — confirmar si ya existe el índice; el doc-comment lo sugiere pero no fue verificado como constraint real |
| `PaymentMethod.SriPaymentMethodCode` (FK opcional a `SriPaymentMethod`, obligatoria solo si el método participa en emisión electrónica) | Antes de producción (P2) — hoy la relación es un string suelto (`SriPaymentMethodCode` en `SalesInvoice`), no una FK real; ver matiz en Fase 11 (no todo `PaymentMethod` interno mapea 1:1 a SRI) |
| CHECK de `CompanyBpTradingSettings` (ej. `CreditLimit >= 0`, `PaymentDays >= 0`, `Installments >= 1`) | Antes de producción (P1/P2) |
| CHECK de rangos decimales (`decimal.* ∈ [0,6]`, ya aplicado hoy en `DecimalConfigRepository` a nivel de código, migrar a CHECK de DB) | Futuro (P3) — hoy funciona vía clamp en código, no es urgente moverlo a DB |
| CHECK de colores si se tipa branding (hex válido) | Futuro (P3) — depende de si `company.branding.*` se tipa formalmente (ver Fase 11) |

---

## FASE 11 — Decisión sobre mecanismos actuales

| Mecanismo | Decisión | Detalle |
|---|---|---|
| `general_parameter` | **Eliminar** (tras migrar datos) | Sus 5 keys `decimal.*` se migran a `org_settings` con scope `Company`, registradas en `ConfigurationDefinition`. La tabla `general_parameter` se elimina — es la definición misma de "doble fuente de verdad" (Principio 3) |
| `Company.BrandingConfiguration` JSON | **Eliminar**, migrar a keys tipadas de **Company Branding** | Hoy son dos branding stores para el mismo concepto, y ninguno de los dos nombra correctamente al dueño. Corrección de esta revisión: el owner de la marca es **Company Branding** (`company.branding.*`), no RIDE — RIDE/PDF es un *consumidor* de esas keys, igual que cualquier otro output futuro (portal de cliente, email, etc.) podría serlo. El JSON crudo se elimina porque viola el Principio 12 ("JSON crudo no debe usarse para reglas críticas") |
| `ride.branding.*` OrgSettings | **Renombrar y reforzar** | Renombrar a `company.branding.*` (`OrgSettingKeys.Ride.*` → `OrgSettingKeys.Branding.*`) para que el naming refleje al dueño real (Company), no a un consumidor específico (RIDE). Tras el rename, extender `AllowedScopes` para permitir override a nivel `Branch` (hoy solo resuelve a `Company`, según el propio doc-comment de `OrgSettingsRideBrandingProvider` que lo marca como diseño futuro). `OrgSettingsRideBrandingProvider` pasa a ser un *lector* de `company.branding.*` (uno más entre los consumidores posibles), no el dueño del namespace |
| `PriceList.IsDefault` | **Reforzar** | Agregar constraint único filtrado (Fase 10); mantener como columna de la entidad (es un catálogo, Principio 13 — no se convierte en `ConfigurationValue`, el *flag* vive en la entidad, pero su *cambio* se audita vía `ConfigurationChangeLog`) |
| `Warehouse.IsMain` | **Reforzar** | Igual patrón que `PriceList.IsDefault` |
| `EmissionPoint.IsDefault` | **Reforzar** | Igual patrón |
| `Branch.IsMainBranch` | **Reforzar** | Igual patrón |
| `Establishment.IsMain` | **Reforzar** | Igual patrón |
| `CreditTerm` | **Mantener, aclarar frontera con `PaymentTerm`** | Son dos catálogos con overlap conceptual (ambos modelan planes de cuotas) sin relación documentada — no se fusionan en este documento porque es una decisión de dominio de Finance/Sales que excede el motor de configuración, pero se deja marcado como deuda a resolver antes de que crezca más superficie sobre ambos |
| `PaymentTerm` | **Mantener** | Catálogo legítimo, ya consumido por `CompanyBpTradingSettings.PaymentTermId` |
| `PaymentMethod` | **Reforzar, con matiz** | Agregar `SriPaymentMethodCode` como FK **nullable** a `SriPaymentMethod` (hoy solo existe el código string suelto en el documento) — antes de producción. Corrección de esta revisión: no se asume que todo `PaymentMethod` interno corresponde 1:1 a un código SRI — un método de pago puramente interno/informativo (ej. "nota de crédito interna", si existiera) puede no tener equivalente fiscal. La FK es **obligatoria solo para métodos marcados como usables en documentos electrónicos/fiscales**; la validación de que exista un código SRI resoluble se hace en el momento de emisión electrónica (`ISriSigningContextResolver`, Fase 6), no como constraint ciego sobre toda la tabla |
| `SriPaymentMethod` | **Mantener** | Catálogo SRI, correcto como está |
| `CompanyBpTradingSettings` | **Reforzar (mejora funcional P1, no requisito estructural del motor base)** | Agregar `PriceListId` (hoy falta — identificado en Fase 3, "Lista de precios" no tiene dónde configurarse por cliente). El motor de configuración en sí no depende de que este campo exista para funcionar — la precedencia BusinessPartner → Company queda declarada en Fase 3 y el resolver simplemente no encuentra valor en el scope BusinessPartner hasta que el campo se agregue, cayendo correctamente al siguiente nivel. Se prioriza cuando exista un flujo de negocio que ya lo necesite, no como parte de P0 |
| `SriSettings` | **Reforzar** | Mantener 1-por-company; agregar snapshot de fingerprint de certificado en cada documento firmado (Fase 9) |
| `FiscalPrecision` constants | **Mantener como System scope inmutable** | Es infraestructura CLOSED (Numeric Precision Standard, frozen 2026-06-25) — no entra al motor de configuración como "editable", entra solo como referencia documental de que su scope es `System` |
| Decimal UI settings (`decimal.*`) | **Migrar** | De `GeneralParameter` a `org_settings`, scope `Company`, ver fila `general_parameter` arriba |

---

## FASE 12 — Diseño final y plan

### 12.1 Diagrama textual de arquitectura objetivo

```
┌─────────────────────────────────────────────────────────────────────┐
│  ConfigurationDefinition (código, ERP.Domain)                        │
│  catálogo estático de qué keys existen, tipo, scopes permitidos      │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ valida contra
┌───────────────────────────────▼───────────────────────────────────────┐
│  ConfigurationValue = org_settings (reforzada)                        │
│  (Tenant, Company, ScopeType, ScopeId, Key) → Value                   │
│  + flags IsDefault/IsMain reforzados en sus propias entidades         │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ leído SOLO por
┌───────────────────────────────▼───────────────────────────────────────┐
│  ConfigurationResolver (Infrastructure, por dominio)                  │
│  aplica ConfigurationPrecedence (Fase 3) → devuelve valor tipado       │
│  ICompanySettingsResolver, IInvoiceDefaultsResolver,                  │
│  IPricingDefaultsResolver, ISriSigningContextResolver, ...             │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ compuesto por
┌───────────────────────────────▼───────────────────────────────────────┐
│  ModuleRuntimeContext (Application, DTO de salida)                    │
│  SalesRuntimeContext, PurchasesRuntimeContext, CashRuntimeContext...   │
│  → único objeto que Frontend/Handler de negocio consume                │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ al emitir/confirmar documento
┌───────────────────────────────▼───────────────────────────────────────┐
│  TransactionSnapshot (embebido en el documento)                       │
│  congela lo que realmente se usó: bodega, punto emisión, certificado, │
│  método pago, lista precios, condición pago, impuestos, decimales     │
└─────────────────────────────────────────────────────────────────────┘

     (en paralelo, todo write a ConfigurationValue o a los flags
      IsDefault/IsMain críticos)
                                 │
┌───────────────────────────────▼───────────────────────────────────────┐
│  ConfigurationChangeLog                                               │
│  quién, cuándo, qué cambió, valor anterior/nuevo, por qué              │
└─────────────────────────────────────────────────────────────────────┘
```

### 12.2 – 12.10
Ver tablas completas en Fases 2 (scopes), 3 (precedencia), 4 (ConfigurationDefinition), 5 (OrgSettings reforzado), 6 (resolvers), 7 (RuntimeContexts), 8 (ChangeLog), 9 (snapshots), 10 (constraints).

### 12.11 Qué se elimina
- `general_parameter` (tras migración de datos).
- `Company.BrandingConfiguration` JSON.
- Cualquier endpoint genérico de escritura de OrgSetting sin key registrada en `ConfigurationDefinition`.
- Fallbacks de default resueltos en frontend (bodega `warehouses[0]`, método de pago hardcodeado, etc.) — se reemplazan por `RuntimeContext` con flags explícitos de "requiere selección manual".

### 12.12 Qué se mantiene
- `org_settings` como tabla central (reforzada).
- `OrgScope` enum (ampliado).
- Patrón resolver ya validado (`OrgConfigResolver`, `SalesFiscalPolicyResolver` como precedente arquitectónico correcto).
- `FiscalPrecision` como constante System, intocable.
- `PriceList.IsDefault`, `Warehouse.IsMain`, `EmissionPoint.IsDefault`, `Branch.IsMainBranch`, `Establishment.IsMain` como columnas en su entidad (no se mueven a `org_settings` — son catálogo, Principio 13), reforzadas con constraint + auditoría.
- Patrón de snapshot VO ya usado en `SalesInvoice` (`CustomerSnapshot`, `PaymentTermSnapshot`) — se extiende, no se reinventa.

### 12.13 Qué se migra
- `GeneralParameter.decimal.*` → `org_settings`, scope `Company`.
- `Company.BrandingConfiguration` JSON → `company.branding.*` en `org_settings` (renombrado desde `ride.branding.*`, con scope extendido a `Branch`; RIDE queda como consumidor, no como owner del namespace).
- `SriPaymentMethodCode` string suelto en `SalesInvoice` → se mantiene el snapshot string (correcto, es histórico) pero se agrega FK real `PaymentMethod → SriPaymentMethod` para la fuente configurable viva.

### 12.14 Qué queda prohibido para futuros módulos
1. Ninguna configuración sin entrada previa en `ConfigurationDefinition`.
2. Ninguna tabla key/value paralela a `org_settings` (prohibido crear un "SettingsV2" o similar).
3. Ningún handler de Application inyecta `IOrgSettingsRepository` directamente — solo resolvers.
4. Ningún fallback crítico decidido en frontend.
5. Ninguna regla de Branch/Warehouse/EmissionPoint/CashRegister/User/Profile/BusinessPartner metida en scope Company "porque es más rápido".
6. Ningún dato fiscal/histórico sin snapshot si el documento ya fue emitido/autorizado.
7. Ningún flag `IsDefault`/`IsMain` nuevo sin su constraint único filtrado desde el día uno de esa entidad.
8. Ningún JSON crudo para reglas de negocio críticas.

### 12.15 Plan de implementación por fases

**P0 — Integridad/confianza** (bloqueante antes de cualquier otra evolución del motor):
- Constraints únicos críticos: `org_settings (Tenant, Company, Scope, ScopeId, Key)` como índice único real (si no existe hoy) + los 5 flags `IsDefault`/`IsMain` (`PriceList`, `Branch`, `Establishment`, `EmissionPoint`, `Warehouse`) con unique filtrado.
- Eliminar fallback frontend inseguro (ej. `warehouses[0].id`) — el frontend deja de adivinar defaults; bloquea y pide selección manual cuando el backend no resuelve.
- Resolver bodega default de venta **server-side** (`IInvoiceDefaultsResolver`, cadena de Fase 3) — es el caso más visible de regla crítica hoy potencialmente resuelta en cliente.
- `RowVersion` en `org_settings` (concurrencia optimista para settings críticos).

**P1 — Unificación:**
- `ConfigurationDefinition` **core** (Domain) + validación de escritura contra el catálogo — sin Presentation/Access metadata todavía, esas son P1 tardío o P2 según necesidad de UI.
- Migración de `GeneralParameter` → `org_settings`.
- Consolidación de branding: `Company.BrandingConfiguration` JSON + `ride.branding.*` → `company.branding.*` único, con Company como owner y RIDE como consumidor.
- Resolvers principales de Fase 6 (`IInvoiceDefaultsResolver`, `IPricingDefaultsResolver`, `ISalesFiscalPolicyResolver` ya existente se adapta al patrón).
- `RuntimeContext` de Sales (`SalesRuntimeContext`), el de mayor superficie operativa diaria.
- Mejora funcional opcional dentro de P1 si el flujo de negocio ya lo necesita: `CompanyBpTradingSettings.PriceListId` (no bloqueante — ver Fase 11).

**P2 — Auditoría/histórico:**
- `ConfigurationChangeLog` + instrumentación de escritura en todos los puntos que tocan `org_settings` y los 5 flags críticos.
- Snapshots SRI/fingerprint y demás snapshots faltantes de Fase 9 (dato interpretativo de bodega/punto de emisión/método de pago/lista de precios usados, ambiente y fingerprint de certificado).
- Mapeo `PaymentMethod`/SRI: `SriPaymentMethodCode` como FK nullable, obligatoria solo para métodos usables en documentos electrónicos, validada en emisión.
- Endpoint de solo lectura de historial de configuración (UI dedicada queda para P3).

**P3 — Mejoras futuras:**
- Preferencias de usuario/formulario: almacenamiento propio separado de `org_settings` si el volumen lo justifica (Fase 2.1).
- Scopes avanzados: ampliación de `OrgScope` bajo demanda real a medida que surjan `ConfigurationDefinition` concretas para `CashRegister`, `User`, `Profile`, `BusinessPartner`, `Module`.
- Mejoras de auditoría visual: UI dedicada de historial de configuración sobre `ConfigurationChangeLog`.
- CHECK constraints de rango decimal/color migrados de código a DB.
- Resolución de la frontera conceptual `CreditTerm` vs `PaymentTerm` (deuda de dominio, no de este motor).
- Extensión de `company.branding.*` a scope `Branch` en la práctica (la Definition ya lo permite desde P1; la UI de administración que lo explote es P3).

---

## Confirmación

- No se modificó código fuente.
- No se creó migración.
- Sí se creó/modificó documentación de arquitectura (este archivo, en su versión inicial y en esta revisión REVIEW-01).
- No se hizo commit.
- No se hizo push.

---

## Registro de cambios de la revisión (REVIEW-01)

Cambios aplicados a este documento por CONFIGURATION-ENGINE-TARGET-ARCHITECTURE-REVIEW-01, antes de aprobar la arquitectura como regla maestra:

1. **Confirmación final corregida** — distingue explícitamente "no se modificó código fuente / no se creó migración" de "sí se creó/modificó documentación de arquitectura", que antes quedaba implícito de forma ambigua ("no se modificó código" podía leerse como que tampoco se tocó este documento).
2. **`ConfigurationDefinition` dividido en tres bloques de metadata** (§1.1, Fase 4): Core definition (vive en `ERP.Domain`, sin dependencias de UI), Presentation metadata (`Name/Description/HelpText/Ordering/I18nKey`, vive en `ERP.Application`/frontend) y Access metadata (`PermissionRequired/IsUserEditable/IsSensitiveOperation`, vive en `ERP.Application`). Regla dura agregada: `ERP.Domain` solo conoce la Core definition.
3. **Regla de manejo de valor corrupto reescrita** (Fase 5): reemplaza el "cae al siguiente scope" uniforme por una distinción fail-closed (settings críticos/sensibles/auditables/con snapshot — bloquean con error explícito, nunca degradan a otro scope) vs fail-open (settings visuales/no críticos — caen a default seguro con warning/log).
4. **Regla de scopes persistidos vs scopes oficiales agregada** (Fase 5): el enum `OrgScope` solo se amplía cuando existe una `ConfigurationDefinition` real que declara ese scope — no se agregan scopes vacíos por anticipación. Aclarado que `Item` y `Document` son scopes oficiales que nunca se modelan como filas de `OrgScope`.
5. **Nueva sección 2.1 "Business Configuration vs User/Form Preferences"**: `Form` limitado estrictamente a preferencia de UI no crítica; `User` solo participa en precedencia de negocio si la `ConfigurationDefinition` lo declara explícitamente; preferencias `Form`/`User` pueden migrar a almacenamiento propio separado de `org_settings` si crecen en volumen.
6. **Corrección de ownership de branding** (§0, Fase 7.5, Fase 11, §12.13): la marca pertenece a Company Branding; RIDE es consumidor, no owner. Naming corregido de `ride.branding.*` a `company.branding.*` en todas las referencias del documento.
7. **Matiz en `PaymentMethod` ↔ `SriPaymentMethod`** (Fase 6, Fase 10, Fase 11): la FK es nullable y obligatoria solo para métodos usables en documentos electrónicos/fiscales, no 1:1 para todo método interno. La validación de resolubilidad del código SRI se hace en emisión electrónica (`ISriSigningContextResolver`), no como constraint ciego de toda la tabla.
8. **`CompanyBpTradingSettings.PriceListId` reclasificado** (Fase 2, Fase 3, Fase 11): de "gap crítico" a mejora funcional de P1 recomendada, no requisito estructural bloqueante del motor base — la precedencia BusinessPartner → Company queda declarada igual, el resolver simplemente no encuentra valor en ese scope hasta que el campo exista.
9. **Regla de snapshots precisada** (§1.7, Fase 9): no se duplica ciegamente cada FK — se snapshotea el dato interpretativo mínimo (código, nombre, número, tasa, ambiente, fingerprint) necesario para lectura histórica; la FK viva puede conservarse para trazabilidad pero deja de ser fuente de lectura fiscal/financiera/legal una vez existe el snapshot.
10. **Plan P0/P1/P2/P3 reescrito** (§12.15) siguiendo exactamente el orden de prioridad de esta revisión: P0 = constraints únicos críticos + eliminar fallback frontend inseguro + resolver bodega default server-side + `RowVersion`; P1 = `ConfigurationDefinition` core + migración `GeneralParameter` + consolidación de branding + resolvers principales + `SalesRuntimeContext`; P2 = `ConfigurationChangeLog` + snapshots SRI/fingerprint + mapeo `PaymentMethod`/SRI + historial (solo lectura); P3 = preferencias usuario/formulario + scopes avanzados bajo demanda + mejoras de auditoría visual. Fase 10 (constraints de integridad) realineada a esta misma clasificación.

Ningún principio ni estándar enterprise del documento original fue relajado — todos los ajustes son de precisión (evitar ambigüedad, separar responsabilidades, corregir un naming incorrecto, matizar una regla que era más rígida de lo necesario sin perder rigor donde importa: fiscal, SRI, documentos, auditoría).
