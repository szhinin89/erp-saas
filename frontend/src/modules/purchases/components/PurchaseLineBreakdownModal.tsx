import { PurchaseActionReason } from "./PurchaseActionReason";
import { PurchaseFieldError } from "./PurchaseFieldError";
import { purchaseFieldErrors } from "../utils/purchaseFieldErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { isAxiosError } from "axios";
import { useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHDataTable } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHRowDeleteAction } from "../../../components/zh/ZHRowDeleteAction";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { useI18n } from "../../../i18n/i18n";
import { usePrecisionPolicy } from "../../../hooks/usePrecisionPolicy";
import { roundToDecimals } from "../../../lib/sanitizers";
import { purchaseService } from "../api/purchaseService";
import type { PurchaseItemContextDto, PurchaseLineDto } from "../api/purchaseService";
import { purchaseLineSchema, type PurchaseLineFormValues as Line } from "../schemas/purchaseInvoiceSchema";
import { resolveLineTaxes } from "../utils/purchaseCalc";
import {
  breakdownBalance,
  emptyXmlAllocation,
  grossXmlSubtotal,
  type BreakdownBalanceRow,
} from "../utils/purchaseXmlBreakdown";
import { ICE_TAX_CODE, IRBPNR_TAX_CODE, VAT_TAX_CODE } from "../utils/purchaseXmlEvidence";
import { ProductPicker, type ProductProfile } from "./ProductPicker";

interface Props {
  source: Line;
  lines: Line[];
  supplierId: string;
  warehouseId?: string;
  onCancel: () => void;
  onApply: (lines: Line[]) => boolean;
}
const schema = z.object({ components: z.array(purchaseLineSchema).min(2) });
const HISTORY_PAGE_SIZE = 25;

/** One editor for the origin group. Applying only updates the existing purchase form. */
export function PurchaseLineBreakdownModal({ source, lines, supplierId, warehouseId, onCancel, onApply }: Readonly<Props>) {
  const { t } = useI18n();
  const policy = usePrecisionPolicy();
  const xml = source.xmlSource!;
  const group = lines.filter(l => l.purchaseReceptionLineId === source.purchaseReceptionLineId);
  const { watch, setValue, handleSubmit, formState: { errors } } = useForm<{ components: Line[] }>({
    resolver: zodResolver(schema),
    defaultValues: { components: group.filter(l => l.xmlAllocation).map(l => structuredClone(l)) },
  });
  const components = watch("components");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const reportError = (err: unknown) => setError(formatApiRequestError(isAxiosError(err) && err.response ? err : undefined, {
    generic: t("purchases.breakdown.error", "No se pudo cargar parte de la información. Intente nuevamente."),
  }));
  const formId = useId();
  const [history, setHistory] = useState<{ invoice: string; rows: PurchaseLineDto[] }[]>([]);
  const [historyPage, setHistoryPage] = useState(0);
  const [hasMore, setHasMore] = useState(true);
  const [openTaxes, setOpenTaxes] = useState<number | null>(null);
  const [showDetail, setShowDetail] = useState(false);
  const balance = breakdownBalance(source, components);

  // ── Labels: tax names from the existing i18n keys; VAT shows its rate, never the SRI code ──
  const taxName = (code: string) => ({
    [VAT_TAX_CODE]: t("purchases.reception.xmlView.lines.vat", "IVA"),
    [ICE_TAX_CODE]: t("purchases.reception.xmlView.lines.ice", "ICE"),
    [IRBPNR_TAX_CODE]: t("purchases.reception.xmlView.lines.irbpnr", "IRBPNR"),
  } as Record<string, string>)[code] ?? code;
  const taxLabel = (taxCode?: string, taxRateCode?: string) => {
    const origin = xml.taxes.find(o => o.taxCode === taxCode && o.taxRateCode === taxRateCode);
    if (!origin) return "";
    if (origin.taxCode === VAT_TAX_CODE) return `${taxName(origin.taxCode)} ${origin.tarifa}%`;
    const repeated = xml.taxes.filter(o => o.taxCode === origin.taxCode).length > 1;
    return repeated ? `${taxName(origin.taxCode)} ${xml.taxes.indexOf(origin) + 1}` : taxName(origin.taxCode);
  };
  const conceptLabel = (r: BreakdownBalanceRow) => {
    if (r.concept === "base") return `${taxLabel(r.taxCode, r.taxRateCode)} · ${t("purchases.breakdown.base", "Base")}`;
    if (r.concept === "tax") return `${taxLabel(r.taxCode, r.taxRateCode)} · ${t("purchases.breakdown.amount", "Importe")}`;
    if (r.concept === "subtotal") return t("purchases.breakdown.subtotal", "Subtotal bruto");
    if (r.concept === "discount") return t("purchases.breakdown.discount", "Descuento");
    return t("purchases.breakdown.total", "Total");
  };

  // ── Footer status (presentation only; validity still comes from breakdownBalance) ──
  const totalRow = balance.rows.find(r => r.concept === "total");
  const pending = totalRow?.difference ?? 0;
  const differences = balance.rows.filter(r => r.difference !== 0).length;
  const ready = balance.valid;
  // The count lives only in «Conciliación (n)»; the reason names the corrective step once.
  const blockedReason = [
    components.length < 2 && t("purchases.breakdown.minTwo", "Agregue al menos dos productos."),
    components.some(l => !(l.quantity > 0)) && t("purchases.breakdown.quantityRequired", "Ingrese la cantidad de cada producto."),
    differences > 0 && t("purchases.breakdown.reviewReconciliation", "Revise la conciliación: cada concepto e impuesto debe cuadrar."),
  ].filter(Boolean).join(" ") || (!ready ? t("purchases.breakdown.incomplete", "El desglose XML debe cuadrar antes de guardar.") : "");
  const parsed = schema.safeParse({ components });
  const validationIssues = parsed.success ? [] : parsed.error.issues.map(issue => ({ path: issue.path.join("."), message: issue.message }));
  const fieldIssues = [...purchaseFieldErrors(errors), ...validationIssues];
  const errorFor = (index: number, path: string) => {
    const issue = fieldIssues.find(issue => issue.path === `components.${index}.${path}`);
    if (issue && path.startsWith("xmlAllocation.")) return t("purchases.validation.amountNonNegative", "El importe no puede ser negativo.");
    return issue?.message;
  };


  const patch = (index: number, update: Partial<Line>) => setValue("components", components.map((l, i) => {
    if (i !== index) return l;
    const next = { ...l, ...update };
    return { ...next, unitPrice: next.quantity > 0 ? roundToDecimals(next.xmlAllocation!.subtotal / next.quantity, policy.purchaseUnitPriceDecimals) : 0,
      quantityInBaseUom: roundToDecimals(next.quantity * (next.conversionFactor ?? 1), policy.quantityDecimals) };
  }), { shouldDirty: true, shouldValidate: true });
  /** Commit only after a real edit, same as the other line editors. The accessible name includes the product. */
  const decimal = (label: string, product: string, value: number, commit: (n: number) => void,
    precision: "quantity" | "money" | "unitCost", index: number, path: string) => {
    const message = errorFor(index, path);
    const id = `${formId}-${index}-${path}-error`;
    return <><ZhDecimalInput aria-label={`${label} · ${product}`} defaultValue={value} precision={precision} positiveOnly density="compact"
      aria-invalid={!!message} aria-describedby={message ? id : undefined}
      onValueCommit={v => commit(Number(v) || 0)} /><PurchaseFieldError id={id} message={message} /></>;
  };
  const presentationPatch = (context: PurchaseItemContextDto | undefined, levelId: string | null | undefined): Partial<Line> => {
    const level = context?.packagingLevels.find(p => p.id === levelId);
    return level
      ? { packagingLevelId: level.id, conversionFactor: level.baseQuantity, uomCode: level.uomCode, baseUomCode: context?.baseUomCode }
      : { packagingLevelId: undefined, conversionFactor: 1, uomCode: context?.baseUomCode, baseUomCode: context?.baseUomCode };
  };
  const newComponent = (key: number, itemId: string, description: string, context: PurchaseItemContextDto, quantity = 0,
    packagingLevelId?: string | null): Line => {
    const presentation = presentationPatch(context, packagingLevelId);
    return {
    _key: key, itemId, description, quantity, unitPrice: 0, discountPct: 0,
    vatCode: xml.vatCode, iceCode: xml.iceCode, context, ...presentation,
    quantityInBaseUom: roundToDecimals(quantity * (presentation.conversionFactor ?? 1), policy.quantityDecimals),
    warehouseId: context.participatesInInventory ? source.warehouseId : null,
    purchaseReceptionLineId: source.purchaseReceptionLineId, xmlSource: xml,
    xmlAllocation: emptyXmlAllocation(xml),
    };
  };

  const addProduct = async (product: ProductProfile) => {
    setLoading(true); setError(null);
    try {
      const context = await purchaseService.getItemContext(product.id, warehouseId, supplierId);
      const purchaseDefault = context.packagingLevels.find(p => p.isPurchaseDefault && !p.isBaseUnit)?.id;
      setValue("components", [...watch("components"), newComponent(Date.now(), product.id, product.label, context, 0, purchaseDefault)]);
    } catch (err) { reportError(err); }
    finally { setLoading(false); }
  };
  // Suggestions only: previous compositions of this supplier + XML code. Nothing is learned or linked.
  const consultHistory = async () => {
    setLoading(true); setError(null);
    try {
      const page = historyPage + 1;
      const result = await purchaseService.list(undefined, "Confirmed", page, HISTORY_PAGE_SIZE, supplierId);
      const fetched = await Promise.allSettled(result.items.map(i => purchaseService.getById(i.id)));
      setError(fetched.some(r => r.status === "rejected") ? t("purchases.breakdown.error", "No se pudo cargar parte de la información. Intente nuevamente.") : null);
      const invoices = fetched.flatMap(r => r.status === "fulfilled" ? [r.value] : []);
      const suggestions = invoices.filter(inv => inv.supplierId === supplierId).flatMap(inv => {
        const groups = new Map<string, PurchaseLineDto[]>();
        inv.lines.filter(l => l.xmlAllocatedSubtotal != null && !!l.itemId && l.xmlSource?.supplierCode === xml.supplierCode)
          .forEach(l => {
            const id = l.purchaseReceptionLineId!;
            groups.set(id, [...(groups.get(id) ?? []), l]);
          });
        return [...groups.values()].map(rows => ({ invoice: inv.invoiceNumber, rows }));
      });
      setHistory(prev => [...prev, ...suggestions]); setHistoryPage(page);
      setHasMore(page * HISTORY_PAGE_SIZE < result.total);
    } catch (err) { reportError(err); }
    finally { setLoading(false); }
  };
  const applySuggestion = async (rows: PurchaseLineDto[]) => {
    setLoading(true); setError(null);
    try {
      const contexts = await Promise.all(rows.map(l => purchaseService.getItemContext(l.itemId!, warehouseId, supplierId)));
      // Products, quantities and packaging are proposed; every fiscal amount must be assigned again.
      setValue("components", rows.map((l, i) => newComponent(Date.now() + i, l.itemId!, l.description, contexts[i], l.quantity,
        contexts[i].packagingLevels.some(p => p.id === l.packagingLevelId) ? l.packagingLevelId : undefined)), { shouldDirty: true });
    } catch (err) { reportError(err); }
    finally { setLoading(false); }
  };

  const money = (value: number) => <ZHMoneyValue precision="money" value={value} />;
  const summaryItem = (label: string, value: number) =>
    <span className="pbd-summary__item"><span className="pbd-summary__label">{label}</span> {money(value)}</span>;

  return <ZHModal open onClose={onCancel} size="2xl" title={`${t("purchases.breakdown.heading", "Desglosar productos")} · ${xml.supplierCode ?? ""}`}
    subtitle={t("purchases.breakdown.subtitle", "Reemplace esta línea XML por los productos reales recibidos. Los importes deben cuadrar.")}
    footer={<>
      <span className="pbd-summary pbd-footer-status" aria-live="polite">
        {summaryItem(t("purchases.breakdown.distributed", "Distribuido"), totalRow?.assigned ?? 0)}
        {pending !== 0
          ? summaryItem(pending < 0 ? t("purchases.breakdown.excess", "Excedente") : t("purchases.breakdown.pending", "Pendiente"), Math.abs(pending))
          // A zero total pending is not a balanced split: per-concept differences still block it.
          : <span className="pbd-summary__item pbd-summary__label">{differences === 0
            ? t("purchases.breakdown.balanced", "Cuadrado")
            : t("purchases.breakdown.unbalancedConcepts", "Conceptos sin cuadrar")}</span>}
      </span>
      <ZHBtn type="button" variant="secondary" icon="compare_arrows" expanded={showDetail} aria-controls="pbd-reconciliation"
        onClick={() => setShowDetail(v => !v)}>{t("purchases.breakdown.reconciliation", "Conciliación")}{differences > 0 ? ` (${differences})` : ""}</ZHBtn>
      <ZHBtn type="button" variant="secondary" onClick={onCancel}>{t("purchases.breakdown.cancel", "Cancelar")}</ZHBtn>
      <ZHBtn type="button" variant="primary" disabled={!ready || loading}
        onClick={() => void handleSubmit(({ components: rows }) => {
          try {
            if (onApply(rows)) onCancel();
            else setError(t("purchases.breakdown.incomplete", "El desglose XML debe cuadrar antes de guardar."));
          } catch (err) { reportError(err); }
        })()}>{t("purchases.breakdown.apply", "Aplicar desglose")}</ZHBtn>
      <PurchaseActionReason compact={false} reason={blockedReason || (loading ? t("common.loading", "Cargando...") : null)} action={t("purchases.breakdown.apply", "Aplicar desglose")} />
    </>}>
    {error && <ZHPageNotice variant="error" message={error} />}
    {loading && <span role="status" aria-live="polite">{t("common.loading", "Cargando...")}</span>}

    <p className="pbd-summary" aria-label={t("purchases.breakdown.original", "Original")}>
      <span className="pbd-summary__item pbd-summary__description">{xml.description}</span>
      {summaryItem(t("purchases.breakdown.subtotal", "Subtotal bruto"), grossXmlSubtotal(xml))}
      {xml.discount > 0 && summaryItem(t("purchases.breakdown.discount", "Descuento"), xml.discount)}
      {summaryItem(t("purchases.breakdown.taxes", "Impuestos"), xml.taxes.reduce((s, tx) => s + tx.taxAmount, 0))}
      {summaryItem(t("purchases.breakdown.total", "Total"), xml.totalLine)}
    </p>

    <div className="pbd-search" role="search" aria-label={t("purchases.breakdown.addProduct", "Agregar producto")}>
      <div className="pbd-search__picker"><ProductPicker floating disabled={loading} onSelect={p => void addProduct(p)} /></div>
      <ZHBtn type="button" variant="ghost" size="sm" disabled={loading || !xml.supplierCode || !supplierId || !hasMore}
        title={t("purchases.breakdown.history", "Consultar historial del proveedor y código XML")}
        onClick={() => void consultHistory()}>{t("purchases.breakdown.historyShort", "Historial")}</ZHBtn>
    </div>
    {!error && !loading && historyPage > 0 && history.length === 0 && <p className="zh-inline-empty">{t("purchases.breakdown.noHistory", "No se encontraron composiciones en esta página del historial.")}</p>}
    {history.map(h => <div key={`${h.invoice}:${h.rows[0].purchaseReceptionLineId}`} className="zh-flex-between pbd-history">
      <span>{h.invoice}: {h.rows.map(l => l.description).join(", ")}</span>
      <ZHBtn type="button" variant="ghost" size="sm" disabled={loading} onClick={() => void applySuggestion(h.rows)}>{t("purchases.breakdown.suggest", "Usar productos como sugerencia; asignar importes nuevamente")}</ZHBtn></div>)}

    {components.length > 0 && <div className="pbd-rows" role="table" aria-label={t("purchases.breakdown.title", "Desglose de línea XML")}>
      <div className="pbd-row pbd-row--head" role="row">
        <span role="columnheader">{t("purchases.breakdown.product", "Producto")}</span>
        <span role="columnheader">{t("purchases.breakdown.presentation", "Presentación")}</span>
        <span role="columnheader">{t("purchases.breakdown.quantityComponent", "Cantidad")}</span>
        <span role="columnheader">{t("purchases.breakdown.assignedCost", "Costo")}</span>
        <span role="columnheader">{t("purchases.breakdown.taxes", "Impuestos")}</span>
        <span role="columnheader" />
      </div>
      {components.map((line, index) => {
        const product = line.description;
        const taxes = resolveLineTaxes(line);
        const expanded = openTaxes === line._key;
        const detailId = `pbd-taxes-${line._key}`;
        const allocation = line.xmlAllocation!;
        return <div key={line._key} className="pbd-row-group" role="rowgroup">
          <div className="pbd-row" role="row">
            <span role="cell" className="pbd-row__product" title={product}>{product}</span>
            <span role="cell" data-label={t("purchases.breakdown.presentation", "Presentación")}><ZhSelect density="compact" aria-label={`${t("purchases.breakdown.presentation", "Presentación")} · ${product}`}
              value={line.packagingLevelId ?? ""} onChange={e => patch(index, presentationPatch(line.context, e.target.value || null))}>
              <option value="">{t("purchases.lines.baseUnit", "Unidad base")}</option>
              {line.context?.packagingLevels.filter(p => !p.isBaseUnit).map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
            </ZhSelect></span>
            <span role="cell" data-label={t("purchases.breakdown.quantityComponent", "Cantidad")}>{decimal(t("purchases.breakdown.quantityComponent", "Cantidad"), product, line.quantity, quantity => patch(index, { quantity }), "quantity", index, "quantity")}</span>
            <span role="cell" data-label={t("purchases.breakdown.assignedCost", "Costo")}>{decimal(t("purchases.breakdown.assignedCost", "Costo"), product, allocation.subtotal,
              subtotal => patch(index, { xmlAllocation: { ...allocation, subtotal } }), "money", index, "xmlAllocation.subtotal")}</span>
            <span role="cell" data-label={t("purchases.breakdown.taxes", "Impuestos")}><ZHBtn type="button" variant="ghost" size="sm" aria-expanded={expanded} aria-controls={detailId}
              aria-label={`${t("purchases.breakdown.taxesAndBases", "Impuestos y bases")} · ${product}`}
              onClick={() => setOpenTaxes(expanded ? null : line._key)}>{money(taxes.vat + taxes.ice + taxes.irbpnr)}
              <span className="material-symbols-outlined zh-icon-sm" aria-hidden="true">{expanded ? "expand_less" : "expand_more"}</span></ZHBtn></span>
            <span role="cell"><ZHRowDeleteAction showText={false} title={t("purchases.breakdown.remove", "Quitar componente")}
              label={`${t("purchases.breakdown.remove", "Quitar componente")} · ${product}`}
              onClick={() => setValue("components", components.filter((_, i) => i !== index), { shouldDirty: true })} /></span>
          </div>
          {expanded && <div id={detailId} className="pbd-taxes">
            {(xml.discount > 0 || allocation.discount > 0) && <ZHField density="compact" label={t("purchases.breakdown.discount", "Descuento")}>
              {decimal(t("purchases.breakdown.discount", "Descuento"), product, allocation.discount,
                discount => patch(index, { xmlAllocation: { ...allocation, discount } }), "money", index, "xmlAllocation.discount")}</ZHField>}
            {allocation.taxes.map((tx, taxIndex) => {
              const original = xml.taxes.find(o => o.purchaseReceptionLineTaxId === tx.purchaseReceptionLineTaxId);
              const label = taxLabel(original?.taxCode, original?.taxRateCode);
              const taxPatch = (values: Partial<typeof tx>) => patch(index, { xmlAllocation: { ...allocation,
                taxes: allocation.taxes.map((x, i) => i === taxIndex ? { ...x, ...values } : x) } });
              const base = `${label} · ${t("purchases.breakdown.base", "Base")}`;
              const amount = `${label} · ${t("purchases.breakdown.amount", "Importe")}`;
              return [
                <ZHField key={`${tx.purchaseReceptionLineTaxId}:base`} density="compact" label={base}>{decimal(base, product, tx.taxableBase, taxableBase => taxPatch({ taxableBase }), "money", index, `xmlAllocation.taxes.${taxIndex}.taxableBase`)}</ZHField>,
                <ZHField key={`${tx.purchaseReceptionLineTaxId}:tax`} density="compact" label={amount}>{decimal(amount, product, tx.taxAmount, taxAmount => taxPatch({ taxAmount }), "money", index, `xmlAllocation.taxes.${taxIndex}.taxAmount`)}</ZHField>,
              ];
            })}
          </div>}
        </div>;
      })}
    </div>}

    {showDetail && <div id="pbd-reconciliation" className="pbd-reconciliation">
      <ZHDataTable rows={balance.rows} rowKey={r => r.key} columns={[
        { key: "label", header: t("purchases.breakdown.concept", "Concepto"), render: conceptLabel },
        { key: "expected", header: t("purchases.breakdown.original", "Original"), render: r => money(r.expected) },
        { key: "assigned", header: t("purchases.breakdown.assigned", "Asignado"), render: r => money(r.assigned) },
        { key: "difference", header: t("purchases.breakdown.difference", "Diferencia"), render: r => money(r.difference) },
      ]} />
    </div>}
  </ZHModal>;
}
