import { useNavigate } from "react-router-dom";
import { Badge } from "../../../components/PageShell";
import { useI18n } from "../../../i18n/i18n";
import { useItemUiStore } from "../../items/store/itemUiStore";
import type { ItemMatchStatus } from "../api/purchaseReceptionService";

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
  const { t } = useI18n();
  const label = t(`purchases.itemMatchStatus.${status}`, status);

  return (
    <Badge
      variant={STATUS_VARIANT[status]}
      label={`${STATUS_ICON[status]} ${label}`}
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
