import { Badge } from "../../../components/PageShell";
import { ZHField } from "../../../components/zh/ZHForm";
import { SupplierSearchSelect } from "../../masterData/facades/supplierPickerFacade";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { useI18n } from "../../../i18n/i18n";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

type PurchasesCtx = ReturnType<typeof usePurchasesPage>;

export function PurchaseHeaderCards({ ctx }: { ctx: PurchasesCtx }) {
  const { t } = useI18n();
  return (

          <div className="pf-header-cards-row pf-header-cards-row--purchase">
            {/* Datos del Proveedor */}
            <div className="pf-mini-card" id="purchase-supplier" tabIndex={-1}>
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  storefront
                </span>
                {t("purchases.supplier.title", "Datos del proveedor")}
                <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_SUPPLIER} />
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label={t("purchases.supplier.searchLabel", "RUC o nombre")}
                  fieldError={ctx.errors.supplierId?.message || (ctx.isSupplierInactiveBlocking ? `${ctx.supplierInactiveMessage} ${ctx.supplierInactiveDetail}` : undefined)}
                  required
                >
                  <SupplierSearchSelect
                    value={ctx.formWatch.supplierId || null}
                    onChange={ctx.handleSupplierChange}
                    disabled={ctx.fieldDisabled}
                  />
                </ZHField>
                {ctx.supplierProfile && (
                  <ZHField
                    density="compact"
                    label={t("purchases.supplier.legalName", "Razón social")}
                  >
                    <div className="pf-mini-card__readonly">
                      {ctx.supplierProfile.name}
                    </div>
                  </ZHField>
                )}
                {ctx.supplierProfile && (
                  <div className="pf-mini-card__badges">
                    {ctx.supplierProfile.isRequiredToKeepAccounting ? (
                      <Badge
                        variant="success"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              check_circle
                            </span>
                            {t(
                              "purchases.supplier.accountingRequired",
                              "Obligado contabilidad",
                            )}
                          </>
                        }
                      />
                    ) : (
                      <Badge
                        variant="error"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              cancel
                            </span>
                            {t(
                              "purchases.supplier.accountingNotRequired",
                              "No obligado",
                            )}
                          </>
                        }
                      />
                    )}
                    {ctx.supplierProfile.config?.isRetentionExempt && (
                      <Badge
                        variant="warning"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              shield
                            </span>
                            {t(
                              "purchases.supplier.retentionExempt",
                              "Exento retención",
                            )}
                          </>
                        }
                      />
                    )}

                  </div>
                )}
              </div>
            </div>

            {/* Datos del Documento */}
            <div className="pf-mini-card" id="purchase-document" tabIndex={-1}>
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  description
                </span>
                {t("purchases.document.title", "Datos del documento")}
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label={t("purchases.document.docType", "Tipo de documento")}
                  required
                  fieldError={ctx.sriDocTypesUnavailable ? t("purchases.validation.sriDocTypesUnavailableDetail") : ctx.errors.docTypeCode?.message}
                >
                  <ZhSelect
                    value={ctx.formWatch.docTypeCode}
                    onChange={(e) =>
                      ctx.setValue("docTypeCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled || !ctx.canUseSriDocTypes}
                  >
                    <option value="">
                      {!ctx.sriDocTypesLoaded
                        ? t(
                            "purchases.document.docTypesLoading",
                            "Cargando tipos de documento...",
                          )
                        : t(
                            "purchases.document.docTypePlaceholder",
                            "Seleccione tipo de documento",
                          )}
                    </option>
                    {ctx.sriDocTypes.map((d) => (
                      <option key={d.code} value={d.code}>
                        {d.code} — {d.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <div className="pf-mini-card__row">
                  <ZHField
                    density="compact"
                    label={t("purchases.document.number", "Nro. documento")}
                    required
                    fieldError={ctx.errors.invoiceNumber?.message}
                  >
                    <ZhTextInput
                      {...ctx.register("invoiceNumber")}
                      placeholder={t(
                        "purchases.document.numberPlaceholder",
                        "001-001...",
                      )}
                      maxLength={30}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  <ZHField
                    density="compact"
                    label={t("purchases.document.issueDate", "Fecha emisión")}
                    required
                    fieldError={ctx.errors.issueDate?.message}
                  >
                    <ZhDateInput
                      {...ctx.register("issueDate")}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                </div>
                <ZHField
                  density="compact"
                  label={
                    <>
                      {t("purchases.logistics.destinationWarehouse", "Bodega destino")}
                      <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_WAREHOUSE} />
                    </>
                  }
                >
                  <ZhSelect
                    value={ctx.formWatch.globalWarehouseId}
                    onChange={(e) => ctx.applyGlobalWarehouse(e.target.value)}
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("purchases.logistics.selectWarehouse", "— Seleccionar bodega —")}
                    </option>
                    {ctx.warehouses.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.code ? `${w.code} — ${w.name}` : w.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
              </div>
            </div>

            {/* Condiciones */}
            <div className="pf-mini-card">
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  handshake
                </span>
                {t("purchases.conditions.title", "Condiciones")}
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label={t("purchases.conditions.sriPaymentMethod", "Forma de pago SRI")}
                >
                  <ZhSelect
                    value={ctx.formWatch.sriPaymentMethodCode}
                    onChange={(e) =>
                      ctx.setValue("sriPaymentMethodCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("common.selectPlaceholder", "— Seleccionar —")}
                    </option>
                    {ctx.sriPaymentMethods.map((m) => (
                      <option key={m.code} value={m.code}>
                        {m.code} — {m.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                {/* taxSupportCode: código de metadato documental SRI (Sustento
                    Tributario) — no participa en cálculo de impuestos/retenciones,
                    solo se persiste como dato del comprobante. Migración visual
                    únicamente, ver reporte 14B-2. */}
                <ZHField
                  density="compact"
                  label={t("purchases.conditions.taxSupport", "Sustento tributario")}
                >
                  <ZhSelect
                    value={ctx.formWatch.taxSupportCode}
                    onChange={(e) =>
                      ctx.setValue("taxSupportCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("common.selectPlaceholder", "— Seleccionar —")}
                    </option>
                    {ctx.sriTaxSupports.map((s) => (
                      <option key={s.code} value={s.code}>
                        {s.code} — {s.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <ZHField
                  density="compact"
                  label={t("purchases.conditions.paymentTerm", "Condición de pago")}
                >
                  <ZhSelect
                    value={ctx.formWatch.paymentTermId}
                    onChange={(e) =>
                      ctx.handlePaymentTermChange(e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">
                      {t("purchases.conditions.supplierDefault", "— Del proveedor —")}
                    </option>
                    {ctx.paymentTermsList
                      .filter((p) => p.isActive)
                      .map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} ({p.summary})
                        </option>
                      ))}
                  </ZhSelect>
                </ZHField>
              </div>
            </div>
          </div>

  );
}
