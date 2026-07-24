# Estándar de Modales (INMUTABLE)

Decisión arquitectónica congelada 2026-06-29. No modificar sin revisión arquitectónica formal.

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
