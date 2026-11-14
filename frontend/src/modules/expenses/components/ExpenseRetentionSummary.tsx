import { ZHFormAlert, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../i18n/i18n";
import type { RetentionEligibilityCandidate } from "../api/expenseDocumentService";
import {
  RetentionAnnulmentPanel,
  RetentionElectronicStatusBadge,
  RetentionSriServiceHealth,
  type RetentionDocumentDto,
} from "../../retentions/facades/retentionDocumentFacade";

// Presentación de solo lectura de la sección Retención del gasto (elegibilidad y retención emitida),
// extraída de ExpenseRetentionSection para mantener el archivo bajo su techo de tamaño (ADR-047).

const RETENTION_STATUS_LABEL_KEY: Record<string, string> = {
  Draft: "expenses.form.retention.status.draft",
  Issued: "expenses.form.retention.status.issued",
  Cancelled: "expenses.form.retention.status.cancelled",
};

const TAX_TYPE_LABEL_KEY: Record<string, string> = {
  Vat: "expenses.form.retention.vat",
  Income: "expenses.form.retention.income",
};

export function EligibilityBadge({
  label,
  eligible,
  candidates,
}: {
  label: string;
  eligible: boolean;
  candidates: RetentionEligibilityCandidate[];
}) {
  const { t } = useI18n();
  return (
    <span
      className={`exp-doc-retention-badge ${eligible ? "exp-doc-retention-badge--yes" : "exp-doc-retention-badge--no"}`}
    >
      {label}: {eligible ? t("expenses.form.retention.applies") : t("expenses.form.retention.notApplies")}
      {eligible && candidates.length > 0
        ? ` (${candidates.map((c) => c.retentionCode).join(", ")})`
        : ""}
    </span>
  );
}

export function RetentionDocumentSummary({
  retention,
  canOperateAnnulment,
  onAnnulmentChanged,
}: {
  retention: RetentionDocumentDto;
  canOperateAnnulment: boolean;
  onAnnulmentChanged: () => void;
}) {
  const { t } = useI18n();
  return (
    <div className="exp-doc-retention-summary">
      <ZHGrid cols={4}>
        <div>
          <span className="exp-doc-retention-summary__label">{t("expenses.form.retention.summary.number")}</span>
          <p>{retention.retentionNumber ?? "—"}</p>
        </div>
        <div>
          <span className="exp-doc-retention-summary__label">{t("expenses.form.retention.summary.issueDate")}</span>
          <p>{retention.issueDate ? formatDate(retention.issueDate) : "—"}</p>
        </div>
        <div>
          <span className="exp-doc-retention-summary__label">{t("expenses.form.retention.summary.status")}</span>
          <p>
            {RETENTION_STATUS_LABEL_KEY[retention.status]
              ? t(RETENTION_STATUS_LABEL_KEY[retention.status])
              : retention.status}
          </p>
        </div>
        <div>
          <span className="exp-doc-retention-summary__label">{t("expenses.form.retention.summary.electronicStatus")}</span>
          <p>
            <RetentionElectronicStatusBadge status={retention.electronicStatus} />
          </p>
          <RetentionSriServiceHealth status={retention.electronicStatus} />
        </div>
      </ZHGrid>

      <div className="exp-doc-retention-summary__total">
        <span>{t("expenses.form.retention.summary.totalRetained")}</span>
        <ZHMoneyValue value={retention.totalRetained} precision="money" emphasis="grand" />
      </div>

      {retention.lines.length > 0 && (
        <div className="exp-doc-lines">
          {retention.lines.map((line) => (
            <div className="exp-doc-line" key={line.id}>
              <div className="exp-doc-line__top">
                <span className="exp-doc-line__number">
                  {TAX_TYPE_LABEL_KEY[line.taxType] ? t(TAX_TYPE_LABEL_KEY[line.taxType]) : line.taxType} —{" "}
                  {line.retentionCode}
                </span>
              </div>
              <div className="exp-doc-line__totals">
                <span>
                  Base <ZHMoneyValue value={line.baseAmount} precision="money" currencySymbol="" />
                </span>
                <span>{line.retentionRate}%</span>
                <span>
                  {t("expenses.form.retention.summary.retained")}{" "}
                  <ZHMoneyValue
                    value={line.retainedAmount}
                    precision="money"
                    currencySymbol=""
                    emphasis="strong"
                  />
                </span>
              </div>
              {line.description && <p className="exp-doc-retention-summary__label">{line.description}</p>}
            </div>
          ))}
        </div>
      )}

      {retention.annulment && (
        <RetentionAnnulmentPanel
          annulment={retention.annulment}
          origin="expense"
          canOperate={canOperateAnnulment}
          onChanged={onAnnulmentChanged}
        />
      )}

      {retention.cancelReason && (
        <ZHFormAlert
          type="warning"
          message={t("expenses.form.retention.cancelledTitle")}
          detail={retention.cancelReason}
        />
      )}
    </div>
  );
}
