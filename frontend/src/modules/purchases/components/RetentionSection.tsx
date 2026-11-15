import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { PurchaseRetentionDraft } from "../components/PurchaseRetentionDraft";
import { RetentionAnnulmentPanel, RetentionElectronicStatusBadge, RetentionSriServiceHealth } from "../../retentions/facades/purchaseRetentionFacade";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { useI18n } from "../../../i18n/i18n";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

export function RetentionSection({
  ctx,
}: {
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const { t } = useI18n();
  const isDraft = ctx.editing?.status === "Draft";
  return (
    <div className="pf-card pf-retention" id="purchase-retention" tabIndex={-1}>
      <div className="pf-card__header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            receipt
          </span>{" "}
          {t("purchases.retention.title", "Retenciones")}
          <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_RETENTIONS} />
        </h4>
        <div className="pf-retention-actions">
          {ctx.retention && ctx.retention.status === "Issued" && (
            <>
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                onClick={() => void ctx.handleViewRetentionXml()}
                disabled={ctx.electronicPending}
                title={t("purchases.retention.viewXml", "Ver XML")}
              >
                <span className="material-symbols-outlined pf-retention-action-icon">
                  code
                </span>
                {t("purchases.retention.xmlLabel", "XML")}
              </ZHBtn>
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                onClick={() => void ctx.handleViewRetentionRidePdf()}
                disabled={ctx.electronicPending}
                title={t("purchases.retention.viewRide", "Ver RIDE (PDF)")}
              >
                <span className="material-symbols-outlined pf-retention-action-icon">
                  picture_as_pdf
                </span>
                {t("purchases.retention.rideLabel", "RIDE")}
              </ZHBtn>
            </>
          )}
        </div>
      </div>
      <div className="pf-card__body">
        {isDraft && <PurchaseRetentionDraft ctx={ctx} />}
        {!isDraft && !ctx.retention && (
          <ZHDataValue variant="muted">{t("purchases.retention.noneIssued")}</ZHDataValue>
        )}
        {ctx.retention && (
          <>
            <div className="pf-retention__issued-meta">
              <span className="pf-retention__issued-item">
                {t("purchases.retention.number", "Número")}:{" "}
                <strong>{ctx.retention.retentionNumber}</strong>
              </span>
              <span className="pf-retention__issued-item">
                {t("purchases.retention.status", "Estado")}:{" "}
                <Badge
                  variant={
                    ctx.retention.status === "Issued" ? "success" : "error"
                  }
                  label={ctx.retention.status}
                />
              </span>
              <span className="pf-retention__issued-item">
                {t("purchases.retention.date", "Fecha")}:{" "}
                <strong>{formatDate(ctx.retention.issueDate)}</strong>
              </span>
              <span className="pf-retention__issued-item">
                {t("retentions.electronicStatus.label", "Estado electrónico")}:{" "}
                <RetentionElectronicStatusBadge status={ctx.retention.electronicStatus} />
              </span>
              <RetentionSriServiceHealth status={ctx.retention.electronicStatus} />
            </div>
            {ctx.retention.annulment ? (
              <RetentionAnnulmentPanel
                annulment={ctx.retention.annulment}
                origin="purchase"
                canOperate={ctx.canOperateRetentionAnnulment}
                onChanged={() => void ctx.handleRetentionAnnulmentChanged()}
              />
            ) : null}
            <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th>{t("purchases.retention.type", "Tipo")}</th>
                  <th>{t("purchases.retention.code", "Código")}</th>
                  <th>{t("purchases.retention.description", "Descripción")}</th>
                  <th className="zh-text-align-right">{t("purchases.retention.base", "Base")}</th>
                  <th className="zh-text-align-right">%</th>
                  <th className="zh-text-align-right">{t("purchases.retention.withheld", "Retenido")}</th>
                </tr>
              </thead>
              <tbody>
                {ctx.retention.lines.map((l) => (
                  <tr key={l.id}>
                    <td>
                      <Badge
                        variant={l.taxType === "Vat" ? "info" : "warning"}
                        label={l.taxType === "Vat" ? t("purchases.retention.typeVat", "IVA") : t("purchases.retention.typeIncome", "RENTA")}
                      />
                    </td>
                    <td className="zh-code-value">
                      {l.retentionCode}
                    </td>
                    <td>{l.retentionCodeDescription}</td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue value={l.baseAmount} precision="money" />
                    </td>
                    <td className="zh-table-cell--num">{l.retentionRate}%</td>
                    <td className="zh-table-cell--num pf-retention-amount">
                      <ZHMoneyValue value={l.retainedAmount} precision="money" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="pf-retention__issued-totals">
              <TotalMiniCard
                label={t("purchases.retention.vat", "Ret. IVA")}
                value={ctx.retention.totalRetainedVat}
                color="var(--color-primary)"
              />
              <TotalMiniCard
                label={t("purchases.retention.income", "Ret. renta")}
                value={ctx.retention.totalRetainedIncome}
                color="var(--color-warning)"
              />
              <TotalMiniCard
                label={t("purchases.retention.totalWithheld", "TOTAL RETENIDO")}
                value={ctx.retention.totalRetained}
                color="var(--color-error)"
                highlight
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function TotalMiniCard({
  label,
  value,
  color,
  highlight,
}: {
  label: string;
  value: number;
  color?: string;
  highlight?: boolean;
}) {
  return (
    <div className={`pf-total-mini-card ${highlight ? "pf-total-mini-card--highlight" : "pf-total-mini-card--default"}`}>
      <div className="pf-total-mini-card__label">
        {label}
      </div>
      <div
        className={`pf-total-mini-card__value ${highlight ? "pf-total-mini-card__value--highlight" : "pf-total-mini-card__value--default"}`}
        data-tone={color === "var(--color-error)" ? "error" : "default"}
      >
        <ZHMoneyValue value={value} precision="money" />
      </div>
    </div>
  );
}
