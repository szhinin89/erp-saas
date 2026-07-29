import { useState, useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { message } from "../../../../lib/messages";
import { orgConfigService } from "../api/orgConfigService";
import { sriLookupService } from "../../../items/catalog/api/catalogService";
import { paymentTermService } from "../../../masterData/api/paymentTermService";

// ── Schema ─────────────────────────────────────────────────────────────────
// Propietario Empresa: DocTypeCode, PaymentMethodCode, PaymentTermId.
// DefaultWarehouseId → Sucursal. DefaultEmissionPointId → EmissionPoint.IsDefault.
const schema = z.object({
  defaultDocTypeCode: z.string().nullable(),
  defaultSriPaymentMethodCode: z.string().nullable(),
  defaultPaymentTermId: z.string().uuid().nullable(),
});

type FormValues = z.infer<typeof schema>;

const defaultValues: FormValues = {
  defaultDocTypeCode: null,
  defaultSriPaymentMethodCode: null,
  defaultPaymentTermId: null,
};

export function CompanyOrgConfigSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();

  const canView = canShow("configuracion.empresa.view");
  const canEdit = canShow("configuracion.empresa.edit");

  const [modalOpen, setModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // ── Catálogos ────────────────────────────────────────────────────────────
  const docTypesState = useAsync(() => sriLookupService.docTypes());
  const paymentMethodsState = useAsync(() => sriLookupService.paymentMethods());
  const paymentTermsState = useAsync(() => paymentTermService.list());

  // ── Valores actuales ─────────────────────────────────────────────────────
  const settingsState = useAsync(() =>
    orgConfigService.getCompanyInvoiceDefaults(),
  );

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

  useEffect(() => {
    if (!settingsState.data) return;
    reset({
      defaultDocTypeCode: settingsState.data.defaultDocTypeCode ?? null,
      defaultSriPaymentMethodCode:
        settingsState.data.defaultSriPaymentMethodCode ?? null,
      defaultPaymentTermId: settingsState.data.defaultPaymentTermId ?? null,
    });
  }, [settingsState.data, reset]);

  const openModal = () => {
    setSaveError(null);
    setModalOpen(true);
  };
  const closeModal = () => {
    setSaveError(null);
    if (settingsState.data) {
      reset({
        defaultDocTypeCode: settingsState.data.defaultDocTypeCode ?? null,
        defaultSriPaymentMethodCode:
          settingsState.data.defaultSriPaymentMethodCode ?? null,
        defaultPaymentTermId: settingsState.data.defaultPaymentTermId ?? null,
      });
    }
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
      message.success("Configuración de empresa guardada.");
      settingsState.refetch();
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
    paymentTermsState.loading;

  if (!canView) return <NoAccessPage title="Configuración Organizacional" />;
  if (settingsState.loading) return <LoadingState />;

  const docTypes = docTypesState.data ?? [];
  const paymentMethods = paymentMethodsState.data ?? [];
  const paymentTerms = (paymentTermsState.data ?? []).filter(
    (pt) => pt.isActive,
  );

  const s = settingsState.data;
  const docTypeLabel = docTypes.find((d) => d.code === s?.defaultDocTypeCode);
  const paymentMethodLabel = paymentMethods.find(
    (p) => p.code === s?.defaultSriPaymentMethodCode,
  );
  const paymentTermLabel = paymentTerms.find(
    (pt) => pt.id === s?.defaultPaymentTermId,
  );

  return (
    <>
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              tune
            </span>
            <div>
              <p className="pg-section-label">Configuraciones de Empresa</p>
              <p className="pg-section-desc">
                Parámetros de factura de venta por defecto configurados a nivel
                empresa. Las sucursales administran su propia bodega por defecto
                desde <em>Editar Sucursal → Configuraciones</em>.
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
                Editar
              </ZHBtn>
            </div>
          )}
        </div>

        <div className="pg-section-body">
          <div className="pg-detail-grid">
            <div className="pg-detail-item">
              <span className="pg-detail-label">Tipo de Documento</span>
              <span className="pg-detail-value">
                {docTypeLabel ? (
                  `${docTypeLabel.code} — ${docTypeLabel.name}`
                ) : (
                  <span className="pg-detail-empty">Sin configurar</span>
                )}
              </span>
            </div>

            <div className="pg-detail-item">
              <span className="pg-detail-label">Forma de Pago SRI</span>
              <span className="pg-detail-value">
                {paymentMethodLabel ? (
                  `${paymentMethodLabel.code} — ${paymentMethodLabel.name}`
                ) : (
                  <span className="pg-detail-empty">Sin configurar</span>
                )}
              </span>
            </div>

            <div className="pg-detail-item">
              <span className="pg-detail-label">Condición de Pago</span>
              <span className="pg-detail-value">
                {paymentTermLabel ? (
                  paymentTermLabel.name
                ) : (
                  <span className="pg-detail-empty">Sin configurar</span>
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
        title="Configuraciones de Empresa"
        subtitle="Valores por defecto para nuevas facturas de venta"
        footer={
          <div className="zh-modal-footer-actions">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              disabled={saving}
              onClick={closeModal}
            >
              Cancelar
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              form="company-org-config-form"
              disabled={saving || !canEdit}
            >
              <span className="material-symbols-outlined">save</span>
              {saving ? t("common.saving") : "Guardar"}
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
          <form id="company-org-config-form" onSubmit={onSubmit}>
            <ZHGrid cols={1}>
              <ZHField
                label="Tipo de Documento"
                error={errors.defaultDocTypeCode?.message}
              >
                <select
                  disabled={saving || !canEdit}
                  {...register("defaultDocTypeCode")}
                >
                  <option value="">— Sin configurar —</option>
                  {docTypes.map((dt) => (
                    <option key={dt.code} value={dt.code}>
                      {dt.code} — {dt.name}
                    </option>
                  ))}
                </select>
              </ZHField>

              <ZHField
                label="Forma de Pago SRI"
                error={errors.defaultSriPaymentMethodCode?.message}
              >
                <select
                  disabled={saving || !canEdit}
                  {...register("defaultSriPaymentMethodCode")}
                >
                  <option value="">— Sin configurar —</option>
                  {paymentMethods.map((pm) => (
                    <option key={pm.code} value={pm.code}>
                      {pm.code} — {pm.name}
                    </option>
                  ))}
                </select>
              </ZHField>

              <ZHField
                label="Condición de Pago"
                error={errors.defaultPaymentTermId?.message}
              >
                <select
                  disabled={saving || !canEdit}
                  {...register("defaultPaymentTermId")}
                >
                  <option value="">— Sin configurar —</option>
                  {paymentTerms.map((pt) => (
                    <option key={pt.id} value={pt.id}>
                      {pt.name}
                    </option>
                  ))}
                </select>
              </ZHField>
            </ZHGrid>
          </form>
        )}
      </ZHModal>
    </>
  );
}
