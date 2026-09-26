# Estándares de Datos — Precisión Numérica y Fechas/Horas (INMUTABLE)

Decisiones arquitectónicas congeladas 2026-06-25. No modificar sin revisión arquitectónica formal.

---

## Estándar de Precisión Numérica

### PostgreSQL — Precisiones oficiales

| Tipo | Precision | Aplica a |
|------|-----------|----------|
| Montos/totales | `numeric(18,2)` | Subtotales, impuestos, grand total, pagos, CxC, CxP, asientos |
| Cantidades | `numeric(18,4)` | Stock, qty líneas, movimientos, tipo de cambio |
| Precios unitarios | `numeric(18,6)` | UnitPrice, LandedCost, DiscountAmount, costo promedio |
| Porcentajes | `numeric(5,2)` | IVA, ICE, descuento %, retención %, margen % |

### Frontend

- **Input obligatorio**: `ZhDecimalInput` para todo decimal, `ZhNumberInput` para enteros
- **Separador**: solo punto (`.`) — coma prohibida
- **Utilities**: `sanitizeDecimal()`, `parseDecimal()`, `formatMoney()` de `lib/sanitizers.ts`
- **Decimales configurables**: `precisionPolicy.config.ts` carga desde `GET /api/v1/config/precision-policy` por empresa (`company_precision_policy`, SSOT; rangos/perfiles desde `/precision-policy/metadata`, sin defaults en frontend — reemplaza el legacy `GET/PUT /api/v1/config/decimals`, eliminado en COMPANY-PRECISION-POLICY-BACKEND-LEGACY-DECIMAL-CONFIG-CLEANUP-08)

### Backend

- **Domain**: solo `decimal`/`int`/`long` — prohibido string monetario
- **Infrastructure**: `CultureInfo.InvariantCulture` obligatorio en todo parsing
- **API**: JSON numbers nativos — prohibido strings numéricos

### Gate para nuevas columnas decimales

Cualquier nueva columna decimal debe justificar antes de implementar:

1. **Tipo de dato** (monto, cantidad, precio, porcentaje)
2. **Precisión** (18 o 5)
3. **Escala** (2, 4 o 6)
4. **Motivo de negocio**

Si no coincide con `numeric(18,2)`, `numeric(18,4)`, `numeric(18,6)` o `numeric(5,2)` → requiere revisión arquitectónica formal.

### Prohibido en todo el sistema (precisión numérica)

- `toLocaleString()` / `Intl.NumberFormat()` para montos
- `<input type="number">` para campos decimales
- `decimal.Parse` sin `InvariantCulture`
- `Convert.ToDecimal` para datos financieros
- Crear columnas decimales sin justificar tipo/precisión/escala/motivo

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

### Prohibido (fechas) — bloqueado por guards

- Backend (`ERP.Architecture.Tests/DateTimeCompanyClockGuardrailTests`): `DateTime.Now`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTime.UtcNow.Date`/`DateOnly.FromDateTime(DateTime.UtcNow)` como fecha de negocio; `DateTime.SpecifyKind(` y `DateOnly.ToDateTime(` fuera de `CompanyTimeZone`; `.ToLocalTime()` / `.LocalDateTime`; `DateTime(Offset).(Try)Parse(Exact)` sin `DateTimeStyles` explícito; `[FromQuery] DateTime` cuyo nombre no termine en `Utc`; `DateOnly.(Try)Parse(Exact)` sin `InvariantCulture`; declarar `DateTimeOffset` o fechas como `string …Date/…At/…Utc` (terceras representaciones — 02J). Excepción documentada: `XadesBesSigner` (vigencia de certificado X509, expuesta por .NET en hora local).
- Frontend (`tools/architecture/check-frontend-datetime.mjs`, regla F-DT): `toISOString().slice(0,10)` / `.split("T")` como fecha; `new Date("YYYY-MM-DD")` o `new Date(x + "T00:00")`; `Intl.DateTimeFormat`, `timeZone:`, `getTimezoneOffset()`, `toLocaleString/DateString/TimeString`, opciones Intl `hour:`/`minute:`/`second:` y getters/setters de calendario (`getDate`, `getUTCHours`, `setDate`, …) fuera de `dateFormatters.ts`; `formatDate(…At|…Utc|authorizationDate)` — un instante se presenta solo con `formatDateTime` (ZH-TEMPORAL-DATETIME-SECONDS-02I).
- `toLocaleString()` / `Intl.NumberFormat` para montos (ver precisión).
- Hardcodear locale en formateo de fechas financieras.
