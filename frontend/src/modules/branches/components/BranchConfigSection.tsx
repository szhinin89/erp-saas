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
import { branchInvoiceDefaultsFacade } from "../../configuracion/empresa/facades/branchInvoiceDefaultsFacade";
import { warehouseLookupFacade } from "../../inventory/facades/warehouseLookupFacade";

// ── Schema ─────────────────────────────────────────────────────────────────
// Propietario Sucursal: DefaultWarehouseId.
// «Sin configurar» (option value="") se registra como null vía setValueAs; null = el backend
// resuelve la bodega principal de la sucursal (Warehouse.IsMain).
const schema = z.object({
  defaultWarehouseId: z.string().uuid().nullable(),
});

type FormValues = z.infer<typeof schema>;

const emptyToNull = (value: unknown) => (value === "" ? null : value);

const MAIN_WAREHOUSE_FALLBACK_HINT =
  "Sin configurar: las facturas usan la bodega principal de la sucursal.";
const INVALID_WAREHOUSE_NOTICE =
  "La bodega configurada ya no es válida (no existe, está inactiva o es de otra sucursal). Las facturas exigirán seleccionar la bodega manualmente hasta corregirla.";
const SETTINGS_LOAD_ERROR = "No se pudo cargar la configuración de la sucursal.";
const WAREHOUSES_LOAD_ERROR = "No se pudieron cargar las bodegas de la sucursal.";

interface Props {
  branchId: string;
}

export function BranchConfigSection({ branchId }: Props) {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();

  // BRANCH-CONFIG-READ-PERMISSION-01: editar exige el permiso del PUT
  // /org-config/branch/{id}/invoice-defaults (erp.companies.update, contrato congelado) y el del
  // listado de bodegas que alimenta el selector (inventory.warehouses.view).
  const hasEditPermissions =
    canShow("erp.companies.update") && canShow("inventory.warehouses.view");

  const [modalOpen, setModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // ── Bodegas de esta sucursal ──────────────────────────────────────────────
  const warehousesState = useAsync(
    () => warehouseLookupFacade.list("active", undefined, branchId),
    true,
    [branchId],
  );

  // ── Configuración actual de la sucursal ───────────────────────────────────
  const settingsState = useAsync(
    () => branchInvoiceDefaultsFacade.getBranchInvoiceDefaults(branchId),
    true,
    [branchId],
  );

  // Un error de carga se muestra como tal; nunca como «sin configurar» (p. ej. 403 por sucursal
  // no autorizada o 404). Sin configuración cargada no se ofrece Editar.
  const settingsLoaded = settingsState.data != null && !settingsState.error;
  const canEdit = hasEditPermissions && settingsLoaded;

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
      await branchInvoiceDefaultsFacade.upsertBranchInvoiceDefaults(branchId, {
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
  const configuredWarehouseId = settingsState.data?.defaultWarehouseId ?? null;
  const warehouseLabel = warehouses.find((w) => w.id === configuredWarehouseId);
  // Solo se declara inválida cuando la lista de bodegas activas cargó: el backend es fail-closed
  // con ese valor (no cae a la bodega principal).
  const configuredIsInvalid =
    configuredWarehouseId !== null &&
    warehousesState.data != null &&
    !warehouseLabel;
  let emptyWarehouseText = "—";
  if (configuredIsInvalid) emptyWarehouseText = "Bodega no válida";
  else if (configuredWarehouseId === null)
    emptyWarehouseText = MAIN_WAREHOUSE_FALLBACK_HINT;

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
          {settingsState.error && (
            <ZHPageNotice
              variant="error"
              message={SETTINGS_LOAD_ERROR}
              detail={settingsState.error}
            />
          )}
          {settingsLoaded && warehousesState.error && (
            <ZHPageNotice
              variant="warning"
              message={WAREHOUSES_LOAD_ERROR}
              detail={warehousesState.error}
            />
          )}
          {settingsLoaded && (
            <div className="pg-detail-grid">
              <div className="pg-detail-item">
                <span className="pg-detail-label">Bodega por Defecto</span>
                <span className="pg-detail-value">
                  {warehouseLabel ? (
                    `${warehouseLabel.name}${warehouseLabel.code ? ` (${warehouseLabel.code})` : ""}`
                  ) : (
                    <span className="pg-detail-empty">{emptyWarehouseText}</span>
                  )}
                </span>
              </div>
            </div>
          )}
          {configuredIsInvalid && (
            <ZHPageNotice variant="warning" message={INVALID_WAREHOUSE_NOTICE} />
          )}
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

        {configuredIsInvalid && (
          <ZHPageNotice variant="warning" message={INVALID_WAREHOUSE_NOTICE} />
        )}

        {warehousesState.error && (
          <ZHPageNotice
            variant="warning"
            message={WAREHOUSES_LOAD_ERROR}
            detail={warehousesState.error}
          />
        )}

        {warehousesState.loading ? (
          <LoadingState />
        ) : (
          <form id="branch-config-form" onSubmit={onSubmit}>
            <ZHField
              label="Bodega por Defecto"
              hint={
                !warehousesState.error && warehouses.length === 0
                  ? "No hay bodegas activas para esta sucursal."
                  : MAIN_WAREHOUSE_FALLBACK_HINT
              }
              error={errors.defaultWarehouseId?.message}
            >
              <select
                disabled={saving || !canEdit}
                {...register("defaultWarehouseId", { setValueAs: emptyToNull })}
              >
                <option value="">— Sin configurar —</option>
                {configuredIsInvalid && configuredWarehouseId && (
                  <option value={configuredWarehouseId} disabled>
                    — Bodega configurada no válida —
                  </option>
                )}
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
