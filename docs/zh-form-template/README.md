# Plantilla visual ZH Form (referencia en repo)

## Archivo principal

- **`zh_erp_component_library.html`** — abrir en el navegador (doble clic o arrastrar al Chrome/Edge). Es **HTML estático** con CSS embebido: no requiere Vite ni servidor.

## Propósito

- Referencia visual única para **tokens** (`:root`) y piezas del **ZH Form System** alineadas con:
  - `frontend/src/index.css` (`:root`, alias `--B` / `--BD` / …)
  - `frontend/src/components/zh/ZHForm.css` (clases `.zh-form-*`, `.zh-btn`, etc.)

## Mantenimiento

1. **Cambios de paleta o radios** en `frontend/src/index.css` → copiar/ajustar el bloque `:root` (y alias) dentro del `<style>` del HTML.
2. **Cambios de layout de componentes ZH** (secciones, campos, botones) → reflejarlos en el `<style>` del HTML y/o en el markup de ejemplo del mismo archivo.
3. Las pantallas reales siguen usando **React** (`ZHForm.tsx`, etc.); esta plantilla no sustituye al código, solo documenta el look esperado.

## Reglas del proyecto

La regla Cursor **`.cursor/rules/erp-unified-rules.mdc`** (sección ZH Form System) apunta a esta ruta como fuente de verdad visual **dentro del monorepo** `erp-saas`.
