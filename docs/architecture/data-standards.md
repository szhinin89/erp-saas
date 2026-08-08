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
- **Decimales configurables**: `getDecimalConfig()` carga desde `GET /api/v1/config/decimals` por empresa

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

### Visualización (frontend)

| Contexto | Formato | Función |
|----------|---------|---------|
| Fecha | `dd/MM/yyyy` | `formatDate()` |
| Fecha + hora | `dd/MM/yyyy HH:mm` | `formatDateTime()` |
| Auditoría | `dd/MM/yyyy HH:mm:ss` | `formatDateTimeSeconds()` |
| Fecha ISO para inputs | `yyyy-MM-dd` | `todayIso()` |

Fuente única: `lib/formatters/dateFormatters.ts`. Usa `getUTC*()` para evitar desfase por timezone del navegador.

### Backend

- Persistencia: `DateTime.UtcNow` siempre — nunca `DateTime.Now`
- API: ISO 8601 (`2026-06-25T19:35:42Z`)
- Fechas sin hora: `DateOnly` → PostgreSQL `date`
- Timestamps: `DateTime` → PostgreSQL `timestamptz`

### Prohibido (fechas)

- `toLocaleDateString()` / `toLocaleString()` para fechas de negocio
- `new Date(iso).toLocaleString('es-EC')` — desfase por timezone
- `DateTime.Now` en backend (usar `DateTime.UtcNow`)
- Hardcodear locale en formateo de fechas financieras
