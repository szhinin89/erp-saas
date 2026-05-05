# Plantilla visual ZH Form (proyecto unificado)

## Ubicación canónica (solo en este repo)

La plantilla **no** se mantiene como copia autoritativa fuera del monorepo **`erp-saas`**. La única fuente de verdad del HTML de referencia es esta carpeta:

`erp-saas/docs/zh-form-template/`

Si hay bocetos o archivos en otros directorios (por ejemplo otro proyecto “ZH Components”), los patrones útiles se **trasladan aquí** y a `frontend/src/index.css` + `frontend/src/components/zh/ZHForm.css`. No al revés: el ERP unificado no depende de rutas externas para el diseño base.

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

### Ideas desde otros repos (no son fuente de verdad)

Bocetos tipo **ZHComponents.html** / **ZHComponents.jsx** pueden inspirar tokens o patrones (inputs en superficie blanca, `select` con chevron, addon + botón `:active`, etc.). Eso se **consolida** en `index.css`, `ZHForm.css` y en el `<style>` de `zh_erp_component_library.html` dentro de **`erp-saas`**. No actualizar “la plantilla” solo en carpetas fuera de este proyecto.

## Reglas del proyecto

La regla Cursor **`.cursor/rules/erp-unified-rules.mdc`** (sección ZH Form System) fija esta ruta como única fuente de verdad visual del HTML de plantilla **en el monorepo** `erp-saas`.
