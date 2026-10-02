# ADR-037 — Gobernanza de catálogos globales SRI

**Status:** Proposed · **Fecha:** 2026-10-02 · **Ticket:** ZH-SRI-GLOBAL-CATALOG-ARCHITECTURE-01
**Evidencia y diseño detallado:** [`docs/sri/SRI-GLOBAL-CATALOG-ARCHITECTURE.md`](../sri/SRI-GLOBAL-CATALOG-ARCHITECTURE.md)
**Base:** [`docs/sri/SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md`](../sri/SRI-2.34-CURRENT-COMPLIANCE-AUDIT.md) (hallazgo P0 R24)
**Complementa:** `docs/architecture/architecture.md § Catálogos y datos configurables (SSOT dinámico)`; ADR-034 (fecha de negocio); ADR-023/ADR-036 (no los modifica).

## Contexto

Los 15 catálogos `global.sri_*` se cargan solo por `HasData` → migraciones EF y no tienen `TenantId`/`CompanyId`.
Tienen tres defectos estructurales:

1. **Identidad y representación mezcladas.** `sri_retention_code.code` (721–728 para IVA, sin fuente documentada)
   se emite tal cual como `codigoRetencion`, mientras la Ficha Técnica 2.34 (Tabla 20) exige 9/10/1/11/2/3/7/8.
2. **Sin historia.** Una fila por código no puede representar cambios normativos. `sri_vat_rate` tiene
   `ValidFrom/ValidUntil`, pero ningún resolver los usa y su historia es incorrecta (el 12 % aparece cerrado en
   2016 aunque rigió hasta 2024-03-31).
3. **Sin salvaguardas.** Editar un valor en `HasData` genera un `UpdateData` destructivo. Tampoco hay trazabilidad
   normativa por fila ni pruebas de coherencia más allá de dos tests puntuales de alineación de seed.

## Decisión

- **D1 — Globales.** Todo código oficial SRI que no depende de una empresa es un maestro global: esquema `global`,
  sin `TenantId` ni `CompanyId`. Toda empresa consume la misma fuente. La configuración por empresa (p. ej.
  `SupplierRetentionDefault`, `SriSettings`) referencia al catálogo; no lo copia.
- **D2 — Tablas tipadas.** Cada catálogo conserva su propia tabla y entidad. No se crea una mega tabla genérica
  de catálogos ni una clase base artificial; lo común se impone por convención y por la suite de compliance.
- **D3 — Una sola fuente versionada.** El único canal de datos oficiales es `HasData` materializado por
  migraciones EF. No se permiten seeding steps que escriban `sri_*`, SQL manual, JSON leído en startup ni
  diccionarios en código.
- **D4 — Vigencia temporal donde la norma cambia.** Los catálogos de reglas (IVA, ICE, IRBPNR, retenciones,
  formas de pago) se modelan como **concepto** (identidad estable) + **versión** tipada hija con
  `ValidFrom`/`ValidUntil` (inclusivos, vocabulario ya existente). Se resuelve por la **fecha de negocio del
  documento** (ADR-034), nunca por "hoy". Por concepto no puede haber vigencias solapadas. Las enumeraciones
  estables (tipo de comprobante, tipo de identificación, país, unidad, etc.) se gestionan con `IsActive`.
- **D5 — Identidad ≠ representación.** La identidad interna (`Id` estable) y la clave de negocio visible (`Code`)
  se separan de las representaciones oficiales (código XML, código ATS, porcentaje/tarifa), que viven en la
  versión. Un código de representación solo se registra con fuente oficial; si no hay fuente, queda nulo.
- **D6 — Los builders no conocen mappings.** Los XML builders transcriben valores ya resueltos. Ningún builder,
  provider ni módulo de negocio contiene tablas del tipo "30 % → 1".
- **D7 — Resolución fail-closed.** Un resolver por catálogo (extendiendo los existentes, sin crear lectores
  paralelos) devuelve exactamente una versión vigente. 0 versiones, más de 1, representación nula o
  incoherencia con el dato del documento (p. ej. tasa aplicada ≠ porcentaje) son errores de configuración
  fiscal, y no se genera el XML.
- **D8 — Upgrades incrementales.** `InitialEnterpriseBaseline` no se modifica. Toda evolución es una migración
  nueva, y una instalación nueva recorre la misma secuencia que un upgrade.
- **D9 — Históricos no se destruyen.** Sobre una versión existente solo se permite cerrar `ValidUntil`
  (`null → fecha`). Porcentaje, código y fuente no cambian. Ni conceptos ni versiones se borran; se retiran con
  `IsActive = false` o cerrando su vigencia.
- **D10 — Trazabilidad normativa.** Cada versión referencia una fila de `global.sri_normative_source`
  (documento, versión, sección, fecha de publicación, URL, hash opcional, migración que la incorporó).
- **D11 — Compliance tests.** `SriCatalogComplianceTests` protege globalidad, no solapamiento, cobertura
  obligatoria, unicidad de resolución, consistencia, fail-closed, inmutabilidad (manifiesto aprobado),
  equivalencia instalación/upgrade y trazabilidad. Un cambio de catálogo que rompa la suite no se integra.
- **D12 — Evidencia histórica.** El XML firmado o autorizado ya almacenado es la evidencia fiscal definitiva. Un
  cambio de catálogo nunca regenera ni reinterpreta comprobantes autorizados. Solo los borradores no
  transmitidos se regeneran, y lo hacen con la regla vigente a su fecha de emisión.
- **D13 — Habilitación operativa (`IsEnabled`) separada de la vigencia normativa.**
  - **Dos dimensiones independientes.** `ValidFrom`/`ValidUntil` (en la versión) responden *qué dice la norma para
    una fecha*. `IsEnabled` (en el concepto, default `true`) responde *si el valor puede elegirse en una operación
    o configuración nueva*. Ninguna sustituye a la otra.
  - **Alcance.** Llevan `IsEnabled` los catálogos globales seleccionables cuyo concepto puede dejar de usarse:
    clase A, 13 catálogos (clasificación en el documento de arquitectura §F.2). No lo llevan `sri_emission_type`,
    `sri_id_type`, las tablas de versión ni `sri_normative_source`. El `IsActive` de `sri_error_code` queda
    documentado como sin efecto operativo.
  - **Mismo concepto que el `IsActive` actual.** `IsEnabled` no se agrega como un segundo booleano: el `IsActive`
    existente se renombra a `IsEnabled` (propiedad y columna) en el slice de cada catálogo. Nunca coexisten ambos
    en una tabla.
  - **Contrato de lectura explícito** en el repositorio o resolver de cada catálogo:
    - `GetSelectable…`: habilitados y, si el catálogo es temporal, vigentes a la fecha de referencia.
    - `ResolveForDate…`: vigencia normativa por `DocumentIssueDate`; **no** filtra `IsEnabled`.
    - `GetByIdIncludingDisabled…`: lectura histórica.
  - **Sin filtros en módulos ni globales.** Los módulos funcionales no implementan filtros `IsEnabled` propios.
    Nunca se usa `HasQueryFilter` para `IsEnabled`.
  - **Deshabilitar es inocuo para la historia.** No elimina ni modifica documentos, snapshots, XML ni versiones;
    las referencias existentes siguen consultables por Id. Una configuración existente que apunte a un valor
    deshabilitado se muestra con su estado y no se aplica en silencio a documentos nuevos.
  - **Nunca se borran filas oficiales** por obsolescencia.
- **D14 — Datos internos del sistema (INTERNAL SYSTEM DATA).**
  - **Sin edición por usuarios.** Los catálogos globales SRI no tienen CRUD ni UI de edición. Ningún usuario
    puede crearlos, modificarlos, activarlos o desactivarlos: ni de tenant/empresa ni `PlatformAdmin`.
  - **Origen de los cambios.** Toda alta, cierre de vigencia o cambio de `IsEnabled` llega únicamente mediante
    releases y migraciones controladas por ZH Technologies (D3, D8).
  - **Significado de los campos.** `IsEnabled` es un atributo interno de disponibilidad operativa actual;
    `ValidFrom`/`ValidUntil` es la vigencia normativa. Las operaciones nuevas usan `IsEnabled = true` + versión
    vigente; los históricos leen también registros con `IsEnabled = false`.
  - **Lecturas permitidas.** Se mantienen los endpoints de **solo lectura** que alimentan selectores
    (`CatalogController`, `GET api/v1/catalog/sri-*`); su semántica es `GetSelectable…`.
  - **Fuera de alcance.** Cualquier mecanismo futuro de modificación manual (endpoint, UI, comando o script
    operativo) queda **OUT OF SCOPE** y requiere diseño, ADR y autorización propios.

## Consecuencias

- El P0 de retenciones (R24) se corrige agregando datos versionados y una resolución fail-closed, no con un switch.
  `RetentionXmlBuilder` no cambia.
- 721–728 se conservan como clave de negocio y snapshot histórico. Su significado ATS queda abierto hasta tener
  fuente oficial.
- Cada catálogo de reglas migra a concepto + versión en su propio slice. Primero va
  `ZH-SRI-RETENTION-CATALOG-SSOT-01`; después IVA (incluye corregir la historia del 12 %), ICE/IRBPNR y formas
  de pago.
- Los 4 lectores actuales de IVA/ICE se consolidan progresivamente en la resolución temporal de cada catálogo.
  No se agrega un 5.º lector genérico.
- Las migraciones de catálogo se revisan en el PR: solo `InsertData`, cierre de `ValidUntil` y cambios de
  `IsEnabled` (único dato operativo mutable del concepto, y solo por release — D14).
- Una solicitud operativa del tipo "dejar de ofrecer el código X" se atiende con un release de ZH Technologies,
  no con soporte en caliente sobre la base de datos.
- Hoy varios métodos de resolución exigen `IsActive` incluso por Id o código: `RetentionCodeResolver`,
  `SriTaxResolver`, `SriGlobalRateReader`, `SriCatalogResolver`, y `SriDocTypeCatalogResolver` usado al generar el
  XML de retención. Deshabilitar un valor hoy rompería el recálculo o la regeneración de documentos existentes.
  Cada slice separa esos métodos según el contrato de D13; hasta entonces **no se debe deshabilitar** ningún
  valor de catálogo en producción.

## Alternativas consideradas

| Alternativa | Motivo de descarte |
|---|---|
| Switch o diccionario en el provider/builder (`725 → "1"`) | Hardcode fiscal disperso, sin historia ni fuente; viola `architecture.md § SSOT dinámico` |
| Reemplazar 721–728 por 1/2/3… en `Code` | Destruye snapshots y significado histórico, rompe tests y XML ya emitidos; mezcla identidad y representación |
| Agregar solo una columna `xml_code` a `sri_retention_code` | No representa cambios en el tiempo (porcentajes de Renta cambian) y repite el defecto del 12 % de IVA |
| Mega tabla genérica `sri_catalog_entry` | Pierde tipado, FKs y validaciones; contradice "1 concepto = 1 implementación" |
| JSON/CSV oficial cargado en startup | El SRI no publica catálogos estructurados (`Resources/SRI/Catalogs/README.md`); crearía una segunda fuente y divergencia install/upgrade |
| Seeding step idempotente | Ese camino es por tenant/empresa; los catálogos globales ya viajan por migraciones |
| Agregar `IsEnabled` junto al `IsActive` existente | Dos flags para un mismo concepto; viola "1 concepto = 1 implementación" y genera estados contradictorios |
| Usar solo `ValidUntil` para retirar valores del uso | Mezcla decisión operativa con norma; obligaría a inventar fechas normativas |
| CRUD/UI de catálogos para `PlatformAdmin` o tenants | Abre una segunda fuente de verdad sin trazabilidad normativa ni revisión; diverge entre instalaciones. Fuera de alcance (D14) |
| `HasQueryFilter` por `IsEnabled` | Ocultaría referencias históricas y rompería la resolución de documentos existentes |
| `EXCLUDE USING gist` obligatorio | Requiere `btree_gist`, sin precedente en el repo; queda como decisión diferida (DR-5), cubierto por unique + tests + resolver |

## Implementación

- **Slice 1 — `ZH-SRI-RETENTION-CATALOG-SSOT-01` (2026-10-02):** `sri_normative_source` + `sri_retention_code_version`
  (migración `SriRetentionCatalogVersioning`). `IRetentionCodeResolver` implementa el contrato de D13 y
  `SriCatalogComplianceTests` cubre la parte de retenciones.
  - Por instrucción del ticket, en este slice `IsEnabled` se materializa en la columna `is_active` existente
    (sin renombrar).
  - `ValidFrom` queda nulo ("sin límite inferior confirmado"): la ficha no permite fijar con certeza la fecha de
    inicio de la Tabla 20 vigente.
  - Estado: pendiente de validación real en `celcer`.
- **Slice 2 — `ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01` (2026-10-02):** Renta alineada con el Catálogo ATS oficial
  (Tabla 3.10, desde 06/08/2026), migración `SriRetentionIncomeCatalogAts20260806`. Concreciones de este ADR:
  - **Tarifa no única:** `SriRetentionRateKind` (`Fixed`/`Conditional`) + `RateRuleText`. Una regla
    condicional nunca guarda porcentaje y su resolución falla cerrado (D7).
  - **`SriRetentionCode.Percentage` es la tasa operativa vigente para operaciones nuevas.** Es una
    denormalización: se actualiza por migración junto con la versión que entra en vigencia, y un compliance
    test garantiza que coincida con ella. La historia vive en las versiones (inmutables salvo `ValidUntil`) y
    en los snapshots de cada línea emitida (D9).
  - **Corrección de significado:** cuando el nombre del concepto no correspondía al significado oficial del
    código, se corrigió conservando Id y código (el XML siempre envió el código oficial). Los defaults de
    proveedor afectados se deshabilitan en la migración, sin borrarlos ni remapearlos, hasta su validación
    explícita.

## Decisiones abiertas

DR-1 significado ATS de 721–728 · DR-2 concepto 15 % (`728`) · DR-3 códigos internos para IVA 50 %/0 %/no procede ·
DR-4 porcentajes vigentes de Renta · DR-5 constraint de exclusión · DR-6 temporalidad de `sri_tax_regime`. Detalle en el documento de arquitectura, §P.
