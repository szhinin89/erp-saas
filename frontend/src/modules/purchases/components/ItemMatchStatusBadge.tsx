import { useNavigate } from "react-router-dom";
import { Badge } from "../../../components/PageShell";
import { useItemUiStore } from "../../items/store/itemUiStore";
import type { ItemMatchStatus } from "../api/purchaseReceptionService";

// Estado nunca depende solo del color: badge + icono + texto siempre juntos (accesibilidad).
const STATUS_LABEL: Record<ItemMatchStatus, string> = {
  PENDING: "Sin Item",
  NEEDS_REVIEW: "Revisar",
  AUTO_MATCHED: "Auto",
  MANUALLY_MATCHED: "Manual",
};

const STATUS_ICON: Record<ItemMatchStatus, string> = {
  PENDING: "🔴",
  NEEDS_REVIEW: "🟡",
  AUTO_MATCHED: "🟢",
  MANUALLY_MATCHED: "🔵",
};

const STATUS_VARIANT: Record<
  ItemMatchStatus,
  "green" | "red" | "blue" | "orange"
> = {
  PENDING: "red",
  NEEDS_REVIEW: "orange",
  AUTO_MATCHED: "green",
  MANUALLY_MATCHED: "blue",
};

export function ItemMatchStatusBadge({ status }: { status: ItemMatchStatus }) {
  return (
    <Badge
      variant={STATUS_VARIANT[status]}
      label={`${STATUS_ICON[status]} ${STATUS_LABEL[status]}`}
    />
  );
}

/**
 * Navega a la ficha del ítem ya conciliado — mismo patrón en PurchasesPage para no duplicar la
 * integración con useItemUiStore/react-router en cada lugar que muestra un ítem ya emparejado.
 */
export function useViewMatchedItem(onBeforeNavigate?: () => void) {
  const navigate = useNavigate();
  const startViewItem = useItemUiStore((s) => s.startView);

  return (itemId: string) => {
    startViewItem(itemId);
    onBeforeNavigate?.();
    navigate("/inventory/items");
  };
}
