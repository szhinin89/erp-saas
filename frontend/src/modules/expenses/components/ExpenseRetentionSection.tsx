import { useEffect, useState } from "react";
import { ZHBtn, ZHField, ZHFormAlert, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import {
  emissionPointsService,
  type EmissionPointListItemDto,
} from "../../emissionPoints/api/emissionPointsService";
import {
  expenseDocumentService,
  type ExpenseStatus,
  type RetentionDocumentDto,
  type RetentionEligibilityResult,
} from "../api/expenseDocumentService";
import {
  newRetentionIntentLine,
  type RetentionIntentFormState,
} from "../utils/expenseRetentionModel";

const RETENTION_STATUS_LABEL: Record<string, string> = {
  Draft: "Borrador",
  Issued: "Emitida",
  Cancelled: "Anulada",
};

const TAX_TYPE_LABEL: Record<string, string> = {
  Vat: "IVA",
  Income: "Renta",
};

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
}

export function ExpenseRetentionSection({
  expenseDocumentId,
  documentStatus,
  refreshKey,
  disabled,
  value,
  onChange,
  onEligibilityChange,
}: Props) {
  const { has } = usePermissionsUi();
  const canReadEmissionPoints = has("settings.emission-points.view");

  const [eligibility, setEligibility] = useState<RetentionEligibilityResult | null>(null);
  const [eligibilityError, setEligibilityError] = useState<string | null>(null);
  const [loadingEligibility, setLoadingEligibility] = useState(false);

  const [retentionDoc, setRetentionDoc] = useState<RetentionDocumentDto | null>(null);
  const [retentionDocError, setRetentionDocError] = useState<string | null>(null);
  const [loadingRetentionDoc, setLoadingRetentionDoc] = useState(false);

  const [emissionPoints, setEmissionPoints] = useState<EmissionPointListItemDto[]>([]);

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
    setEligibilityError(null);
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
        setEligibilityError("No se pudo evaluar la elegibilidad de retención de este gasto.");
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
    setRetentionDocError(null);
    expenseDocumentService
      .getExpenseRetention(expenseDocumentId)
      .then((result) => {
        if (!cancelled) setRetentionDoc(result);
      })
      .catch(() => {
        if (!cancelled) {
          setRetentionDocError("No se pudo consultar la retención asociada a este gasto.");
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
    emissionPointsService
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
      <section className="exp-doc-section" aria-label="Retención">
        <div className="exp-doc-section__heading">
          <h2>Retención</h2>
          <p>Guarde el borrador del gasto para evaluar si aplica retención.</p>
        </div>
      </section>
    );
  }

  if (!isDraftDocument) {
    return (
      <section className="exp-doc-section" aria-label="Retención">
        <div className="exp-doc-section__heading">
          <h2>Retención</h2>
        </div>
        {loadingRetentionDoc && <p className="exp-doc-retention-hint">Consultando retención asociada...</p>}
        {retentionDocError && (
          <ZHFormAlert type="error" message="No se pudo consultar la retención asociada" detail={retentionDocError} />
        )}
        {!loadingRetentionDoc && !retentionDocError && !retentionDoc && (
          <ZHFormAlert type="neutral" message="Sin retención asociada." />
        )}
        {retentionDoc && <RetentionDocumentSummary retention={retentionDoc} />}
      </section>
    );
  }

  const decimals = getDecimalConfig();
  const canApply = !!eligibility?.isEligible;

  const updateLine = (key: string, patch: Partial<RetentionIntentFormState["lines"][number]>) =>
    onChange({
      lines: value.lines.map((line) => (line.key === key ? { ...line, ...patch } : line)),
    });

  const removeLine = (key: string) =>
    onChange({ lines: value.lines.filter((line) => line.key !== key) });

  return (
    <section className="exp-doc-section" aria-label="Retención">
      <div className="exp-doc-section__heading">
        <h2>Retención</h2>
        <p>Evaluación tributaria del proveedor y la empresa actual para este gasto.</p>
      </div>

      {loadingEligibility && <p className="exp-doc-retention-hint">Evaluando elegibilidad de retención...</p>}

      {eligibilityError && (
        <ZHFormAlert type="error" message="No se pudo evaluar la retención" detail={eligibilityError} />
      )}

      {eligibility && (
        <div className="exp-doc-retention-status">
          <EligibilityBadge
            label="IVA"
            eligible={eligibility.canRetainVat}
            suggestedCode={eligibility.suggestedVatRetentionCode}
          />
          <EligibilityBadge
            label="Renta"
            eligible={eligibility.canRetainIncome}
            suggestedCode={eligibility.suggestedIncomeRetentionCode}
          />
          {eligibility.isSupplierExempt && (
            <span className="exp-doc-retention-flag">Proveedor exento</span>
          )}
          {eligibility.missingRetentionCode && (
            <span className="exp-doc-retention-flag">Falta código de retención activo</span>
          )}
        </div>
      )}

      {eligibility && eligibility.reasons.length > 0 && (
        <ul className="exp-doc-retention-reasons">
          {eligibility.reasons.map((reason, index) => (
            <li key={`${index}-${reason}`}>{reason}</li>
          ))}
        </ul>
      )}

      {eligibility && !eligibility.isEligible && (
        <ZHFormAlert
          type="attention"
          message="Este gasto no es elegible para retención con la configuración actual."
          detail="Revise los motivos anteriores — la empresa, el proveedor o el catálogo de códigos de retención."
        />
      )}

      <ZHField
        label="Aplicar retención a este gasto"
        density="compact"
        hint={
          !canApply
            ? "Disponible solo cuando la elegibilidad es positiva para IVA o Renta."
            : undefined
        }
      >
        <label className="exp-doc-retention-toggle">
          <input
            type="checkbox"
            checked={value.appliesRetention}
            disabled={disabled || !canApply}
            aria-label="Aplicar retención a este gasto"
            onChange={(event) => {
              const checked = event.target.checked;
              onChange({
                appliesRetention: checked,
                lines: checked && value.lines.length === 0 ? [newRetentionIntentLine()] : value.lines,
              });
            }}
          />
          <span>Generar retención al confirmar este gasto</span>
        </label>
      </ZHField>

      {value.appliesRetention && canApply && (
        <>
          <ZHFormAlert type="success" message="La retención se generará al confirmar este documento." />

          {!canReadEmissionPoints && (
            <ZHFormAlert
              type="warning"
              message="Sin permiso para leer puntos de emisión."
              detail="La selección de punto de emisión requiere settings.emission-points.view."
            />
          )}

          <ZHGrid cols={3}>
            <ZHField label="Punto de emisión" required>
              <ZhSelect
                value={value.emissionPointId}
                disabled={disabled || !canReadEmissionPoints}
                onChange={(event) => onChange({ emissionPointId: event.target.value })}
              >
                <option value="">Seleccione...</option>
                {emissionPoints.map((point) => (
                  <option key={point.id} value={point.id}>
                    {point.establishmentCode}-{point.code} {point.name ? `- ${point.name}` : ""}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            <ZHField
              label="Número de retención"
              required
              hint="Numeración automática pendiente de una fase futura — ingrese el número manualmente."
            >
              <ZhTextInput
                value={value.retentionNumber}
                maxLength={30}
                disabled={disabled}
                onChange={(event) => onChange({ retentionNumber: event.target.value })}
              />
            </ZHField>

            <ZHField label="Fecha de emisión" required>
              <ZhDateInput
                value={value.issueDate}
                disabled={disabled}
                onChange={(event) => onChange({ issueDate: event.target.value })}
              />
            </ZHField>
          </ZHGrid>

          <div className="exp-doc-section__heading exp-doc-section__heading--actions">
            <div>
              <h3>Líneas de retención</h3>
              <p>
                Código y porcentaje los revalida el servidor contra el catálogo SRI — no se
                sugieren valores por defecto.
              </p>
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
              Agregar línea
            </ZHBtn>
          </div>

          <div className="exp-doc-lines">
            {value.lines.map((line, index) => (
              <div className="exp-doc-line" key={line.key}>
                <div className="exp-doc-line__top">
                  <span className="exp-doc-line__number">Línea {index + 1}</span>
                  <ZHIconButton
                    icon="delete"
                    title="Quitar línea"
                    ariaLabel={`Quitar línea de retención ${index + 1}`}
                    variant="ghost"
                    disabled={disabled}
                    onClick={() => removeLine(line.key)}
                  />
                </div>

                <div className="exp-doc-line__grid">
                  <ZHField label="Impuesto" required density="compact">
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
                        {TAX_TYPE_LABEL.Vat}
                      </option>
                      <option value="Income" disabled={!eligibility?.canRetainIncome}>
                        {TAX_TYPE_LABEL.Income}
                      </option>
                    </ZhSelect>
                  </ZHField>

                  <ZHField label="Código de retención" required density="compact">
                    <ZhTextInput
                      density="compact"
                      value={line.retentionCode}
                      maxLength={10}
                      disabled={disabled}
                      placeholder="Ej. 303"
                      onChange={(event) => updateLine(line.key, { retentionCode: event.target.value })}
                    />
                  </ZHField>

                  <ZHField label="Base" required density="compact">
                    <ZhDecimalInput
                      density="compact"
                      positiveOnly
                      decimals={decimals.totalAmount}
                      value={line.baseAmount}
                      disabled={disabled}
                      onChange={(event) => updateLine(line.key, { baseAmount: event.target.value })}
                    />
                  </ZHField>

                  <ZHField label="% Retención" required density="compact">
                    <ZhDecimalInput
                      density="compact"
                      positiveOnly
                      decimals={decimals.percentage}
                      value={line.retentionRate}
                      disabled={disabled}
                      onChange={(event) => updateLine(line.key, { retentionRate: event.target.value })}
                    />
                  </ZHField>

                  <ZHField label="Valor retenido" required density="compact">
                    <ZhDecimalInput
                      density="compact"
                      positiveOnly
                      decimals={decimals.totalAmount}
                      value={line.retainedAmount}
                      disabled={disabled}
                      onChange={(event) => updateLine(line.key, { retainedAmount: event.target.value })}
                    />
                  </ZHField>
                </div>

                <ZHField label="Descripción" density="compact">
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

function EligibilityBadge({
  label,
  eligible,
  suggestedCode,
}: {
  label: string;
  eligible: boolean;
  suggestedCode: string | null;
}) {
  return (
    <span
      className={`exp-doc-retention-badge ${eligible ? "exp-doc-retention-badge--yes" : "exp-doc-retention-badge--no"}`}
    >
      {label}: {eligible ? "Aplica" : "No aplica"}
      {eligible && suggestedCode ? ` (${suggestedCode})` : ""}
    </span>
  );
}

function RetentionDocumentSummary({ retention }: { retention: RetentionDocumentDto }) {
  const decimals = getDecimalConfig().totalAmount;
  return (
    <div className="exp-doc-retention-summary">
      <ZHGrid cols={3}>
        <div>
          <span className="exp-doc-retention-summary__label">Número</span>
          <p>{retention.retentionNumber ?? "—"}</p>
        </div>
        <div>
          <span className="exp-doc-retention-summary__label">Fecha de emisión</span>
          <p>{retention.issueDate ? formatDate(retention.issueDate) : "—"}</p>
        </div>
        <div>
          <span className="exp-doc-retention-summary__label">Estado</span>
          <p>{RETENTION_STATUS_LABEL[retention.status] ?? retention.status}</p>
        </div>
      </ZHGrid>

      <div className="exp-doc-retention-summary__total">
        <span>Total retenido</span>
        <ZHMoneyValue value={retention.totalRetained} decimals={decimals} emphasis="grand" />
      </div>

      {retention.lines.length > 0 && (
        <div className="exp-doc-lines">
          {retention.lines.map((line) => (
            <div className="exp-doc-line" key={line.id}>
              <div className="exp-doc-line__top">
                <span className="exp-doc-line__number">
                  {TAX_TYPE_LABEL[line.taxType] ?? line.taxType} — {line.retentionCode}
                </span>
              </div>
              <div className="exp-doc-line__totals">
                <span>
                  Base <ZHMoneyValue value={line.baseAmount} decimals={decimals} currencySymbol="" />
                </span>
                <span>{line.retentionRate}%</span>
                <span>
                  Retenido{" "}
                  <ZHMoneyValue
                    value={line.retainedAmount}
                    decimals={decimals}
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

      {retention.cancelReason && (
        <ZHFormAlert type="warning" message="Retención anulada" detail={retention.cancelReason} />
      )}
    </div>
  );
}

export default ExpenseRetentionSection;
