import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Badge, PageShell } from "../../../components/PageShell";
import { ZHBtn, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import {
  customerAdvanceService,
  type CustomerAdvanceDto,
  type CustomerAdvanceRefundTransactionDto,
} from "../api/customerAdvanceService";
import { ApplyCustomerAdvanceModal } from "../components/ApplyCustomerAdvanceModal";
import {
  CustomerAdvanceCompensationModal,
  type CompensationTarget,
} from "../components/CustomerAdvanceCompensationModal";
import { CustomerAdvanceHistory } from "../components/CustomerAdvanceHistory";
import { RefundCustomerAdvanceModal } from "../components/RefundCustomerAdvanceModal";
import { useCustomerAdvanceCatalogs } from "../hooks/useCustomerAdvanceCatalogs";
import { useCustomerReceivables } from "../hooks/useCustomerReceivables";
import {
  advanceStatusKey,
  advanceStatusVariant,
  CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS,
  CUSTOMER_ADVANCE_PERMISSIONS,
  CUSTOMER_ADVANCES_ROUTE,
  isIntactAdvance,
} from "../utils/customerAdvance";

/**
 * Detalle de un anticipo: datos del ingreso, saldo oficial (nunca recalculado aquí), historial y
 * las operaciones que permiten el estado y los permisos. El servidor decide la elegibilidad.
 */
export function CustomerAdvanceDetailPage() {
  const { t } = useI18n();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const catalogs = useCustomerAdvanceCatalogs();
  const [advance, setAdvance] = useState<CustomerAdvanceDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [applying, setApplying] = useState(false);
  const [refunding, setRefunding] = useState(false);
  const [compensation, setCompensation] = useState<CompensationTarget | null>(null);
  const hasApplications = !!advance?.movements.some((m) => m.receivableId);
  const canListReceivables = canShow(CUSTOMER_ADVANCE_LOOKUP_PERMISSIONS.receivables);
  const {
    receivables,
    hasMore,
    loading: loadingReceivables,
    loadMore,
    reload: reloadReceivables,
  } = useCustomerReceivables(
    hasApplications && canListReceivables ? (advance?.customerId ?? null) : null,
  );

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError("");
    try {
      setAdvance(await customerAdvanceService.getById(id));
    } catch (err: unknown) {
      setError(formatApiRequestError(err, { generic: t("finance.customerAdvances.detail.error") }));
    } finally {
      setLoading(false);
    }
  }, [id, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const back = (
    <ZHBtn type="button" variant="ghost" onClick={() => navigate(CUSTOMER_ADVANCES_ROUTE)}>
      {t("common.back")}
    </ZHBtn>
  );

  if (!advance) {
    return (
      <PageShell title={t("finance.customerAdvances.detail.title")} action={back}>
        {loading ? <p>{t("common.loading")}</p> : null}
        {error ? (
          <>
            <ZHPageNotice variant="error" message={error} />
            <ZHBtn type="button" variant="ghost" onClick={() => void load()}>
              {t("common.retry")}
            </ZHBtn>
          </>
        ) : null}
      </PageShell>
    );
  }

  const receivableLabel = (receivableId: string) =>
    receivables.find((r) => r.id === receivableId)?.invoiceNumber ??
    t("finance.customerAdvances.history.receivableUnavailable");
  const destination = catalogs.destination(advance.companyBankAccountId, advance.cashRegisterId);
  const destinationText = `${t(
    destination.isCash
      ? "finance.customerAdvances.field.cashRegister"
      : "finance.customerAdvances.field.bankAccount",
  )}: ${destination.name}`;
  const hasBalance = advance.status === "Confirmed" && advance.availableAmount > 0;
  const intact = isIntactAdvance(advance);
  const canRefund = canShow(CUSTOMER_ADVANCE_PERMISSIONS.refund);
  const canReverse = canShow(CUSTOMER_ADVANCE_PERMISSIONS.reverse);

  const onUpdated = (updated: CustomerAdvanceDto) => {
    setAdvance(updated);
    reloadReceivables();
  };

  const wholeAdvance = (kind: "reverseIncome" | "cancel") =>
    setCompensation({
      kind,
      targetId: "",
      amount: advance.originalAmount,
      originalDate: advance.advanceDate,
      destination: destinationText,
    });

  const reverseApplication = (operationId: string) => {
    const lines = advance.movements.filter(
      (m) => m.movementType === "Application" && m.operationId === operationId,
    );
    setCompensation({
      kind: "reverseApplication",
      targetId: operationId,
      amount: lines.reduce((sum, m) => Math.round((sum + m.amount) * 100) / 100, 0),
      originalDate: lines[0]?.effectiveDate ?? advance.advanceDate,
      affected: lines.map((m) => ({
        label: receivableLabel(m.receivableId ?? ""),
        amount: m.amount,
      })),
    });
  };

  const reverseRefund = (transaction: CustomerAdvanceRefundTransactionDto) => {
    const original = catalogs.destination(
      transaction.companyBankAccountId,
      transaction.cashRegisterId,
    );
    setCompensation({
      kind: "reverseRefund",
      targetId: transaction.id,
      amount: transaction.amount,
      originalDate: transaction.effectiveDate,
      destination: `${t(
        original.isCash
          ? "finance.customerAdvances.field.cashRegister"
          : "finance.customerAdvances.field.bankAccount",
      )}: ${original.name}`,
    });
  };

  // Lo que decide la siguiente operación: importes y estado.
  const highlights: [string, React.ReactNode][] = [
    [
      "originalAmount",
      <ZHMoneyValue
        key="o"
        precision="money"
        value={advance.originalAmount}
        emphasis="grand"
        align="start"
      />,
    ],
    [
      "available",
      <ZHMoneyValue
        key="a"
        precision="money"
        value={advance.availableAmount}
        emphasis="grand"
        align="start"
      />,
    ],
    [
      "status",
      <div key="s">
        <Badge
          variant={advanceStatusVariant(advance.status)}
          label={t(advanceStatusKey(advance.status))}
        />
      </div>,
    ],
  ];
  const rows: [string, string][] = [
    ["customer", advance.customerName ?? "—"],
    ["advanceDate", formatDate(advance.advanceDate)],
    ["branch", catalogs.branchName(advance.branchId)],
    ["paymentMethod", catalogs.methodName(advance.paymentMethodId)],
    ["destination", destinationText],
    ["reference", advance.externalReference ?? "—"],
    ["reason", advance.note ?? "—"],
  ];

  return (
    <PageShell
      kicker={t("finance.customerAdvances.title")}
      title={t("finance.customerAdvances.detail.title")}
      subtitle={advance.customerName ?? undefined}
      action={back}
    >
      {error ? <ZHPageNotice variant="error" message={error} /> : null}
      <ZHCard>
        <ZHGrid cols={1}>
          <ZHGrid cols={3}>
            {highlights.map(([field, value]) => (
              <div className="zh-stack" key={field}>
                <span className="zh-summary-grid__label">
                  {t(`finance.customerAdvances.field.${field}`)}
                </span>
                {value}
              </div>
            ))}
          </ZHGrid>
          <div className="zh-summary-grid">
            {rows.map(([field, value]) => (
              <div key={field}>
                <span className="zh-summary-grid__label">
                  {t(`finance.customerAdvances.field.${field}`)}
                </span>
                <span className="zh-summary-grid__value">{value}</span>
              </div>
            ))}
          </div>
        </ZHGrid>
        <div className="zh-actions">
          {hasBalance && canShow(CUSTOMER_ADVANCE_PERMISSIONS.apply) ? (
            <ZHBtn type="button" onClick={() => setApplying(true)}>
              {t("finance.customerAdvances.apply.title")}
            </ZHBtn>
          ) : null}
          {hasBalance && canRefund ? (
            <ZHBtn type="button" variant="secondary" onClick={() => setRefunding(true)}>
              {t("finance.customerAdvances.refund.title")}
            </ZHBtn>
          ) : null}
          {intact && canRefund ? (
            <ZHBtn type="button" variant="secondary" onClick={() => wholeAdvance("reverseIncome")}>
              {t("finance.customerAdvances.reverseIncome.title")}
            </ZHBtn>
          ) : null}
          {intact && canReverse && !advance.cashRegisterId ? (
            <ZHBtn type="button" variant="ghost" onClick={() => wholeAdvance("cancel")}>
              {t("finance.customerAdvances.cancel.title")}
            </ZHBtn>
          ) : null}
        </div>
      </ZHCard>

      {hasApplications && !canListReceivables ? (
        <ZHPageNotice
          variant="warning"
          message={t("finance.customerAdvances.access.receivables")}
        />
      ) : null}
      {hasMore &&
      advance.movements.some(
        (m) => m.receivableId && !receivables.some((r) => r.id === m.receivableId),
      ) ? (
        <ZHBtn type="button" variant="ghost" disabled={loadingReceivables} onClick={loadMore}>
          {t("finance.customerAdvances.history.loadMoreDocuments")}
        </ZHBtn>
      ) : null}

      <CustomerAdvanceHistory
        advance={advance}
        catalogs={catalogs}
        receivableLabel={receivableLabel}
        canReverseApplication={canReverse}
        canReverseRefund={canRefund}
        onReverseApplication={reverseApplication}
        onReverseRefund={reverseRefund}
      />

      <ApplyCustomerAdvanceModal
        open={applying}
        advance={advance}
        onClose={() => setApplying(false)}
        onApplied={onUpdated}
      />
      <RefundCustomerAdvanceModal
        open={refunding}
        advance={advance}
        onClose={() => setRefunding(false)}
        onRefunded={onUpdated}
      />
      <CustomerAdvanceCompensationModal
        advance={advance}
        target={compensation}
        onClose={() => setCompensation(null)}
        onCompensated={onUpdated}
      />
    </PageShell>
  );
}
