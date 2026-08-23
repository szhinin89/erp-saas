import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHToggleTile } from "../../../components/zh/ZHToggleTile";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { ZhDecimalInput } from "../../../components/zh/inputs";
import { formatMoney } from "../../../lib/sanitizers";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import type { SalesPageContext } from "../hooks/useSalesPage";
import { remainingToCollect } from "./paymentRemaining";

export interface PaymentMethodsSectionProps {
  ctx: SalesPageContext;
}

// ── Payment Methods Section ─────────────────────────────────────────────
export function PaymentMethodsSection({ ctx }: PaymentMethodsSectionProps) {
  return (
    <div className="sf-sidebar__section">
      <div className="sf-sidebar__header zh-section-title">
        <span className="material-symbols-outlined sf-sidebar__header-icon">
          payments
        </span>
        Formas de Cobro
        <ZHFieldHelp helpKey={HELP_KEYS.SALES_PAYMENTS_SECTION} />
        {!ctx.readOnly && ctx.payments.length > 0 && (
          <span
            className="material-symbols-outlined sf-sidebar__header-right zh-icon-md"
            title="Limpiar cobros"
            onClick={() => ctx.setInvoicePayments([])}
          >
            delete_sweep
          </span>
        )}
      </div>
      {ctx.readOnly ? (
        <div className="sales-payment-readonly-list">
          {(ctx.editing?.payments ?? [])
            .filter((p) => p.amount > 0)
            .map((p) => (
              <div key={p.id} className="sales-payment-chip">
                {p.paymentMethodName}{" "}
                <span className="sales-payment-chip__amount">
                  <ZHMoneyValue
                    value={p.amount}
                    decimals={getDecimalConfig().totalAmount}
                  />
                </span>
              </div>
            ))}
        </div>
      ) : (
        <>
          {ctx.isCreditTerm &&
            !ctx.payments.some((p) =>
              ctx.paymentMethods.find(
                (pm) => pm.id === p.paymentMethodId && pm.isCreditAllowed,
              ),
            ) && (
              <ZHPageNotice
                variant="info"
                message="Esta venta es a crédito. Seleccione el método de pago Crédito para continuar."
              />
            )}
          <div className="sales-payment-grid">
            {ctx.paymentMethods.map((pm) => {
              const entries = ctx.payments.filter(
                (ip) => ip.paymentMethodId === pm.id,
              );
              const entry = entries[0];
              const totalForMethod = entries.reduce(
                (s, e) => s + (e.amount || 0),
                0,
              );
              const hasValue = totalForMethod > 0;
              const isCredit = pm.isCreditAllowed;
              // BUGFIX-SALES-CREDIT-PAYMENT-CONSISTENCY-01: el backend bloquea Contado + método
              // Crédito (AuthorizeSalesInvoiceHandler) — se deshabilita aquí para prevenir el
              // intento, nunca reemplaza esa validación.
              const creditTileDisabled =
                ctx.fieldDisabled || (isCredit && !ctx.isCreditTerm);
              const calcRemaining = () =>
                Math.max(0, remainingToCollect(ctx, pm.id));

              return (
                <div key={pm.id} className="sales-payment-method">
                  <ZHToggleTile
                    active={hasValue}
                    disabled={creditTileDisabled}
                    title={pm.name}
                    subtitle={
                      isCredit && !ctx.isCreditTerm
                        ? "Requiere condición de pago a crédito"
                        : undefined
                    }
                    onClick={() => {
                      if (creditTileDisabled) return;
                      if (isCredit) {
                        const rem = calcRemaining();
                        ctx.setCreditAmount(rem);
                        ctx.setCreditRows(ctx.simulateCreditInstallments(rem));
                        ctx.setModalCredit(true);
                      } else if (pm.requiresReference) {
                        ctx.setDetailMethodId(pm.id);
                        ctx.setDetailMethodType(pm.detailType);
                        ctx.setDetailMethodName(pm.name);
                        const existing = ctx.payments.filter(
                          (p) => p.paymentMethodId === pm.id,
                        );
                        if (existing.length > 0) {
                          ctx.setDetailRows(
                            existing.map((e, i) => ({
                              _k: i + 1,
                              amount: e.amount,
                              card:
                                pm.detailType === "Card"
                                  ? (e.cardDetail ?? {})
                                  : undefined,
                              transfer:
                                pm.detailType === "Transfer"
                                  ? (e.transferDetail ?? {})
                                  : undefined,
                              cheque:
                                pm.detailType === "Check"
                                  ? (e.chequeDetail ?? {})
                                  : undefined,
                            })),
                          );
                          ctx.setDetailKey(existing.length + 1);
                        } else {
                          ctx.setDetailRows([]);
                          ctx.setDetailKey(1);
                        }
                        ctx.setModalDetail(true);
                      } else if (!hasValue) {
                        const rem = calcRemaining();
                        if (rem > 0) {
                          ctx.setInvoicePayments((prev) => [
                            ...prev,
                            {
                              _key: ctx.payKey,
                              paymentMethodId: pm.id,
                              amount: rem,
                              reference: null,
                            },
                          ]);
                          ctx.setPayKey((k) => k + 1);
                        }
                      }
                    }}
                  />
                  {hasValue && !isCredit && !pm.requiresReference && (
                    <div className="sales-payment-amount-row">
                      <span className="sales-payment-dollar">$</span>
                      <ZhDecimalInput
                        decimals={getDecimalConfig().totalAmount}
                        positiveOnly
                        defaultValue={formatMoney(
                          entry!.amount,
                          getDecimalConfig().totalAmount,
                        )}
                        disabled={ctx.fieldDisabled}
                        onBlur={(e) => {
                          const val = Number(e.target.value) || 0;
                          if (val > 0) {
                            ctx.setInvoicePayments((prev) =>
                              prev.map((p) =>
                                p._key === entry!._key
                                  ? { ...p, amount: val }
                                  : p,
                              ),
                            );
                          } else {
                            ctx.setInvoicePayments((prev) =>
                              prev.filter((p) => p._key !== entry!._key),
                            );
                          }
                        }}
                        className="sales-payment-input"
                      />
                      <ZHIconButton
                        icon="close"
                        title="Eliminar pago"
                        variant="danger"
                        onClick={() =>
                          ctx.setInvoicePayments((prev) =>
                            prev.filter((p) => p._key !== entry!._key),
                          )
                        }
                      />
                    </div>
                  )}
                  {hasValue && pm.requiresReference && !isCredit && (
                    <span className="sales-payment-ref-amount">
                      <ZHMoneyValue
                        value={totalForMethod}
                        decimals={getDecimalConfig().totalAmount}
                      />{" "}
                      <span className="sales-payment-ref-count">
                        ({entries.length})
                      </span>
                    </span>
                  )}
                  {hasValue && isCredit && (
                    <span
                      className="sales-payment-credit-amount"
                      onClick={() => {
                        ctx.setCreditAmount(entry!.amount);
                        ctx.setCreditRows(
                          ctx.simulateCreditInstallments(entry!.amount),
                        );
                        ctx.setModalCredit(true);
                      }}
                    >
                      <ZHMoneyValue
                        value={entry!.amount}
                        decimals={getDecimalConfig().totalAmount}
                      />
                    </span>
                  )}
                </div>
              );
            })}
          </div>
          {ctx.cashDue > 0 && (
            <div
              className={`sales-cash-box${ctx.cashInsufficient ? " sales-cash-box--insufficient" : ""}`}
            >
              <div className="sales-cash-box__row">
                <span className="sales-cash-box__label">
                  Monto recibido (Efectivo):
                </span>
                <ZHFieldHelp helpKey={HELP_KEYS.SALES_PAYMENTS_CASH_RECEIVED} />
                <div className="sales-cash-box__input-wrap">
                  <span className="sales-cash-box__currency">$</span>
                  <ZhDecimalInput
                    decimals={getDecimalConfig().totalAmount}
                    positiveOnly
                    defaultValue={
                      ctx.cashReceived > 0
                        ? formatMoney(
                            ctx.cashReceived,
                            getDecimalConfig().totalAmount,
                          )
                        : ""
                    }
                    disabled={ctx.fieldDisabled}
                    onBlur={(e) =>
                      ctx.setCashReceived(Number(e.target.value) || 0)
                    }
                    className="sales-cash-input"
                  />
                </div>
              </div>
              <div
                className={`sales-cash-box__total-row${ctx.cashInsufficient ? " sales-cash-box__total-row--insufficient" : ""}`}
              >
                <span>
                  {ctx.cashInsufficient ? "✗ Insuficiente" : "Vuelto:"}
                  {!ctx.cashInsufficient && (
                    <ZHFieldHelp helpKey={HELP_KEYS.SALES_PAYMENTS_CHANGE} />
                  )}
                </span>
                <span className="sales-cash-box__amount">
                  <ZHMoneyValue
                    value={
                      ctx.cashInsufficient
                        ? ctx.cashDue - ctx.cashReceived
                        : ctx.cashChange
                    }
                    decimals={getDecimalConfig().totalAmount}
                  />
                </span>
              </div>
            </div>
          )}
          {(() => {
            const paid = ctx.paidTotal;
            const total = ctx.summary.total;
            const factor = 10 ** getDecimalConfig().totalAmount;
            const diff = Math.round((total - paid) * factor) / factor;
            const exceeds = diff < 0;
            return (
              <div
                className={`sales-summary-box${diff === 0 ? " sales-summary-box--complete" : ""}${exceeds ? " sales-summary-box--exceeds" : ""}`}
              >
                {/* SALES-POS-UI-REFINE-01: se retiró la fila "Total factura" — el mismo valor
                    (ctx.summary.total) ya es el dato más prominente de la pantalla en
                    "Total a Cobrar" (sf-total-box, arriba en el sidebar); mostrarlo de nuevo acá
                    era una duplicación visual sin aportar información nueva. `total` se conserva
                    solo para el cálculo de `diff` (Pendiente/Completo/Excede) debajo. */}
                <div className="sales-summary-row">
                  <span>Total cobrado:</span>
                  <span className="sales-summary-row__amount">
                    <ZHMoneyValue
                      value={paid}
                      decimals={getDecimalConfig().totalAmount}
                    />
                  </span>
                </div>
                <div
                  className={`sales-summary-total-row${diff === 0 ? " sales-summary-total-row--complete" : ""}${exceeds ? " sales-summary-total-row--exceeds" : ""}`}
                >
                  <span>
                    {diff === 0
                      ? "✓ Cobro completo"
                      : exceeds
                        ? "✗ Excede"
                        : "Pendiente:"}
                  </span>
                  {diff !== 0 && (
                    <span className="sales-summary-total-row__amount">
                      <ZHMoneyValue
                        value={Math.abs(diff)}
                        decimals={getDecimalConfig().totalAmount}
                      />
                    </span>
                  )}
                </div>
              </div>
            );
          })()}
        </>
      )}
    </div>
  );
}
