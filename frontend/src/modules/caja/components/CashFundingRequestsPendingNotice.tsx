import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { cashFundingRequestService } from "../api/cashFundingRequestService";
import { CASH_FUNDING_REQUESTS_ROUTE } from "../facades/cashFundingRequestFacade";

/**
 * ZH-CASH-FUNDING-REQUEST-UI-FINAL-02E-EF — aviso en Caja: "Solicitudes de efectivo: N [Ver]".
 * N es el `totalCount` de la bandeja filtrada por Pending (sin endpoint de conteo propio). Solo con
 * `caja.funding-requests.view` y si hay pendientes; nunca incrusta el listado en Caja.
 */
export function CashFundingRequestsPendingNotice() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { has } = usePermissionsUi();
  const canView = has("caja.funding-requests.view");
  const [pending, setPending] = useState(0);

  useEffect(() => {
    if (!canView) {
      setPending(0);
      return;
    }
    let cancelled = false;
    cashFundingRequestService
      .list(1, 1, { status: "Pending" })
      .then((page) => {
        if (!cancelled) setPending(page.totalCount);
      })
      .catch(() => {
        if (!cancelled) setPending(0);
      });
    return () => {
      cancelled = true;
    };
  }, [canView]);

  if (!canView || pending <= 0) return null;

  return (
    <div className="cj-toolbar" role="status">
      <strong>{t("caja.fundingRequests.pending", { count: String(pending) })}</strong>
      <ZHBtn type="button" variant="secondary" size="sm" onClick={() => navigate(`${CASH_FUNDING_REQUESTS_ROUTE}?tab=pending`)}>
        {t("caja.fundingRequests.view")}
      </ZHBtn>
    </div>
  );
}
