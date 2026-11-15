import { Badge } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhNumberInput } from "../../../components/zh/inputs/ZhNumberInput";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { formatMoney } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { roundToTotalAmount } from "../utils/purchaseCalc";
import { useI18n } from "../../../i18n/i18n";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

export function PaymentScheduleSection({
  ctx,
}: {
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const { t } = useI18n();
  const moneyDecimals = usePrecisionDecimals("money"); // texto compuesto (06)
  return (
    <div className="pf-card" id="purchase-schedule" tabIndex={-1}>
      <div className="pf-card__header pf-schedule-header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            calendar_month
          </span>
          {t("purchases.schedule.title", "Plazos de pago")}
          <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_PAYMENT_SCHEDULE} />
          {ctx.hasPersistedSchedule && (
            <Badge
              variant="success"
              className="pf-badge--offset"
              label={
                <>
                  <span className="material-symbols-outlined pf-badge__icon">
                    lock
                  </span>{" "}
                  {t("purchases.schedule.confirmed", "Confirmado")}
                </>
              }
            />
          )}
          {!ctx.hasPersistedSchedule && ctx.ptLoaded && (
            <Badge
              variant="warning"
              className="pf-badge--offset"
              label={t("purchases.schedule.preview", "Preview")}
            />
          )}
        </h4>
        {ctx.isDraft && !ctx.hasPersistedSchedule && (
          <div className="pf-schedule-actions">
            <ZHBtn
              type="button"
              variant="primary"
              size="xs"
              onClick={ctx.regenerateSchedule}
              disabled={ctx.saving || !ctx.formWatch.issueDate}
            >
              <span
                className="material-symbols-outlined pf-schedule-action-icon"
              >
                autorenew
              </span>
              {t("purchases.schedule.regenerate", "Regenerar")}
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="secondary"
              size="xs"
              onClick={ctx.addInstallment}
              disabled={ctx.saving || !ctx.formWatch.issueDate}
            >
              <span
                className="material-symbols-outlined pf-schedule-action-icon"
              >
                add
              </span>
              {t("purchases.schedule.installment", "Cuota")}
            </ZHBtn>
          </div>
        )}
      </div>
      <div className="pf-card__body">
        {ctx.isDraft && !ctx.hasPersistedSchedule && (
          <>
            {ctx.xmlPaymentProposal && (
              <ZHPageNotice variant="warning" message={ctx.xmlPaymentProposal.suggestedDueDate
                ? t("purchases.schedule.xmlProposalDue", { date: formatDate(ctx.xmlPaymentProposal.suggestedDueDate) })
                : t("purchases.schedule.xmlProposalNoDate", {
                    term: ctx.xmlPaymentProposal.xmlPaymentTerm ?? "—",
                    unit: ctx.xmlPaymentProposal.xmlPaymentTimeUnit ?? "",
                    count: ctx.xmlPaymentProposal.xmlPaymentCount ?? 0,
                  })} />
            )}
            {ctx.xmlPaymentProposal?.suggestedDueDate && (
              <div className="pf-schedule-actions">
                <ZHBtn type="button" size="xs" onClick={ctx.acceptXmlPaymentProposal} disabled={ctx.fieldDisabled}>{t("purchases.schedule.acceptXmlProposal", "Aceptar propuesta XML")}</ZHBtn>
                <ZHBtn type="button" size="xs" variant="secondary" onClick={ctx.dismissXmlPaymentProposal} disabled={ctx.fieldDisabled}>{t("purchases.schedule.useSelectedCondition", "Usar condición seleccionada")}</ZHBtn>
              </div>
            )}
            {ctx.scheduleConditionError && <ZHPageNotice variant="warning" message={ctx.scheduleConditionError} />}
          </>
        )}
        {ctx.isDraft && !ctx.hasPersistedSchedule && (
          <div className="pf-schedule-setup">
            <ZHField
              density="compact"
              label={t("purchases.schedule.installmentCount", "Nro. cuotas")}
              className="pf-schedule-field--installments"
            >
              <ZhNumberInput
                positiveOnly
                value={String(ctx.ptInstallments)}
                onChange={(e) =>
                  ctx.setPtInstallments(
                    Math.min(60, Math.max(1, Number(e.target.value) || 1)),
                  )
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
            <ZHField
              density="compact"
              label={t("purchases.schedule.daysBetween", "Días entre cuotas")}
              className="pf-schedule-field--days-between"
            >
              <ZhNumberInput
                positiveOnly
                value={String(ctx.ptDaysBetween)}
                onChange={(e) =>
                  ctx.setPtDaysBetween(Math.max(0, Number(e.target.value) || 0))
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
            <div className="pf-schedule-total">
              {t("purchases.schedule.totalTerm", "Total plazo")}:{" "}
              <strong>
                {t("purchases.schedule.daysValue", {
                  days: ctx.ptInstallments * ctx.ptDaysBetween,
                })}
              </strong>
            </div>
          </div>
        )}

        {ctx.ptMismatch && ctx.ptRows.length > 0 && (
          <ZHPageNotice
            variant="warning"
            message={t("purchases.schedule.mismatch", {
              amount: formatMoney(
                roundToTotalAmount(ctx.localTotal - ctx.ptRowsSum),
                moneyDecimals,
              ),
            })}
          />
        )}

        {ctx.hasPersistedSchedule && (
          <div className="table-scroll">
          <table className="table table--compact table--neutral">
            <thead>
              <tr>
                <th className="pf-schedule-col--number">#</th>
                <th>{t("purchases.schedule.dueDate", "Vencimiento")}</th>
                <th className="zh-text-align-right">{t("purchases.schedule.amount", "Monto")}</th>
                <th>{t("purchases.schedule.notes", "Notas")}</th>
              </tr>
            </thead>
            <tbody>
              {ctx.editing!.paymentSchedules.map((s) => (
                <tr key={s.id}>
                  <td className="zh-text-align-center pf-schedule-installment-number">
                    {s.installmentNumber}
                  </td>
                  <td>{formatDate(s.dueDate)}</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={s.amount} precision="money" />
                  </td>
                  <td className="pf-schedule-notes">
                    {s.notes || "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        )}

        {!ctx.hasPersistedSchedule && ctx.ptRows.length > 0 && (
          <>
            <div className="table-scroll">
            <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th className="pf-schedule-col--number">#</th>
                  <th>{t("purchases.schedule.dueDate", "Vencimiento")}</th>
                  <th className="zh-text-align-right pf-schedule-col--amount">
                    {t("purchases.schedule.amountCurrency", "Monto ($)")}
                  </th>
                  <th>{t("purchases.schedule.notes", "Notas")}</th>
                  <th>{t("purchases.schedule.status", "Estado")}</th>
                  <th className="zh-text-align-center pf-schedule-col--actions"></th>
                </tr>
              </thead>
              <tbody>
                {ctx.ptRows.map((r, idx) => (
                  <tr key={idx}>
                    <td className="zh-text-align-center pf-schedule-installment-number pf-schedule-installment-number--muted">
                      {r.number}
                    </td>
                    <td>
                      <ZhDateInput
                        density="compact"
                        className="pf-schedule-input--date"
                        value={r.dueDate}
                        onChange={(e) =>
                          ctx.updateScheduleRow(idx, "dueDate", e.target.value)
                        }
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      {/* Input no controlado: la key por monto lo remonta cuando Regenerar/cargar
                          cambia la cuota, para no mostrar ni re-confirmar un monto obsoleto. */}
                      <ZhDecimalInput
                        key={r.amount}
                        precision="money"
                        positiveOnly
                        density="compact"
                        className="pf-schedule-input--amount"
                        defaultValue={r.amount}
                        onValueCommit={(value) =>
                          ctx.updateScheduleRow(idx, "amount", Number(value) || 0)
                        }
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <ZhTextInput
                        density="compact"
                        className="pf-schedule-input--notes"
                        placeholder={t("purchases.schedule.notePlaceholder", "Observación...")}
                        value={r.notes}
                        onChange={(e) =>
                          ctx.updateScheduleRow(idx, "notes", e.target.value)
                        }
                        maxLength={500}
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <Badge
                        variant="warning"
                        label={t("purchases.schedule.pending", "Pendiente")}
                      />
                    </td>
                    <td className="zh-text-align-center">
                      {ctx.isDraft && ctx.ptRows.length > 1 && (
                        <ZHIconButton
                          icon="close"
                          variant="danger"
                          title={t("purchases.schedule.deleteInstallment", "Eliminar cuota")}
                          onClick={() => ctx.removeInstallment(idx)}
                          disabled={ctx.fieldDisabled}
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
            <div className="pf-schedule-footer">
              <span className="pf-schedule-footer__count">
                {t("purchases.schedule.installmentCountValue", {
                  count: ctx.ptRows.length,
                })}
              </span>
              <span>
                {t("purchases.schedule.installmentsTotal", "Total cuotas")}:{" "}
                <strong
                  className={`pf-schedule-footer__amount ${ctx.ptMismatch ? "pf-schedule-footer__amount--error" : "pf-schedule-footer__amount--default"}`}
                >
                  <ZHMoneyValue value={ctx.ptRowsSum} precision="money" />
                </strong>
                {" / "}
                {t("purchases.schedule.purchaseTotal", "Total compra")}:{" "}
                <strong className="pf-schedule-footer__purchase-total">
                  <ZHMoneyValue value={ctx.localTotal} precision="money" />
                </strong>
              </span>
            </div>
          </>
        )}

        {!ctx.hasPersistedSchedule && ctx.ptRows.length === 0 && (
          <div className="pf-schedule-empty">
            <span
              className="material-symbols-outlined pf-schedule-empty__icon"
            >
              event_busy
            </span>
            {!ctx.ptLoaded
              ? t(
                  "purchases.schedule.emptySelectSupplier",
                  "Seleccione un proveedor para cargar la condición de pago",
                )
              : !ctx.formWatch.issueDate
                ? t(
                    "purchases.schedule.emptyIssueDate",
                    "Ingrese la fecha de emisión para calcular vencimientos",
                  )
                : t(
                    "purchases.schedule.emptyConfigure",
                    "Configure cuotas arriba para generar el cronograma",
                  )}
          </div>
        )}
      </div>
    </div>
  );
}
