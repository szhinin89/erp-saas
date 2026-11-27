import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../../../components/PageShell";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHDataTable } from "../../../components/zh/ZHDataTable";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import {
  customerAdvanceService,
  type CustomerAdvanceReceivableApplicationDto,
} from "../api/customerAdvanceService";
import type { SalesReceivableDto } from "../api/receivableService";
import { CUSTOMER_ADVANCE_PERMISSIONS, CUSTOMER_ADVANCES_ROUTE } from "../utils/customerAdvance";
import { ApplyCustomerAdvanceModal } from "./ApplyCustomerAdvanceModal";

/**
 * Anticipos desde una CxC: saldo disponible del cliente, aplicaciones recibidas (distintas de los
 * cobros) y «Aplicar anticipo». La reversa se hace en el detalle, sobre la operación completa.
 */
export function ReceivableCustomerAdvanceModal({
  receivable,
  onClose,
  onApplied,
}: {
  receivable: SalesReceivableDto;
  onClose: () => void;
  onApplied: () => void;
}) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const [applications, setApplications] = useState<CustomerAdvanceReceivableApplicationDto[]>([]);
  const [balance, setBalance] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [applying, setApplying] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [rows, available] = await Promise.all([
        customerAdvanceService.byReceivable(receivable.id),
        customerAdvanceService.balance(receivable.customerId),
      ]);
      setApplications(rows);
      setBalance(available.availableAmount);
    } catch (err: unknown) {
      setError(
        formatApiRequestError(err, { generic: t("finance.customerAdvances.receivable.error") }),
      );
    } finally {
      setLoading(false);
    }
  }, [receivable.id, receivable.customerId, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const canApply =
    canShow(CUSTOMER_ADVANCE_PERMISSIONS.apply) &&
    receivable.balanceDue > 0 &&
    receivable.status !== "cancelled" &&
    (balance ?? 0) > 0;

  return (
    <>
      <ZHModal
        open={!applying}
        onClose={onClose}
        size="lg"
        title={t("finance.customerAdvances.receivable.title")}
        subtitle={`${receivable.invoiceNumber} — ${receivable.customerName}`}
      >
        <ZHGrid cols={1}>
          {error ? <ZHPageNotice variant="error" message={error} /> : null}
          <div>
            <ZHInfoRow
              label={t("finance.customerAdvances.receivable.balance")}
              value={balance === null ? "—" : <ZHMoneyValue precision="money" value={balance} />}
            />
            <ZHInfoRow
              label={t("finance.customerAdvances.field.pending")}
              value={<ZHMoneyValue precision="money" value={receivable.balanceDue} />}
            />
          </div>

          <div>
            {canApply ? (
              <ZHBtn type="button" onClick={() => setApplying(true)}>
                {t("finance.customerAdvances.apply.title")}
              </ZHBtn>
            ) : null}
          </div>
          <ZHDataTable
            rows={applications}
            rowKey={(a) => a.movementId}
            loading={loading}
            emptyMessage={t("finance.customerAdvances.receivable.empty")}
            columns={[
              {
                key: "date",
                header: t("finance.customerAdvances.field.effectiveDate"),
                render: (a) => formatDate(a.effectiveDate),
              },
              {
                key: "amount",
                header: t("finance.customerAdvances.field.amount"),
                align: "right",
                render: (a) => <ZHMoneyValue precision="money" value={a.amount} />,
              },
              {
                key: "state",
                header: t("finance.customerAdvances.field.status"),
                render: (a) => (
                  <Badge
                    variant={a.isReversed ? "gray" : "blue"}
                    label={
                      a.isReversed
                        ? `${t("finance.customerAdvances.receivable.reversed")} ${formatDate(a.reversalDate)}`
                        : t("finance.customerAdvances.receivable.active")
                    }
                  />
                ),
              },
              {
                key: "advance",
                header: "",
                align: "right",
                render: (a) => (
                  <Link
                    className="zh-link"
                    to={`${CUSTOMER_ADVANCES_ROUTE}/${a.customerAdvanceId}`}
                  >
                    {t("finance.customerAdvances.receivable.viewAdvance")}
                  </Link>
                ),
              },
            ]}
          />
        </ZHGrid>
      </ZHModal>
      {applying ? (
        <ApplyCustomerAdvanceModal
          open
          receivable={receivable}
          onClose={() => setApplying(false)}
          onApplied={() => {
            onApplied();
            onClose();
          }}
        />
      ) : null}
    </>
  );
}
