import { ItemLookupPicker } from "../../items/facades/itemPickerFacade";

/** Producto elegido para una línea de documento de inventario (ajuste, transferencia). */
export type StockItemProfile = {
  id: string;
  sku: string;
  name: string;
  /** Unidad base del ítem: los ajustes la usan para resolver la equivalencia de presentación. */
  baseUomCode: string;
};

type Props = {
  onSelect: (profile: StockItemProfile) => void;
  placeholder: string;
  emptyText: (query: string) => string;
  disabled?: boolean;
};

/**
 * ZH-PRODUCT-SELECTOR-SSOT-01 — picker de producto de los documentos de inventario (antes
 * `AdjustmentProductPicker` y `TransferProductPicker`, dos copias del mismo componente). Usa el
 * picker del owner `items` (`itemPickerFacade` → `ItemLookupPicker`) y solo agrega la regla propia
 * de Inventario —un ítem sin control de stock no tiene saldo que ajustar o mover— pedida al
 * backend (`tracksStock`, filtrada antes de paginar: ZH-INVENTORY-STOCK-ITEM-LOOKUP-01) y el
 * perfil de línea. No consulta existencias: el saldo por
 * bodega es otra capacidad (stockService / warehouse-availability).
 */
export function StockItemPicker({ onSelect, placeholder, emptyText, disabled }: Props) {
  return (
    <ItemLookupPicker
      placeholder={placeholder}
      emptyText={emptyText}
      disabled={disabled}
      pageSize={12}
      tracksStock
      onSelect={(item) =>
        onSelect({
          id: item.id,
          sku: item.sku,
          name: item.shortName,
          baseUomCode: item.defaultUomCode,
        })
      }
    />
  );
}
