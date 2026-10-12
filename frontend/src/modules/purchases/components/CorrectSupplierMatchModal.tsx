import { useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { formatMoney } from "../../../lib/sanitizers";
import { formatApiRequestError } from "../../lib/apiError";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
import type { ItemPackagingLevelDto } from "../../../types/items";
import { ProductPicker } from "./ProductPicker";

const correctMatchSchema = z.object({
  itemId: z.string().min(1, "Seleccione el producto correcto."),
  itemLabel: z.string(),
  // Presentación que entrega el proveedor con este código; el backend la exige para inventariables.
  packagingLevelId: z.string(),
});
type CorrectMatchFormValues = z.infer<typeof correctMatchSchema>;

type Props = {
  open: boolean;
  supplierName: string;
  supplierCode: string;
  xmlDescription: string;
  currentLabel: string;
  onClose: () => void;
  /** Devuelve el mensaje de error del servidor, o `null` si la corrección se aplicó. */
  onSubmit: (itemId: string, packagingLevelId: string | null) => Promise<string | null>;
};

/**
 * FASE 3 — "Corregir relación": elegir el producto y la presentación correctos para una línea XML
 * ya vinculada. La corrección (línea actual + equivalencia futura Proveedor + Código + otras líneas
 * del mismo código) la hace el backend en una sola transacción.
 */
export function CorrectSupplierMatchModal({
  open,
  supplierName,
  supplierCode,
  xmlDescription,
  currentLabel,
  onClose,
  onSubmit,
}: Props) {
  const { t } = useI18n();
  const quantityDecimals = usePrecisionDecimals("quantity");
  const form = useForm<CorrectMatchFormValues>({
    resolver: zodResolver(correctMatchSchema),
    defaultValues: { itemId: "", itemLabel: "", packagingLevelId: "" },
  });
  const [levels, setLevels] = useState<ItemPackagingLevelDto[]>([]);
  const [serverError, setServerError] = useState<string | null>(null);
  const itemLabel = useWatch({ control: form.control, name: "itemLabel" });

  useEffect(() => {
    if (!open) return;
    form.reset({ itemId: "", itemLabel: "", packagingLevelId: "" });
    setLevels([]);
    setServerError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const selectProduct = async (itemId: string, label: string) => {
    form.setValue("itemId", itemId, { shouldValidate: true });
    form.setValue("itemLabel", label);
    form.setValue("packagingLevelId", "");
    setServerError(null);
    try {
      const active = (await itemLookupFacade.getById(itemId)).packagingLevels.filter((p) => p.isActive);
      if (form.getValues("itemId") !== itemId) return;
      setLevels(active);
      const preferred = active.find((l) => l.isPurchaseDefault) ?? active.find((l) => l.isBaseUnit);
      form.setValue("packagingLevelId", preferred?.id ?? "");
    } catch (err) {
      setServerError(formatApiRequestError(err, { generic: t("purchases.correctMatch.errors.generic") }));
    }
  };

  const submit = form.handleSubmit(async (values) => {
    setServerError(null);
    const error = await onSubmit(values.itemId, values.packagingLevelId || null);
    if (error) setServerError(error);
    else onClose();
  });

  const busy = form.formState.isSubmitting;
  return (
    <ZHModal
      open={open}
      size="md"
      onClose={() => { if (!busy) onClose(); }}
      title={t("purchases.correctMatch.title", "Corregir producto de la línea XML")}
      subtitle={
        supplierCode
          ? t("purchases.correctMatch.subtitle", { supplier: supplierName, code: supplierCode })
          : t("purchases.correctMatch.subtitleNoCode", "La línea no trae código del proveedor: solo se corrige esta línea.")
      }
      footer={
        <>
          <ZHBtn variant="ghost" size="md" onClick={onClose} disabled={busy}>
            {t("common.cancel", "Cancelar")}
          </ZHBtn>
          <ZHBtn variant="primary" size="md" onClick={() => void submit()} disabled={busy}>
            {t("purchases.correctMatch.confirm", "Corregir relación")}
          </ZHBtn>
        </>
      }
    >
      {serverError && <ZHPageNotice variant="error" message={serverError} />}
      <ZHField density="compact" label={t("purchases.pendingProducts.col.xml", "Línea XML del proveedor")}>
        <div>
          <strong>{supplierCode || "—"}</strong> · {xmlDescription}
        </div>
      </ZHField>
      <ZHField density="compact" label={t("purchases.correctMatch.current", "Producto actual")}>
        <div>{currentLabel}</div>
      </ZHField>
      <ZHField
        density="compact"
        label={t("purchases.correctMatch.newProduct", "Producto correcto")}
        fieldError={form.formState.errors.itemId?.message}
      >
        {itemLabel ? (
          <div className="prl-link">
            <span>{itemLabel}</span>
            <ZHBtn type="button" variant="ghost" size="xs" onClick={() => form.setValue("itemLabel", "")}>
              {t("purchases.pendingProducts.change", "Cambiar")}
            </ZHBtn>
          </div>
        ) : (
          <ProductPicker
            initialQuery={xmlDescription}
            onSelect={(p) => void selectProduct(p.id, p.label)}
          />
        )}
      </ZHField>
      {levels.length > 0 && (
        <ZHField density="compact" label={t("purchases.correctMatch.presentation", "Presentación que entrega el proveedor")}>
          <ZhSelect density="compact" {...form.register("packagingLevelId")}>
            <option value="">—</option>
            {levels.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name} ({formatMoney(l.baseQuantity, quantityDecimals)})
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      )}
    </ZHModal>
  );
}
