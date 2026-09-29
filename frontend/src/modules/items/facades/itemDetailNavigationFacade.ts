/**
 * itemDetailNavigationFacade — superficie pública para abrir la ficha de un ítem en modo
 * solo-lectura desde otros módulos (purchases: producto conciliado en recepción/factura).
 *
 * Encapsula el estado de UI privado de items (itemUiStore) y la ruta del catálogo; los
 * módulos externos nunca importan items/store directamente.
 */
import { useNavigate } from "react-router-dom";
import { useItemUiStore } from "../store/itemUiStore";

export function useOpenItemDetail() {
  const navigate = useNavigate();
  const startViewItem = useItemUiStore((s) => s.startView);

  return (itemId: string) => {
    startViewItem(itemId);
    navigate("/products/items");
  };
}
