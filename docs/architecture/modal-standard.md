# Estándar de Modales (INMUTABLE)

Decisión arquitectónica congelada 2026-06-29. No modificar sin revisión arquitectónica formal.

## Addendum responsive — BUG REAL (2026-10-13)

Corrección del contrato raíz autorizada en la fase Design System / Modales Responsive.
Conserva las APIs y la lógica funcional de los componentes.

- `ZHModal`, `ZHConfirmModal`, `ZHPromptModal` y `ZHDrawer`: ancho contraíble dentro del viewport; body con `min-width: 0`, `min-height: 0`, scroll vertical y sin scroll horizontal por defecto. Modales limitados por altura dinámica disponible y gutter del overlay.
- Header y footer fuera del scroll del body; títulos/subtítulos completos con wrap. Footer permite wrap; botones directos ocupan una fila cada uno hasta 640 px. Las agrupaciones de acciones deben usar primitives flex con wrap existentes.
- Grids de campos/detalle: tracks `minmax(0, 1fr)`, hijos contraíbles; spans de dos columnas se desactivan al pasar a una columna. Resumen auto-fit limita su mínimo al ancho disponible.
- Texto largo, emails, UUID y claves SRI pueden romper palabras. Montos y variantes de códigos cortos conservan `nowrap`.
- `zh-description-grid`: primitive compartido label/valor en dos tracks contraíbles. SalesIssueModal lo adopta en confirmación y éxito; se elimina la definición local duplicada, sin CSS correctivo de Sales.
- Scroll horizontal únicamente dentro de regiones explícitas existentes (`table-scroll` / `table-card`, también utilizables para XML); no añadir scroll horizontal al body del overlay.

Validación focal: `ZHOverlayGeometry.test.tsx`, Chromium real con markup de los componentes React y CSS/tokens reales, altura 720 px y anchos 320 / 375 / 768 / 1280. Modal sm/full, drawer, confirmación y prompt: 20/20 PASS. Clave de 49 dígitos, UUID/email largo, cinco acciones, contenido vertical y tabla de 1600 px con scroll propio. Antes del fix: 19/20 FAIL (fixture de tabla inicial: 900 px). Regresión focal ZHModal + SalesIssueModal: 19/19 PASS. ESLint focal y diff check PASS. Sin suites completas, commit ni push.

STRUCTURAL DEBT: consumidores con layouts o agrupaciones personalizados fuera de los casos focales no fueron auditados. No incluye smoke autenticado de Sales ni otras alturas de viewport.

---

## Componente oficial: `ZHModal`

```tsx
import { ZHModal } from 'components/zh/ZHModal';

<ZHModal
  open={open}
  onClose={handleClose}
  size="md"
  title="Título"
  subtitle="Descripción"
  footer={<ZHFormActions ... />}
>
  {/* contenido del body */}
</ZHModal>
```

### Props

| Prop | Tipo | Default | Descripción |
|------|------|---------|-------------|
| `open` | `boolean` | — | Controla visibilidad |
| `onClose` | `() => void` | — | Callback de cierre |
| `size` | `ZHModalSize` | `'md'` | Tamaño del modal |
| `title` | `string` | — | Título del header |
| `subtitle` | `string?` | — | Subtítulo del header |
| `children` | `ReactNode` | — | Contenido del body |
| `footer` | `ReactNode?` | — | Footer con acciones |
| `closeLabel` | `string?` | `'Cerrar'` | Aria label del botón X |
| `closeOnBackdrop` | `boolean?` | `true` | Cerrar al click en overlay |

---

## Tamaños oficiales

| Size | Max-width | Uso típico |
|------|-----------|------------|
| `sm` | 420px | Confirmaciones, formularios simples |
| `md` | 520px | CRUD estándar |
| `lg` | min(720px, 95vw) | Formularios complejos |
| `xl` | min(900px, 95vw) | Multi-sección (Sucursales) |
| `2xl` | min(1080px, 95vw) | Layouts 2 columnas (Perfiles) |
| `full` | 100vw - 32px | Full screen |

---

## Comportamiento automático

ZHModal maneja internamente:

- **Overlay** con backdrop-filter blur
- **Animación** de entrada (fade + slide)
- **ESC** cierra el modal
- **Click en backdrop** cierra (configurable)
- **Body scroll lock** durante apertura
- **Focus automático** al primer input
- **Scroll del body** cuando el contenido excede `max-height: 90vh`
- **Header fijo + footer fijo** — solo el body scrollea
- **Responsive** — padding y max-width se ajustan en `<640px`

---

## Confirmaciones: `ZHConfirmModal` / `ZHPromptModal`

Para confirmaciones simples, usar `message.confirm()` (API de mensajes).

Para confirmaciones inline, usar `ZHConfirmModal` directamente:

```tsx
<ZHConfirmModal open={open} variant="danger" title="Anular"
  message="¿Está seguro?" onConfirm={handle} onCancel={close} />
```

Variantes: `danger`, `warning`, `default`.

---

## CSS

| Clase | Archivo | Propósito |
|-------|---------|-----------|
| `.zh-modal-overlay` | zh-ui.css | Overlay con blur |
| `.zh-modal` | zh-ui.css | Container base |
| `.zh-modal--{size}` | zh-ui.css | Variantes de tamaño |
| `.zh-modal-body` | zh-ui.css | Body con scroll |
| `.zh-modal-footer` | zh-ui.css | Footer fijo con acciones |
| `.zh-form-header` | zh-ui.css | Header con gradiente azul |
| `.zh-form-header-close` | zh-ui.css | Botón X del header |
| `.zh-confirm-*` | zh-ui.css | Estilos del confirm dialog |

---

## Prohibido

- Crear modales con `position: fixed` + estilos inline
- Usar `zh-modal-header` + `zh-modal-close` manual (usar `ZHModal`)
- Usar `prd-modal`, `pg-modal--*` (eliminados)
- Hardcodear z-index, colores, border-radius en modales
- Crear componentes `*ConfirmModal` locales por módulo
- Importar `ZHModalHeader` (eliminado — `ZHModal` incluye el header)

---

## Architecture Gate

| # | Criterio | Estado |
|---|----------|--------|
| MOD-1 | Todo modal usa `ZHModal` o `ZHConfirmModal` | obligatorio |
| MOD-2 | No existen estilos inline de modal (position fixed + backdrop) | obligatorio |
| MOD-3 | Tamaño viene de `size` prop, no de clases CSS ad-hoc | obligatorio |
| MOD-4 | Footer con acciones usa `footer` prop, no div manual | obligatorio |
| MOD-5 | No existe `ZHModalHeader` ni wrappers manuales | obligatorio |
| MOD-6 | No existen clases `prd-modal`, `pg-modal--*` | obligatorio |
