# ADR-034 — Contrato temporal único: fecha de negocio vs. instante UTC

**Status:** Accepted · **Fecha:** 2026-09-25 · **Ticket:** ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02
**Regla vigente:** [`docs/architecture/data-standards.md § Contrato temporal`](../architecture/data-standards.md#contrato-temporal-zh-temporal-contract-single-source-02--adr-034)
**Extiende de forma controlada:** ADR-023 (ElectronicDocuments v1.0) — causa permitida n.º 2, *bug demostrado*.

## Contexto

El ERP convivía con varias maneras de representar y convertir fechas, que se compensaban entre sí:

- **Drift +5h en `ExpenseDocument.AuthorizationDate`**: el formulario convertía la hora ingresada con la zona del *navegador* (`new Date(local).toISOString()`) y al recargar mostraba el UTC con `getUTC*()` como si fuera hora local. Cada edición/guardado sumaba 5 h.
- **Hora local etiquetada UTC**: `UtcDateTime.Normalize` marcaba `Kind=Unspecified` como UTC con `SpecifyKind`; el TXT SRI (`FECHA_AUTORIZACION`, hora Ecuador) y el `datetime-local` de Compras quedaban guardados 5 h antes del instante real.
- **"Hoy" en UTC**: 9 usos productivos de `new Date().toISOString().slice(0,10)` (cobros, pagos a proveedor, créditos, reportes contables) — entre 19:00 y 23:59 Ecuador devolvían mañana.
- **Filtros por día ambiguos**: `[FromQuery] DateTime` + `SpecifyKind(Utc)` convertían "2026-09-25" en `00:00Z` y filtraban `CreatedAt <= 00:00Z`: el Kardex perdía el día final completo y el filtro "Hoy" del Monitor de documentos electrónicos devolvía prácticamente vacío.
- **Fechas de negocio como instante**: `StockTransfer.TransferDate`/`StockAdjustment.AdjustmentDate` (timestamptz, medianoche UTC), `Dashboard.AsOf`, `ElectronicDocumentData.IssueDate` (DateOnly → DateTime → string).
- **`fechaEmision` de la Nota de Crédito de venta** tomada del día UTC de `UpdatedAt` — entre 19:00 y 23:59 Ecuador, el día siguiente (mismo patrón del rechazo SRI [65]).
- **Dos relojes de "hoy"**: `CompanyClock` y `SriCatalogClock` con aritmética duplicada; `formatDateTime` mostraba UTC crudo.

## Decisión

1. **Fecha de negocio** = `DateOnly` ↔ PostgreSQL `date` ↔ `"YYYY-MM-DD"` ↔ `dd/MM/yyyy`. Sin zona horaria en ninguna capa.
2. **Instante** = `DateTime` Kind=Utc ↔ `timestamptz` ↔ ISO-8601 con `Z` ↔ presentado en `Company.Timezone`.
3. **Aritmética de zona única**: `CompanyTimeZone` (Application). `ICompanyClock` (resuelve `Company.Timezone`) delega en ella y agrega `DayUtcRangeAsync` y `CompanyLocalToUtcAsync`. `SriCatalogClock` eliminado.
4. **`UtcDateTime.Normalize` → `UtcDateTime.EnsureUtc`**: acepta Utc (y Local = offset explícito ya resuelto), **rechaza** Unspecified (400). `TryParseInstant` es el único parser de instantes de la API.
5. **Borde HTTP**: `UtcInstantJsonConverter` (JSON exige y emite `Z`) + `UtcInstantModelBinder` (query/route exige zona). Filtros por día pasan a `DateOnly` + `ICompanyClock.DayUtcRangeAsync`.
6. **Frontend**: `dateFormatters.ts` es el único punto de zona/"hoy"/conversión; la zona sale de `session.tenant.timezone`. Salida visual única (ZH-TEMPORAL-DATETIME-SECONDS-02I): fecha de negocio `formatDate` → `dd/MM/yyyy`; instante `formatDateTime` → `dd/MM/yyyy HH:mm:ss` (segundos siempre; `formatDateTimeSeconds` eliminado). `datetime-local` conserva los segundos reales (`ZhDateTimeInput` con `step=1`), sin inventar `:00` en el dato persistido.
7. **Guards**: extensión de `DateTimeCompanyClockGuardrailTests` (backend) y nuevo check `frontend-datetime` dentro de `tools/architecture/run-all.mjs` (mismo motor que `frontend-precision`).

## Cambios en ElectronicDocuments (ADR-023, causa n.º 2: bug demostrado)

| Cambio | Bug / evidencia | Test de regresión |
|---|---|---|
| `GET /electronic-documents?dateFrom&dateTo` → `DateOnly` + rango `[inicio, fin)` del día de empresa | Filtro "Hoy" (`dateFrom=dateTo=hoy`) se traducía a `CreatedAt >= 00:00Z AND <= 00:00Z` | `GetElectronicDocumentsListQueryHandlerTests` (compila con el nuevo contrato), `CompanyTimeZoneContractTests.DayUtcRange_*` |
| `ElectronicDocumentEmissionContext.IssueDate` / `ElectronicDocumentModifiedReference.IssueDate` → `DateOnly` | Fecha de negocio convertida a DateTime solo para volver a formatearla | Builders XML/RIDE (suites existentes, mismas salidas `ddMMyyyy` / `dd/MM/yyyy`) |
| `SalesReturnCreditNoteDataProvider`: `fechaEmision` = `ICompanyClock.LocalDateAsync(UpdatedAt ?? CreatedAt)` | Día UTC ≠ día Ecuador entre 19:00 y 23:59 | `SalesReturnCreditNoteDataProviderTests.GetDataAsync_con_devolucion_autorizada_construye_el_modelo_correctamente` |

No se modificó la máquina de estados, el pipeline, la firma ni los clientes SOAP (`SriSoapClient` ya parseaba `fechaAutorizacion` con offset → UTC real).

## Revisión de compatibilidad (contrato API)

| Endpoint / DTO | Antes | Ahora |
|---|---|---|
| `StockAdjustmentDto.adjustmentDate`, `StockTransferDto.transferDate` | `"2026-09-25T00:00:00Z"` | `"2026-09-25"` (UI sigue mostrando 25/09/2026) |
| `GET /inventory/stock/adjustments?startDate&endDate` | DateTime | DateOnly (inclusivo) |
| `GET /inventory/kardex/{id}?from&to`, `GET /inventory/stock/movements?from&to` | DateTime → UTC día | DateOnly → día de empresa `[inicio, fin)` |
| `GET /dashboard/kpis?asOf`, `DashboardKpisDto.asOf` | DateTime | DateOnly |
| `GET /configuration/change-log?from&to` | `from`/`to` | `fromUtc`/`toUtc` (sin consumidores frontend) |
| `GET /admin/user-sessions?fromUtc&toUtc` | `toUtc` inclusivo | `toUtc` exclusivo `[from, to)` (único consumidor actualizado) |
| Cualquier `DateTime` en body JSON | sin zona aceptado (etiquetado UTC) | sin zona → 400; con `Z`/offset → mismo instante UTC |
| `GET /session/context` → `tenant.timezone` | — | nuevo (Company.Timezone) |
| Ventas: `payments[].transferDetail.transferDate`, `payments[].chequeDetail.cashDate` | `string` + `DateOnly.TryParse` sensible a cultura | `DateOnly` ("YYYY-MM-DD" ISO estricto; `"25/09/2026"`/`"09/25/2026"` → 400 en cualquier cultura) — 02J |
| Sobre `meta.timestamp` (todas las respuestas) | `DateTimeOffset` → `"…+00:00"` | `DateTime` UTC → `"…Z"` (MVC y `ExceptionMiddleware` con el mismo `UtcInstantJsonConverter`) — 02J |

## Datos

- **Migración de esquema** `TemporalContractInventoryBusinessDates02`: `transfer_date`/`adjustment_date` timestamptz → date. Guard previo aborta si alguna fila no es medianoche UTC; conversión explícita `AT TIME ZONE 'UTC'` (el cast implícito usa la zona de sesión y corre el día — verificado: sesión Guayaquil daba 2026-09-24). `Down` reconstruye la medianoche UTC exacta.
- **AuthorizationDate histórico**: no se autocorrige. Plan separado: [`docs/operations/AUTHORIZATION-DATE-REMEDIATION-PLAN-02.md`](../operations/AUTHORIZATION-DATE-REMEDIATION-PLAN-02.md).

## Consecuencias

- Un cliente que envíe instantes sin zona recibe 400 (antes se guardaban con +5h de error silencioso).
- `KardexSnapshot` (entidad + `IKardexSnapshotRepository` + `IKardexSnapshotCalculator`) eliminados: sin DbSet, tabla, implementación ni consumidores. `KardexOptions.UseScalableMode` queda sin efecto (pendiente de retiro).
