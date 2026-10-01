import { Badge } from "../../../components/PageShell";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHToggleTile } from "../../../components/zh/ZHToggleTile";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { useI18n } from "../../../i18n/i18n";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

/** Solo lo que la definición de la retención en el borrador necesita del hook de la página. */
export type PurchaseRetentionDraftContext = Pick<
  ReturnType<typeof usePurchasesPage>,
  | "editing"
  | "saving"
  | "whPreview"
  | "whPreviewError"
  | "whLoading"
  | "refreshRetentionPreview"
  | "retentionIntent"
  | "updateRetentionIntent"
  | "canApplyRetention"
  | "emissionPoints"
  | "canReadEmissionPoints"
>;

/**
 * ZH-PURCHASE-RETENTION-CONFIRM-01 — borrador: vista previa (elegibilidad + montos calculados por el
 * backend, precargados y no editables) y la decisión de emitir la retención al confirmar, con su
 * punto de emisión y fecha. No hay acción "Emitir" separada: la retención viaja en "Confirmar compra".
 */
export function PurchaseRetentionDraft({ ctx }: { ctx: PurchaseRetentionDraftContext }) {
  const { t } = useI18n();
  const preview = ctx.whPreview;
  const intent = ctx.retentionIntent;
  return (
    <>
      {ctx.whLoading && (
        <div className="pf-retention__skip">
          {t("purchases.retention.loading", "Calculando la retención propuesta...")}
        </div>
      )}
      {ctx.whPreviewError && ctx.editing && (
        <>
          <ZHPageNotice
            variant="error"
            message={t("purchases.retention.previewFailed", "No se pudo calcular la retención")}
            detail={ctx.whPreviewError}
          />
          <ZHBtn
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => ctx.editing && void ctx.refreshRetentionPreview(ctx.editing.id)}
            disabled={ctx.whLoading}
          >
            {t("common.retry", "Reintentar")}
          </ZHBtn>
        </>
      )}
      {preview && !ctx.canApplyRetention && (
        <ZHPageNotice
          variant="neutral"
          message={t(
            "purchases.retention.notApplicable",
            "Esta compra no genera retención con la configuración actual.",
          )}
          detail={preview.skipReason}
        />
      )}
      {preview && ctx.canApplyRetention && (
        <>
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
              {preview.lines.map((l) => (
                <tr key={`${l.taxType}-${l.retentionCode}`}>
                  <td>
                    <Badge variant={l.taxType === "IVA" ? "info" : "warning"} label={l.taxType} />
                  </td>
                  <td className="zh-code-value">{l.retentionCode}</td>
                  <td>{l.retentionCodeName}</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue value={l.taxableBase} precision="money" />
                  </td>
                  <td className="zh-table-cell--num">{l.retentionPct}%</td>
                  <td className="zh-table-cell--num pf-retention-amount">
                    <ZHMoneyValue value={l.amountRetained} precision="money" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="pf-retention__total">
            {t("purchases.retention.totalToWithhold", "Total a retener")}:{" "}
            <ZHMoneyValue value={preview.totalRetained} precision="money" />
          </div>
          <ZHToggleTile
            active={intent.appliesRetention}
            icon="receipt_long"
            title={t("purchases.retention.applyOnConfirm", "Emitir la retención al confirmar la compra")}
            subtitle={t(
              "purchases.retention.applyOnConfirmHint",
              "Se emite junto con la compra en una sola operación; el registro ante el SRI se hace después.",
            )}
            disabled={ctx.saving}
            onClick={() => ctx.updateRetentionIntent({ appliesRetention: !intent.appliesRetention })}
          />
          {intent.appliesRetention && (
            <>
              <ZHPageNotice
                variant="info"
                message={t(
                  "purchases.retention.numberOnConfirm",
                  "El número de retención se generará automáticamente al confirmar la compra.",
                )}
              />
              {!ctx.canReadEmissionPoints && (
                <ZHPageNotice
                  variant="warning"
                  message={t(
                    "purchases.retention.noEmissionPointPermission",
                    "Sin permiso para leer puntos de emisión.",
                  )}
                  detail="settings.emission-points.view"
                />
              )}
              <ZHGrid cols={2}>
                <ZHField label={t("purchases.retention.emissionPoint", "Punto de emisión")} required>
                  <ZhSelect
                    value={intent.emissionPointId}
                    disabled={ctx.saving || !ctx.canReadEmissionPoints}
                    onChange={(event) => ctx.updateRetentionIntent({ emissionPointId: event.target.value })}
                  >
                    <option value="">{t("common.select", "Seleccione...")}</option>
                    {ctx.emissionPoints.map((point) => (
                      <option key={point.id} value={point.id}>
                        {point.establishmentCode}-{point.code}
                        {point.name ? ` - ${point.name}` : ""}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <ZHField label={t("purchases.retention.issueDate", "Fecha de emisión")} required>
                  <ZhDateInput
                    value={intent.issueDate}
                    disabled={ctx.saving}
                    onChange={(event) => ctx.updateRetentionIntent({ issueDate: event.target.value })}
                  />
                </ZHField>
              </ZHGrid>
            </>
          )}
        </>
      )}
    </>
  );
}
