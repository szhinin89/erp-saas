import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { useI18n } from "../../../i18n/i18n";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import type {
  CustomerAdvanceDto,
  CustomerAdvanceMovementDto,
  CustomerAdvanceRefundTransactionDto,
} from "../api/customerAdvanceService";
import type { CustomerAdvanceCatalogs } from "../hooks/useCustomerAdvanceCatalogs";
import {
  movementBalanceSign,
  movementTypeKey,
  movementTypeVariant,
} from "../utils/customerAdvance";

/**
 * Historial del anticipo: libro de movimientos y devoluciones. Nada se oculta ni se presenta como
 * eliminado; lo revertido queda marcado junto a su compensación.
 */
export function CustomerAdvanceHistory({
  advance,
  catalogs,
  receivableLabel,
  canReverseApplication,
  canReverseRefund,
  onReverseApplication,
  onReverseRefund,
}: {
  advance: CustomerAdvanceDto;
  catalogs: CustomerAdvanceCatalogs;
  receivableLabel: (receivableId: string) => string;
  canReverseApplication: boolean;
  canReverseRefund: boolean;
  onReverseApplication: (operationId: string) => void;
  onReverseRefund: (transaction: CustomerAdvanceRefundTransactionDto) => void;
}) {
  const { t } = useI18n();
  const byId = new Map(advance.movements.map((m) => [m.id, m]));
  const reversed = new Set(
    advance.movements.flatMap((m) => (m.reversalOfMovementId ? [m.reversalOfMovementId] : [])),
  );
  const active = advance.status === "Confirmed";

  const movementColumns: ZHDataTableColumn<CustomerAdvanceMovementDto>[] = [
    {
      key: "type",
      header: t("finance.customerAdvances.field.movement"),
      render: (m) => (
        <div className="zh-stack">
          <div>
            <Badge
              variant={movementTypeVariant(m.movementType)}
              label={t(movementTypeKey(m.movementType))}
            />
          </div>
          <span>{formatDate(m.effectiveDate)}</span>
          <span className="zh-text-muted">{formatDateTime(m.createdAtUtc)}</span>
        </div>
      ),
    },
    {
      key: "effect",
      header: t("finance.customerAdvances.field.balanceEffect"),
      align: "right",
      // Efecto sobre el saldo del anticipo, no entrada o salida de dinero.
      render: (m) => {
        const sign = movementBalanceSign(m.movementType);
        return (
          <ZHMoneyValue
            precision="money"
            value={sign * m.amount}
            currencySymbol={sign > 0 ? "+$" : "$"}
          />
        );
      },
    },
    {
      key: "document",
      header: t("finance.customerAdvances.field.receivable"),
      render: (m) => (m.receivableId ? receivableLabel(m.receivableId) : "—"),
    },
    {
      key: "detail",
      header: t("finance.customerAdvances.field.referenceAndReason"),
      render: (m) => {
        const original = m.reversalOfMovementId ? byId.get(m.reversalOfMovementId) : undefined;
        return (
          <div className="zh-stack">
            {m.reference ? (
              <span>
                {t("finance.customerAdvances.field.reference")}: {m.reference}
              </span>
            ) : null}
            {m.reason ? (
              <span>
                {t("finance.customerAdvances.field.reason")}: {m.reason}
              </span>
            ) : null}
            {m.evidenceDate ? (
              <span>
                {t("finance.customerAdvances.field.evidenceDate")}: {formatDate(m.evidenceDate)}
              </span>
            ) : null}
            {original ? (
              <span className="zh-text-muted">
                {t("finance.customerAdvances.history.reverses", {
                  movement: t(movementTypeKey(original.movementType)),
                  date: formatDate(original.effectiveDate),
                })}
              </span>
            ) : null}
          </div>
        );
      },
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (m) => {
        // El ingreso y la devolución muestran su compensación en sus propias acciones.
        if (reversed.has(m.id))
          return (
            <span className="zh-text-muted">{t("finance.customerAdvances.history.reversed")}</span>
          );
        if (m.movementType !== "Application" || !active || !canReverseApplication) return null;
        // La reversa es de la operación completa: una sola acción, en su primera línea.
        if (m.lineNumber !== 1) return null;
        return (
          <ZHBtn
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => onReverseApplication(m.operationId)}
          >
            {t("finance.customerAdvances.reverseApplication.action")}
          </ZHBtn>
        );
      },
    },
  ];

  const refundColumns: ZHDataTableColumn<CustomerAdvanceRefundTransactionDto>[] = [
    {
      key: "type",
      header: t("finance.customerAdvances.field.movement"),
      render: (r) => (
        <div className="zh-stack">
          <span>
            {t(movementTypeKey(r.transactionType === "Refund" ? "Refund" : "ReversalOfRefund"))}
          </span>
          <span className="zh-text-muted">{formatDate(r.effectiveDate)}</span>
        </div>
      ),
    },
    {
      key: "amount",
      header: t("finance.customerAdvances.field.amount"),
      align: "right",
      render: (r) => <ZHMoneyValue precision="money" value={r.amount} />,
    },
    {
      key: "destination",
      header: t("finance.customerAdvances.field.destination"),
      render: (r) => {
        const destination = catalogs.destination(r.companyBankAccountId, r.cashRegisterId);
        return (
          <div className="zh-stack">
            <span>
              {t(
                destination.isCash
                  ? "finance.customerAdvances.field.cashRegister"
                  : "finance.customerAdvances.field.bankAccount",
              )}
              : {destination.name}
            </span>
            <span className="zh-text-muted">{catalogs.methodName(r.paymentMethodId)}</span>
          </div>
        );
      },
    },
    {
      key: "detail",
      header: t("finance.customerAdvances.field.referenceAndReason"),
      render: (r) => (
        <div className="zh-stack">
          <span>
            {t("finance.customerAdvances.field.reference")}: {r.reference}
          </span>
          <span>
            {t("finance.customerAdvances.field.reason")}: {r.reason}
          </span>
        </div>
      ),
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (r) => {
        if (r.isReversed)
          return (
            <span className="zh-text-muted">{t("finance.customerAdvances.history.reversed")}</span>
          );
        if (r.transactionType !== "Refund" || !active || !canReverseRefund) return null;
        return (
          <ZHBtn type="button" variant="ghost" size="sm" onClick={() => onReverseRefund(r)}>
            {t("finance.customerAdvances.reverseRefund.action")}
          </ZHBtn>
        );
      },
    },
  ];

  return (
    <>
      <ZHCard>
        <ZHFieldLabel size="sm">{t("finance.customerAdvances.history.movements")}</ZHFieldLabel>
        <ZHDataTable
          columns={movementColumns}
          rows={advance.movements}
          tableClassName="table--align-top"
          rowKey={(m) => m.id}
          emptyMessage={t("finance.customerAdvances.history.empty")}
        />
      </ZHCard>
      {advance.refundTransactions.length > 0 ? (
        <ZHCard>
          <ZHFieldLabel size="sm">{t("finance.customerAdvances.history.refunds")}</ZHFieldLabel>
          <ZHDataTable
            columns={refundColumns}
            rows={advance.refundTransactions}
            tableClassName="table--align-top"
            rowKey={(r) => r.id}
            emptyMessage={t("finance.customerAdvances.history.empty")}
          />
        </ZHCard>
      ) : null}
    </>
  );
}
