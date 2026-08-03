import { useEffect, useRef } from "react";
import { useFormContext } from "react-hook-form";
import {
  ZHField,
  ZHFormActions,
  ZHFormAlert,
  ZHFormSection,
  ZHGrid,
} from "../../zh/ZHForm";
import { ZhDecimalInput } from "../../zh/inputs/ZhDecimalInput";
import { Badge } from "../../PageShell";
import { useAsync } from "../../../hooks/useAsync";
import { apiGet } from "../../../modules/lib/apiEnvelope";
import { applyServerErrors } from "../../../modules/lib/validationErrors";
import { formatApiRequestError } from "../../../modules/lib/apiError";
import { normalizeOptionalCode } from "../../../lib/sanitizers";
import { useItemTypeOptions } from "../../../modules/items/hooks/useItemTypeOptions";
import { itemService } from "../../../modules/items/api/itemService";
import {
  sriLookupService,
  type SriVatRateLookup,
} from "../../../modules/items/catalog/api/catalogService";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { calcMarginAmount, calcMarginPercent } from "../../../lib/margin";
import type { ItemEditorFormValues } from "./itemEditorSchema";
import type {
  CreateItemInitialData,
  ItemEditorMode,
  ItemCreatedResult,
} from "./types";
import type { ItemDetailDto } from "../../../types/items";

// CONTRACT: mismos catálogos ya consumidos por ItemFormTabs.tsx (formulario completo de Items) —
// no se crea un segundo contrato, solo se reutilizan los GET ya existentes.
interface BrandOption {
  id: string;
  name: string;
}
interface CategoryNodeApi {
  id: string;
  name: string;
  path: string;
  parentId: string | null;
  isActive: boolean;
}
interface UomOption {
  code: string;
  name: string;
  abbrev: string | null;
}
interface BarcodeTypeOption {
  code: string;
  name: string;
}

type Props = {
  mode: ItemEditorMode;
  existingItem: ItemDetailDto | null;
  initialData?: CreateItemInitialData;
  onClose: () => void;
  onSaved: (item: ItemCreatedResult) => void;
};

export function ItemEditorForm({
  mode,
  existingItem,
  initialData,
  onClose,
  onSaved,
}: Props) {
  const {
    register,
    handleSubmit,
    setError,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useFormContext<ItemEditorFormValues>();
  const isUpdate = mode === "update";

  const itemTypesState = useItemTypeOptions();
  const itemTypeOptions = itemTypesState.data ?? [];

  const brandsState = useAsync(() =>
    apiGet<BrandOption[]>("/api/v1/catalog/brands").catch(
      () => [] as BrandOption[],
    ),
  );
  const brandOptions = brandsState.data ?? [];

  const categoriesState = useAsync(() =>
    apiGet<{ nodes: CategoryNodeApi[] }>(
      "/api/v1/catalog/category-nodes",
    ).catch(() => ({ nodes: [] })),
  );
  const allNodes = categoriesState.data?.nodes ?? [];
  const nodesById = new Map(allNodes.map((n) => [n.id, n]));
  const parentIds = new Set(
    allNodes
      .filter((n) => n.isActive)
      .map((n) => n.parentId)
      .filter(Boolean),
  );
  const breadcrumb = (node: CategoryNodeApi) =>
    node.path
      .split("/")
      .filter(Boolean)
      .map((id) => nodesById.get(id)?.name)
      .filter(Boolean)
      .join(" > ") || node.name;
  const categoryOptions = allNodes
    .filter((n) => n.isActive && !parentIds.has(n.id))
    .map((n) => ({ id: n.id, name: breadcrumb(n) }));

  const uomState = useAsync(() =>
    apiGet<UomOption[]>("/api/v1/catalog/sri-uom").catch(
      () => [] as UomOption[],
    ),
  );
  const uomOptions = uomState.data ?? [];

  const barcodeTypeState = useAsync(() =>
    apiGet<BarcodeTypeOption[]>("/api/v1/catalog/barcode-types").catch(
      () => [] as BarcodeTypeOption[],
    ),
  );
  const barcodeTypeOptions = barcodeTypeState.data ?? [];

  // Único catálogo de IVA — resuelve a la vez las opciones del selector "IVA del Item" y el
  // porcentaje de referencia "IVA XML" (misma fuente, sin duplicar el lookup).
  const vatRatesState = useAsync(() =>
    sriLookupService.vatRates().catch(() => [] as SriVatRateLookup[]),
  );
  const vatRateOptions = vatRatesState.data ?? [];
  const vatRateByCode = new Map(vatRateOptions.map((v) => [v.code, v]));

  // El <select> de IVA solo puede reflejar seleccionada una opción que ya exista en el DOM — como
  // el catálogo de IVA carga async (useAsync más arriba) y siempre después de que ItemEditorModal
  // ya llamó a form.reset() con la sugerencia de purchaseContext.vatCode, esa asignación inicial
  // se pierde (el <select> uncontrolled no tiene aún la opción "2" cuando reset() intenta fijar el
  // valor). Este efecto reaplica la sugerencia una sola vez, apenas el catálogo está disponible —
  // por eso usa un ref en vez de leer el valor actual del form (que ya "dice" tener la sugerencia
  // por dentro, aunque el DOM no la refleje), y nunca vuelve a correr para no pisar una elección
  // del usuario.
  const appliedVatSuggestionRef = useRef(false);
  useEffect(() => {
    if (isUpdate || vatRateOptions.length === 0) return;
    if (appliedVatSuggestionRef.current) return;
    const suggested = initialData?.purchaseContext?.vatCode;
    if (!suggested) return;
    appliedVatSuggestionRef.current = true;
    setValue("purchaseVatCode", suggested, { shouldValidate: false });
    setValue("saleVatCode", suggested, { shouldValidate: false });
  }, [isUpdate, vatRateOptions.length, initialData?.purchaseContext?.vatCode, setValue]);

  // Mismo problema estructural que la sugerencia de IVA de arriba, pero en modo Actualizar: el
  // reset() con los valores del Item existente corre en ItemEditorModal (buildDefaults) antes de
  // que categoría/marca/UOM/IVA (catálogos cargados por separado acá, en ItemEditorForm) terminen
  // de resolver — el <select> uncontrolled no puede reflejar (ni retener al enviar el formulario,
  // por ser uncontrolled) un valor cuya opción todavía no existe en el DOM. Sin este efecto, "Actualizar
  // Item" podía mostrarse con categoría/marca/UOM/IVA en blanco y, peor, guardar `null` sobre un
  // Item que sí tenía esos datos. Se reaplica una sola vez por Item (ref por id) apenas los 4
  // catálogos de los que depende están listos, sin pisar una edición ya hecha por el usuario.
  const appliedExistingItemIdRef = useRef<string | null>(null);
  const updateCatalogsReady =
    itemTypesState.data != null &&
    brandsState.data != null &&
    categoriesState.data != null &&
    uomState.data != null &&
    vatRatesState.data != null;
  useEffect(() => {
    if (!isUpdate || !existingItem || !updateCatalogsReady) return;
    if (appliedExistingItemIdRef.current === existingItem.id) return;
    appliedExistingItemIdRef.current = existingItem.id;
    setValue("categoryNodeId", existingItem.categoryNodeId ?? "", {
      shouldValidate: false,
    });
    setValue("brandId", existingItem.brandId ?? "", { shouldValidate: false });
    setValue("defaultUomCode", existingItem.defaultUomCode, {
      shouldValidate: false,
    });
    setValue("saleVatCode", existingItem.taxConfig.saleVatCode ?? "", {
      shouldValidate: false,
    });
    setValue("purchaseVatCode", existingItem.taxConfig.purchaseVatCode ?? "", {
      shouldValidate: false,
    });
  }, [isUpdate, existingItem, updateCatalogsReady, setValue]);

  const onSubmit = handleSubmit(async (values) => {
    try {
      if (isUpdate && existingItem) {
        const item = await itemService.update({
          id: existingItem.id,
          sku: values.sku,
          shortName: values.shortName,
          description: values.description,
          defaultUomCode: values.defaultUomCode,
          categoryNodeId: values.categoryNodeId || null,
          brandId: values.brandId || null,
          saleVatCode: normalizeOptionalCode(values.saleVatCode),
          purchaseVatCode: normalizeOptionalCode(values.purchaseVatCode),
          exciseTaxCode: existingItem.taxConfig.exciseTaxCode,
          observations: values.observations || null,
          // BaseSalePrice es obligatorio en Update (SSOT, ADR-021) — si el usuario no activó
          // "Actualizar precio", se reenvía el valor actual del Item para no pisarlo con null.
          baseSalePrice: values.updatePrice
            ? (values.salePrice ?? existingItem.baseSalePrice ?? 0)
            : (existingItem.baseSalePrice ?? 0),
          isForSale: existingItem.saleConfig.isForSale,
          maxDiscountPercent: existingItem.saleConfig.maxDiscountPercent,
          isAvailableOnWeb: existingItem.saleConfig.isAvailableOnWeb,
          isAvailableOnPOS: existingItem.saleConfig.isAvailableOnPOS,
          isAvailableOnMobile: existingItem.saleConfig.isAvailableOnMobile,
          isEcommerceActive: existingItem.saleConfig.isEcommerceActive,
          tracksStock: existingItem.stockConfig.tracksStock,
          tracksLot: existingItem.stockConfig.tracksLot,
          tracksSeries: existingItem.stockConfig.tracksSeries,
          allowDecimalQty: existingItem.stockConfig.allowDecimalQty,
          allowDecimalSale: existingItem.stockConfig.allowDecimalSale,
          minStockQty: existingItem.stockConfig.minStockQty,
          maxStockQty: existingItem.stockConfig.maxStockQty,
        });
        onSaved({
          id: item.id,
          sku: item.sku,
          shortName: item.shortName,
          baseSalePrice: item.baseSalePrice,
          purchaseVatCode: normalizeOptionalCode(values.purchaseVatCode),
        });
        return;
      }

      // Create: el precio y ambos IVA (venta/compra) son obligatorios en este flujo (el Item
      // debe quedar listo para comprar y vender sin volver al módulo de Items) — el schema ya
      // garantiza que salePrice/saleVatCode/purchaseVatCode no lleguen vacíos acá.
      const item = await itemService.create({
        sku: values.sku,
        shortName: values.shortName,
        description: values.description,
        itemTypeId: values.itemTypeId,
        categoryNodeId: values.categoryNodeId,
        brandId: values.brandId,
        defaultUomCode: values.defaultUomCode,
        barcodes: [
          {
            code: values.barcode ?? "",
            barcodeType: values.barcodeType ?? "",
            isPrimary: true,
          },
        ],
        saleVatCode: normalizeOptionalCode(values.saleVatCode),
        purchaseVatCode: normalizeOptionalCode(values.purchaseVatCode),
        baseSalePrice: values.salePrice ?? null,
        observations: values.observations || null,
        supplierCodes:
          initialData?.supplierId && initialData?.supplierCode
            ? [
                {
                  supplierId: initialData.supplierId,
                  code: initialData.supplierCode,
                  isPrimary: true,
                },
              ]
            : undefined,
      });
      onSaved({
        id: item.id,
        sku: item.sku,
        shortName: item.shortName,
        baseSalePrice: item.baseSalePrice,
        purchaseVatCode: normalizeOptionalCode(values.purchaseVatCode),
      });
    } catch (err) {
      // applyServerErrors solo mapea 422 con mapa de campos. Cualquier otro caso (400/409 con
      // data.errors plano, p.ej. SKU_DUPLICATE/BARCODE_DUPLICATE, o error inesperado) no dispara
      // su fallback interno — hay que revisar el resultado y formatear el mensaje aquí siempre.
      const handledAsFieldErrors = applyServerErrors(err, setError);
      if (!handledAsFieldErrors) {
        setError("root", {
          type: "server",
          message: formatApiRequestError(err, {
            generic: isUpdate
              ? "No se pudo actualizar el ítem."
              : "No se pudo crear el ítem.",
          }),
        });
      }
    }
  });

  const rootError = (errors.root as { message?: string } | undefined)?.message;
  const purchaseContext = initialData?.purchaseContext;
  const salePrice = watch("salePrice");
  const updatePrice = watch("updatePrice");
  const saleVatCode = watch("saleVatCode");

  return (
    <form onSubmit={onSubmit}>
      {rootError && <ZHFormAlert type="error" message={rootError} />}

      {isUpdate && existingItem && (
        <ZHFormSection title="Actualizar Item existente">
          <ZHGrid cols={3}>
            <ZHField label="SKU" readOnly density="compact">
              <input value={existingItem.sku} disabled />
            </ZHField>
            <ZHField label="Nombre" readOnly density="compact">
              <input value={existingItem.shortName} disabled />
            </ZHField>
            <ZHField label="Estado" readOnly density="compact">
              <div>
                <Badge
                  variant={existingItem.isActive ? "green" : "gray"}
                  label={existingItem.isActive ? "Activo" : "Inactivo"}
                />
              </div>
            </ZHField>
          </ZHGrid>
        </ZHFormSection>
      )}

      <ZHFormSection
        title="Información del Item"
        description="Datos del producto en el catálogo."
      >
        {(initialData?.supplierName || initialData?.supplierCode) && (
          <ZHGrid cols={2}>
            {initialData.supplierName && (
              <ZHField label="Proveedor" readOnly density="compact">
                <input value={initialData.supplierName} disabled />
              </ZHField>
            )}
            {initialData.supplierCode && (
              <ZHField label="Código proveedor" readOnly density="compact">
                <input value={initialData.supplierCode} disabled />
              </ZHField>
            )}
          </ZHGrid>
        )}

        <ZHGrid cols={2}>
          <ZHField label="SKU" required fieldError={errors.sku?.message}>
            <input {...register("sku")} />
          </ZHField>
          <ZHField
            label="Nombre corto"
            required
            fieldError={errors.shortName?.message}
          >
            <input {...register("shortName")} />
          </ZHField>
        </ZHGrid>

        <ZHField
          label="Descripción"
          required
          fieldError={errors.description?.message}
        >
          <textarea {...register("description")} rows={2} />
        </ZHField>

        <ZHGrid cols={2}>
          <ZHField
            label="Tipo de ítem"
            required
            fieldError={errors.itemTypeId?.message}
            hint={
              isUpdate
                ? "El tipo de ítem no se puede cambiar luego de creado."
                : undefined
            }
            hintType="muted"
          >
            <select {...register("itemTypeId")} disabled={isUpdate}>
              <option value="">Seleccione...</option>
              {itemTypeOptions.map((it) => (
                <option key={it.id} value={it.id}>
                  {it.name}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField
            label="Unidad de medida"
            required
            fieldError={errors.defaultUomCode?.message}
          >
            <select {...register("defaultUomCode")}>
              <option value="">Seleccione...</option>
              {uomOptions.map((u) => (
                <option key={u.code} value={u.code}>
                  {u.name}
                </option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>

        <ZHGrid cols={2}>
          <ZHField
            label="Categoría"
            required
            fieldError={errors.categoryNodeId?.message}
          >
            <select {...register("categoryNodeId")}>
              <option value="">Seleccione...</option>
              {categoryOptions.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </ZHField>
          <ZHField label="Marca" required fieldError={errors.brandId?.message}>
            <select {...register("brandId")}>
              <option value="">Seleccione...</option>
              {brandOptions.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>

        {!isUpdate && (
          <ZHGrid cols={2}>
            <ZHField
              label="Código de barras"
              required
              fieldError={errors.barcode?.message}
            >
              <input {...register("barcode")} />
            </ZHField>
            <ZHField
              label="Tipo de código de barras"
              required
              fieldError={errors.barcodeType?.message}
            >
              <select {...register("barcodeType")}>
                <option value="">Seleccione...</option>
                {barcodeTypeOptions.map((t) => (
                  <option key={t.code} value={t.code}>
                    {t.name}
                  </option>
                ))}
              </select>
            </ZHField>
          </ZHGrid>
        )}

        <ZHField
          label="Observaciones"
          fieldError={errors.observations?.message}
        >
          <textarea {...register("observations")} rows={2} />
        </ZHField>
      </ZHFormSection>

      {purchaseContext && (
        <PurchaseInfoReadOnly purchaseContext={purchaseContext} />
      )}

      <PriceAndProfitability
        purchaseContext={purchaseContext}
        salePrice={salePrice ?? null}
        updatePrice={!!updatePrice}
        saleVatCode={saleVatCode ?? ""}
        vatRateOptions={vatRateOptions}
        xmlVatPercent={
          purchaseContext?.vatCode
            ? vatRateByCode.get(purchaseContext.vatCode)?.percentage
            : undefined
        }
        register={register}
        setValue={setValue}
        salePriceFieldError={errors.salePrice?.message}
        saleVatCodeFieldError={errors.saleVatCode?.message}
        purchaseVatCodeFieldError={errors.purchaseVatCode?.message}
        isUpdate={isUpdate}
      />

      <ZHFormActions
        onCancel={onClose}
        hideDraft
        saveButtonType="submit"
        disableSave={isSubmitting}
        labels={{
          save: isSubmitting
            ? isUpdate
              ? "Actualizando..."
              : "Creando..."
            : isUpdate
              ? "Actualizar Item"
              : "Crear Item",
        }}
      />
    </form>
  );
}

/** "¿Qué llegó del proveedor?" — solo lectura, nunca editable, igual en ambos modos. */
function PurchaseInfoReadOnly({
  purchaseContext,
}: {
  purchaseContext: NonNullable<CreateItemInitialData["purchaseContext"]>;
}) {
  const dc = getDecimalConfig();
  const { unitCost, quantity, discountPct } = purchaseContext;
  const hasDiscount = discountPct != null;
  const costFinal = unitCost * (1 - (discountPct ?? 0) / 100);

  return (
    <ZHFormSection
      title="Información de Compra"
      description="Datos de la factura de origen — solo lectura."
    >
      <ZHGrid cols={2}>
        <ZHField label="Costo unitario" readOnly density="compact">
          <input
            value={formatMoneyWithSymbol(unitCost, dc.purchaseUnitPrice)}
            disabled
          />
        </ZHField>
        <ZHField label="Cantidad" readOnly density="compact">
          <input value={formatMoney(quantity, dc.quantity)} disabled />
        </ZHField>
        <ZHField label="Descuento aplicado" readOnly density="compact">
          <input
            value={
              hasDiscount
                ? `${formatMoney(discountPct ?? 0, dc.percentage)}%`
                : "—"
            }
            disabled
          />
        </ZHField>
        <ZHField
          label="Costo final utilizado para la simulación"
          readOnly
          density="compact"
        >
          <input
            value={formatMoneyWithSymbol(costFinal, dc.purchaseUnitPrice)}
            disabled
          />
        </ZHField>
      </ZHGrid>
    </ZHFormSection>
  );
}

/**
 * "Precio y Rentabilidad" — siempre al final, en ambos modos. Único bloque editable de precio/IVA,
 * con simulación de margen en tiempo real. Modular a propósito (props mínimas) para poder
 * reutilizarse el día que existan múltiples listas/mayorista/distribuidor sin reescribirlo. La
 * fórmula de margen es la misma que usa el resto del ERP — ver `lib/margin.ts`, nunca se
 * reimplementa acá. El IVA no altera el margen (el Pricing Engine nunca mezcla IVA con margen,
 * ver PriceListSimulationTable) — es un dato adicional de referencia, "Precio final" incluido.
 */
function PriceAndProfitability({
  purchaseContext,
  salePrice,
  updatePrice,
  saleVatCode,
  vatRateOptions,
  xmlVatPercent,
  register,
  setValue,
  salePriceFieldError,
  saleVatCodeFieldError,
  purchaseVatCodeFieldError,
  isUpdate,
}: {
  purchaseContext?: CreateItemInitialData["purchaseContext"];
  salePrice: number | null;
  updatePrice: boolean;
  saleVatCode: string;
  vatRateOptions: SriVatRateLookup[];
  xmlVatPercent: number | undefined;
  register: ReturnType<typeof useFormContext<ItemEditorFormValues>>["register"];
  setValue: ReturnType<typeof useFormContext<ItemEditorFormValues>>["setValue"];
  salePriceFieldError?: string;
  saleVatCodeFieldError?: string;
  purchaseVatCodeFieldError?: string;
  isUpdate: boolean;
}) {
  const dc = getDecimalConfig();
  const costFinal = purchaseContext
    ? purchaseContext.unitCost * (1 - (purchaseContext.discountPct ?? 0) / 100)
    : null;
  const hasCost = costFinal != null;
  const hasPrice = salePrice != null && salePrice > 0;
  const salePriceValue = salePrice ?? 0;
  const itemVatPercent = vatRateOptions.find(
    (v) => v.code === saleVatCode,
  )?.percentage;
  const hasItemVat = itemVatPercent != null;
  const priceFinal =
    hasPrice && hasItemVat ? salePriceValue * (1 + itemVatPercent / 100) : null;
  const marginAmount =
    hasCost && hasPrice ? calcMarginAmount(costFinal, salePriceValue) : 0;
  const marginPct =
    hasCost && hasPrice ? calcMarginPercent(costFinal, salePriceValue) : 0;

  return (
    <ZHFormSection
      title="Precio y Rentabilidad"
      description={
        isUpdate
          ? "El precio y el IVA son editables — la simulación es solo una ayuda visual, no requiere guardarse."
          : "El precio, el IVA de venta y el IVA de compra son obligatorios: el Item debe quedar listo para comprar y vender sin volver a configurarlo en el módulo de Items."
      }
    >
      <ZHGrid cols={3}>
        <ZHField label="IVA XML" readOnly density="compact">
          <input
            value={
              xmlVatPercent != null
                ? `${formatMoney(xmlVatPercent, dc.percentage)}%`
                : "—"
            }
            disabled
          />
        </ZHField>
        <ZHField
          label="IVA de venta"
          required={!isUpdate}
          fieldError={saleVatCodeFieldError}
        >
          <select {...register("saleVatCode")}>
            <option value="">Sin IVA configurado</option>
            {vatRateOptions.map((v) => (
              <option key={v.code} value={v.code}>
                {v.name} ({formatMoney(v.percentage, dc.percentage)}%)
              </option>
            ))}
          </select>
        </ZHField>
        <ZHField
          label="IVA de compra"
          required={!isUpdate}
          fieldError={purchaseVatCodeFieldError}
        >
          <select
            {...register("purchaseVatCode", {
              onChange: (e) => {
                // Sugerencia: si aún no hay IVA de venta elegido, se propone el mismo código de
                // IVA de compra — el usuario puede cambiarlo, nunca se le impone.
                if (!isUpdate && !saleVatCode) {
                  setValue("saleVatCode", e.target.value, {
                    shouldValidate: true,
                  });
                }
              },
            })}
          >
            <option value="">Sin IVA configurado</option>
            {vatRateOptions.map((v) => (
              <option key={v.code} value={v.code}>
                {v.name} ({formatMoney(v.percentage, dc.percentage)}%)
              </option>
            ))}
          </select>
        </ZHField>
      </ZHGrid>

      <ZHGrid cols={2}>
        <ZHField
          label="Precio sugerido"
          required={!isUpdate}
          fieldError={salePriceFieldError}
        >
          <ZhDecimalInput
            decimals={dc.salesUnitPrice}
            positiveOnly
            placeholder="0.00"
            {...register("salePrice", {
              valueAsNumber: true,
              setValueAs: (v) => (v === "" ? null : Number(v)),
            })}
          />
        </ZHField>
        {isUpdate && (
          <ZHField label="Actualizar precio del Item">
            <label className="zh-checkbox-label">
              <input type="checkbox" {...register("updatePrice")} />
              <span>Usar este precio como nuevo precio del Item</span>
            </label>
          </ZHField>
        )}
      </ZHGrid>

      <div className="citm-margin-sim">
        <ZHGrid cols={3}>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Costo</span>
            <span className="citm-margin-sim__value">
              {hasCost
                ? formatMoneyWithSymbol(costFinal, dc.purchaseUnitPrice)
                : "—"}
            </span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Precio</span>
            <span className="citm-margin-sim__value">
              {hasPrice
                ? formatMoneyWithSymbol(salePriceValue, dc.salesUnitPrice)
                : "—"}
            </span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">IVA</span>
            <span className="citm-margin-sim__value">
              {hasItemVat
                ? `${formatMoney(itemVatPercent, dc.percentage)}%`
                : "—"}
            </span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Precio final</span>
            <span className="citm-margin-sim__value">
              {priceFinal != null
                ? formatMoneyWithSymbol(priceFinal, dc.salesUnitPrice)
                : "—"}
            </span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Ganancia</span>
            <span
              className={`citm-margin-sim__value ${hasCost && hasPrice && marginAmount < 0 ? "citm-margin-sim__value--neg" : ""}`}
            >
              {hasCost && hasPrice
                ? formatMoneyWithSymbol(marginAmount, dc.salesUnitPrice)
                : "—"}
            </span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Margen</span>
            <span
              className={`citm-margin-sim__value citm-margin-sim__value--pct ${hasCost && hasPrice && marginPct < 0 ? "citm-margin-sim__value--neg" : ""}`}
            >
              {hasCost && hasPrice
                ? `${formatMoney(marginPct, dc.percentage)}%`
                : "—"}
            </span>
          </div>
        </ZHGrid>
      </div>
      {isUpdate && !updatePrice && hasPrice && (
        <ZHFormAlert
          type="info"
          message='El precio no se guardará: active "Actualizar precio del Item" para usarlo como nuevo precio.'
        />
      )}
    </ZHFormSection>
  );
}
