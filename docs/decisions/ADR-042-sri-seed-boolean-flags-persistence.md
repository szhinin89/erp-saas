# ADR-042 — Flags booleanos de catálogos SRI: el seed persiste el valor declarado

Estado: Accepted. Fecha: 2026-10-09. Ticket: SRI-VAT-SEED-ACTIVE-FLAGS. Extensión controlada de la
infraestructura CLOSED de Configuración Tributaria por bug demostrado (mismo criterio que ADR-034).
Compatible con [ADR-037](./ADR-037-sri-global-catalog-governance.md) (Proposed): no adelanta su modelo
concepto + versión ni el renombre `IsActive` → `IsEnabled`; corrige el valor del flag vigente hoy.

## Contexto

Los catálogos globales SRI (`global.*`) se siembran con `HasData` en su `IEntityTypeConfiguration` y solo
cambian por migración (release), nunca por CRUD (ADR-037). `SriVatRateConfiguration` declara los códigos
IVA 2 (12% histórico) y 3 (14% histórico) con `IsActive = false`, pero en PostgreSQL ambos quedaban
activos (verificado en `dberpsaas` y `erp_sumak_pilot`). Con eso `SriTaxResolver` (resolución por código,
filtra `IsActive`) todavía resolvía 12%/14% para Compras, Ventas y Gastos. El mismo síntoma se había
encontrado y corregido en `global.sri_doc_type` (02/08/09/18, `is_active` e `is_electronic`) durante IL-6A;
`global.sri_retention_code` ya lo tenía resuelto (ZH-SRI-RETENTION-CATALOG-SSOT-01).

## Causa raíz

La propiedad estaba configurada `HasDefaultValue(true)` con la generación de valor por defecto de EF
("on add"). EF trata el valor CLR por defecto de un `bool` (`false`) como "no asignado" y omite la columna
en el `INSERT`, así que la BD aplica su DEFAULT `true`. Se ve en la migración base
`InitialEnterpriseBaseline`: el `InsertData` de los códigos 2 y 3 no incluye `is_active`. Ningún test
comparaba la BD real contra el seed declarado.

## Decisión

1. En un catálogo SRI con seed, un flag booleano con DEFAULT de BD (`is_active`, `is_electronic` o
   equivalente) persiste siempre el valor declarado. Patrón único, el ya usado por `SriRetentionCodeConfiguration`:
   `HasDefaultValue(true).ValueGeneratedNever()`. El DEFAULT de la columna se conserva; EF escribe el valor real.
2. `global.sri_vat_rate.is_active` adopta ese patrón. La tabla no tiene `is_electronic`; `sri_doc_type` ya
   lo aplicó a ambos flags.
3. Los códigos IVA 2 y 3 son históricos y quedan inactivos. Migración `SriVatRateSeedActiveFlags`:
   `UpdateData` de `is_active = false` para 2 y 3, nada más.
4. No cambia ningún porcentaje ni vigencia (`ValidFrom`/`ValidUntil`), ni el esquema: solo la metadata EF
   del modelo y los datos seed existentes.
5. Cada catálogo corregido lleva un test PostgreSQL real (todas las migraciones) que compara BD vs seed
   declarado y prueba que un insert inactivo por EF persiste `false` (`SriVatRateActiveFlagsPostgreSqlTests`,
   `SriDocTypeActiveFlagsPostgreSqlTests`).

## Compatibilidad

- **Tarifas activas:** 0, 4, 5, 6, 7, 8 y 10 conservan `is_active = true`, su porcentaje y su vigencia
  (el test las verifica una por una).
- **Datos existentes:** ninguna columna de IVA de `dberpsaas` ni de `erp_sumak_pilot` referencia 2 o 3
  (ítems de compra/venta, detalles de compra, NC y devolución de compra, resúmenes tributarios, recepciones,
  detalles de venta y devolución de venta, líneas de gasto, incluidos los snapshots de nombre).
- **Resolver tributario** (`SriTaxResolver`, `SriCatalogResolver`, `SriGlobalRateReader`): 2 y 3 dejan de
  resolverse; un documento nuevo con esos códigos falla de forma controlada, como cualquier código inactivo.
- **Lookup / API** (`GET .../sri-vat-rates`, `GetActiveVatRatesAsync`): ya filtraba por vigencia a la fecha
  actual, por lo que 2 y 3 no aparecían; el contrato y el DTO no cambian. No hay breaking change.
- **Migraciones:** sin cambios de esquema; `UPDATE` por clave primaria sobre una tabla global pequeña.
  Es idempotente (repetirla deja el mismo estado) y el historial de EF la aplica una sola vez en un
  despliegue normal.

## Consecuencias

- Se cierra una divergencia silenciosa entre el catálogo declarado y el real. Un `false` futuro en estos
  seeds ya no puede perderse.
- **Observación, fuera de alcance:** el seed fija la vigencia del código 2 (12%) hasta 2016-05-31, pero en
  la normativa ecuatoriana la tarifa de 12% volvió a regir entre 2017-06-01 y 2024-03-31. Con 2 inactivo,
  un documento con fecha de emisión en ese periodo y tarifa 12% no se resuelve. Hoy no hay datos afectados.
  La vigencia histórica corresponde al modelo concepto + versión de ADR-037; no se cambia aquí.

## Rollback

`dotnet ef database update SriDocTypeSeedActiveFlags` ejecuta el `Down` de `SriVatRateSeedActiveFlags`
(`is_active = true` para 2 y 3), y luego se revierte la línea de configuración. No hay esquema ni datos de
negocio que restaurar.

## Alternativas consideradas

- **Quitar el DEFAULT de la columna:** también corrige, pero cambia el esquema y crea un segundo patrón
  frente al de `SriRetentionCodeConfiguration`. Descartada.
- **`UPDATE` manual sin cambiar la configuración:** el próximo seed o insert con `false` repetiría el bug.
  Descartada.
