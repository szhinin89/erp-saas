import { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhDateInput, ZhSelect, ZhTextarea } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { message } from "../../../lib/messages";
import {
  accountingApi,
  type AccountingOperationalPolicyDto,
  type AccountingOperationalPolicyVersionDto,
} from "../api/accountingApi";
import {
  operationalModeSchema,
  type OperationalModeFormValues,
} from "../schemas/operationalModeSchema";

import "../../../styles/shared/items-catalog.css";

const SETTINGS_UPDATE_PERMISSION = "accounting.settings.update";

/**
 * Contabilidad → Configuración → Modo operativo (ACCOUNTING-CONTINUITY-01). Auditoría de
 * reutilización: revisadas `PostingRulesPage.tsx` (lista de solo lectura con PageShell/ZHCard/
 * ZHDataTable/Badge/ZHPageNotice) y `ChartOfAccountsPage.tsx` (formulario RHF+Zod+ZHField/ZHGrid+
 * applyServerErrors). Sin componentes nuevos. Muestra la obligación fiscal (solo lectura, se edita
 * en los datos fiscales de la empresa), el modo vigente hoy y el historial de vigencias; con
 * `accounting.settings.update` permite registrar una vigencia nueva. Disabled se muestra
 * deshabilitado mientras el backend lo reporte no disponible.
 */
export function AccountingOperationalModePage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canUpdate = canShow(SETTINGS_UPDATE_PERMISSION);
  const [policy, setPolicy] = useState<AccountingOperationalPolicyDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");

  const form = useForm<OperationalModeFormValues>({
    resolver: zodResolver(operationalModeSchema),
    defaultValues: { mode: "Enabled", effectiveFrom: "", reason: "" },
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const dto = await accountingApi.getOperationalPolicy();
      setPolicy(dto);
      if (!form.getValues("effectiveFrom")) form.setValue("effectiveFrom", dto.today);
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.continuity.mode.error.load") }));
    } finally {
      setLoading(false);
    }
  }, [form, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const onSubmit = form.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      const dto = await accountingApi.setOperationalMode({
        mode: values.mode,
        effectiveFrom: values.effectiveFrom,
        reason: values.reason ? values.reason : null,
      });
      setPolicy(dto);
      message.success(t("accounting.continuity.mode.saved"));
    } catch (err: unknown) {
      const applied = applyServerErrors(err, form.setError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(formatApiRequestError(err, { generic: t("accounting.continuity.mode.error.save") }));
    } finally {
      setSaving(false);
    }
  });

  const modeLabel = (mode: string) => t(`accounting.continuity.mode.${mode}`);

  const columns: ZHDataTableColumn<AccountingOperationalPolicyVersionDto>[] = [
    {
      key: "mode",
      header: t("accounting.continuity.mode.column.mode"),
      render: (row) => (
        <Badge label={modeLabel(row.mode)} variant={row.mode === "Enabled" ? "green" : "gray"} />
      ),
    },
    {
      key: "from",
      header: t("accounting.continuity.mode.column.from"),
      render: (row) => formatDate(row.effectiveFrom),
    },
    {
      key: "to",
      header: t("accounting.continuity.mode.column.to"),
      render: (row) => (row.effectiveTo ? formatDate(row.effectiveTo) : t("accounting.continuity.mode.openEnded")),
    },
    {
      key: "reason",
      header: t("accounting.continuity.mode.column.reason"),
      render: (row) => row.reason ?? "—",
    },
  ];

  const disabledBlocked = !policy?.disabledAvailable || policy?.isAccountingRequired;

  return (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={t("accounting.continuity.mode.title")}
      subtitle={t("accounting.continuity.mode.subtitle")}
    >
      {policy && !policy.isConfigured && (
        <ZHPageNotice variant="warning" message={t("accounting.continuity.mode.notConfigured")} />
      )}

      <ZHCard
        title={t("accounting.continuity.mode.currentTitle")}
        actions={
          policy && (
            <div className="zh-form-actions-row">
              <Badge
                label={
                  policy.isAccountingRequired
                    ? t("accounting.continuity.mode.required")
                    : t("accounting.continuity.mode.notRequired")
                }
                variant={policy.isAccountingRequired ? "blue" : "gray"}
              />
              <Badge
                label={
                  policy.currentMode
                    ? `${t("accounting.continuity.mode.current")}: ${modeLabel(policy.currentMode)}`
                    : t("accounting.continuity.mode.noneToday")
                }
                variant={policy.currentMode === "Enabled" ? "green" : "orange"}
              />
            </div>
          )
        }
      >
        <h4>{t("accounting.continuity.mode.historyTitle")}</h4>
        <ZHDataTable
          columns={columns}
          rows={policy?.versions ?? []}
          rowKey={(row) => row.id}
          loading={loading}
          emptyMessage={t("accounting.continuity.mode.historyEmpty")}
        />
      </ZHCard>

      {canUpdate && policy && (
        <ZHCard title={t("accounting.continuity.mode.formTitle")}>
          {saveError && <ZHPageNotice variant="error" message={saveError} />}
          <ZHGrid cols={2}>
            <ZHField
              label={t("accounting.continuity.mode.field.mode")}
              required
              fieldError={form.formState.errors.mode?.message}
              hint={disabledBlocked ? t("accounting.continuity.mode.disabledUnavailable") : null}
            >
              <ZhSelect disabled={saving} {...form.register("mode")}>
                <option value="Enabled">{modeLabel("Enabled")}</option>
                <option value="Disabled" disabled={disabledBlocked}>
                  {modeLabel("Disabled")}
                </option>
              </ZhSelect>
            </ZHField>
            <ZHField
              label={t("accounting.continuity.mode.field.effectiveFrom")}
              required
              fieldError={form.formState.errors.effectiveFrom?.message}
              hint={t("accounting.continuity.mode.effectiveFromHint")}
            >
              <ZhDateInput className="zh-input" disabled={saving} {...form.register("effectiveFrom")} />
            </ZHField>
          </ZHGrid>
          <ZHField
            label={t("accounting.continuity.mode.field.reason")}
            fieldError={form.formState.errors.reason?.message}
          >
            <ZhTextarea maxLength={500} disabled={saving} {...form.register("reason")} />
          </ZHField>
          <div className="zh-form-actions-row">
            <ZHBtn type="button" onClick={() => void onSubmit()} disabled={saving}>
              {t("accounting.continuity.mode.save")}
            </ZHBtn>
          </div>
        </ZHCard>
      )}
    </PageShell>
  );
}
