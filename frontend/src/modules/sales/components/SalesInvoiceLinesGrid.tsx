import type { SalesInvoiceDetailDto } from "../api/salesService";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import type { WarehouseDto, ItemWarehouseAvailabilityDto } from "../../inventory/types";
import { SalesInvoiceLineGridRow } from "./SalesInvoiceLineGridRow";

export type LineWithKey = SalesLineFormValues;

interface SalesInvoiceLinesGridProps {
  lines: LineWithKey[];
  backendLines?: SalesInvoiceDetailDto[];
  disabled: boolean;
  readOnly: boolean;
  vatLabel: (code: string) => string;
  vatRates?: Record<string, number>;
  warehouses: WarehouseDto[];
  selectedWarehouseId: string;
  onUpdate: (key: number, field: string, value: unknown) => void;
  onUpdateWarehouse: (
    key: number,
    warehouseId: string,
    option?: ItemWarehouseAvailabilityDto,
  ) => void;
  onUpdatePresentation?: (key: number, packagingLevelId: string) => void;
  onRemove: (key: number) => void;
}

/**
 * SALES-INVOICE-LINES-GRID-UX-01B / COMPACT-DENSITY-01C: grilla horizontal de líneas ya
 * agregadas a la factura, con cabecera de columnas visible — mismo patrón visual Y densidad que
 * el buscador de productos (SalesItemSearchResultsGrid): un único contenedor de tabla
 * (`.sfl-table`) con la cabecera pegada arriba y las filas una debajo de otra, separadas solo
 * por un borde inferior sutil, sin gap ni sombra ni apariencia de tarjetas sueltas.
 * Responsabilidad de este componente: renderizar la cabecera, mapear cada línea a
 * `SalesInvoiceLineGridRow` (que es solo la fila) y mostrar el estado vacío — nada de cálculo ni
 * de estado propio. Local al módulo Sales, no Design System global, por la misma razón que el
 * buscador: depende de datos/reglas propias de Ventas.
 *
 * La cabecera (`.sfl-header`) y cada fila (`.sf-product`, dentro de SalesInvoiceLineGridRow)
 * comparten el mismo `grid-template-columns` a través de la variable CSS `--sfl-cols` (definida
 * una sola vez en `.sf-products`, sales-product-card.css) — así quedan alineadas por
 * construcción, nunca por coincidencia de valores repetidos en dos lugares.
 */
export function SalesInvoiceLinesGrid({
  lines,
  backendLines,
  disabled,
  readOnly,
  vatLabel,
  vatRates,
  warehouses,
  selectedWarehouseId,
  onUpdate,
  onUpdateWarehouse,
  onUpdatePresentation,
  onRemove,
}: SalesInvoiceLinesGridProps) {
  if (lines.length === 0) {
    return (
      <div className="sf-products-empty">
        <span className="material-symbols-outlined sf-products-empty__icon">
          add_shopping_cart
        </span>
        <p>Busca un producto arriba para agregarlo a la factura</p>
      </div>
    );
  }

  return (
    // SALES-INVOICE-LINES-GRID-COMPACT-DENSITY-01C: cabecera + filas comparten un único
    // contenedor de tabla (.sfl-table, sales-product-card.css) — sin gap entre ellas, para que
    // se vean como una grilla continua (mismo criterio que .sf-search-dropdown en el buscador),
    // no como tarjetas sueltas espaciadas.
    <div className="sfl-table">
      <div className="sfl-header" role="row">
        <span className="sfl-header__cell">Línea</span>
        <span className="sfl-header__cell">Producto</span>
        <span className="sfl-header__cell">Precio lista</span>
        <span className="sfl-header__cell">Descuento</span>
        <span className="sfl-header__cell">Precio facturado</span>
        <span className="sfl-header__cell">Stock / Ubicación</span>
        <span className="sfl-header__cell">Cantidad</span>
        <span className="sfl-header__cell">Total</span>
      </div>

      {lines.map((l, idx) => (
        <SalesInvoiceLineGridRow
          key={l._key}
          index={idx}
          line={l}
          backendLine={backendLines?.[idx]}
          disabled={disabled}
          readOnly={readOnly}
          vatLabel={vatLabel(l.vatCode)}
          vatRates={vatRates}
          warehouses={warehouses}
          selectedWarehouseId={selectedWarehouseId}
          onUpdate={onUpdate}
          onUpdateWarehouse={onUpdateWarehouse}
          onUpdatePresentation={onUpdatePresentation}
          onRemove={onRemove}
        />
      ))}
    </div>
  );
}
