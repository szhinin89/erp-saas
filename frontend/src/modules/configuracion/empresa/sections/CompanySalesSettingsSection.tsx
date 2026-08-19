import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { message } from "../../../../lib/messages";
import { orgConfigService } from "../api/orgConfigService";
import { sriLookupFacade } from "../../../items/facades/sriLookupFacade";
import { paymentTermLookupFacade } from "../../../masterData/facades/paymentTermLookupFacade";
import { priceListService } from "../../../pricing/api/pricingService";

// ── Schema ─────────────────────────────────────────────────────────────────
// Propietario Empresa: DocTypeCode, PaymentMethodCode, PaymentTermId, PriceList.IsDefault.
// DefaultWarehouseId → Sucursal. DefaultEmissionPointId → EmissionPoint.IsDefault.
// Lista de precios predeterminada: fuente de verdad única = PriceList.IsDefault (backend),
// esta sección solo la reasigna vía priceListService.setDefault — nunca crea un OrgSetting
// paralelo (evitaría dos fuentes de verdad para el mismo default, ver auditoría
// COMPANY-SETTINGS-SALES-SECTION-01).
const schema = z.object({
  defaultDocTypeCode: z.string().nullable(),
  defaultSriPaymentMethodCode: z.string().nullable(),
  defaultPaymentTermId: z.string().uuid().nullable(),
  defaultPriceListId: z.string().uuid().nullable(),
});

type FormValues = z.infer<typeof schema>;

const defaultValues: FormValues = {
  defaultDocTypeCode: null,
  defaultSriPaymentMethodCode: null,
  defaultPaymentTermId: null,
  defaultPriceListId: null,
};

export function CompanySalesSettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();

  const canView = canShow("erp.companies.view");
  const canEdit = canShow("erp.companies.update");

  const [modalOpen, setModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // ── Catálogos ────────────────────────────────────────────────────────────
  const docTypesState = useAsync(() => sriLookupFacade.docTypes());
  const paymentMethodsState = useAsync(() => sriLookupFacade.paymentMethods());
  const paymentTermsState = useAsync(() => paymentTermLookupFacade.list());
  const priceListsState = useAsync(() => priceListService.list(true));

  // ── Valores actuales ─────────────────────────────────────────────────────
  const settingsState = useAsync(() =>
    orgConfigService.getCompanyInvoiceDefaults(),
  );

  const currentDefaultPriceListId =
    priceListsState.data?.find((pl) => pl.isDefault)?.id ?? null;

  const {
    register,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues,
  });

  const currentFormValues = (): FormValues => ({
    defaultDocTypeCode: settingsState.data?.defaultDocTypeCode ?? null,
    defaultSriPaymentMethodCode:
      settingsState.data?.defaultSriPaymentMethodCode ?? null,
    defaultPaymentTermId: settingsState.data?.defaultPaymentTermId ?? null,
    defaultPriceListId: currentDefaultPriceListId,
  });

  const openModal = () => {
    setSaveError(null);
    reset(currentFormValues());
    setModalOpen(true);
  };
  const closeModal = () => {
    setSaveError(null);
    reset(currentFormValues());
    setModalOpen(false);
  };

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaving(true);
    try {
      await orgConfigService.upsertCompanyInvoiceDefaults({
        defaultDocTypeCode: values.defaultDocTypeCode ?? null,
        defaultSriPaymentMethodCode: values.defaultSriPaymentMethodCode ?? null,
        defaultPaymentTermId: values.defaultPaymentTermId ?? null,
      });
      if (
        values.defaultPriceListId &&
        values.defaultPriceListId !== currentDefaultPriceListId
      ) {
        await priceListService.setDefault(values.defaultPriceListId);
      }
      message.success("Configuración de ventas guardada.");
      settingsState.refetch();
      priceListsState.refetch();
      setModalOpen(false);
    } catch (err) {
      const applied = applyServerErrors(err, setFieldError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          formatApiRequestError(err, {
            offline: t("common.apiUnreachable"),
            generic: t("common.errorGeneric"),
          }),
        );
    } finally {
      setSaving(false);
    }
  });

  const catalogsLoading =
    docTypesState.loading ||
    paymentMethodsState.loading ||
    paymentTermsState.loading ||
    priceListsState.loading;

  if (!canView)
    return <NoAccessPage title={t("settings.company.tabs.sales")} />;
  if (settingsState.loading) return <LoadingState />;

  const docTypes = docTypesState.data ?? [];
  const paymentMethods = paymentMethodsState.data ?? [];
  const paymentTerms = (paymentTermsState.data ?? []).filter(
    (pt) => pt.isActive,
  );
  const priceLists = priceListsState.data ?? [];

  const s = settingsState.data;
  const docTypeLabel = docTypes.find((d) => d.code === s?.defaultDocTypeCode);
  const paymentMethodLabel = paymentMethods.find(
    (p) => p.code === s?.defaultSriPaymentMethodCode,
  );
  const paymentTermLabel = paymentTerms.find(
    (pt) => pt.id === s?.defaultPaymentTermId,
  );
  const priceListLabel = priceLists.find(
    (pl) => pl.id === currentDefaultPriceListId,
  );

  return (
    <>
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              sell
            </span>
            <div>
              <p className="pg-section-label">
                {t("settings.company.sales.title")}
              </p>
              <p className="pg-section-desc">
                {t("settings.company.sales.description")}
              </p>
            </div>
          </div>
          {canEdit && (
            <div className="pg-section-header-right">
              <ZHBtn
                variant="secondary"
                size="md"
                type="button"
                onClick={openModal}
              >
                <span className="material-symbols-outlined">edit</span>
                {t("common.edit")}
              </ZHBtn>
            </div>
          )}
        </div>

        <div className="pg-section-body">
          <div className="pg-detail-grid">
            <div className="pg-detail-item">
              <span className="pg-detail-label">
                {t("settings.company.sales.defaultPriceList")}
              </span>
              <span className="pg-detail-value">
                {priceListLabel ? (
                  `${priceListLabel.code} — ${priceListLabel.name}`
                ) : (
                  <span className="pg-detail-empty">
                    {t("settings.company.sales.notConfigured")}
                  </span>
                )}
              </span>
            </div>

            <div className="pg-detail-item">
              <span className="pg-detail-label">
                {t("settings.company.sales.defaultDocType")}
              </span>
              <span className="pg-detail-value">
                {docTypeLabel ? (
                  `${docTypeLabel.code} — ${docTypeLabel.name}`
                ) : (
                  <span className="pg-detail-empty">
                    {t("settings.company.sales.notConfigured")}
                  </span>
                )}
              </span>
            </div>

            <div className="pg-detail-item">
              <span className="pg-detail-label">
                {t("settings.company.sales.defaultSriPaymentMethod")}
              </span>
              <span className="pg-detail-value">
                {paymentMethodLabel ? (
                  `${paymentMethodLabel.code} — ${paymentMethodLabel.name}`
                ) : (
                  <span className="pg-detail-empty">
                    {t("settings.company.sales.notConfigured")}
                  </span>
                )}
              </span>
            </div>

            <div className="pg-detail-item">
              <span className="pg-detail-label">
                {t("settings.company.sales.defaultPaymentTerm")}
              </span>
              <span className="pg-detail-value">
                {paymentTermLabel ? (
                  paymentTermLabel.name
                ) : (
                  <span className="pg-detail-empty">
                    {t("settings.company.sales.notConfigured")}
                  </span>
                )}
              </span>
            </div>
          </div>
        </div>
      </div>

      <ZHModal
        open={modalOpen}
        onClose={closeModal}
        size="md"
        title={t("settings.company.sales.title")}
        subtitle={t("settings.company.sales.modalSubtitle")}
        footer={
          <div className="zh-modal-footer-actions">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              disabled={saving}
              onClick={closeModal}
            >
              {t("common.cancel")}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              form="company-sales-settings-form"
              disabled={saving || !canEdit}
            >
              <span className="material-symbols-outlined">save</span>
              {saving ? t("common.saving") : t("common.save")}
            </ZHBtn>
          </div>
        }
      >
        {saveError && (
          <ZHPageNotice
            variant="error"
            message={t("common.errorPrefix")}
            detail={saveError}
          />
        )}

        {catalogsLoading ? (
          <LoadingState />
        ) : (
          <form id="company-sales-settings-form" onSubmit={onSubmit}>
            <ZHGrid cols={1}>
              <ZHField
                label={t("settings.company.sales.defaultPriceList")}
                error={errors.defaultPriceListId?.message}
              >
                <ZhSelect
                  disabled={saving || !canEdit || priceLists.length === 0}
                  {...register("defaultPriceListId")}
                >
                  <option value="">
                    — {t("settings.company.sales.notConfigured")} —
                  </option>
                  {priceLists.map((pl) => (
                    <option key={pl.id} value={pl.id}>
                      {pl.code} — {pl.name}
                    </option>
                  ))}
                </ZhSelect>
              </ZHField>
              {priceLists.length === 0 && (
                <p className="zh-text-muted zh-text-xs">
                  {t("settings.company.sales.noPriceLists")}
                </p>
              )}

              <ZHField
                label={t("settings.company.sales.defaultDocType")}
                error={errors.defaultDocTypeCode?.message}
              >
                <ZhSelect
                  disabled={saving || !canEdit}
                  {...register("defaultDocTypeCode")}
                >
                  <option value="">
                    — {t("settings.company.sales.notConfigured")} —
                  </option>
                  {docTypes.map((dt) => (
                    <option key={dt.code} value={dt.code}>
                      {dt.code} — {dt.name}
                    </option>
                  ))}
                </ZhSelect>
              </ZHField>

              <ZHField
                label={t("settings.company.sales.defaultSriPaymentMethod")}
                error={errors.defaultSriPaymentMethodCode?.message}
              >
                <ZhSelect
                  disabled={saving || !canEdit}
                  {...register("defaultSriPaymentMethodCode")}
                >
                  <option value="">
                    — {t("settings.company.sales.notConfigured")} —
                  </option>
                  {paymentMethods.map((pm) => (
                    <option key={pm.code} value={pm.code}>
                      {pm.code} — {pm.name}
                    </option>
                  ))}
                </ZhSelect>
              </ZHField>

              <ZHField
                label={t("settings.company.sales.defaultPaymentTerm")}
                error={errors.defaultPaymentTermId?.message}
              >
                <ZhSelect
                  disabled={saving || !canEdit}
                  {...register("defaultPaymentTermId")}
                >
                  <option value="">
                    — {t("settings.company.sales.notConfigured")} —
                  </option>
                  {paymentTerms.map((pt) => (
                    <option key={pt.id} value={pt.id}>
                      {pt.name}
                    </option>
                  ))}
                </ZhSelect>
              </ZHField>
            </ZHGrid>
          </form>
        )}
      </ZHModal>
    </>
  );
}
