# Estándares de Datos — Precisión Numérica y Fechas/Horas (INMUTABLE)

Decisiones arquitectónicas congeladas 2026-06-25. No modificar sin revisión arquitectónica formal.

---

## Estándar de Precisión Numérica

Aplica a toda implementación que cree, modifique, calcule, compare, redondee o persista valores
numéricos de negocio: costos, precios, cantidades, importes, descuentos, impuestos, porcentajes,
márgenes, COGS, inventario/Kardex, compras, ventas, contabilidad, reportes y exportes.

### Autoridad única

Una sola política de precisión, leída siempre como `EffectivePrecisionPolicyDto`:

| Clase | Campos | Fuente | Configurable |
|---|---|---|---|
| Operativa por empresa | precio unitario de venta y de compra, cantidad, porcentaje operativo, costo unitario, costo promedio, factor de conversión, tolerancia de cuadre | `CompanyPrecisionPolicy` (tabla `company_precision_policy`); keys, rangos y perfiles solo en `PrecisionPolicyDefinitions` | Sí, por empresa, dentro de los rangos definidos |
| Fija del sistema | dinero, impuestos, contabilidad (`numeric(18,2)`), porcentaje fiscal | `FiscalPrecision` | No (invariante fiscal) |
| Contrato fijo de dominio | capacidad de bodega, % de cuota de crédito, peso de empaque | `WarehousePrecision`, `CreditTermsPrecision`, `ItemPrecision` | No |

- Backend: `ICompanyPrecisionPolicyProvider.GetEffectiveAsync()` (empresa activa; fail-closed: sin
  contexto de empresa o sin política lanza excepción, nunca un default silencioso).
- Frontend: `GET /api/v1/config/precision-policy` y `/precision-policy/metadata` →
  `precisionPolicy.config.ts`; ruta y guard en [frontend.md § Presentación numérica](frontend.md).
- Asignaciones internas del prorrateo de Compras: `EffectivePrecisionPolicyDto.CostAllocationDecimals`
  publica la escala fija existente `FiscalPrecision.UnitCost` (6), compatible con
  `freight_allocated`, `other_costs_allocated` y `total_line_cost` (`numeric(18,6)`). No es un
  campo editable de empresa ni reemplaza `UnitCostDecimals` para el costo unitario. Decisión del
  propietario aprobada el 2026-10-25; frontend consume este campo sin otra escala independiente.
- Prohibido crear otra política, servicio, configuración o regla de precisión (también por módulo,
  p. ej. Accounting): todos los módulos consumen esta autoridad. Si no puede identificarse el campo
  aplicable o existe más de una implementación para el mismo dato, se clasifica DEBT/FALTA antes de
  introducir lógica nueva.

### Backend

- Application resuelve la política con `ICompanyPrecisionPolicyProvider` y pasa explícitamente a
  Domain los dígitos que necesita. Domain no consulta configuración empresarial.
- Los parámetros de Domain con valor por defecto `FiscalPrecision.*` para un campo configurable
  (cantidad, costo unitario, factor…) son compatibilidad histórica: no agregar nuevos y pasar siempre
  el valor de la política.
- Redondeo funcional: `Math.Round(valor, política.<Campo>Decimals, MidpointRounding.AwayFromZero)`, sin
  helpers ni escalas literales paralelas. Cálculo, comparación con cero y validación de signo usan la
  misma precisión.
- **Domain**: solo `decimal`/`int`/`long` — prohibido string monetario.
- **Infrastructure**: `CultureInfo.InvariantCulture` obligatorio en todo parsing.
- **API**: JSON numbers nativos — prohibido strings numéricos.

### Frontend

- **Input obligatorio**: `ZhDecimalInput` para todo decimal, `ZhNumberInput` para enteros.
- **Separador**: solo punto (`.`) — coma prohibida.
- **Utilities**: `sanitizeDecimal()`, `parseDecimal()`, `formatMoney()` de `lib/sanitizers.ts`.
- **Decimales**: solo desde la política (sin defaults en frontend); el legacy `GET/PUT
  /api/v1/config/decimals` fue eliminado (COMPANY-PRECISION-POLICY-BACKEND-LEGACY-DECIMAL-CONFIG-CLEANUP-08).

### PostgreSQL — escalas técnicas

La escala técnica de la columna es la capacidad de persistencia y debe cubrir el máximo configurable;
la precisión funcional la decide la política. No confundir ambas.

| Tipo | Escala técnica | Aplica a |
|------|----------------|----------|
| Montos/totales fiscales y contables | `numeric(18,2)` | Subtotales, impuestos, grand total, pagos, CxC, CxP, asientos |
| Precios y costos unitarios configurables | `numeric(22,10)` | Precio unitario de compra/venta, costo unitario, `LandedUnitCost`, costo promedio, costos del Kardex/COGS, factor de conversión |
| Factor y cantidad base de presentación | `numeric(18,10)` | Factor de presentación y cantidad base asociada |
| Cantidades operativas | `numeric(20,6)` | Stock, cantidades de línea, movimientos, `QuantityInBaseUom` |
| Valores derivados de escala fija | `numeric(18,6)` | `DiscountAmount`, `TotalLineCost`, costos totales, precios base y de lista |
| Porcentajes operativos | `numeric(9,6)` | Descuento %, descuento máximo % |
| Porcentajes fiscales | `numeric(5,2)` | IVA, ICE, retención % |
| Escalas fijas de contrato | `numeric(18,4)` / `numeric(16,6)` / `numeric(10,3)` | Capacidad de bodega, tipo de cambio, metas de despacho / stock mínimo y máximo / peso de empaque |

### Gate para nuevas columnas decimales

Cualquier nueva columna decimal debe justificar antes de implementar:

1. **Tipo de dato** (monto, cantidad, precio/costo, porcentaje operativo o fiscal)
2. **Campo de la política** que gobierna su precisión funcional (o la constante fija que la define)
3. **Escala técnica** según la tabla anterior
4. **Motivo de negocio**

Si no coincide con una fila de la tabla → requiere revisión arquitectónica formal.

### Prohibido en todo el sistema (precisión numérica)

- Crear otra política de precisión o duplicar estas reglas
- Hardcodear escalas funcionales (`2`, `4`, `6`, …) donde la política define la precisión
- Resolver configuración de precisión desde Domain
- `toLocaleString()` / `Intl.NumberFormat()` para montos
- `<input type="number">` para campos decimales
- `decimal.Parse` sin `InvariantCulture`
- `Convert.ToDecimal` para datos financieros
- Crear columnas decimales sin justificar tipo/campo/escala/motivo

---

## Estándar de Fechas y Horas

### Contrato temporal (ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — ADR-034)

Dos tipos de dato temporal. Nunca se mezclan ni se compensan entre sí.

| Capa | FECHA DE NEGOCIO (día calendario) | INSTANTE (momento exacto) |
|------|-----------------------------------|---------------------------|
| PostgreSQL | `date` | `timestamp with time zone` |
| Backend | `DateOnly` / `DateOnly?` | `DateTime` Kind=`Utc` |
| API JSON | `"2026-09-25"` | `"2026-09-25T19:38:00Z"` (ISO-8601 con `Z`) |
| Query string | `[FromQuery] DateOnly` | `[FromQuery] DateTime <nombre>Utc` (zona explícita obligatoria) |
| Frontend | string `"YYYY-MM-DD"` | string ISO UTC |
| UI | `dd/MM/yyyy` (`formatDate`) | `dd/MM/yyyy HH:mm:ss` (`formatDateTime`, única salida visual de instantes, segundos siempre), **en `Company.Timezone`** |

- **Fecha de negocio**: el día jamás se suma ni se resta por zona horaria — "2026-09-25" es "2026-09-25" en BD, backend, API, frontend y UI, sea cual sea la zona del navegador, del servidor o de la sesión PostgreSQL. No pasa por `Date`, `toISOString()`, `DateTime` ni conversiones.
- **Instante**: se guarda en UTC real y se presenta en la zona de la empresa (`Company.Timezone`, expuesta en `GET /session/context` como `tenant.timezone`), nunca en la zona del navegador ni del servidor. Editar/guardar N veces conserva exactamente el mismo instante (cero drift).
- **Hora ingresada por el usuario** (`<input type="datetime-local">`) = hora de pared de la empresa → UTC **una sola vez** (`fromDateTimeLocalInputValue`); al cargar, UTC → hora de empresa (`toDateTimeLocalInputValue`).
- **Hora sin offset de una fuente externa** (FECHA_AUTORIZACION del TXT SRI, en hora Ecuador) = hora de pared de la empresa → UTC con `ICompanyClock.CompanyLocalToUtcAsync`. Nunca `DateTime.SpecifyKind(Utc)`.
- **Filtro "día de empresa" sobre un instante**: `DateOnly` → `ICompanyClock.DayUtcRangeAsync` → rango semiabierto `[inicioUtc, finUtc)`. Única implementación (frontend: `companyDayUtcRange`).
- Una hora ambigua (retroceso de horario de verano) se resuelve en horario estándar; una hora inexistente (salto) se rechaza — nunca se inventa un instante.

### Fuentes únicas (SSOT)

| Necesidad | Backend | Frontend (`lib/formatters/dateFormatters.ts`) |
|-----------|---------|-----------------------------------------------|
| "Hoy" de negocio | `ICompanyClock.TodayAsync` | `todayIso()` / `firstDayOfMonthIso()` |
| Día de empresa de un instante | `ICompanyClock.LocalDateAsync` | `formatDate(instante)` |
| Hora de empresa → UTC | `ICompanyClock.CompanyLocalToUtcAsync` | `fromDateTimeLocalInputValue` |
| UTC → hora de empresa | `CompanyTimeZone.ToLocal` | `toDateTimeLocalInputValue` (conserva segundos reales; `ZhDateTimeInput` usa `step=1`) / `formatDateTime` |
| Día de empresa → rango UTC | `ICompanyClock.DayUtcRangeAsync` | `companyDayUtcRange` |
| Aritmética de calendario | `DateOnly.AddDays` | `addDaysIso` |
| Guard "ya es instante UTC" | `UtcDateTime.EnsureUtc` | — |
| Parser de instantes de la API | `UtcDateTime.TryParseInstant` (vía `UtcInstantJsonConverter` / `UtcInstantModelBinder`) | — |

`ICompanyClock` resuelve `Company.Timezone` y delega toda la aritmética en `CompanyTimeZone` (única implementación; el catálogo SRI platform-scoped usa la misma con la zona fiscal nacional `America/Guayaquil`).

**Fecha contable:** [Accounting](./accounting.md).

### Prohibido (fechas) — bloqueado por guards

- Backend (`ERP.Architecture.Tests/DateTimeCompanyClockGuardrailTests`): `DateTime.Now`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTime.UtcNow.Date`/`DateOnly.FromDateTime(DateTime.UtcNow)` como fecha de negocio; `DateTime.SpecifyKind(` y `DateOnly.ToDateTime(` fuera de `CompanyTimeZone`; `.ToLocalTime()` / `.LocalDateTime`; `DateTime(Offset).(Try)Parse(Exact)` sin `DateTimeStyles` explícito; `[FromQuery] DateTime` cuyo nombre no termine en `Utc`; `DateOnly.(Try)Parse(Exact)` sin `InvariantCulture`; declarar `DateTimeOffset` o fechas como `string …Date/…At/…Utc` (terceras representaciones — 02J). Excepción documentada: `XadesBesSigner` (vigencia de certificado X509, expuesta por .NET en hora local).
- Frontend (`tools/architecture/check-frontend-datetime.mjs`, regla F-DT): `toISOString().slice(0,10)` / `.split("T")` como fecha; `new Date("YYYY-MM-DD")` o `new Date(x + "T00:00")`; `Intl.DateTimeFormat`, `timeZone:`, `getTimezoneOffset()`, `toLocaleString/DateString/TimeString`, opciones Intl `hour:`/`minute:`/`second:` y getters/setters de calendario (`getDate`, `getUTCHours`, `setDate`, …) fuera de `dateFormatters.ts`; `formatDate(…At|…Utc|authorizationDate)` — un instante se presenta solo con `formatDateTime` (ZH-TEMPORAL-DATETIME-SECONDS-02I).
- `toLocaleString()` / `Intl.NumberFormat` para montos (ver precisión).
- Hardcodear locale en formateo de fechas financieras.
