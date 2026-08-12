import { useState, useEffect } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, FormProvider, type Resolver } from "react-hook-form";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { ZHBtn, ZHFormSection } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHTabBar } from "../../../../components/zh/ZHTabBar";
import { LoadingState } from "../../../../components/PageShell";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { apiGet } from "../../../lib/apiEnvelope";
import { useItemTypeOptions } from "../../hooks/useItemTypeOptions";
import { useItemDetailPage } from "../../detail/hooks/useItemDetail";
import { VariantsSection } from "../../detail/components/VariantsSection";
import {
  ImagesSection,
  SubstitutesSection,
  PackagingLevelsSection,
  SupplierCodesDetailSection,
} from "../../detail/components/CollectionSection";
import {
  createItemSchema,
  updateItemSchema,
  defaultCreateItemValues,
  type CreateItemFormValues,
} from "../../schemas/createItemSchema";
import { GeneralTab } from "./GeneralTab";
import { PricingTab } from "./PricingTab";
import { TaxConfigTab } from "./TaxConfigTab";
import { SettingsTab } from "./SettingsTab";
import { InventoryTab } from "./InventoryTab";
import { BarcodeListEditor } from "./BarcodeListEditor";
import { SupplierCodesSection } from "./SupplierCodesSection";
import {
  BarcodePrincipalSummary,
  SupplierCodesPrincipalSummary,
} from "./PrincipalCodesSummary";
import {
  ITEM_FORM_TABS,
  type ItemFormTabId,
} from "./itemFormTabConfig";
import type { ItemDetailDto } from "../../../../types/items";

type TabId = ItemFormTabId;

/** Mapea el DTO ya cargado (fuente única) a los valores del formulario RHF. */
function toFormValues(item: ItemDetailDto): CreateItemFormValues {
  return {
    ...defaultCreateItemValues,
    sku: item.sku,
    shortName: item.shortName,
    description: item.description,
    observations: item.observations,
    itemTypeId: item.itemTypeId,
    defaultUomCode: item.defaultUomCode,
    categoryNodeId: item.categoryNodeId,
    brandId: item.brandId,
    barcodes: [],
    supplierCodes: [],
    taxConfig: {
      saleVatCode: item.taxConfig.saleVatCode,
      purchaseVatCode: item.taxConfig.purchaseVatCode,
      exciseTaxCode: item.taxConfig.exciseTaxCode,
    },
    saleConfig: {
      isForSale: item.saleConfig.isForSale,
      maxDiscountPercent: item.saleConfig.maxDiscountPercent,
      isAvailableOnWeb: item.saleConfig.isAvailableOnWeb,
      isAvailableOnPOS: item.saleConfig.isAvailableOnPOS,
      isAvailableOnMobile: item.saleConfig.isAvailableOnMobile,
      isEcommerceActive: item.saleConfig.isEcommerceActive,
    },
    stockConfig: {
      tracksStock: item.stockConfig.tracksStock,
      tracksLot: item.stockConfig.tracksLot,
      tracksSeries: item.stockConfig.tracksSeries,
      allowDecimalQty: item.stockConfig.allowDecimalQty,
      allowDecimalSale: item.stockConfig.allowDecimalSale,
      minStockQty: item.stockConfig.minStockQty,
      maxStockQty: item.stockConfig.maxStockQty,
    },
    // SSOT (ADR-021): el precio base viene siempre de Item.BaseSalePrice, nunca de
    // un fetch aparte a pricing — ver también ItemsPage.handleSubmit.
    baseSalePrice: item.baseSalePrice,
  } as CreateItemFormValues;
}

/** Aviso uniforme para pestañas de sub-colecciones que requieren un ítem ya guardado. */
function AfterCreateNotice({ message }: { message: string }) {
  return <div className="empty-state">{message}</div>;
}

type Props = {
  submitting: boolean;
  /** undefined = modo creación. */
  itemId?: string;
  /** true = solo lectura (acción "Ver detalle" del listado) — mismos componentes, sin edición ni guardado. */
  disabled?: boolean;
  onSubmit: (values: CreateItemFormValues) => Promise<void>;
  onCancel?: () => void;
};

export function ItemFormTabs({
  submitting,
  itemId,
  disabled = false,
  onSubmit,
  onCancel,
}: Props) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<TabId>("principal");
  const [formError, setFormError] = useState<string | null>(null);
  const isEditMode = !!itemId;

  // Fuente única del Item: la única llamada a itemService.getById() de todo el módulo.
  // Ningún otro componente (ItemsPage, pestañas) vuelve a pedir este recurso.
  const detail = useItemDetailPage(itemId);

  // Único contrato de modo para todo el árbol de pestañas: `disabled`. `submitting`
  // (guardado en curso) y el modo solo-lectura del listado se combinan aquí una sola vez.
  const fieldsDisabled = submitting || disabled;

  // CONTRACT: GET /api/v1/catalog/brands — implemented in CatalogController
  const brandsState = useAsync(() =>
    apiGet<{ id: string; name: string }[]>("/api/v1/catalog/brands").catch(
      () => [] as { id: string; name: string }[],
    ),
  );
  const brandOptions = brandsState.data ?? [];

  const categoriesState = useAsync(() =>
    apiGet<{
      nodes: {
        id: string;
        name: string;
        code: string;
        level: string;
        path: string;
        parentId: string | null;
        isActive: boolean;
      }[];
    }>("/api/v1/catalog/category-nodes").catch(() => ({ nodes: [] })),
  );
  const allNodes = categoriesState.data?.nodes ?? [];
  const nodesById = new Map(allNodes.map((n) => [n.id, n]));
  const parentIds = new Set(
    allNodes
      .filter((n) => n.isActive)
      .map((n) => n.parentId)
      .filter(Boolean),
  );

  // Breadcrumb: usa `path` (ids separados por "/") para mostrar la ruta completa
  // "Línea > Categoría > Subcategoría", evitando ambigüedad entre hojas del mismo
  // nombre en ramas distintas del árbol.
  const breadcrumb = (node: { path: string; name: string }) =>
    node.path
      .split("/")
      .filter(Boolean)
      .map((id) => nodesById.get(id)?.name)
      .filter(Boolean)
      .join(" > ") || node.name;

  const categoryOptions = allNodes
    .filter((n) => n.isActive && !parentIds.has(n.id))
    .map((c) => ({ id: c.id, name: breadcrumb(c), depth: 0 }));

  const sriUomState = useAsync(() =>
    apiGet<{ code: string; name: string; abbrev: string | null }[]>(
      "/api/v1/catalog/sri-uom",
    ).catch(() => []),
  );
  const sriUomOptions = sriUomState.data ?? [];

  const vatRateState = useAsync(() =>
    apiGet<{ code: string; name: string; percentage: number }[]>(
      "/api/v1/catalog/sri-vat-rates",
    ).catch(() => []),
  );
  const vatRateOptions = vatRateState.data ?? [];

  const iceRateState = useAsync(() =>
    apiGet<{ code: string; name: string; percentage: number }[]>(
      "/api/v1/catalog/sri-ice-rates",
    ).catch(() => []),
  );
  const iceRateOptions = iceRateState.data ?? [];

  const itemTypesState = useItemTypeOptions();
  const itemTypeOptions = (itemTypesState.data ?? []).map((it) => ({
    id: it.id,
    name: it.name,
  }));

  // CONTRACT: GET /api/v1/catalog/barcode-types — catálogo global de solo lectura
  const barcodeTypesState = useAsync(() =>
    apiGet<{ code: string; name: string }[]>(
      "/api/v1/catalog/barcode-types",
    ).catch(() => []),
  );
  const barcodeTypeOptions = barcodeTypesState.data ?? [];

  const schema = isEditMode ? updateItemSchema : createItemSchema;
  const form = useForm<CreateItemFormValues>({
    resolver: zodResolver(schema) as unknown as Resolver<CreateItemFormValues>,
    defaultValues: defaultCreateItemValues as CreateItemFormValues,
  });

  // El detalle del ítem llega de forma asíncrona (única fuente: `detail`, ver arriba).
  // En cuanto está disponible, se vuelca al formulario — reemplaza el antiguo patrón de
  // `defaultValues` síncronos, que asumía que el detalle ya venía cargado por el padre.
  useEffect(() => {
    if (detail.item) form.reset(toFormValues(detail.item));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detail.item]);

  // Los <select> de marca/categoría/UOM/tipo/IVA/ICE dependen de catálogos que se cargan
  // de forma asíncrona (useAsync). El reset anterior se aplica en cuanto llega `detail.item`,
  // pero si un catálogo aún no había llegado en ese momento, el <select> queda sin la opción
  // correspondiente aunque el valor interno del formulario sea correcto. Este efecto re-aplica
  // el valor guardado cada vez que un catálogo termina de cargar.
  useEffect(() => {
    if (!detail.item) return;
    const item = detail.item;
    if (brandOptions.length > 0) form.setValue("brandId", item.brandId ?? "");
    if (categoryOptions.length > 0)
      form.setValue("categoryNodeId", item.categoryNodeId ?? "");
    if (sriUomOptions.length > 0)
      form.setValue("defaultUomCode", item.defaultUomCode);
    if (itemTypeOptions.length > 0)
      form.setValue("itemTypeId", item.itemTypeId);
    if (vatRateOptions.length > 0) {
      form.setValue("taxConfig.saleVatCode", item.taxConfig.saleVatCode);
      form.setValue(
        "taxConfig.purchaseVatCode",
        item.taxConfig.purchaseVatCode,
      );
    }
    if (iceRateOptions.length > 0)
      form.setValue("taxConfig.exciseTaxCode", item.taxConfig.exciseTaxCode);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    detail.item,
    brandOptions.length,
    categoryOptions.length,
    sriUomOptions.length,
    itemTypeOptions.length,
    vatRateOptions.length,
    iceRateOptions.length,
  ]);

  const handleSubmit = async (values: CreateItemFormValues) => {
    setFormError(null);
    try {
      await onSubmit(values);
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setFormError(msg),
      );
      if (!applied)
        setFormError(
          formatApiRequestError(err, {
            generic: t(
              "common.saveError",
              "Error al guardar. Revisa los datos.",
            ),
          }),
        );
    }
  };

  const handleSubmitError = () => {
    // Move to first tab that has errors. Principal concentra identidad,
    // tributación y precio; Avanzado conserva configuración comercial secundaria.
    const errors = form.formState.errors;
    const errorKeys = Object.keys(errors) as (keyof CreateItemFormValues)[];
    const stockConfigKey: keyof CreateItemFormValues = "stockConfig";
    const saleConfigKey: keyof CreateItemFormValues = "saleConfig";

    if (errorKeys.includes(stockConfigKey)) {
      setActiveTab("inventory-presentations");
    } else if (errorKeys.includes(saleConfigKey)) {
      setActiveTab("advanced");
    } else {
      setActiveTab("principal");
    }
  };

  // Modo edición/vista esperando el único fetch del ítem — ninguna pestaña se monta
  // todavía porque todas dependen de `detail.item` (fuente única).
  if (isEditMode && detail.loading && !detail.item) {
    return <LoadingState />;
  }

  return (
    <FormProvider {...form}>
      <div>
        {formError && <ZHPageNotice variant="error" message={formError} />}

        <ZHTabBar
          tabs={ITEM_FORM_TABS.map((tab) => ({
            id: tab.id,
            label: t(tab.labelKey, tab.labelFb),
          }))}
          activeTab={activeTab}
          onChange={setActiveTab}
          fill
        />

        <form
          onSubmit={
            disabled
              ? (e) => e.preventDefault()
              : form.handleSubmit(handleSubmit, handleSubmitError)
          }
        >
          {activeTab === "principal" && (
            <>
              <GeneralTab
                t={t}
                disabled={fieldsDisabled}
                isEditMode={isEditMode}
                categoryOptions={categoryOptions}
                brandOptions={brandOptions}
                sriUomOptions={sriUomOptions}
                itemTypeOptions={itemTypeOptions}
              />
              {!isEditMode ? (
                <BarcodeListEditor
                  t={t}
                  disabled={fieldsDisabled}
                  barcodeTypeOptions={barcodeTypeOptions}
                />
              ) : (
                <BarcodePrincipalSummary
                  t={t}
                  item={detail.item}
                  onManageBarcodes={() => setActiveTab("advanced")}
                />
              )}
              {!isEditMode ? (
                <SupplierCodesSection t={t} disabled={fieldsDisabled} />
              ) : (
                <SupplierCodesPrincipalSummary
                  t={t}
                  item={detail.item}
                  onManageSupplierPresentations={() =>
                    setActiveTab("inventory-presentations")
                  }
                />
              )}
              <TaxConfigTab
                t={t}
                disabled={fieldsDisabled}
                vatRateOptions={vatRateOptions}
                iceRateOptions={iceRateOptions}
              />
              <PricingTab
                t={t}
                disabled={fieldsDisabled}
                itemId={itemId}
                vatRateOptions={vatRateOptions}
              />
            </>
          )}
          {activeTab === "inventory-presentations" && (
            <>
              <InventoryTab
                t={t}
                disabled={fieldsDisabled}
                isEditMode={isEditMode}
                itemId={itemId}
                unitConversions={detail.item?.unitConversions}
              />
              {isEditMode && detail.item ? (
                <PackagingLevelsSection
                  t={t}
                  levels={detail.item.packagingLevels}
                  uomOptions={sriUomOptions}
                  baseUomCode={detail.item.defaultUomCode}
                  tracksStock={detail.item.tracksStock}
                  usedPackagingLevelIds={
                    new Set(
                      detail.item.supplierCodes
                        .filter((s) => s.packagingLevelId)
                        .map((s) => s.packagingLevelId as string),
                    )
                  }
                  disabled={fieldsDisabled}
                  onSave={detail.replacePackagingLevels}
                />
              ) : (
                <ZHFormSection
                  title={t(
                    "items.packaging.sectionTitle",
                    "Presentaciones y empaques",
                  )}
                  description={t(
                    "items.packaging.sectionDesc",
                    "Defina la unidad base X1 y las presentaciones de compra o venta, como PACA X12.",
                  )}
                >
                  <AfterCreateNotice
                    message={t(
                      "items.packaging.availableAfterCreate",
                      "Disponible después de guardar el ítem.",
                    )}
                  />
                </ZHFormSection>
              )}
              {isEditMode && detail.item ? (
                <SupplierCodesDetailSection
                  t={t}
                  supplierCodes={detail.item.supplierCodes}
                  packagingLevels={detail.item.packagingLevels}
                  baseUomAbbrev={detail.item.defaultUomAbbrev}
                  disabled={fieldsDisabled}
                  onUpdatePresentation={detail.updateSupplierCodePresentation}
                />
              ) : (
                <ZHFormSection
                  title={t(
                    "items.supplierCodes.detailTitle",
                    "Códigos del proveedor y presentaciones",
                  )}
                  description={t(
                    "items.supplierCodes.detailDesc",
                    "Muestre el proveedor, su código y la presentación que llega en compras XML.",
                  )}
                >
                  <AfterCreateNotice
                    message={t(
                      "items.supplierCodes.availableAfterCreate",
                      "Disponible después de guardar el ítem.",
                    )}
                  />
                </ZHFormSection>
              )}
            </>
          )}
          {activeTab === "images" &&
            (isEditMode && detail.item ? (
              <ImagesSection
                t={t}
                images={detail.item.images}
                disabled={fieldsDisabled}
                onDisable={detail.disableImage}
              />
            ) : (
              <AfterCreateNotice
                message={t(
                  "items.images.availableAfterCreate",
                  "Guarda el ítem primero para agregar imágenes.",
                )}
              />
            ))}
          {activeTab === "advanced" && (
            <>
              <SettingsTab t={t} disabled={fieldsDisabled} />
              {isEditMode && itemId && detail.item ? (
                <VariantsSection
                  itemId={itemId}
                  variants={detail.item.variants}
                  disabled={fieldsDisabled}
                  onToggle={detail.toggleVariant}
                  onRefresh={detail.refetch}
                />
              ) : (
                <ZHFormSection
                  title={t("items.tabs.variants", "Variantes")}
                  description={t(
                    "items.variants.sectionDesc",
                    "Opciones internas del ítem cuando aplica.",
                  )}
                >
                  <AfterCreateNotice
                    message={t(
                      "items.variants.availableAfterCreate",
                      "Disponible después de guardar el ítem.",
                    )}
                  />
                </ZHFormSection>
              )}
              {isEditMode && detail.item ? (
                <SubstitutesSection
                  t={t}
                  substitutes={detail.item.substitutes}
                />
              ) : (
                <ZHFormSection
                  title={t("items.substitutes.sectionTitle", "Sustitutos")}
                  description={t(
                    "items.substitutes.sectionDesc",
                    "Ítems alternativos para consulta operativa.",
                  )}
                >
                  <AfterCreateNotice
                    message={t(
                      "items.substitutes.availableAfterCreate",
                      "Disponible después de guardar el ítem.",
                    )}
                  />
                </ZHFormSection>
              )}
            </>
          )}

          {/* Actions */}
          <div className="zh-form-actions-row zh-form-actions-row--end zh-form-actions-row--lg">
            {onCancel && (
              <ZHBtn
                type="button"
                variant="ghost"
                size="md"
                onClick={onCancel}
                disabled={submitting}
              >
                {disabled
                  ? t("common.back", "Volver")
                  : t("common.cancel", "Cancelar")}
              </ZHBtn>
            )}
            {!disabled && (
              <ZHBtn
                type="submit"
                variant="primary"
                size="md"
                disabled={submitting}
              >
                {submitting
                  ? t("common.saving", "Guardando...")
                  : isEditMode
                    ? t("items.form.update", "Actualizar ítem")
                    : t("items.form.create", "Crear ítem")}
              </ZHBtn>
            )}
          </div>
        </form>
      </div>
    </FormProvider>
  );
}
