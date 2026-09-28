import { useEffect, useMemo, useRef, useState } from "react";
import {
  FormProvider,
  useForm,
  useFormContext,
  useWatch,
} from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { Badge } from "../../../components/PageShell";
import { useItemCreationCatalogs } from "../../../components/items/ItemEditorModal/useItemCreationCatalogs";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { formatMoney } from "../../../lib/sanitizers";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
import type { ItemPackagingLevelDto } from "../../../types/items";
import {
  purchaseReceptionService,
  type ResolveReceptionLinesResult,
} from "../api/purchaseReceptionService";
import { ProductPicker } from "./ProductPicker";
import {
  pendingProductsFormSchema,
  type PendingProductsFormValues,
  type PendingProductMode,
} from "../schemas/pendingProductsSchema";
import {
  applyBulkAssignment,
  applyMarginToSelected,
  buildInitialRow,
  buildResolveRequest,
  groupRowErrors,
  suggestBarcodeType,
  type BulkAssignment,
  type PendingProductSource,
} from "../utils/pendingProductsResolution";

type Props = {
  open: boolean;
  sources: PendingProductSource[];
  supplierName: string;
  onClose: () => void;
  /** Lote aplicado (todo o nada) — el llamador actualiza las líneas de la compra. */
  onResolved: (result: ResolveReceptionLinesResult) => void;
};

type Catalogs = ReturnType<typeof useItemCreationCatalogs>;
type LevelsByItem = Record<string, ItemPackagingLevelDto[]>;

/**
 * COMPRAS-METODO-ZH-01B — "Resolver productos pendientes": todas las líneas XML sin producto en una
 * sola tabla. Cada fila crea un producto, lo vincula a uno existente o se declara como otra
 * presentación (otro código del proveedor) de un producto nuevo del mismo lote. Las acciones en
 * bloque aplican valores a las filas seleccionadas; cada fila sigue siendo editable. El envío es
 * todo o nada: si el servidor rechaza alguna fila no se persiste nada y los datos quedan en pantalla.
 */
export function ResolvePendingProductsModal({
  open,
  sources,
  supplierName,
  onClose,
  onResolved,
}: Props) {
  const { t } = useI18n();
  const catalogs = useItemCreationCatalogs();
  const barcodeTypeCodes = catalogs.barcodeTypeOptions.map((b) => b.code);
  const form = useForm<PendingProductsFormValues>({
    resolver: zodResolver(pendingProductsFormSchema),
    defaultValues: { rows: [] },
  });
  const [rowErrors, setRowErrors] = useState<Map<string, string[]>>(new Map());
  const [generalErrors, setGeneralErrors] = useState<string[]>([]);
  const [levelsByItem, setLevelsByItem] = useState<LevelsByItem>({});
  const initialized = useRef(false);
  const sourcesById = useMemo(
    () => new Map(sources.map((s) => [s.rowId, s])),
    [sources],
  );

  // Precarga al abrir (y cuando llega el catálogo de tipos de código de barras para sugerirlo).
  useEffect(() => {
    if (!open) {
      initialized.current = false;
      return;
    }
    if (initialized.current) return;
    initialized.current = true;
    form.reset({ rows: sources.map((s) => buildInitialRow(s, barcodeTypeCodes)) });
    setRowErrors(new Map());
    setGeneralErrors([]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, sources, catalogs.barcodeTypeOptions.length]);

  useEffect(() => {
    if (!open) return;
    form.getValues("rows").forEach((row, index) => {
      if (!row.barcodeType && !form.getFieldState(`rows.${index}.barcodeType`).isDirty)
        form.setValue(`rows.${index}.barcodeType`, suggestBarcodeType(row.barcode, barcodeTypeCodes));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, catalogs.barcodeTypeOptions.length]);

  // Líneas con sugerencias del motor de matching: se proponen como "Vincular" (el usuario confirma).
  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    const review = sources.filter((s) => s.matchStatus === "NEEDS_REVIEW");
    void Promise.allSettled(
      review.map((s) => purchaseReceptionService.getLineMatch(s.rowId)),
    ).then((results) => {
      if (cancelled) return;
      results.forEach((r) => {
        if (r.status !== "fulfilled") return;
        const best = r.value.suggestions[0];
        const index = form.getValues("rows").findIndex((row) => row.rowId === r.value.lineId);
        if (!best || index < 0 || form.getFieldState(`rows.${index}`).isDirty) return;
        form.setValue(`rows.${index}.mode`, "link");
        form.setValue(`rows.${index}.linkItemId`, best.itemId);
        form.setValue(`rows.${index}.linkItemLabel`, `${best.sku} — ${best.shortName}`);
        void loadLevels(best.itemId, index);
      });
    });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, sources]);

  const loadLevels = async (itemId: string, index: number) => {
    form.setValue(`rows.${index}.linkPackagingLevelId`, "");
    try {
      const levels =
        levelsByItem[itemId] ??
        (await itemLookupFacade.getById(itemId).then((i) => i.packagingLevels.filter((p) => p.isActive)));
      setLevelsByItem((prev) => ({ ...prev, [itemId]: levels }));
      const preferred =
        levels.find((l) => l.isPurchaseDefault) ?? levels.find((l) => l.isBaseUnit);
      if (form.getValues(`rows.${index}.linkItemId`) !== itemId) return;
      form.setValue(`rows.${index}.linkPackagingLevelId`, preferred?.id ?? "");
    } catch (err) {
      setGeneralErrors([formatApiRequestError(err, { generic: t("purchases.pendingProducts.errors.generic") })]);
    }
  };

  const submit = form.handleSubmit(async (values) => {
    setRowErrors(new Map());
    setGeneralErrors([]);
    try {
      const result = await purchaseReceptionService.resolveLines(
        buildResolveRequest(values.rows),
      );
      if (!result.applied) {
        const grouped = groupRowErrors(result.errors);
        setRowErrors(grouped.byRow);
        setGeneralErrors(grouped.general);
        return;
      }
      onResolved(result);
    } catch (err) {
      if (!applyServerErrors(err, form.setError))
        setGeneralErrors([
          formatApiRequestError(err, {
            generic: t(
              "purchases.pendingProducts.errors.generic",
              "No se pudieron resolver los productos pendientes.",
            ),
          }),
        ]);
    }
  });

  const rows = useWatch({ control: form.control, name: "rows" }) ?? [];
  const selectedCount = rows.filter((r) => r.selected).length;
  const rowsWithErrors = rows.filter((r) => rowErrors.has(r.rowId)).length;

  return (
    <ZHModal
      open={open}
      size="full"
      onClose={() => { if (!form.formState.isSubmitting) onClose(); }}
      title={t("purchases.pendingProducts.title", "Resolver productos pendientes")}
      subtitle={t("purchases.pendingProducts.subtitle", {
        count: sources.length,
        supplier: supplierName,
      })}
      footer={
        <>
          <ZHBtn variant="ghost" size="md" onClick={onClose} disabled={form.formState.isSubmitting}>
            {t("common.cancel", "Cancelar")}
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="md"
            onClick={() => void submit()}
            disabled={selectedCount === 0 || form.formState.isSubmitting}
          >
            {t("purchases.pendingProducts.apply", { count: selectedCount })}
          </ZHBtn>
        </>
      }
    >
      <FormProvider {...form}>
        {generalErrors.length > 0 && (
          <ZHPageNotice variant="error" message={generalErrors.join("\n")} />
        )}
        {rowsWithErrors > 0 && (
          <ZHPageNotice
            variant="warning"
            message={t("purchases.pendingProducts.rowsRejected", { count: rowsWithErrors })}
          />
        )}
        <BulkActionsBar catalogs={catalogs} sourcesById={sourcesById} />
        <div className="prl-scroll">
          <table className="pf-table prl-table">
            <thead>
              <tr>
                <th>
                  <SelectAllCheckbox />
                </th>
                <th>{t("purchases.pendingProducts.col.xml", "Línea XML del proveedor")}</th>
                <th>{t("purchases.pendingProducts.col.action", "Acción")}</th>
                <th>{t("purchases.pendingProducts.col.product", "Producto ERP")}</th>
                <th>{t("purchases.pendingProducts.col.classification", "Clasificación")}</th>
                <th>{t("purchases.pendingProducts.col.presentation", "Presentación")}</th>
                <th>{t("purchases.pendingProducts.col.taxPrice", "Impuestos y precio")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) => (
                <PendingProductRowView
                  key={row.rowId}
                  index={index}
                  source={sourcesById.get(row.rowId)}
                  catalogs={catalogs}
                  levels={levelsByItem[row.linkItemId] ?? []}
                  serverErrors={rowErrors.get(row.rowId) ?? []}
                  onItemLinked={(itemId) => void loadLevels(itemId, index)}
                />
              ))}
            </tbody>
          </table>
        </div>
      </FormProvider>
    </ZHModal>
  );
}

function SelectAllCheckbox() {
  const { control, setValue, getValues } = useFormContext<PendingProductsFormValues>();
  const rows = useWatch({ control, name: "rows" }) ?? [];
  const all = rows.length > 0 && rows.every((r) => r.selected);
  return (
    <input
      type="checkbox"
      aria-label="Seleccionar todas"
      checked={all}
      onChange={() =>
        setValue("rows", getValues("rows").map((row) => ({ ...row, selected: !all })), { shouldDirty: true })
      }
    />
  );
}

/** Patrón: seleccionar filas → elegir valor → aplicar a seleccionados. */
function BulkActionsBar({
  catalogs,
  sourcesById,
}: {
  catalogs: Catalogs;
  sourcesById: ReadonlyMap<string, PendingProductSource>;
}) {
  const { t } = useI18n();
  const { getValues, setValue, clearErrors } = useFormContext<PendingProductsFormValues>();
  const priceDecimals = usePrecisionDecimals("salesUnitPrice");
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
      <CatalogSelect label={t("purchases.pendingProducts.field.barcodeType", "Tipo de código de barras")} value={assignment.barcodeType} onChange={(v) => set("barcodeType", v)} options={catalogs.barcodeTypeOptions.map((o) => ({ value: o.code, label: o.name }))} />
      <CatalogSelect label={t("purchases.pendingProducts.field.purchaseVat", "IVA compra")} value={assignment.purchaseVatCode} onChange={(v) => set("purchaseVatCode", v)} options={catalogs.vatRateOptions.map((o) => ({ value: o.code, label: o.name }))} />
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
          replaceRows(applyMarginToSelected(getValues("rows"), sourcesById, margin ?? 0, priceDecimals))
        }
      >
        {t("purchases.pendingProducts.applyMargin", "Calcular precio de venta")}
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

function PendingProductRowView({
  index,
  source,
  catalogs,
  levels,
  serverErrors,
  onItemLinked,
}: {
  index: number;
  source: PendingProductSource | undefined;
  catalogs: Catalogs;
  levels: ItemPackagingLevelDto[];
  serverErrors: string[];
  onItemLinked: (itemId: string) => void;
}) {
  const { t } = useI18n();
  const quantityDecimals = usePrecisionDecimals("quantity");
  const { register, control, setValue, formState } = useFormContext<PendingProductsFormValues>();
  const row = useWatch({ control, name: `rows.${index}` });
  const allRows = useWatch({ control, name: "rows" }) ?? [];
  const errors = formState.errors.rows?.[index];
  const [picking, setPicking] = useState(false);
  if (!row || !source) return null;
  const field = (name: keyof PendingProductsFormValues["rows"][number]) => `rows.${index}.${name}` as const;
  const err = (name: keyof NonNullable<typeof errors>) =>
    (errors?.[name] as { message?: string } | undefined)?.message;
  const parentOptions = allRows.filter((r) => r.selected && r.mode === "create" && r.rowId !== row.rowId);
  const factorIsBase = row.presentationFactor == null || row.presentationFactor === 1;

  return (
    <tr className={serverErrors.length > 0 ? "prl-row prl-row--error" : "prl-row"}>
      <td>
        <input type="checkbox" aria-label={source.supplierCode} {...register(field("selected"))} />
      </td>
      <td className="prl-xml">
        <strong>{source.supplierCode || "—"}</strong>
        {source.supplierAuxCode && <span className="prl-muted"> · {source.supplierAuxCode}</span>}
        <div className="prl-xml__desc">{source.xmlDescription}</div>
        <div className="prl-muted">
          {formatMoney(source.quantity, quantityDecimals)} ×{" "}
          <ZHMoneyValue value={source.unitPrice} precision="purchaseUnitPrice" currencySymbol="" />
        </div>
        {source.matchStatus === "NEEDS_REVIEW" && (
          <Badge variant="warning" label={t("purchases.pendingProducts.suggested", "Con sugerencia")} />
        )}
        {serverErrors.map((m) => (
          <div key={m} className="prl-row__error">
            {m}
          </div>
        ))}
      </td>
      <td>
        <ZhSelect density="compact" {...register(field("mode"))}>
          <option value="create">{t("purchases.pendingProducts.mode.create", "Crear producto")}</option>
          <option value="link">{t("purchases.pendingProducts.mode.link", "Vincular existente")}</option>
          <option value="presentation">
            {t("purchases.pendingProducts.mode.presentation", "Otra presentación de un producto nuevo")}
          </option>
        </ZhSelect>
      </td>
      <td className="prl-stack">
        {(row.mode as PendingProductMode) === "create" && (
          <>
            <ZHField density="compact" label={t("purchases.pendingProducts.field.name", "Nombre ERP")} fieldError={err("shortName")}>
              <input {...register(field("shortName"))} />
            </ZHField>
            <ZHField density="compact" label="SKU" fieldError={err("sku")}>
              <input {...register(field("sku"))} />
            </ZHField>
            <ZHField density="compact" label={t("purchases.pendingProducts.field.description", "Descripción ERP")} fieldError={err("description")}>
              <input {...register(field("description"))} />
            </ZHField>
            <ZHField density="compact" label={t("purchases.pendingProducts.field.barcode", "Código de barras")} fieldError={err("barcode") ?? err("barcodeType")}>
              <input {...register(field("barcode"))} />
              <ZhSelect density="compact" {...register(field("barcodeType"))}>
                <option value="">—</option>
                {catalogs.barcodeTypeOptions.map((o) => (
                  <option key={o.code} value={o.code}>
                    {o.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </>
        )}
        {row.mode === "link" && (
          <ZHField density="compact" label={t("purchases.pendingProducts.field.existing", "Producto existente")} fieldError={err("linkItemId")}>
            {row.linkItemLabel && !picking ? (
              <div className="prl-link">
                <span>{row.linkItemLabel}</span>
                <ZHBtn type="button" variant="ghost" size="xs" onClick={() => setPicking(true)}>
                  {t("purchases.pendingProducts.change", "Cambiar")}
                </ZHBtn>
              </div>
            ) : (
              <ProductPicker
                initialQuery={source.xmlDescription}
                onSelect={(p) => {
                  setValue(field("linkItemId"), p.id, { shouldValidate: true, shouldDirty: true });
                  setValue(field("linkItemLabel"), p.label);
                  setPicking(false);
                  onItemLinked(p.id);
                }}
              />
            )}
          </ZHField>
        )}
        {row.mode === "presentation" && (
          <ZHField density="compact" label={t("purchases.pendingProducts.field.parent", "Producto nuevo (otra fila)")} fieldError={err("parentRowId")}>
            <ZhSelect density="compact" {...register(field("parentRowId"))}>
              <option value="">—</option>
              {parentOptions.map((p) => (
                <option key={p.rowId} value={p.rowId}>
                  {p.shortName || p.sku}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        )}
      </td>
      <td className="prl-stack">
        {row.mode === "create" && (
          <>
            <RowSelect index={index} name="itemTypeId" label={t("purchases.pendingProducts.field.itemType", "Tipo de producto")} error={err("itemTypeId")} options={catalogs.itemTypeOptions.map((o) => ({ value: o.id, label: o.name }))} />
            <RowSelect index={index} name="brandId" label={t("purchases.pendingProducts.field.brand", "Marca")} error={err("brandId")} options={catalogs.brandOptions.map((o) => ({ value: o.id, label: o.name }))} />
            <RowSelect index={index} name="categoryNodeId" label={t("purchases.pendingProducts.field.category", "Categoría")} error={err("categoryNodeId")} options={catalogs.categoryOptions.map((o) => ({ value: o.id, label: o.name }))} />
          </>
        )}
      </td>
      <td className="prl-stack">
        {row.mode === "create" && (
          <RowSelect index={index} name="defaultUomCode" label={t("purchases.pendingProducts.field.uom", "Unidad base")} error={err("defaultUomCode")} options={catalogs.uomOptions.map((o) => ({ value: o.code, label: o.name }))} />
        )}
        {row.mode === "link" ? (
          <ZHField density="compact" label={t("purchases.pendingProducts.field.supplierPresentation", "Presentación del proveedor")}>
            <ZhSelect density="compact" {...register(field("linkPackagingLevelId"))}>
              <option value="">—</option>
              {levels.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name} ({formatMoney(l.baseQuantity, quantityDecimals)})
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        ) : (
          <>
            <ZHField density="compact" label={t("purchases.pendingProducts.field.factor", "Unidades por presentación")} fieldError={err("presentationFactor")}>
              <ZhDecimalInput
                precision="conversionFactor"
                positiveOnly
                {...register(field("presentationFactor"), {
                  setValueAs: (v) => (v === "" || v == null ? null : Number(v)),
                })}
              />
            </ZHField>
            {!factorIsBase && (
              <>
                <ZHField density="compact" label={t("purchases.pendingProducts.field.presentationName", "Nombre presentación")} fieldError={err("presentationName")}>
                  <input placeholder="Caja x12" {...register(field("presentationName"))} />
                </ZHField>
                <RowSelect index={index} name="presentationUomCode" label={t("purchases.pendingProducts.field.presentationUom", "Unidad presentación")} error={err("presentationUomCode")} options={catalogs.uomOptions.map((o) => ({ value: o.code, label: o.name }))} />
              </>
            )}
          </>
        )}
      </td>
      <td className="prl-stack">
        {row.mode === "create" && (
          <>
            <RowSelect index={index} name="purchaseVatCode" label={t("purchases.pendingProducts.field.purchaseVat", "IVA compra")} error={err("purchaseVatCode")} options={catalogs.vatRateOptions.map((o) => ({ value: o.code, label: o.name }))} />
            <RowSelect index={index} name="saleVatCode" label={t("purchases.pendingProducts.field.saleVat", "IVA venta")} error={err("saleVatCode")} options={catalogs.vatRateOptions.map((o) => ({ value: o.code, label: o.name }))} />
            <ZHField density="compact" label={t("purchases.pendingProducts.field.salePrice", "Precio de venta")} fieldError={err("salePrice")}>
              <ZhDecimalInput
                precision="salesUnitPrice"
                positiveOnly
                {...register(field("salePrice"), {
                  setValueAs: (v) => (v === "" || v == null ? null : Number(v)),
                })}
              />
            </ZHField>
          </>
        )}
      </td>
    </tr>
  );
}

function RowSelect({
  index,
  name,
  label,
  error,
  options,
}: {
  index: number;
  name: "itemTypeId" | "brandId" | "categoryNodeId" | "defaultUomCode" | "presentationUomCode" | "purchaseVatCode" | "saleVatCode";
  label: string;
  error: string | undefined;
  options: { value: string; label: string }[];
}) {
  const { register } = useFormContext<PendingProductsFormValues>();
  return (
    <ZHField density="compact" label={label} fieldError={error}>
      <ZhSelect density="compact" {...register(`rows.${index}.${name}` as const)}>
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
