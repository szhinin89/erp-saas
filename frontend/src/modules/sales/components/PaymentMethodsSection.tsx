import { useRef } from "react";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHToggleTile } from "../../../components/zh/ZHToggleTile";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { ZhDecimalInput } from "../../../components/zh/inputs";
import { formatDecimalDisplay, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import type { SalesPageContext } from "../hooks/useSalesPage";
import {
  quickTenderAmounts,
  type SalesCollectionStatus,
} from "../utils/salesCollectionStatus";
import { remainingToCollect } from "./paymentRemaining";

export interface PaymentMethodsSectionProps {
  ctx: SalesPageContext;
}

/**
 * SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01: resuelve el código SRI ("formaPago" del XML) que
 * corresponde a una forma de cobro concreta — SIEMPRE PaymentMethod.sriPaymentMethodCode primero
 * (mapeo configurado en Configuración → Métodos de Pago), y solo si esa forma de cobro no tiene
 * mapeo propio cae al default de empresa (ctx.formWatch.sriPaymentMethodCode, ya resuelto por el
 * backend/precargado en el header). Ningún código SRI se hardcodea aquí — ambos valores vienen de
 * catálogo/config real. Undefined = ni mapeo propio ni default de empresa configurados.
 */
function resolveSriPaymentMethodCode(
  ctx: SalesPageContext,
  paymentMethodId: string,
): string | undefined {
  const pm = ctx.paymentMethods.find((p) => p.id === paymentMethodId);
  return pm?.sriPaymentMethodCode || ctx.formWatch.sriPaymentMethodCode || undefined;
}

const STATE_ICON: Record<SalesCollectionStatus["tone"], string> = {
  success: "check_circle",
  neutral: "schedule",
  warning: "error",
  error: "error",
};

/** Cifra destacada de la tarjeta: Vuelto / Falta / Excede — solo cuando existe. */
function highlightOf(c: SalesCollectionStatus): { label: string; amount: number } | null {
  if (c.state === "change") return { label: "Vuelto", amount: c.cashChange };
  if (c.state === "cashShort" || c.state === "pending") return { label: "Falta", amount: c.amount ?? 0 };
  if (c.state === "exceeds") return { label: "Excede", amount: c.amount ?? 0 };
  return null;
}

/** Texto del estado único (pie de la tarjeta). Las cifras ya están en la tarjeta. */
const STATE_LABEL: Partial<Record<SalesCollectionStatus["state"], string>> = {
  change: "Cobro completo",
  complete: "Cobro completo",
  cashShort: "Falta por cobrar",
  pending: "Falta por cobrar",
};

/**
 * POS-COLLECTION-INLINE-B-01 — ÚNICA tarjeta dinámica de resultado del cobro (flujo B):
 * Total a cobrar / Recibido (o Cobrado en multipago) → cifra destacada (Vuelto · Falta · Excede)
 * → un solo estado. Todo sale de `ctx.collection` (computeSalesCollectionStatus): la vista no
 * recalcula reglas ni repite mensajes en otras cajas.
 */
function CollectionResultCard({
  c,
  isMultiPayment,
}: {
  c: SalesCollectionStatus;
  isMultiPayment: boolean;
}) {
  const highlight = highlightOf(c);
  const showReceived = c.cashApplied > 0 && !isMultiPayment;
  return (
    <div
      className={`sales-result sales-result--${c.tone}`}
      data-collection-state={c.state}
    >
      <dl className="sales-result__rows">
        {/* En pantallas bajas se omite (CSS): el mismo total está a la vista justo arriba. */}
        <dt className="sales-result__total">Total a cobrar</dt>
        <dd className="sales-result__total">
          <ZHMoneyValue value={c.total} precision="money" />
        </dd>
        {showReceived ? (
          <>
            <dt>Recibido</dt>
            <dd>
              {c.cashReceived !== null ? (
                <ZHMoneyValue value={c.cashReceived} precision="money" />
              ) : (
                <span className="sales-result__empty">—</span>
              )}
            </dd>
          </>
        ) : (
          <>
            <dt>Cobrado</dt>
            <dd>
              <ZHMoneyValue value={c.appliedTotal} precision="money" />
            </dd>
          </>
        )}
        {isMultiPayment && c.cashApplied > 0 && c.cashReceived !== null && (
          <>
            {/* Visible en pantallas altas; en el panel del POS se omite (CSS) porque el mismo
                valor está en el campo "Efectivo recibido" justo arriba. */}
            <dt className="sales-result__received-dup">Efectivo recibido</dt>
            <dd className="sales-result__received-dup">
              <ZHMoneyValue value={c.cashReceived} precision="money" />
            </dd>
          </>
        )}
      </dl>
      {highlight && (
        <div className="sales-result__highlight" data-highlight={highlight.label}>
          <span className="sales-result__highlight-label">
            {highlight.label}
            {highlight.label === "Vuelto" && (
              <ZHFieldHelp helpKey={HELP_KEYS.SALES_PAYMENTS_CHANGE} />
            )}
          </span>
          <span className="sales-result__highlight-amount">
            <ZHMoneyValue value={highlight.amount} precision="money" />
          </span>
        </div>
      )}
      <div className="sales-result__state" role="status">
        <span className="material-symbols-outlined sales-result__state-icon">
          {STATE_ICON[c.tone]}
        </span>
        <span>{STATE_LABEL[c.state] ?? c.label}</span>
      </div>
    </div>
  );
}

// ── Payment Methods Section ─────────────────────────────────────────────
// POS-COLLECTION-INLINE-B-01 — flujo B inline: Forma de cobro → (Efectivo) Efectivo recibido
// (campo ancho + montos rápidos) → UNA tarjeta de resultado (Total / Recibido / Vuelto·Falta +
// estado único) → Emitir.
// - Efectivo como ÚNICO cobro: el cajero edita solo "Efectivo recibido"; lo aplicado a la factura
//   es derivado (sigue al total, ver useSalesPage POS-CASH-ONLY-FOLLOWS-TOTAL-01) y no se muestra
//   como un segundo campo de dinero idéntico.
// - Multipago: cada forma de cobro conserva su monto "Aplicado" explícito; Efectivo además pide
//   "Efectivo recibido" para el vuelto.
export function PaymentMethodsSection({ ctx }: PaymentMethodsSectionProps) {
  // Presentación de textos compuestos: semántica declarada (04E). Los `factor` de redondeo siguen
  // leyendo la escala de money de la policy (cálculo).
  const moneyDecimals = usePrecisionDecimals("money");
  const cashInputRef = useRef<HTMLInputElement>(null);
  const c = ctx.collection;
  const isMultiPayment = ctx.payments.filter((p) => p.amount > 0).length > 1;

  const focusCashReceived = () => {
    // El bloque de efectivo se monta en el render siguiente al primer cobro en efectivo.
    setTimeout(() => cashInputRef.current?.focus({ preventScroll: true }), 0);
  };

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
            onClick={() => {
              ctx.setInvoicePayments([]);
              ctx.setCashReceivedInput("");
            }}
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
                    precision="money"
                  />
                </span>
              </div>
            ))}
          {(() => {
            // SALES-PAYMENT-TOLERANCE-NOTE-01: el monto mostrado arriba es SIEMPRE el cobro real
            // (nunca se falsea mostrando el total como si se hubiera cobrado exacto). Cuando la
            // suma de pagos difiere del total dentro de la tolerancia de settlement de la empresa
            // (CompanyPrecisionPolicy.SettlementToleranceAmount — la misma que usó el backend al
            // autorizar), se aclara esa diferencia en vez de dejarla como un descuadre visual.
            const policy = getPrecisionPolicy();
            const factor = 10 ** policy.moneyDecimals;
            const total = ctx.grandTotal;
            const paid = (ctx.editing?.payments ?? []).reduce(
              (s, p) => s + (p.amount || 0),
              0,
            );
            const diff = Math.round((total - paid) * factor) / factor;
            const absDiff = Math.abs(diff);
            if (total <= 0 || absDiff === 0 || absDiff > policy.settlementToleranceAmount) {
              return null;
            }
            return (
              <div className="sales-payment-tolerance-note">
                Diferencia {formatMoneyWithSymbol(absDiff, moneyDecimals)} dentro de
                tolerancia — saldada
              </div>
            );
          })()}
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
              const isCash = ctx.isCashMethodId(pm.id);
              // BUGFIX-SALES-CREDIT-PAYMENT-CONSISTENCY-01: el backend bloquea Contado + método
              // Crédito (AuthorizeSalesInvoiceHandler) — se deshabilita aquí para prevenir el
              // intento, nunca reemplaza esa validación.
              const creditTileDisabled =
                ctx.fieldDisabled || (isCredit && !ctx.isCreditTerm);
              const calcRemaining = () =>
                Math.max(0, remainingToCollect(ctx, pm.id));
              // Monto aplicado editable solo en multipago: con Efectivo único lo aplicado es
              // derivado del total y el cajero solo escribe "Efectivo recibido".
              const showAppliedInput =
                hasValue && !isCredit && !pm.requiresReference && !(isCash && c.isCashOnly);

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
                          const base = ctx.paymentsForAdditionalMethod(pm.id);
                          ctx.setInvoicePayments([
                            ...base,
                            {
                              _key: ctx.payKey,
                              paymentMethodId: pm.id,
                              amount: rem,
                              reference: null,
                            },
                          ]);
                          ctx.setPayKey((k) => k + 1);
                        }
                        if (isCash) focusCashReceived();
                      } else if (isCash) {
                        focusCashReceived();
                      }
                    }}
                  />
                  {showAppliedInput && (
                    <div className="sales-payment-amount-row">
                      <span className="sales-payment-applied-label">Aplicado $</span>
                      <ZhDecimalInput
                        precision="money"
                        positiveOnly
                        aria-label={`Monto aplicado ${pm.name}`}
                        // Valor canónico: el input (precision="money") decide la escala (04G).
                        defaultValue={entry!.amount}
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
                  {/* Efectivo único: sin controles extra bajo la forma de cobro — el recorrido sigue
                      directo a "Efectivo recibido". Se quita con "Limpiar cobros" del encabezado, o
                      eligiendo otra forma de cobro (paymentsForAdditionalMethod). */}
                  {hasValue && pm.requiresReference && !isCredit && (
                    <span className="sales-payment-ref-amount">
                      <ZHMoneyValue
                        value={totalForMethod}
                        precision="money"
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
                        precision="money"
                      />
                    </span>
                  )}
                  {/* POS-EMISSION-VISIBILITY-01: el código SRI (formaPago) solo existe en el XML
                      electrónico — una factura física nunca se vuelve electrónica (snapshot
                      inmutable), así que en física no se muestra. */}
                  {ctx.isElectronic &&
                    hasValue &&
                    !isCredit &&
                    (() => {
                      const sriCode = resolveSriPaymentMethodCode(ctx, pm.id);
                      const sriName = ctx.sriPaymentMethods.find(
                        (s) => s.code === sriCode,
                      )?.name;
                      return sriCode ? (
                        <span
                          className="sales-payment-sri-hint"
                          title={`Forma de pago SRI ${sriCode}${sriName ? ` — ${sriName}` : ""}, derivada automáticamente de "${pm.name}" (SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01)`}
                        >
                          {/* POS-OPERATIONAL-HEADER-01: solo el código (la descripción va en el
                              tooltip) — el panel de cobro no crece por un texto de 3 líneas. */}
                          SRI {sriCode}
                        </span>
                      ) : (
                        <span
                          className="sales-payment-sri-hint sales-payment-sri-hint--warning"
                          title="Configure el mapeo SRI de esta forma de cobro en Configuración → Métodos de Pago, o un default de empresa en Configuración → Ventas."
                        >
                          ⚠ Sin forma de pago SRI
                        </span>
                      );
                    })()}
                </div>
              );
            })}
          </div>

          {c.cashApplied > 0 && (
            <div className="sales-tender">
              <div className="sales-tender__label">
                <label htmlFor="sales-cash-received">Efectivo recibido</label>
                <ZHFieldHelp helpKey={HELP_KEYS.SALES_PAYMENTS_CASH_RECEIVED} />
              </div>
              <div className="sales-tender__field">
                <span className="sales-tender__currency" aria-hidden="true">
                  $
                </span>
                <ZhDecimalInput
                  id="sales-cash-received"
                  ref={cashInputRef}
                  precision="money"
                  positiveOnly
                  placeholder="0.00"
                  // Controlado: Falta / Pago exacto / Vuelto / canEmit se recalculan en cada
                  // pulsación (POS-COLLECTION-SSOT-01), no al salir del campo.
                  value={ctx.cashReceivedInput}
                  onChange={(e) => ctx.setCashReceivedInput(e.target.value)}
                  disabled={ctx.fieldDisabled}
                  // POS-F8-CASH-INPUT-01: F8 emite aunque el foco siga aquí (opt-in
                  // POS_EMIT_SHORTCUT_ATTR, ver useSalesPage); Enter equivale.
                  data-pos-emit-shortcut="true"
                  onKeyDown={(e) => {
                      if (e.key === "Enter" && (ctx.canEmit || !!ctx.lineIssues?.length)) {
                      e.preventDefault();
                      ctx.openIssueFlow();
                    }
                  }}
                  className="sales-tender__input"
                />
              </div>
              {/* POS-OPERATIONAL-HEADER-01: en multipago el monto aplicado ya se ve rotulado
                  ("APLICADO") en la celda de Efectivo — sin texto repetido aquí. */}
              {!ctx.fieldDisabled && (
                <div className="sales-tender__quick" aria-label="Montos rápidos de efectivo">
                  {quickTenderAmounts(c.cashApplied).map((amount) => (
                    <ZHBtn
                      key={amount}
                      type="button"
                      variant="secondary"
                      size="xs"
                      className="sales-tender__quick-btn"
                      onClick={() => {
                        ctx.setCashReceivedInput(formatDecimalDisplay(amount, moneyDecimals));
                        cashInputRef.current?.focus({ preventScroll: true });
                      }}
                    >
                      ${amount}
                    </ZHBtn>
                  ))}
                  <ZHBtn
                    type="button"
                    variant="ghost"
                    size="xs"
                    className="sales-tender__quick-btn"
                    onClick={() => {
                      ctx.setCashReceivedInput("");
                      cashInputRef.current?.focus({ preventScroll: true });
                    }}
                  >
                    Limpiar
                  </ZHBtn>
                </div>
              )}
            </div>
          )}

          {c.state !== "noTotal" && (
            <CollectionResultCard c={c} isMultiPayment={isMultiPayment} />
          )}
        </>
      )}
    </div>
  );
}
