import { useState } from "react";
import { Controller } from "react-hook-form";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  ZHField,
  ZHToggle,
  ZHGrid,
  ZHBtn,
} from "../../../components/zh/ZHForm";
import { Badge } from "../../../components/PageShell";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import {
  EMISSION_TYPE_ELECTRONIC,
  EMISSION_TYPE_PHYSICAL,
} from "../api/emissionPointsService";
import type { EmissionPointsPageContext } from "../hooks/useEmissionPointsPage";

type Props = Pick<
  EmissionPointsPageContext,
  | "editingId"
  | "editingCode"
  | "editingName"
  | "saving"
  | "saveError"
  | "establishments"
  | "loadingEstablishments"
  | "register"
  | "control"
  | "errors"
  | "closePanel"
  | "save"
>;

export function EmissionPointsFormPanel({
  editingId,
  editingCode,
  editingName,
  saving,
  saveError,
  establishments,
  loadingEstablishments,
  register,
  control,
  errors,
  closePanel,
  save,
}: Props) {
  const isEdit = Boolean(editingId);
  const [activeTab] = useState<"general">("general");

  return (
    <div>
      {/* Panel header */}
      <div className="cfg-panel-hd">
        <span className="material-symbols-outlined cfg-panel-hd__icon">
          {isEdit ? "edit" : "add_circle"}
        </span>
        <div>
          <p className="cfg-panel-hd__title">
            {isEdit
              ? (editingName ?? "Editar Punto de EmisiÃ³n")
              : "Nuevo Punto de EmisiÃ³n"}
          </p>
          {isEdit && editingCode && (
            <p className="cfg-panel-hd__sub">CÃ³digo: {editingCode}</p>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="prd-tabs cfg-panel-tabs">
        <button
          type="button"
          className={`prd-tab-btn ${activeTab === "general" ? "prd-tab-btn--active" : ""}`}
        >
          General
        </button>
      </div>

      {/* Error */}
      {saveError && (
        <div className="cfg-panel-error">
          <ZHPageNotice variant="error" message="Error" detail={saveError} />
        </div>
      )}

      {/* Form */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="cfg-panel-body">
          {/* InformaciÃ³n del punto de emisiÃ³n */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  info
                </span>
                <span className="pg-section-label">
                  InformaciÃ³n del punto de emisiÃ³n
                </span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField
                  label="Sucursal"
                  required
                  error={errors.establishmentId?.message}
                >
                  <ZhSelect
                    className="zh-input"
                    disabled={isEdit || saving || loadingEstablishments}
                    {...register("establishmentId")}
                  >
                    <option value="">
                      {loadingEstablishments ? "Cargandoâ€¦" : "â€” seleccionar â€”"}
                    </option>
                    {establishments.map((e) => (
                      <option key={e.id} value={e.id}>
                        {e.code} â€” {e.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>

                <ZHField label="CÃ³digo" required error={errors.code?.message}>
                  {isEdit ? (
                    <ZhTextInput
                      className="zh-input mono ep-code-readonly"
                      readOnly
                      value={editingCode ?? "â€”"}
                    />
                  ) : (
                    <ZhTextInput
                      className="zh-input mono"
                      placeholder="001"
                      maxLength={3}
                      disabled={saving}
                      {...register("code")}
                    />
                  )}
                </ZHField>
              </ZHGrid>

              <ZHField label="Nombre" error={errors.name?.message}>
                <ZhTextInput
                  className="zh-input"
                  placeholder="Ej: Caja Principal"
                  disabled={saving}
                  {...register("name")}
                />
              </ZHField>
            </div>
          </div>

          {/* ConfiguraciÃ³n de emisiÃ³n */}
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  settings_input_component
                </span>
                <span className="pg-section-label">
                  ConfiguraciÃ³n de emisiÃ³n
                </span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHField
                label="Tipo de emisiÃ³n"
                required
                error={errors.emissionType?.message}
              >
                <Controller
                  name="emissionType"
                  control={control}
                  render={({ field }) => (
                    <div className="zh-radio-group">
                      <label className="zh-radio-option">
                        <input
                          type="radio"
                          value={EMISSION_TYPE_ELECTRONIC}
                          checked={field.value === EMISSION_TYPE_ELECTRONIC}
                          onChange={() =>
                            field.onChange(EMISSION_TYPE_ELECTRONIC)
                          }
                          disabled={saving}
                        />
                        <Badge label="ElectrÃ³nico" variant="info" size="md" />
                        <span className="zh-radio-desc">
                          Documentos emitidos electrÃ³nicamente (SRI)
                        </span>
                      </label>
                      <label className="zh-radio-option">
                        <input
                          type="radio"
                          value={EMISSION_TYPE_PHYSICAL}
                          checked={field.value === EMISSION_TYPE_PHYSICAL}
                          onChange={() =>
                            field.onChange(EMISSION_TYPE_PHYSICAL)
                          }
                          disabled={saving}
                        />
                        <Badge label="FÃ­sico" variant="neutral" size="md" />
                        <span className="zh-radio-desc">
                          Documentos impresos en papel
                        </span>
                      </label>
                    </div>
                  )}
                />
              </ZHField>

              <Controller
                name="isDefault"
                control={control}
                render={({ field }) => (
                  <ZHToggle
                    label="Punto de emisiÃ³n por defecto"
                    description="Se usarÃ¡ este punto al crear documentos fiscales sin selecciÃ³n explÃ­cita."
                    value={field.value}
                    onChange={field.onChange}
                    disabled={saving}
                  />
                )}
              />
            </div>
          </div>
        </div>

        {/* Actions bar */}
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
                ? "Guardandoâ€¦"
                : isEdit
                  ? "Guardar cambios"
                  : "Crear punto de emisiÃ³n"}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}

