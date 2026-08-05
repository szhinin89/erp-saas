# Design System SSOT — Frontend ERP

## Objetivo

El frontend del ERP debe tener una sola fuente de verdad visual.

## Fuentes oficiales

### 1. `frontend/src/styles/design-tokens.css`

Única fuente para:

- colores
- sombras
- radios
- spacing
- tipografía
- z-index
- transiciones
- superficies semánticas

Ningún módulo debe definir valores visuales base por su cuenta.

### 2. `frontend/src/styles/zh-ui.css`

Única fuente para componentes visuales reutilizables:

- botones
- inputs
- selects
- textareas
- campos
- cards
- badges
- tablas
- modales
- drawers
- alerts
- grids
- toolbars
- estados vacíos
- utilidades de layout

Toda pantalla nueva debe preferir clases `zh-*`.

### 3. CSS local de módulo o página

Permitido solo para layout específico de la pantalla.

Ejemplos permitidos:

- distribución propia de una página
- grid específico del documento
- ancho de columnas de una pantalla concreta
- ajustes de composición no reutilizables

No permitido:

- colores hardcodeados
- sombras hardcodeadas
- border-radius hardcodeado
- botones propios
- badges propios
- inputs propios
- tablas propias
- cards genéricas propias
- estilos inline estáticos

## CSS legacy

Estos archivos existen por compatibilidad y están en migración:

- `frontend/src/styles/shared/legacy-pages.css`
- `frontend/src/styles/page-template.css`
- `frontend/src/styles/shared/erp-form-core.css`
- `frontend/src/styles/shared/items-catalog.css`

No deben usarse en pantallas nuevas salvo autorización técnica explícita.

## Orden global de carga

El único punto de entrada global es:

```css
frontend/src/index.css