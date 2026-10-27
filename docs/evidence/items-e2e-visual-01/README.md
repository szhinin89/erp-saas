# Ítems — E2E visual Product y Service

Fecha: 2026-10-27. Clasificación: **BUG REAL** (2 corregidos); E2E visual final **48/48 PASS**.
SSOT: [Ítems](../../architecture/items.md). Cierra el límite «Guardar/reabrir en ERP real
pendiente» de la [alineación de formularios](../items-forms-alignment-01/README.md). Sin cambios
de backend, contratos API, migraciones ni reglas fiscales; sin commit, push ni deploy.

## Entorno

- Base `erp_zh_test` recreada con la baseline vigente (`20261021162651_InitialProductionBaseline`)
  por autorización expresa del propietario; usuario `e2e.admin` del seed E2E con credencial
  efímera no almacenada. Otras bases sin cambios.
- API aislada en `:5004` (Development, seed E2E, JWT efímero), detenida al terminar.
- Chromium (Playwright 1.59) sobre el frontend de desarrollo; `/api/**` redirigido a `:5004`.

## Flujo verificado (Product y Service)

Crear → guardar → recargar → reabrir en «Ver detalle» → editar (nombre comercial, descripción,
tarifa IVA venta, precio) → guardar → recargar → reabrir en edición. Se conservan SKU, nombre
comercial, descripción completa, tipo, naturaleza operativa, marca, categoría, unidad base,
IVA venta/compra, código de barras y precio base. Detalle: [visual-results.json](visual-results.json).

| SKU | Naturaleza | Precio base persistido | IVA venta | Unidad |
|---|---|---|---|---|
| `VIS-PRD-762834` | Product | 13.750000 | 0 | 07 |
| `VIS-SRV-762834` | Service | 45.000000 | 6 | 01 |

## Bugs corregidos

| Bug | Síntoma | Causa | Corrección | Prueba |
|---|---|---|---|---|
| Precio base perdido al reabrir | El campo «Precio ingresado» quedaba vacío con «El precio base (PVP) es obligatorio.» aunque `base_sale_price` estaba persistido; ninguna edición podía guardarse sin reescribirlo | `reset()` entrega precio y tarifa IVA en el mismo commit; el efecto derivado de tarifa/modo persistía el precio calculado desde el input aún vacío | `PricingTab` omite la persistencia derivada solo en el commit que sincroniza el precio persistido | `PricingTab.test.tsx` «al reabrir un ítem…»: FAIL sin la corrección, 17/17 PASS con ella |
| Mensajes de validación sin traducir | Se mostraban claves crudas (`items.validation.brandRequired`, `categoryNodeRequired`) | 13 claves `items.validation.*` de los esquemas sin entrada en `es.json`/`en.json` | Claves añadidas en ambos idiomas | Navegador: 0 claves crudas; «La marca es obligatoria.» / «La categoría es obligatoria.» visibles |

Capturas: [validación](validation-messages.png), [Product reabierto](product-reopened-after-edit.png),
[Service reabierto](service-reopened-after-edit.png).

## Gates

ESLint (archivos tocados), `tsc -b`, `npm run build`, `architecture:check` (0 violaciones nuevas)
y `git diff --check` PASS. Suite completa no ejecutada.
