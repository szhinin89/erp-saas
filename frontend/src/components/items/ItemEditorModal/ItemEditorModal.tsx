import { useEffect } from "react";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../zh/ZHModal";
import { LoadingState, ErrorState } from "../../PageShell";
import { useAsync } from "../../../hooks/useAsync";
import { itemService } from "../../../modules/items/api/itemService";
import type { ItemDetailDto } from "../../../types/items";
import { ItemEditorForm } from "./ItemEditorForm";
import {
  buildItemEditorSchema,
  type ItemEditorFormValues,
} from "./itemEditorSchema";
import type { CreateItemInitialData, ItemEditorModalProps } from "./types";

function buildDefaults(
  initialData: CreateItemInitialData | undefined,
  existingItem: ItemDetailDto | null,
): ItemEditorFormValues {
  if (existingItem) {
    return {
      sku: existingItem.sku,
      shortName: existingItem.shortName,
      description: existingItem.description,
      itemTypeId: existingItem.itemTypeId,
      categoryNodeId: existingItem.categoryNodeId ?? "",
      brandId: existingItem.brandId ?? "",
      defaultUomCode: existingItem.defaultUomCode,
      barcode: "",
      barcodeType: "",
      observations: existingItem.observations ?? "",
      saleVatCode: existingItem.taxConfig.saleVatCode ?? "",
      salePrice: existingItem.baseSalePrice,
      updatePrice: false,
    };
  }

  const name = initialData?.name ?? "";
  return {
    sku: (initialData?.supplierCode ?? initialData?.barcode ?? "")
      .trim()
      .toUpperCase(),
    shortName: name.slice(0, 50),
    description: name.slice(0, 254),
    itemTypeId: "",
    categoryNodeId: "",
    brandId: "",
    defaultUomCode: initialData?.defaultUomCode ?? "",
    barcode: initialData?.barcode ?? "",
    barcodeType: "",
    observations: "",
    saleVatCode: "",
    salePrice: null,
    updatePrice: false,
  };
}

/**
 * Editor de Item reutilizable — dos modos sobre un único formulario (`ItemEditorForm`), nunca dos
 * componentes: Crear (sin `itemId`) y Actualizar (con `itemId`, precarga el Item existente). No
 * sabe de dónde viene la invocación (Compras, Inventario, Ventas...); depende únicamente del
 * contrato de Items (`itemService.create`/`itemService.getById`/`itemService.update`). Pensado
 * como el editor oficial de Items del ERP — cualquier módulo futuro puede reutilizarlo igual.
 */
export function ItemEditorModal({
  open,
  itemId,
  initialData,
  onClose,
  onSaved,
}: ItemEditorModalProps) {
  const mode = itemId ? "update" : "create";

  const itemState = useAsync(
    () => itemService.getById(itemId!),
    open && !!itemId,
    [itemId],
  );
  const existingItem = itemState.data;

  const form = useForm<ItemEditorFormValues>({
    resolver: zodResolver(buildItemEditorSchema(mode)),
    defaultValues: buildDefaults(initialData, null),
  });

  useEffect(() => {
    if (!open) return;
    if (mode === "update" && !existingItem) return; // espera a que cargue antes de resetear
    form.reset(buildDefaults(initialData, existingItem ?? null));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, initialData, existingItem, mode]);

  const isLoadingExisting =
    mode === "update" && itemState.loading && !existingItem;

  return (
    <ZHModal
      open={open}
      onClose={onClose}
      size="lg"
      title={mode === "update" ? "Editar Item" : "Crear nuevo Item"}
      subtitle={
        mode === "update"
          ? "Los cambios se aplican al Item ya vinculado a esta línea."
          : "El producto se crea en el catálogo de Items."
      }
    >
      {isLoadingExisting && <LoadingState />}
      {mode === "update" && itemState.error && !existingItem && (
        <ErrorState message={itemState.error} />
      )}
      {!isLoadingExisting && (mode === "create" || existingItem) && (
        <FormProvider {...form}>
          <ItemEditorForm
            mode={mode}
            existingItem={existingItem ?? null}
            initialData={initialData}
            onClose={onClose}
            onSaved={onSaved}
          />
        </FormProvider>
      )}
    </ZHModal>
  );
}
