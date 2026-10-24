import { useState } from "react";
import { useFormContext } from "react-hook-form";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { useI18n } from "../../../i18n/i18n";
import type { PendingProductsFormValues } from "../schemas/pendingProductsSchema";
import {
  applyBulkAssignment,
  applyMarginToSelected,
  type BulkAssignment,
  type PendingProductSource,
  type SalePricing,
} from "../utils/pendingProductsResolution";
import type { PendingProductsCatalogs } from "./PendingProductFacts";

/** Patrón: seleccionar filas → elegir valor → aplicar a seleccionados. */
export function PendingProductsBulkBar({
  catalogs,
  sourcesById,
  pricing,
}: {
  catalogs: PendingProductsCatalogs;
  sourcesById: ReadonlyMap<string, PendingProductSource>;
  pricing: SalePricing;
}) {
  const { t } = useI18n();
  const { getValues, setValue, clearErrors } = useFormContext<PendingProductsFormValues>();
  const [assignment, setAssignment] = useState<BulkAssignment>({});
  const [margin, setMargin] = useState<number | null>(null);
  const set = (field: keyof BulkAssignment, value: string) =>
    setAssignment((prev) => ({ ...prev, [field]: value }));
  const replaceRows = (rows: PendingProductsFormValues["rows"]) => {
    setValue("rows", rows, { shouldDirty: true });
    clearErrors();
  };

  return (
    <div className="prl-bulk">
      <ZHField density="compact" label={t("purchases.pendingProducts.col.action", "Acción")}>
        <ZhSelect density="compact" value={assignment.mode ?? ""} onChange={(e) => set("mode", e.target.value)}>
          <option value="">—</option>
          <option value="create">{t("purchases.pendingProducts.mode.create", "Crear producto")}</option>
          <option value="link">{t("purchases.pendingProducts.mode.link", "Vincular existente")}</option>
        </ZhSelect>
      </ZHField>
      <CatalogSelect label={t("purchases.pendingProducts.field.itemType", "Tipo de producto")} value={assignment.itemTypeId} onChange={(v) => set("itemTypeId", v)} options={catalogs.itemTypeOptions.map((o) => ({ value: o.id, label: o.name }))} />
      <CatalogSelect label={t("purchases.pendingProducts.field.brand", "Marca")} value={assignment.brandId} onChange={(v) => set("brandId", v)} options={catalogs.brandOptions.map((o) => ({ value: o.id, label: o.name }))} />
      <CatalogSelect label={t("purchases.pendingProducts.field.category", "Categoría")} value={assignment.categoryNodeId} onChange={(v) => set("categoryNodeId", v)} options={catalogs.categoryOptions.map((o) => ({ value: o.id, label: o.name }))} />
      <CatalogSelect label={t("purchases.pendingProducts.field.uom", "Unidad base")} value={assignment.defaultUomCode} onChange={(v) => set("defaultUomCode", v)} options={catalogs.uomOptions.map((o) => ({ value: o.code, label: o.name }))} />
      <CatalogSelect label={t("purchases.pendingProducts.field.saleVat", "IVA venta")} value={assignment.saleVatCode} onChange={(v) => set("saleVatCode", v)} options={catalogs.vatRateOptions.map((o) => ({ value: o.code, label: o.name }))} />
      <ZHBtn
        type="button"
        variant="secondary"
        size="md"
        onClick={() => {
          replaceRows(applyBulkAssignment(getValues("rows"), assignment));
          setAssignment({});
        }}
      >
        {t("purchases.pendingProducts.applySelected", "Aplicar a seleccionados")}
      </ZHBtn>
      <ZHField density="compact" label={t("purchases.pendingProducts.field.margin", "Margen sobre precio (%)")}>
        <ZhDecimalInput
          precision="percentage"
          positiveOnly
          defaultValue={margin ?? undefined}
          onValueCommit={(v) => setMargin(v === "" ? null : Number(v))}
        />
      </ZHField>
      <ZHBtn
        type="button"
        variant="secondary"
        size="md"
        disabled={margin == null || margin >= 100}
        onClick={() =>
          replaceRows(applyMarginToSelected(getValues("rows"), sourcesById, margin ?? 0, pricing))
        }
      >
        {t("purchases.pendingProducts.applyMargin", "Sugerir PVP final")}
      </ZHBtn>
    </div>
  );
}

function CatalogSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string | undefined;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
}) {
  return (
    <ZHField density="compact" label={label}>
      <ZhSelect density="compact" value={value ?? ""} onChange={(e) => onChange(e.target.value)}>
        <option value="">—</option>
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </ZhSelect>
    </ZHField>
  );
}
