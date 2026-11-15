import { Badge } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhDateTimeInput } from "../../../components/zh/inputs/ZhDateTimeInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { useI18n } from "../../../i18n/i18n";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

type PurchasesCtx = ReturnType<typeof usePurchasesPage>;

export function PurchaseExtrasPanel({ ctx }: { ctx: PurchasesCtx }) {
  const { t } = useI18n();
  return (

          <div className="pf-extras">
            <div className="pf-extras__bar">
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                icon="qr_code_2"
                expanded={ctx.showElectronic}
                aria-controls="purchase-electronic-panel"
                onClick={() => ctx.setShowElectronic((v) => !v)}
              >
                {t("purchases.electronic.title", "Información electrónica")}
                {!ctx.showElectronic &&
                  (ctx.formWatch.accessKey || ctx.formWatch.authorizationNumber) && (
                    <Badge
                      variant="info"
                      label={t("purchases.electronic.hasData", "Con datos")}
                      className="pf-extras__status"
                    />
                  )}
              </ZHBtn>
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                icon="notes"
                expanded={ctx.showNotes}
                aria-controls="purchase-notes-panel"
                onClick={() => ctx.setShowNotes((v) => !v)}
              >
                {t("purchases.notes.title", "Observaciones")}
                {!ctx.showNotes && ctx.formWatch.notes && (
                  <Badge
                    variant="info"
                    label={t("purchases.notes.hasNotes", "Con notas")}
                    className="pf-extras__status"
                  />
                )}
              </ZHBtn>
            </div>
            {ctx.showElectronic && (
              <div id="purchase-electronic-panel" className="pf-extras__panel">
                <div className="pf-mini-card__row pf-mini-card__row--3col">
                  <ZHField
                    density="compact"
                    label={t("purchases.electronic.accessKey", "Clave de acceso")}
                  >
                    <ZhTextInput
                      {...ctx.register("accessKey")}
                      placeholder={t("purchases.electronic.accessKeyPlaceholder", "49 dígitos")}
                      maxLength={49}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  <ZHField
                    density="compact"
                    label={
                      <>
                        {t("purchases.electronic.authorizationNumber", "Nro. autorización")}
                        <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_SRI_AUTHORIZATION} />
                      </>
                    }
                  >
                    <ZhTextInput
                      {...ctx.register("authorizationNumber")}
                      maxLength={49}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  {/* authorizationDate: ZhDateTimeInput (type="datetime-local", fecha + hora)
                      — ZhDateInput fuerza type="date" (sin hora); no es equivalente.
                      El valor debe llegar ya normalizado a yyyy-MM-ddTHH:mm vía
                      toDateTimeLocalInputValue() al poblar el formulario (reset()). */}
                  <ZHField
                    density="compact"
                    label={
                      <>
                        {t("purchases.electronic.authorizationDate", "Fecha y hora autorización")}
                        <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_SRI_AUTHORIZATION_DATE} />
                      </>
                    }
                  >
                    <ZhDateTimeInput
                      density="compact"
                      {...ctx.register("authorizationDate")}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                </div>
              </div>
            )}
            {ctx.showNotes && (
              <div id="purchase-notes-panel" className="pf-extras__panel">
                <ZHField density="compact">
                  <ZhTextarea
                    {...ctx.register("notes")}
                    rows={3}
                    placeholder={t(
                      "purchases.notes.placeholder",
                      "Ingrese notas adicionales o descripciones del servicio/producto...",
                    )}
                    disabled={ctx.fieldDisabled}
                  />
                </ZHField>
              </div>
            )}
          </div>

  );
}
