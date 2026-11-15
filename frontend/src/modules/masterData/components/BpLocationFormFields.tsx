import { Controller, type UseFormReturn } from "react-hook-form";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import {
  ZhTextInput,
  ZhPhoneInput,
  ZhTextarea,
  ZhSelect,
  ZhCheckbox,
} from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import type { GeographyItemDto } from "../../branches/facades/geographyLookupFacade";
import {
  LOCATION_TYPES,
  PURPOSE_FLAGS,
  type LocationFormValues,
} from "./bpLocationForm";

/**
 * Campos del formulario de ubicación (crear/editar). Presentacional: el formulario RHF, la
 * geografía y el guardado pertenecen a BpLocationsSection.
 */
export function BpLocationFormFields({
  form,
  saving,
  isCreate,
  provinces,
  cantons,
  parishes,
  onProvinceChange,
  onCantonChange,
}: {
  form: UseFormReturn<LocationFormValues>;
  saving: boolean;
  isCreate: boolean;
  provinces: GeographyItemDto[];
  cantons: GeographyItemDto[];
  parishes: GeographyItemDto[];
  onProvinceChange: (provinceId: string) => void;
  onCantonChange: (cantonId: string) => void;
}) {
  const { t } = useI18n();
  return (
  <div className="pg-section-body">
    <ZHGrid cols={2}>
      <ZHField
        label={t("masterdata.bp.detail.loc.field.name", "Nombre *")}
        required
        error={form.formState.errors.name?.message}
      >
        <ZhTextInput
          className="zh-input"
          disabled={saving}
          {...form.register("name")}
        />
      </ZHField>
      <ZHField
        label={t("masterdata.bp.detail.loc.field.type", "Tipo *")}
        required
        error={form.formState.errors.type?.message}
      >
        <ZhSelect
          disabled={saving}
          {...form.register("type", { valueAsNumber: true })}
        >
          {LOCATION_TYPES.map((type) => (
            <option key={type.value} value={type.value}>
              {t(type.labelKey, type.fallback)}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
    </ZHGrid>
    <ZHField
      label={t("masterdata.bp.detail.loc.field.address", "Dirección *")}
      required
      error={form.formState.errors.addressLine?.message}
    >
      <ZhTextarea
        className="zh-input"
        rows={2}
        disabled={saving}
        {...form.register("addressLine")}
      />
    </ZHField>
    <ZHGrid cols={3}>
      <ZHField
        label={t("masterdata.bp.detail.loc.field.province", "Provincia")}
        error={form.formState.errors.provinceCode?.message}
      >
        <ZhSelect
          disabled={saving}
          {...form.register("provinceCode")}
          onChange={(e) => {
            form.setValue("provinceCode", e.target.value);
            form.setValue("cantonCode", "");
            form.setValue("parishCode", "");
            onProvinceChange(e.target.value);
          }}
        >
          <option value="">{t("masterdata.bp.detail.select", "— Seleccionar —")}</option>
          {provinces.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
      <ZHField
        label={t("masterdata.bp.detail.loc.field.canton", "Cantón")}
        error={form.formState.errors.cantonCode?.message}
      >
        <ZhSelect
          disabled={saving || cantons.length === 0}
          {...form.register("cantonCode")}
          onChange={(e) => {
            form.setValue("cantonCode", e.target.value);
            form.setValue("parishCode", "");
            onCantonChange(e.target.value);
          }}
        >
          <option value="">{t("masterdata.bp.detail.select", "— Seleccionar —")}</option>
          {cantons.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
      <ZHField
        label={t("masterdata.bp.detail.loc.field.parish", "Parroquia")}
        error={form.formState.errors.parishCode?.message}
      >
        <ZhSelect
          disabled={saving || parishes.length === 0}
          {...form.register("parishCode")}
        >
          <option value="">{t("masterdata.bp.detail.select", "— Seleccionar —")}</option>
          {parishes.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </ZhSelect>
      </ZHField>
    </ZHGrid>
    <ZHGrid cols={2}>
      <ZHField label={t("masterdata.bp.detail.loc.field.phone", "Teléfono")}>
        <Controller
          name="phone"
          control={form.control}
          render={({ field }) => (
            <ZhPhoneInput {...field} disabled={saving} />
          )}
        />
      </ZHField>
      <ZHField label={t("masterdata.bp.detail.loc.field.email", "Email")}>
        <ZhTextInput
          className="zh-input"
          disabled={saving}
          {...form.register("email")}
        />
      </ZHField>
    </ZHGrid>
    <ZHField label={t("masterdata.bp.detail.loc.field.purposes", "Propósitos")}>
      <div className="md-detail-purpose-options">
        {PURPOSE_FLAGS.map((pf) => (
          <label key={pf.flag} className="zh-checkbox-label">
            <ZhCheckbox
              checked={(form.watch("purpose") & pf.flag) !== 0}
              onChange={(e) => {
                const cur = form.getValues("purpose");
                form.setValue(
                  "purpose",
                  e.target.checked ? cur | pf.flag : cur & ~pf.flag,
                );
              }}
              disabled={saving}
            />
            <span>{t(pf.labelKey, pf.fallback)}</span>
          </label>
        ))}
      </div>
    </ZHField>
    {isCreate && (
      <label className="zh-checkbox-label md-detail-primary-option">
        <ZhCheckbox
          {...form.register("isPrimary")}
          disabled={saving}
        />
        <span>{t("masterdata.bp.detail.loc.markPrimary", "Marcar como ubicación principal")}</span>
      </label>
    )}
  </div>
  );
}
