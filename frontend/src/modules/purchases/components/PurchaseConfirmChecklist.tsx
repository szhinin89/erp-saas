import { useI18n } from "../../../i18n/i18n";
import type {
  ConfirmChecklistSummary,
  ConfirmCommercialRisk,
  ConfirmLineWarning,
} from "../utils/purchaseConfirmChecklist";

/**
 * Blockers and warnings first, one line each; then the non-blocking line warnings grouped as
 * "Advertencias (N)"; commercial risk as one compact line (details stay on each line);
 * passed checks summarized in a single line. Warnings never block.
 */
export function PurchaseConfirmChecklist({
  summary,
  lineWarnings = [],
  commercialRisk = null,
}: {
  summary: ConfirmChecklistSummary;
  lineWarnings?: ConfirmLineWarning[];
  commercialRisk?: ConfirmCommercialRisk | null;
}) {
  const { t } = useI18n();
  return (
    <div className="pf-confirm-checklist">
      <p className="pf-confirm-checklist__intro">
        {t(
          "purchases.confirm.message",
          "La compra quedará de solo lectura y generará los movimientos de inventario y cuentas por pagar que correspondan. Su anulación posterior exige cumplir las restricciones operativas y tributarias, incluida la retención.",
        )}
      </p>
      {summary.blockerCount > 0 && (
        <p className="pf-confirm-checklist__hint">
          {t(
            "purchases.confirm.checklist.blockingHint",
            "Hay controles pendientes que bloquearán la confirmación.",
          )}
        </p>
      )}
      <ul className="pf-confirm-checklist__list">
        {summary.issues.map((item) => (
          <li
            key={item.key}
            className={`pf-confirm-checklist__item pf-confirm-checklist__item--${item.tone}`}
          >
            <span className="material-symbols-outlined pf-confirm-checklist__icon" aria-hidden="true">
              {item.tone === "danger" ? "error" : "warning"}
            </span>
            <span>
              <strong>{item.label}</strong>
              <span className="pf-confirm-checklist__detail">{item.detail}</span>
            </span>
          </li>
        ))}
        {lineWarnings.length > 0 && (
          <li className="pf-confirm-checklist__item pf-confirm-checklist__item--warning">
            <span className="material-symbols-outlined pf-confirm-checklist__icon" aria-hidden="true">
              warning
            </span>
            <span>
              <strong>{t("purchases.confirm.checklist.lineWarnings", { count: lineWarnings.length })}</strong>
              <ul className="pf-confirm-checklist__sublist">
                {lineWarnings.map((warning) => (
                  <li key={warning.key} className="pf-confirm-checklist__detail">
                    <span className="pf-confirm-checklist__line">{warning.line}</span>
                    {" — "}
                    {warning.reason}
                  </li>
                ))}
              </ul>
            </span>
          </li>
        )}
        {commercialRisk && (
          <li className="pf-confirm-checklist__item pf-confirm-checklist__item--warning">
            <span className="material-symbols-outlined pf-confirm-checklist__icon" aria-hidden="true">
              trending_down
            </span>
            <span>
              <strong>{t("purchases.confirm.checklist.commercialRisk", { count: commercialRisk.lineCount })}</strong>
              <span className="pf-confirm-checklist__detail">{commercialRisk.detail}</span>
            </span>
          </li>
        )}
        {!summary.hasXmlPurchase && (
          <li className="pf-confirm-checklist__item pf-confirm-checklist__item--neutral">
            <span className="material-symbols-outlined pf-confirm-checklist__icon" aria-hidden="true">
              description
            </span>
            <span>
              <strong>{t("purchases.confirm.checklist.manualTitle")}</strong>
              <span className="pf-confirm-checklist__detail">
                {t("purchases.confirm.checklist.manualDetail")}
              </span>
            </span>
          </li>
        )}
        {summary.passed.length > 0 && (
          <li className="pf-confirm-checklist__item pf-confirm-checklist__item--success">
            <span className="material-symbols-outlined pf-confirm-checklist__icon" aria-hidden="true">
              check_circle
            </span>
            <span>
              <strong>{t("purchases.confirm.checklist.passed", "Correcto")}</strong>
              <span className="pf-confirm-checklist__detail">{summary.passed.join(" · ")}</span>
            </span>
          </li>
        )}
      </ul>
    </div>
  );
}
