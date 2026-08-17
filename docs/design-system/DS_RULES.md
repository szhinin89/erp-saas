# Design System — reglas de consistencia visual

Fuente normativa de las reglas visuales validadas por `scripts/ds/*.ps1`.
No repite reglas ya cubiertas en `/frontend/CLAUDE.md` (reutilización de
componentes, prohibición de `style={{}}` estático, etc.) — este documento se
enfoca en **tipografía y apariencia base**, el dominio que auditan estos
scripts.

**Módulo de referencia**: `frontend/src/modules/sales/**` y
`frontend/src/modules/purchases/**` son el estándar vigente — quedaron
cerrados bajo `ds-guard.ps1 -Scope modules -ModuleName sales|purchases`
(exit 0) tras las tareas ERP-DS-FONT-FAMILY-BASELINE-01 →
ERP-DS-TYPOGRAPHY-SCALE-02. Cualquier módulo nuevo o en revisión debe
apuntar a ese mismo nivel antes de darse por cerrado.

---

## 1. Qué va global vs. qué puede quedar local

| Puede vivir en CSS local de módulo | Debe venir de global DS |
|---|---|
| `grid`, `flex`, columnas | `font-family` (texto normal) |
| `width`/`min-width`/`max-width` | `font-family-mono` (solo vía `.zh-code-value`) |
| `gap` | tamaño/peso base de labels |
| `margin`/`padding` contextual | tamaño/peso base de valores/datos |
| `alignment`, `position`, `overflow` | composición visual de montos (`ZHMoneyValue`) |
| tamaño de **íconos** (`.*-icon`, `material-symbols`) | borde/foco de inputs |
| layout de secciones/paneles propios del módulo | estilo base de badge/chip |
| `line-height` puntual para evitar recorte (ratio, no token) | `text-transform`/`letter-spacing` fuera de labels/títulos globales |

Regla general: **un módulo puede decidir dónde van las cosas (layout), nunca
cómo se ven las cosas en sí (tipografía/color base)**.

## 2. Prohibido

1. **`font-family` local en módulos.** Texto normal hereda `var(--font-family)`
   global (ya lo da `body`/`index.css`, no hace falta redeclararlo). Código
   técnico (SKU, código de proveedor/auxiliar, clave de acceso, secuencial,
   código SRI) usa **solo** uno de:
   - clase utilitaria `.zh-code-value` (combinable con una clase local que
     ya defina tamaño/color, para no duplicar esas propiedades);
   - `<ZHDataValue variant="code">`;
   - `<Badge code>`.

2. **`italic` en datos normales.** Prohibido `font-style: italic`, la clase
   `zh-data-value--italic` y la prop `italic` de `ZHDataValue` para
   IVA/ICE/IRBPNR, notas fiscales, datos XML, subtítulos, hints, precios,
   cantidades, totales o cualquier valor secundario. Si hace falta jerarquía
   secundaria: usar `variant="muted"` (o `.zh-text-muted`), nunca cursiva.

3. **`style={{...}}` en módulos**, salvo la excepción ya documentada en
   `/frontend/CLAUDE.md` (valores dinámicos: dimensiones calculadas,
   transforms, colores de datos).

4. **Colores hardcodeados en módulos**: `#hex`, `rgb(...)`, `rgba(...)`.
   Los colores viven en `design-tokens.css` como `var(--color-*)`; efectos
   de transparencia usan `color-mix(in srgb, var(--color-*) N%, transparent)`.

## 3. Tipografía local controlada

`font-size` / `font-weight` / `line-height` / `letter-spacing` /
`text-transform` en CSS de módulo se auditan y clasifican así:

| Clasificación | Significa |
|---|---|
| `OK_GLOBAL` | Compone con un componente/clase global (`.zh-section-title`, `ZHFieldLabel`, `inherit` sobre `.zh-money-value`, etc.) |
| `OK_ICON` | El selector es claramente un ícono (`*-icon`, `material-symbols`) |
| `OK_LAYOUT` | Uso contextual sin escala propia (ej. `line-height: 1.3` para truncar sin recortar) |
| `OK_CODE` | Asociado a un valor de código técnico ya gobernado por `.zh-code-value`/`variant="code"` |
| `OK_TOKEN` | Usa `var(--text-*)` de `design-tokens.css` |
| `NOT_OK_VISUAL_LOCAL` | Apariencia base local prohibida — bloquea `ds-guard.ps1` |
| `NEEDS_DECISION` | Valor crudo (px/peso numérico) sin justificación global visible — **no bloquea el guard**, pero requiere revisión humana antes de aceptarse como definitivo |

`NEEDS_DECISION` no es automáticamente "malo": puede ser un hueco real de la
escala (`design-tokens.css` no cubre 9/10/11/13/17/21/27px) documentado con
una razón concreta (ver ejemplos en `DS_AUDIT_REPORT.md` y en los commits
`ERP-DS-VISUAL-CONSISTENCY-CLOSEOUT-01`/`ERP-DS-TYPOGRAPHY-SCALE-02`). Lo que
nunca es aceptable es dejarlo así **sin revisarlo**.

## 4. Cómo usar los átomos del DS

### `.zh-code-value`
Utilidad CSS pura (`zh-ui.css`) — agrega `font-family: var(--font-family-mono)`
sin tocar tamaño/peso/color. Combinarla con la clase local existente:

```tsx
// Bien — el layout/tamaño quedan en la clase local, el mono lo da la utilidad
<td className="pf-invoice-number zh-code-value">{inv.invoiceNumber}</td>
```

```css
/* Mal — mono redefinido a mano en el módulo */
.pf-invoice-number { font-family: var(--font-family-mono); }
```

### `.zh-row-title`
Utilidad CSS (`zh-ui.css`) — `font-weight: 700; color: var(--color-text-primary);`
para el nombre destacado de una fila/card (producto, ítem). Combinar con la
clase local que da tamaño/layout:

```tsx
<div className="pdl-line__product-name zh-row-title">{productName}</div>
```

### `ZHMoneyValue`
Único punto de entrada para renderizar montos. `white-space: nowrap` ya
garantizado a nivel global (`.zh-money-value`) — símbolo y valor nunca se
parten en líneas distintas, en ningún contenedor.

```tsx
<ZHMoneyValue value={total} decimals={2} emphasis="grand" />
```

`emphasis`: `default` | `muted` | `strong` | `total` | `grand`. Si un monto
necesita un tamaño que ningún `emphasis` cubre, la opción es **ajustar el
componente global** (agregar/objetar un tier), no crear `font-size` local
sobre `.zh-money-value`.

### `ZHDataValue`
Dato de solo lectura no monetario. `variant`: `default` | `muted` | `numeric`
| `strong` | `code` (única variante monoespaciada). Nunca usar `italic` en
`muted`/`default`/`numeric`/`strong`.

### `ZHInfoRow`
Layout puro (label/value en fila) para paneles de detalle — no define
tipografía; `label` normalmente lleva `<ZHFieldLabel size="sm">` y `value`
lleva `ZHDataValue`/`ZHMoneyValue`.

```tsx
<ZHInfoRow
  label={<ZHFieldLabel size="sm">Base imponible</ZHFieldLabel>}
  value={<ZHDataValue variant="numeric">{vm.xml.taxableBase}</ZHDataValue>}
/>
```

### `ZHFieldLabel`
`size="sm"` para labels compactos de línea/card (12px/600), `size="md"` para
labels de formulario estándar. Nunca redefinir `font-size`/`font-weight`
sobre él en CSS de módulo.

### `.zh-section-title`
Título de sección compacto (11px/700/uppercase/tracking) — "PRODUCTO
RECIBIDO XML", "INFORMACIÓN COMERCIAL", "ESTADO DEL FORMULARIO", etc. El
color se deja al consumidor (semántico por bloque).

## 5. Ejemplos buenos / malos

```tsx
// BIEN: monto vía componente, sin envoltorio con tipografía propia
<ZHMoneyValue value={vatAmt} emphasis="muted" />

// MAL: recrear el look de un ZHMoneyValue a mano
<span style={{ fontSize: 12, color: "#888" }}>${vatAmt.toFixed(2)}</span>
```

```css
/* BIEN: el contenedor decide el tier, el átomo hereda */
.pf-totals__value .zh-money-value {
  font-size: inherit;
  font-weight: inherit;
  color: inherit;
}

/* MAL: font-size arbitrario duplicando un tier que ya existe */
.pf-totals__value { font-size: 13px; font-weight: 600; }
```

```tsx
// BIEN
<ZHDataValue variant="muted">{vm.xml.description}</ZHDataValue>

// MAL — italic en un dato normal
<ZHDataValue variant="muted" italic>{vm.xml.description}</ZHDataValue>
```

## 6. Cómo correr audit / fix / guard

Todos los scripts viven en `scripts/ds/` y son PowerShell (`-ExecutionPolicy
Bypass -File ...`), solo tocan `frontend/src/{modules,components,styles}`,
nunca `backend/`, nunca hacen commit.

```powershell
# Auditoría de solo lectura — genera docs/design-system/DS_AUDIT_REPORT.md
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-audit.ps1 -Scope modules
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-audit.ps1 -Scope modules -ModuleName sales
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-audit.ps1 -Scope all -Output docs/design-system/DS_AUDIT_REPORT.md

# Guard — falla (exit 1) si hay violaciones bloqueantes; advierte (exit 0) por NEEDS_DECISION
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-guard.ps1 -Scope modules
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-guard.ps1 -Scope modules -ModuleName purchases

# Fix de patrones 100% seguros — DRY-RUN por defecto, no escribe nada
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-fix-known-patterns.ps1 -Scope modules
# Solo con -Apply explícito escribe cambios:
powershell -ExecutionPolicy Bypass -File scripts/ds/ds-fix-known-patterns.ps1 -Scope modules -Apply
```

`ds-fix-known-patterns.ps1` **solo** corrige (ver cabecera del script para el
detalle exacto):
1. Prop `italic` en `<ZHDataValue>` dentro de `modules/**/*.tsx`.
2. Clase `zh-data-value--italic` en `className` dentro de `modules/**/*.tsx`.
3. Reglas CSS de una sola línea `SELECTOR { font-style: italic; }` en
   `modules/**/*.css` (única declaración del bloque).
4. Invariante global `white-space: nowrap` dentro de `.zh-money-value`
   (`zh-ui.css`) si faltara.
5. Reglas CSS de una sola línea `SELECTOR { font-family: var(--font-family-mono); }`
   en `modules/**/*.css`, solo cuando el nombre del selector matchea un
   patrón de código técnico conocido (`sku|code|clave|secuencial|auxcode|access`)
   — borra solo la declaración CSS, nunca toca el `.tsx` (el `className`
   `zh-code-value` se agrega a mano, el script lo señala en su salida).

Cualquier otro caso (`font-size`/`font-weight`/`letter-spacing`/
`text-transform` sin token, `italic` en contexto ambiguo, `font-family-mono`
en un bloque multilínea) se reporta como `NEEDS_DECISION` y **no se toca**.

## 7. Whitelist del guard

`ds-guard.ps1` no trae ninguna excepción por defecto. Si un hallazgo puntual
ya fue revisado y aceptado explícitamente (por ejemplo, un caso
`NEEDS_DECISION` de tipografía que se decidió mantener), se puede excluir
pasando `-WhitelistPath <archivo>` con líneas `ruta/relativa/al/archivo.css:linea`
(una por renglón, `#` para comentarios). No usar la whitelist para silenciar
un `NOT_OK_VISUAL_LOCAL` corregible — corregirlo primero.

## 8. Limitaciones conocidas

- Los scripts clasifican **por línea con regex**, no parsean CSS/TSX de
  verdad. Comentarios multilínea sin `*` inicial en cada renglón pueden
  colarse como falso positivo — revisar antes de actuar sobre un hallazgo
  puntual.
- `NEEDS_DECISION` es intencionalmente conservador: prefiere pedir revisión
  humana antes que asumir que un valor crudo es incorrecto o correcto.
