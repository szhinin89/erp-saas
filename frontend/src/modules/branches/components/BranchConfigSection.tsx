import { useState, useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { LoadingState } from "../../../components/PageShell";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../i18n/i18n";
import { useAsync } from "../../../hooks/useAsync";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { orgConfigService } from "../../configuracion/empresa/api/orgConfigService";
import { warehouseService } from "../../inventory/warehouses/api/warehouseService";

// ── Schema ─────────────────────────────────────────────────────────────────
// Propietario Sucursal: DefaultWarehouseId.
const schema = z.object({
  defaultWarehouseId: z.string().uuid().nullable(),
});

type FormValues = z.infer<typeof schema>;

interface Props {
  branchId: string;
}

export function BranchConfigSection({ branchId }: Props) {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();

  const canEdit = canShow("settings.branches.update");

  const [modalOpen, setModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // ── Bodegas de esta sucursal ──────────────────────────────────────────────
  const warehousesState = useAsync(
    () => warehouseService.list("active", undefined, branchId),
    true,
    [branchId],
  );

  // ── Configuración actual de la sucursal ───────────────────────────────────
  const settingsState = useAsync(
    () => orgConfigService.getBranchInvoiceDefaults(branchId),
    true,
    [branchId],
  );

  const {
    register,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { defaultWarehouseId: null },
  });

  useEffect(() => {
    if (!settingsState.data) {
      reset({ defaultWarehouseId: null });
      return;
    }
    reset({
      defaultWarehouseId: settingsState.data.defaultWarehouseId ?? null,
    });
  }, [settingsState.data, reset]);

  const openModal = () => {
    setSaveError(null);
    setModalOpen(true);
  };
  const closeModal = () => {
    setSaveError(null);
    reset({
      defaultWarehouseId: settingsState.data?.defaultWarehouseId ?? null,
    });
    setModalOpen(false);
  };

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaving(true);
    try {
      await orgConfigService.upsertBranchInvoiceDefaults(branchId, {
        defaultWarehouseId: values.defaultWarehouseId ?? null,
      });
      message.success("Configuración de sucursal guardada.");
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

  const warehouses = warehousesState.data ?? [];
  const warehouseLabel = warehouses.find(
    (w) => w.id === settingsState.data?.defaultWarehouseId,
  );

  if (settingsState.loading) return <LoadingState />;

  return (
    <>
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              tune
            </span>
            <div>
              <p className="pg-section-label">Configuraciones</p>
              <p className="pg-section-desc">
                Parámetros operativos de esta sucursal. La bodega por defecto se
                aplica al crear facturas de venta originadas desde esta
                sucursal.
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
              <span className="pg-detail-label">Bodega por Defecto</span>
              <span className="pg-detail-value">
                {warehouseLabel ? (
                  `${warehouseLabel.name}${warehouseLabel.code ? ` (${warehouseLabel.code})` : ""}`
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
        size="sm"
        title="Configuraciones de Sucursal"
        subtitle="Bodega por defecto para facturas de venta"
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
              form="branch-config-form"
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

        {warehousesState.loading ? (
          <LoadingState />
        ) : (
          <form id="branch-config-form" onSubmit={onSubmit}>
            <ZHField
              label="Bodega por Defecto"
              hint={
                warehouses.length === 0
                  ? "No hay bodegas activas para esta sucursal."
                  : undefined
              }
              error={errors.defaultWarehouseId?.message}
            >
              <select
                disabled={saving || !canEdit}
                {...register("defaultWarehouseId")}
              >
                <option value="">— Sin configurar —</option>
                {warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                    {w.code ? ` (${w.code})` : ""}
                  </option>
                ))}
              </select>
            </ZHField>
          </form>
        )}
      </ZHModal>
    </>
  );
}
