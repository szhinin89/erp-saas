import { ZHBtn, ZHFormAlert } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";

export function PurchaseSearchFeedback({ status, empty, query, onRetry }: {
  status: string; empty: boolean; query: string; onRetry: () => void;
}) {
  const { t } = useI18n();
  if (status === "error") return <div>
    <ZHFormAlert type="error" message={t("purchases.productPicker.searchError", "Error al consultar productos.")} />
    <ZHBtn type="button" size="sm" onClick={onRetry}>{t("common.retry", "Reintentar")}</ZHBtn>
  </div>;
  if (status !== "loading" && !(status === "ready" && empty)) return null;
  return <div className="zh-picker__empty" role="status" aria-live="polite">
    {status === "loading" ? t("common.searching", "Buscando...") : t("purchases.productPicker.noResults", { query })}
  </div>;
}
