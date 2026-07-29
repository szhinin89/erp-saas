import { Controller } from "react-hook-form";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid, ZHBtn } from "../../../components/zh/ZHForm";
import { CustomerPicker } from "../../sales/components/CustomerPicker";
import type { CashRegistersPageContext } from "../hooks/useCashRegistersPage";

type Props = Pick<
  CashRegistersPageContext,
  | "editingId"
  | "editingCode"
  | "editingName"
  | "editingHasHistory"
  | "saving"
  | "saveError"
  | "branches"
  | "loadingBranches"
  | "emissionPoints"
  | "loadingEmissionPoints"
  | "derivedEstablishmentCode"
  | "warehouses"
  | "loadingWarehouses"
  | "register"
  | "control"
  | "setValue"
  | "watch"
  | "errors"
  | "closePanel"
  | "save"
>;

export function CashRegistersFormPanel({
  editingId,
  editingCode,
  editingName,
  editingHasHistory,
  saving,
  saveError,
  branches,
  loadingBranches,
  emissionPoints,
  loadingEmissionPoints,
  derivedEstablishmentCode,
  warehouses,
  loadingWarehouses,
  register,
  control,
  setValue,
  watch,
  errors,
  closePanel,
  save,
}: Props) {
  const isEdit = Boolean(editingId);
  const watchedBranchId = watch("branchId");

  return (
    <div>
      {/* Panel header */}
      <div className="cfg-panel-hd">
        <span className="material-symbols-outlined cfg-panel-hd__icon">
          {isEdit ? "edit" : "add_circle"}
        </span>
        <div>
          <p className="cfg-panel-hd__title">
            {isEdit ? (editingName ?? "Editar Caja") : "Nueva Caja"}
          </p>
          {isEdit && editingCode && (
            <p className="cfg-panel-hd__sub">Código: {editingCode}</p>
          )}
        </div>
      </div>

      {saveError && (
        <div className="cfg-panel-error">
          <ZHPageNotice variant="error" message="Error" detail={saveError} />
        </div>
      )}

      {isEdit && editingHasHistory && (
        <div className="cfg-panel-error">
          <ZHPageNotice
            variant="info"
            message="Caja con historial operativo"
            detail="Esta caja ya registró aperturas, movimientos o cierres. Su Sucursal, Código y Punto de emisión quedan congelados por trazabilidad histórica — solo puede editar Nombre, Notas y Estado. Para cambiar de sucursal o punto de emisión, desactive esta caja y cree una nueva."
          />
        </div>
      )}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="cfg-panel-body">
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  info
                </span>
                <span className="pg-section-label">Información de la caja</span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField
                  label="Sucursal"
                  required
                  error={errors.branchId?.message}
                >
                  <select
                    className="zh-input"
                    disabled={isEdit || saving || loadingBranches}
                    {...register("branchId")}
                  >
                    <option value="">
                      {loadingBranches ? "Cargando…" : "— seleccionar —"}
                    </option>
                    {branches.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.code ? `${b.code} — ${b.name}` : b.name}
                      </option>
                    ))}
                  </select>
                </ZHField>

                <ZHField label="Código" required error={errors.code?.message}>
                  {isEdit ? (
                    <input
                      className="zh-input mono cr-code-readonly"
                      readOnly
                      value={editingCode ?? "—"}
                    />
                  ) : (
                    <input
                      className="zh-input mono"
                      placeholder="001"
                      maxLength={30}
                      disabled={saving}
                      {...register("code")}
                    />
                  )}
                </ZHField>
              </ZHGrid>

              <ZHField label="Nombre" required error={errors.name?.message}>
                <input
                  className="zh-input"
                  placeholder="Ej: Caja Principal"
                  disabled={saving}
                  {...register("name")}
                />
              </ZHField>
            </div>
          </div>

          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  point_of_sale
                </span>
                <span className="pg-section-label">
                  Configuración fiscal (SRI)
                </span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField label="Establecimiento">
                  <input
                    className="zh-input mono cr-code-readonly"
                    readOnly
                    value={derivedEstablishmentCode ?? "—"}
                  />
                </ZHField>

                <ZHField
                  label="Punto de emisión"
                  required
                  error={errors.emissionPointId?.message}
                >
                  <select
                    className="zh-input"
                    disabled={
                      editingHasHistory ||
                      saving ||
                      loadingEmissionPoints ||
                      emissionPoints.length === 0
                    }
                    title={
                      editingHasHistory
                        ? "Caja con historial operativo: el punto de emisión no puede reasignarse."
                        : undefined
                    }
                    {...register("emissionPointId")}
                  >
                    <option value="">
                      {loadingEmissionPoints ? "Cargando…" : "— seleccionar —"}
                    </option>
                    {emissionPoints.map((ep) => (
                      <option key={ep.id} value={ep.id}>
                        {ep.code} — {ep.name ?? ep.code}
                      </option>
                    ))}
                  </select>
                </ZHField>
              </ZHGrid>

              <ZHField label="Notas" error={errors.notes?.message}>
                <textarea
                  className="zh-input"
                  rows={2}
                  disabled={saving}
                  {...register("notes")}
                />
              </ZHField>
            </div>
          </div>

          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  tune
                </span>
                <span className="pg-section-label">
                  Configuración operativa
                </span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField
                  label="Bodega por defecto"
                  error={errors.defaultWarehouseId?.message}
                >
                  <select
                    className="zh-input"
                    disabled={saving || loadingWarehouses || !watchedBranchId}
                    {...register("defaultWarehouseId")}
                  >
                    <option value="">
                      {loadingWarehouses
                        ? "Cargando…"
                        : "— sin bodega por defecto —"}
                    </option>
                    {warehouses.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.code ? `${w.code} — ${w.name}` : w.name}
                      </option>
                    ))}
                  </select>
                </ZHField>

                <ZHField
                  label="Cliente por defecto"
                  error={errors.defaultCustomerId?.message}
                >
                  <Controller
                    name="defaultCustomerId"
                    control={control}
                    render={({ field }) => (
                      <CustomerPicker
                        value={field.value || null}
                        onChange={(c) =>
                          setValue("defaultCustomerId", c?.id ?? "")
                        }
                        disabled={saving}
                      />
                    )}
                  />
                </ZHField>
              </ZHGrid>
            </div>
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="ghost"
              type="button"
              onClick={closePanel}
              disabled={saving}
            >
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" type="submit" disabled={saving}>
              {saving
                ? "Guardando…"
                : isEdit
                  ? "Guardar cambios"
                  : "Crear caja"}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}
