import { useEffect, useId, useState } from "react";
import { ZHBtn, ZHField, ZHFormAlert, ZHGrid } from "../../../components/zh/ZHForm";
import { Badge } from "../../../components/PageShell";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import {
  emissionPointLookupFacade,
  type EmissionPointListItemDto,
} from "../../emissionPoints/facades/emissionPointLookupFacade";
import {
  expenseDocumentService,
  type ExpenseStatus,
  type RetentionEligibilityResult,
} from "../api/expenseDocumentService";
import type { RetentionDocumentDto } from "../../retentions/facades/retentionDocumentFacade";
import { EligibilityBadge, RetentionDocumentSummary } from "./ExpenseRetentionSummary";
import { newRetentionIntentLine, type RetentionIntentFormState } from "../utils/expenseRetentionModel";
import { ZhCheckbox } from "../../../components/zh/inputs";

interface Props {
  /** `null` mientras el gasto no tiene id todavía (borrador nuevo sin guardar). */
  expenseDocumentId: string | null;
  documentStatus: ExpenseStatus | undefined;
  /** Se incrementa cada vez que el documento se recarga (guardar/confirmar) para forzar refetch. */
  refreshKey: number;
  disabled?: boolean;
  value: RetentionIntentFormState;
  onChange: (patch: Partial<RetentionIntentFormState>) => void;
  onEligibilityChange: (eligibility: RetentionEligibilityResult | null) => void;
  /** ZH-RETENTION-SRI-ANNULMENT-01 — la anulación ante el SRI cambió (recargar el gasto). */
  onAnnulmentChanged?: () => void;
}

export function ExpenseRetentionSection({
  expenseDocumentId,
  documentStatus,
  refreshKey,
  disabled,
  value,
  onChange,
  onEligibilityChange,
  onAnnulmentChanged,
}: Props) {
  const { t } = useI18n();
  const { has } = usePermissionsUi();
  const canOperateAnnulment = has("expenses.documents.cancel");
  const canReadEmissionPoints = has("settings.emission-points.view");

  const [eligibility, setEligibility] = useState<RetentionEligibilityResult | null>(null);
  const [eligibilityError, setEligibilityError] = useState(false);
  const [loadingEligibility, setLoadingEligibility] = useState(false);

  const [retentionDoc, setRetentionDoc] = useState<RetentionDocumentDto | null>(null);
  const [retentionDocError, setRetentionDocError] = useState(false);
  const [loadingRetentionDoc, setLoadingRetentionDoc] = useState(false);

  const [emissionPoints, setEmissionPoints] = useState<EmissionPointListItemDto[]>([]);
  // Motivos: abiertos por defecto cuando NO aplica retención (son la explicación principal) y
  // plegados cuando sí aplica; el usuario puede alternarlos en ambos casos.
  const [reasonsToggled, setReasonsToggled] = useState<boolean | null>(null);
  const reasonsId = useId();

  const isDraftDocument = !documentStatus || documentStatus === "Draft";

  // ── Elegibilidad (solo mientras el gasto es Draft y ya tiene id) ─────────
  useEffect(() => {
    if (!expenseDocumentId || !isDraftDocument) {
      setEligibility(null);
      onEligibilityChange(null);
      return;
    }
    let cancelled = false;
    setLoadingEligibility(true);
    setEligibilityError(false);
    expenseDocumentService
      .getRetentionEligibility(expenseDocumentId)
      .then((result) => {
        if (cancelled) return;
        setEligibility(result);
        onEligibilityChange(result);
      })
      .catch(() => {
        if (cancelled) return;
        setEligibility(null);
        onEligibilityChange(null);
        setEligibilityError(true);
      })
      .finally(() => {
        if (!cancelled) setLoadingEligibility(false);
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [expenseDocumentId, isDraftDocument, refreshKey]);

  // ── Retención asociada (solo cuando el gasto ya no es Draft) ──────────────
  useEffect(() => {
    if (!expenseDocumentId || isDraftDocument) {
      setRetentionDoc(null);
      return;
    }
    let cancelled = false;
    setLoadingRetentionDoc(true);
    setRetentionDocError(false);
    expenseDocumentService
      .getExpenseRetention(expenseDocumentId)
      .then((result) => {
        if (!cancelled) setRetentionDoc(result);
      })
      .catch(() => {
        if (!cancelled) {
          setRetentionDocError(true);
        }
      })
      .finally(() => {
        if (!cancelled) setLoadingRetentionDoc(false);
      });
    return () => {
      cancelled = true;
    };
  }, [expenseDocumentId, isDraftDocument, refreshKey]);

  // ── Puntos de emisión (solo si el usuario puede leerlos) ──────────────────
  useEffect(() => {
    if (!canReadEmissionPoints || !isDraftDocument) return;
    let cancelled = false;
    emissionPointLookupFacade
      .list("active")
      .then((rows) => {
        if (!cancelled) setEmissionPoints(rows);
      })
      .catch(() => {
        if (!cancelled) setEmissionPoints([]);
      });
    return () => {
      cancelled = true;
    };
  }, [canReadEmissionPoints, isDraftDocument]);

  if (!expenseDocumentId) {
    return (
      <section className="exp-doc-section" aria-label={t("expenses.form.retention.title")}>
        <div className="exp-doc-section__heading exp-doc-section__heading--actions">
          <div className="exp-doc-section__title-row">
            <h2>{t("expenses.form.retention.title")}</h2>
            <ZHFieldHelp helpKey={HELP_KEYS.EXPENSES_RETENTION_EVALUATION} />
          </div>
        </div>
        <p className="exp-doc-retention-hint">{t("expenses.form.retention.saveFirst")}</p>
      </section>
    );
  }

  if (!isDraftDocument) {
    return (
      <section className="exp-doc-section" aria-label={t("expenses.form.retention.title")}>
        <div className="exp-doc-section__heading">
          <h2>{t("expenses.form.retention.title")}</h2>
        </div>
        {loadingRetentionDoc && (
          <p className="exp-doc-retention-hint">{t("expenses.form.retention.loadingDoc")}</p>
        )}
        {retentionDocError && (
          <ZHFormAlert
            type="error"
            message={t("expenses.form.retention.docErrorTitle")}
            detail={t("expenses.form.retention.docErrorDetail")}
          />
        )}
        {!loadingRetentionDoc && !retentionDocError && !retentionDoc && (
          <ZHFormAlert type="neutral" message={t("expenses.form.retention.none")} />
        )}
        {retentionDoc && (
          <RetentionDocumentSummary
            retention={retentionDoc}
            canOperateAnnulment={canOperateAnnulment}
            onAnnulmentChanged={() => onAnnulmentChanged?.()}
          />
        )}
      </section>
    );
  }

  const canApply = !!eligibility?.isEligible;
  const reasonsOpen = reasonsToggled ?? !canApply;

  const updateLine = (key: string, patch: Partial<RetentionIntentFormState["lines"][number]>) =>
    onChange({
      lines: value.lines.map((line) => (line.key === key ? { ...line, ...patch } : line)),
    });

  const removeLine = (key: string) =>
    onChange({ lines: value.lines.filter((line) => line.key !== key) });

  return (
    <section className="exp-doc-section" aria-label={t("expenses.form.retention.title")}>
      <div className="exp-doc-section__heading exp-doc-section__heading--actions">
        <div className="exp-doc-section__title-row">
          <h2>{t("expenses.form.retention.title")}</h2>
          <ZHFieldHelp helpKey={HELP_KEYS.EXPENSES_RETENTION_EVALUATION} />
        </div>
      </div>

      {loadingEligibility && (
        <p className="exp-doc-retention-hint">{t("expenses.form.retention.loadingEligibility")}</p>
      )}

      {eligibilityError && (
        <ZHFormAlert
          type="error"
          message={t("expenses.form.retention.eligibilityErrorTitle")}
          detail={t("expenses.form.retention.eligibilityErrorDetail")}
        />
      )}

      {eligibility && (
        <div className="exp-doc-retention-eval">
          {eligibility.isEligible ? (
            <div className="exp-doc-retention-eval__main">
              <Badge
                label={t("expenses.form.retention.status.eligible")}
                variant="green"
                size="md"
              />
              <span className="exp-doc-retention-hint">
                {t("expenses.form.retention.status.eligibleDetail")}
              </span>
            </div>
          ) : (
            <ZHFormAlert
              type="attention"
              message={t("expenses.form.retention.notEligible")}
              detail={t("expenses.form.retention.notEligibleDetail")}
            />
          )}

          <div className="exp-doc-retention-status">
            <EligibilityBadge
              label={t("expenses.form.retention.vat")}
              eligible={eligibility.canRetainVat}
              candidates={eligibility.candidates.filter((c) => c.taxType === "IVA")}
            />
            <EligibilityBadge
              label={t("expenses.form.retention.income")}
              eligible={eligibility.canRetainIncome}
              candidates={eligibility.candidates.filter((c) => c.taxType === "RENTA")}
            />
            {eligibility.isSupplierExempt && (
              <span className="exp-doc-retention-flag">{t("expenses.form.retention.exempt")}</span>
            )}
            {eligibility.missingRetentionCode && (
              <span className="exp-doc-retention-flag">{t("expenses.form.retention.missingCode")}</span>
            )}
          </div>

          {eligibility.reasons.length > 0 && (
            <>
              <ZHBtn
                type="button"
                variant="ghost"
                size="sm"
                className="exp-doc-retention-eval__toggle"
                expanded={reasonsOpen}
                aria-controls={reasonsId}
                onClick={() => setReasonsToggled(!reasonsOpen)}
              >
                {reasonsOpen
                  ? t("expenses.form.retention.reasonsHide")
                  : t("expenses.form.retention.reasonsShow", { count: eligibility.reasons.length })}
              </ZHBtn>
              {reasonsOpen && (
                <ul id={reasonsId} className="exp-doc-retention-reasons">
                  {eligibility.reasons.map((reason, index) => (
                    <li key={`${index}-${reason}`}>{reason}</li>
                  ))}
                </ul>
              )}
            </>
          )}
        </div>
      )}

      {/* EXPENSE-UI-02 — checkbox oficial con su propio <label class="zh-checkbox-label">: ya no
          queda dentro de `.zh-field-control` (cuyo `input { width: 100% }` lo estiraba y partía el
          texto en vertical) ni anida un <label> dentro del <label> de ZHField. */}
      <div className="exp-doc-retention-apply">
        <ZhCheckbox
          checked={value.appliesRetention}
          disabled={disabled || !canApply}
          aria-label={t("expenses.form.retention.apply")}
          label={t("expenses.form.retention.applyLabel")}
          onChange={(event) => {
            const checked = event.target.checked;
            onChange({
              appliesRetention: checked,
              lines: checked && value.lines.length === 0 ? [newRetentionIntentLine()] : value.lines,
            });
          }}
        />
        {!canApply && (
          <p className="zh-field-hint zh-field-hint--muted">
            {t("expenses.form.retention.applyHint")}
          </p>
        )}
      </div>

      {value.appliesRetention && canApply && (
        <>
          <p className="exp-doc-retention-hint">{t("expenses.form.retention.autoNumber")}</p>

          {!canReadEmissionPoints && (
            <ZHFormAlert
              type="warning"
              message={t("expenses.form.retention.noEmissionPermission")}
              detail={t("expenses.form.retention.noEmissionPermissionDetail")}
            />
          )}

          <ZHGrid cols={2}>
            <ZHField label={t("expenses.form.retention.emissionPoint")} required>
              <ZhSelect
                value={value.emissionPointId}
                disabled={disabled || !canReadEmissionPoints}
                onChange={(event) => onChange({ emissionPointId: event.target.value })}
              >
                <option value="">{t("expenses.form.subcategory.select")}</option>
                {emissionPoints.map((point) => (
                  <option key={point.id} value={point.id}>
                    {point.establishmentCode}-{point.code} {point.name ? `- ${point.name}` : ""}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            <ZHField label={t("expenses.form.retention.issueDate")} required>
              <ZhDateInput
                value={value.issueDate}
                disabled={disabled}
                onChange={(event) => onChange({ issueDate: event.target.value })}
              />
            </ZHField>
          </ZHGrid>

          <div className="exp-doc-section__heading exp-doc-section__heading--actions">
            <div>
              <h3>{t("expenses.form.retention.linesTitle")}</h3>
              <p>{t("expenses.form.retention.linesHint")}</p>
            </div>
            <ZHBtn
              type="button"
              variant="secondary"
              size="sm"
              disabled={disabled}
              onClick={() => onChange({ lines: [...value.lines, newRetentionIntentLine()] })}
            >
              <span className="material-symbols-outlined" aria-hidden="true">
                add
              </span>
              {t("expenses.form.retention.addLine")}
            </ZHBtn>
          </div>

          <div className="exp-doc-lines">
            {value.lines.map((line, index) => (
              <div className="exp-doc-line" key={line.key}>
                <div className="exp-doc-line__top">
                  <span className="exp-doc-line__number">
                    {t("expenses.form.retention.line", { number: index + 1 })}
                  </span>
                  <ZHIconButton
                    icon="delete"
                    title={t("expenses.form.retention.removeLine")}
                    ariaLabel={t("expenses.form.retention.removeLineAria", { number: index + 1 })}
                    variant="ghost"
                    disabled={disabled}
                    onClick={() => removeLine(line.key)}
                  />
                </div>

                <div className="exp-doc-line__grid">
                  <ZHField label={t("expenses.form.retention.tax")} required density="compact">
                    <ZhSelect
                      density="compact"
                      value={line.taxType}
                      disabled={disabled}
                      onChange={(event) =>
                        updateLine(line.key, {
                          taxType: event.target.value as RetentionIntentFormState["lines"][number]["taxType"],
                        })
                      }
                    >
                      <option value="Vat" disabled={!eligibility?.canRetainVat}>
                        {t("expenses.form.retention.vat")}
                      </option>
                      <option value="Income" disabled={!eligibility?.canRetainIncome}>
                        {t("expenses.form.retention.income")}
                      </option>
                    </ZhSelect>
                  </ZHField>

                  <ZHField label={t("expenses.form.retention.code")} required density="compact">
                    <ZhTextInput
                      density="compact"
                      value={line.retentionCode}
                      maxLength={10}
                      disabled={disabled}
                      placeholder={t("expenses.form.retention.codePlaceholder")}
                      onChange={(event) => updateLine(line.key, { retentionCode: event.target.value })}
                    />
                  </ZHField>

                  <ZHField label={t("expenses.form.retention.base")} required density="compact">
                    <ZhDecimalInput
                      density="compact"
                      positiveOnly
                      precision="money"
                      value={line.baseAmount}
                      disabled={disabled}
                      onChange={(event) => updateLine(line.key, { baseAmount: event.target.value })}
                    />
                  </ZHField>

                  <ZHField label={t("expenses.form.retention.rate")} required density="compact">
                    <ZhDecimalInput
                      density="compact"
                      positiveOnly
                      precision="fiscalPercentage"
                      value={line.retentionRate}
                      disabled={disabled}
                      onChange={(event) => updateLine(line.key, { retentionRate: event.target.value })}
                    />
                  </ZHField>

                  <ZHField label={t("expenses.form.retention.retained")} required density="compact">
                    <ZhDecimalInput
                      density="compact"
                      positiveOnly
                      precision="money"
                      value={line.retainedAmount}
                      disabled={disabled}
                      onChange={(event) => updateLine(line.key, { retainedAmount: event.target.value })}
                    />
                  </ZHField>
                </div>

                <ZHField label={t("expenses.form.retention.description")} density="compact">
                  <ZhTextInput
                    density="compact"
                    value={line.description}
                    maxLength={300}
                    disabled={disabled}
                    onChange={(event) => updateLine(line.key, { description: event.target.value })}
                  />
                </ZHField>
              </div>
            ))}
          </div>
        </>
      )}
    </section>
  );
}

export default ExpenseRetentionSection;
