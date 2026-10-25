import { ZHCodeConflicts } from "../../../components/zh/ZHCodeConflicts";
import type { ItemCodeConflict } from "../../items/facades/itemLookupFacade";
import { useState, type ReactNode } from "react";
import { useFormContext, useWatch } from "react-hook-form";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { Badge } from "../../../components/PageShell";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { formatMoney } from "../../../lib/sanitizers";
import type { ItemPackagingLevelDto } from "../../../types/items";
import type { NoticeVM } from "../../../notices";
import { ProductPicker } from "./ProductPicker";
import { PurchaseLineIssuesTag } from "./PurchaseLineIssuesTag";
import { SalePriceSummary, XmlFacts, type PendingProductsCatalogs } from "./PendingProductFacts";
import type { PendingProductsFormValues, PendingProductMode } from "../schemas/pendingProductsSchema";
import {
  isBasePresentation,
  needsProvisionalUnitBarcode,
  rowSalePrice,
  type PendingProductSource,
  type PendingRowIssue,
  type SalePricing,
} from "../utils/pendingProductsResolution";

type Row = PendingProductsFormValues["rows"][number];

/**
 * Una línea XML pendiente: fila compacta (selección, línea del proveedor, acción y producto ERP)
 * con sus datos por completar en la etiqueta de incidencias de Compras + detalle expandible con
 * todos los campos. El detalle colapsado sigue montado (`hidden`):
 * los datos ingresados se conservan al expandir o colapsar.
 */
export function PendingProductRow({
  index,
  source,
  catalogs,
  pricing,
  levels,
  serverErrors,
  codeConflicts = [],
  codeCheckMessage,
  issues,
  expanded,
  onExpandedChange,
  onItemLinked,
}: {
  index: number;
  source: PendingProductSource | undefined;
  catalogs: PendingProductsCatalogs;
  pricing: SalePricing;
  levels: ItemPackagingLevelDto[];
  serverErrors: string[];
  codeConflicts?: ItemCodeConflict[];
  codeCheckMessage?: string;
  issues: PendingRowIssue[];
  expanded: boolean;
  onExpandedChange: (expanded: boolean) => void;
  onItemLinked: (itemId: string) => void;
}) {
  const { t } = useI18n();
  const quantityDecimals = usePrecisionDecimals("quantity");
  const { register, control, setValue, formState } = useFormContext<PendingProductsFormValues>();
  const row = useWatch({ control, name: `rows.${index}` });
  const allRows = useWatch({ control, name: "rows" }) ?? [];
  const errors = formState.errors.rows?.[index];
  const [picking, setPicking] = useState(false);
  const detailId = `prl-row-detail-${index}`;
  if (!row || !source) return null;
  const field = (name: keyof Row) => `rows.${index}.${name}` as const;
  const err = (name: keyof NonNullable<typeof errors>) => {
    const message = (errors?.[name] as { message?: string } | undefined)?.message;
    return message && !represented(message) ? message : undefined;
  };
  const parentOptions = allRows.filter((r) => r.selected && r.mode === "create" && r.rowId !== row.rowId);
  const factorIsBase = isBasePresentation(row);
  const provisionalCode =
    row.mode === "create" && row.selected ? needsProvisionalUnitBarcode(allRows, row.rowId) : null;
  // Factor de la presentación que entrega el proveedor: la del Item vinculado o la declarada aquí.
  const factor =
    row.mode === "link"
      ? (levels.find((l) => l.id === row.linkPackagingLevelId)?.baseQuantity ?? null)
      : row.presentationFactor;
  const modeField = register(field("mode"));
  const conflicts = codeConflicts.filter((c, i, all) => all.findIndex(other => other.kind === c.kind && other.code === c.code && other.itemId === c.itemId) === i);
  // Presentation-only matching: persistence and diagnostic validation remain authoritative.
  const represented = (message: string) => conflicts.some(c => {
    const text = message.toLocaleLowerCase();
    const kind = c.kind === "SupplierCode" ? "proveedor" : c.kind === "Barcode" ? "barras" : "sku";
    return text.includes(kind) && /duplic|ya est\u00e1|ya existe|repetid|varias l\u00edneas/.test(text);
  });
  const messages = [...new Set([...serverErrors, ...issues.map(i => i.message)])].filter(m => !represented(m));
  const lineIssues: NoticeVM[] = [
    ...(codeCheckMessage ? [{ severity: "info" as const, intent: "actionRequired" as const, source: "frontend-state" as const,
      label: codeCheckMessage }] : []),
    ...(conflicts.length ? [{ severity: "danger" as const, intent: "actionRequired" as const, source: "frontend-state" as const,
      label: t("purchases.pendingProducts.codeConflicts") }] : []),
    ...messages.map((message, i) => ({
      severity: serverErrors.includes(message) ? "danger" as const : "warning" as const,
      intent: "actionRequired" as const, source: "frontend-state" as const, label: message,
      ...(i === 0 ? { actionLabel: t("purchases.pendingProducts.completeData", "Completar datos"), onAction: () => onExpandedChange(true) } : {}),
    })),
  ];
  const hasConflict = (kind: ItemCodeConflict["kind"]) => conflicts.some(c => c.kind === kind);
  const code = source.supplierCode || "—";

  return (
    <div className={lineIssues.length > 0 ? "prl-row-group prl-row-group--has-issues" : "prl-row-group"} role="rowgroup">
      {lineIssues.length > 0 && <PurchaseLineIssuesTag issues={lineIssues}>
        <ZHCodeConflicts compact viewLabel={t("items.codeConflicts.view")} conflicts={conflicts.map(c => ({
          code: c.code, label: t(`items.codeConflicts.kind.${c.kind}`),
          description: c.itemId ? c.itemName ?? "" : t("items.codeConflicts.batch", { rows: c.rows.join(", ") }),
          href: c.itemId ? `/products/items?view=${encodeURIComponent(c.itemId)}` : undefined,
        }))} />
      </PurchaseLineIssuesTag>}
      <div className={serverErrors.length > 0 ? "prl-row prl-row--error" : "prl-row"} role="row">
        <span role="cell" className="prl-row__check">
          <input type="checkbox" aria-label={source.supplierCode} {...register(field("selected"))}
            aria-invalid={serverErrors.length > 0 || undefined} />
        </span>
        <span role="cell" className="prl-xml">
          <span className="prl-xml__code">
            <strong className={hasConflict("SupplierCode") ? "prl-row__error" : undefined} aria-invalid={hasConflict("SupplierCode") || undefined}>{code}</strong>
            {source.supplierAuxCode && <span className="prl-muted"> · {source.supplierAuxCode}</span>}
            {source.matchStatus === "NEEDS_REVIEW" && (
              <Badge variant="warning" label={t("purchases.pendingProducts.suggested", "Con sugerencia")} />
            )}
          </span>
          <span className="prl-xml__desc" title={source.xmlDescription}>{source.xmlDescription}</span>
          <span className="prl-muted">
            {formatMoney(source.quantity, quantityDecimals)} ×{" "}
            <ZHMoneyValue value={source.unitPrice} precision="purchaseUnitPrice" currencySymbol="" />
          </span>
        </span>
        <span role="cell" data-label={t("purchases.pendingProducts.col.action", "Acción")}>
          <ZhSelect density="compact" aria-label={`${t("purchases.pendingProducts.col.action", "Acción")} · ${code}`}
            {...modeField}
            onChange={(e) => {
              void modeField.onChange(e);
              onExpandedChange(true);
            }}
          >
            <option value="create">{t("purchases.pendingProducts.mode.create", "Crear producto")}</option>
            <option value="link">{t("purchases.pendingProducts.mode.link", "Vincular existente")}</option>
            <option value="presentation">
              {t("purchases.pendingProducts.mode.presentation", "Otra presentación de un producto nuevo")}
            </option>
          </ZhSelect>
        </span>
        <span role="cell" className="prl-row__product" data-label={t("purchases.pendingProducts.col.product", "Producto ERP")}>
          <ProductSummary row={row} allRows={allRows} />
        </span>
        <span role="cell" className="prl-row__toggle">
          <ZHBtn type="button" variant="ghost" size="sm" expanded={expanded} aria-controls={detailId}
            aria-label={`${t("purchases.pendingProducts.details", "Detalles")} · ${code}`}
            onClick={() => onExpandedChange(!expanded)}>
            {t("purchases.pendingProducts.details", "Detalles")}
          </ZHBtn>
        </span>
      </div>
      <div className="prl-detail-row" role="row" id={detailId} hidden={!expanded}>
        <div role="cell" className="prl-detail">
          <DetailSection title={t("purchases.pendingProducts.col.xml", "Línea XML del proveedor")}>
            <XmlFacts source={source} factor={factor} catalogs={catalogs} purchaseVatError={err("purchaseVatCode")} />
          </DetailSection>
          <DetailSection title={t("purchases.pendingProducts.col.product", "Producto ERP")}>
            {(row.mode as PendingProductMode) === "create" && (
              <>
                <ZHField density="compact" required label={t("items.form.shortName", "Nombre comercial")} fieldError={err("shortName")}>
                  <input {...register(field("shortName"))} />
                </ZHField>
                <ZHField className={hasConflict("Sku") ? "zh-field--error" : undefined} density="compact" required label="SKU" fieldError={err("sku")}>
                  <input aria-invalid={hasConflict("Sku") || undefined} {...register(field("sku"))} />
                </ZHField>
                <ZHField density="compact" required label={t("items.form.description", "Descripción completa")} fieldError={err("description")}>
                  <ZhTextarea rows={3} {...register(field("description"))} />
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
          </DetailSection>
          {row.mode === "create" && (
            <DetailSection title={t("purchases.pendingProducts.col.classification", "Clasificación")}>
              <RowSelect index={index} name="itemTypeId" label={t("items.form.itemType", "Tipo")} error={err("itemTypeId")} options={catalogs.itemTypeOptions.map((o) => ({ value: o.id, label: o.name }))} />
              <RowSelect index={index} name="brandId" label={t("items.form.brand", "Marca")} error={err("brandId")} options={catalogs.brandOptions.map((o) => ({ value: o.id, label: o.name }))} />
              <RowSelect index={index} name="categoryNodeId" label={t("items.form.category", "Categoría")} error={err("categoryNodeId")} options={catalogs.categoryOptions.map((o) => ({ value: o.id, label: o.name }))} />
            </DetailSection>
          )}
          <DetailSection title={t("purchases.pendingProducts.col.presentation", "Presentación")}>
            {row.mode === "create" && (
              <RowSelect index={index} name="defaultUomCode" label={t("items.form.uom", "Unidad base de inventario")} error={err("defaultUomCode")} options={catalogs.uomOptions.map((o) => ({ value: o.code, label: o.name }))} />
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
                <ZHField density="compact" label={t("items.packaging.col.baseQuantity", "Equivalencia en unidad base")} fieldError={err("presentationFactor")}>
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
                    <ZHField density="compact" label={t("items.packaging.col.name", "Nombre")} fieldError={err("presentationName")}>
                      <input placeholder="Caja x12" {...register(field("presentationName"))} />
                    </ZHField>
                    <RowSelect index={index} name="presentationUomCode" label={t("items.packaging.col.uom", "Unidad de presentación")} error={err("presentationUomCode")} options={catalogs.uomOptions.map((o) => ({ value: o.code, label: o.name }))} />
                  </>
                )}
                <ZHField
                  density="compact"
                  label={
                    factorIsBase
                      ? t("purchases.pendingProducts.field.unitBarcode", "Código de barras de la unidad (opcional)")
                      : t("purchases.pendingProducts.field.presentationBarcode", "Código de barras de esta presentación (opcional)")
                  }
                  className={hasConflict("Barcode") ? "zh-field--error" : undefined}
                  fieldError={err("barcode") ?? err("barcodeType")}
                  hint={
                    provisionalCode
                      ? t("purchases.pendingProducts.provisionalBarcode")
                      : undefined
                  }
                >
                  <input aria-invalid={hasConflict("Barcode") || undefined} placeholder="EAN / UPC" {...register(field("barcode"))} />
                  {factorIsBase && (
                    <ZhSelect density="compact" aria-label={t("items.barcodes.type", "Tipo de código de barras")} {...register(field("barcodeType"))}>
                      <option value="">—</option>
                      {catalogs.barcodeTypeOptions.map((o) => (
                        <option key={o.code} value={o.code}>
                          {o.name}
                        </option>
                      ))}
                    </ZhSelect>
                  )}
                </ZHField>
              </>
            )}
          </DetailSection>
          {row.mode === "create" && (
            <DetailSection title={t("purchases.pendingProducts.col.taxPrice", "Impuestos y precio")}>
              <RowSelect index={index} name="saleVatCode" label={t("items.tax.saleVatCode", "Tarifa IVA venta")} error={err("saleVatCode")} options={catalogs.vatRateOptions.map((o) => ({ value: o.code, label: o.name }))} />
              <ZHField
                density="compact"
                label={
                  (pricing.vatPercentByCode.get(row.saleVatCode) ?? 0) > 0
                    ? t("purchases.pendingProducts.field.salePriceGross", "PVP final con IVA (unidad base)")
                    : t("purchases.pendingProducts.field.salePriceNoVat", "PVP final (unidad base)")
                }
                fieldError={err("salePriceGross")}
              >
                <ZhDecimalInput
                  precision="salesUnitPrice"
                  positiveOnly
                  {...register(field("salePriceGross"), {
                    setValueAs: (v) => (v === "" || v == null ? null : Number(v)),
                  })}
                />
              </ZHField>
              <SalePriceSummary
                row={row}
                breakdown={rowSalePrice(row, source, pricing)}
                priceDecimals={pricing.priceDecimals}
              />
            </DetailSection>
          )}
        </div>
      </div>
    </div>
  );
}

function DetailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="prl-section" aria-label={title}>
      <h4 className="zh-section-title prl-section__title">{title}</h4>
      {children}
    </section>
  );
}

/** Lo que la fila resolverá, en una línea: nombre nuevo, producto vinculado o producto padre. */
function ProductSummary({ row, allRows }: { row: Row; allRows: Row[] }) {
  const { t } = useI18n();
  const muted = (text: string) => <span className="prl-muted">{text}</span>;
  if (row.mode === "link")
    return row.linkItemLabel ? <span className="prl-truncate" title={row.linkItemLabel}>{row.linkItemLabel}</span>
      : muted(t("purchases.pendingProducts.productSummary.noLink", "Sin producto vinculado"));
  if (row.mode === "presentation") {
    const parent = allRows.find((r) => r.rowId === row.parentRowId);
    return parent ? <span className="prl-truncate">{t("purchases.pendingProducts.productSummary.presentationOf", { name: parent.shortName || parent.sku })}</span>
      : muted(t("purchases.pendingProducts.productSummary.noParent", "Sin producto nuevo asignado"));
  }
  return (
    <>
      {row.shortName ? <span className="prl-truncate" title={row.shortName}>{row.shortName}</span>
        : muted(t("purchases.pendingProducts.productSummary.noName", "Sin nombre"))}
      {row.sku && <span className="prl-muted prl-truncate">{row.sku}</span>}
    </>
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
  name: "itemTypeId" | "brandId" | "categoryNodeId" | "defaultUomCode" | "presentationUomCode" | "saleVatCode";
  label: string;
  error: string | undefined;
  options: { value: string; label: string }[];
}) {
  const { register } = useFormContext<PendingProductsFormValues>();
  const { t } = useI18n();
  return (
    <ZHField density="compact" label={label} required fieldError={error} hint={name === "defaultUomCode" ? t("items.form.uomHint") : undefined}>
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
