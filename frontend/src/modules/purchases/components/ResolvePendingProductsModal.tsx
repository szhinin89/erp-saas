import { useEffect, useMemo, useRef, useState } from "react";
import { flushSync } from "react-dom";
import {
  FormProvider,
  useForm,
  useFormContext,
  useWatch,
} from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useItemCreationCatalogs } from "../../../components/items/ItemEditorModal/useItemCreationCatalogs";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
import type { ItemPackagingLevelDto } from "../../../types/items";
import {
  purchaseReceptionService,
  type ResolveReceptionLinesResult,
} from "../api/purchaseReceptionService";
import { PendingProductsBulkBar } from "./PendingProductsBulkBar";
import { PendingProductRow } from "./PendingProductRow";
import {
  pendingProductsFormSchema,
  type PendingProductsFormValues,
} from "../schemas/pendingProductsSchema";
import {
  buildInitialRow,
  buildResolveRequest,
  groupRowErrors,
  lacksNetSalePrice,
  pendingRowIssues,
  suggestBarcodeType,
  type PendingProductSource,
  type SalePricing,
} from "../utils/pendingProductsResolution";

type Props = {
  open: boolean;
  sources: PendingProductSource[];
  supplierName: string;
  onClose: () => void;
  /** Lote aplicado (todo o nada) — el llamador actualiza las líneas de la compra. */
  onResolved: (result: ResolveReceptionLinesResult) => void;
};

type LevelsByItem = Record<string, ItemPackagingLevelDto[]>;

/**
 * COMPRAS-METODO-ZH-01B — "Resolver productos pendientes": todas las líneas XML sin producto en una
 * sola lista de filas compactas con detalle expandible. Cada fila crea un producto, lo vincula a uno
 * existente o se declara como otra presentación (otro código del proveedor) de un producto nuevo
 * del mismo lote. Las acciones en bloque aplican valores a las filas seleccionadas; cada fila sigue
 * siendo editable. El envío es todo o nada: si el servidor rechaza alguna fila no se persiste nada
 * y los datos quedan en pantalla.
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
  const priceDecimals = usePrecisionDecimals("salesUnitPrice");
  const pricing = useMemo<SalePricing>(
    () => ({
      vatPercentByCode: new Map(catalogs.vatRateOptions.map((v) => [v.code, v.percentage])),
      priceDecimals,
    }),
    [catalogs.vatRateOptions, priceDecimals],
  );
  const noNetPriceMessage = t(
    "purchases.pendingProducts.errors.noNetPrice",
    "No se pudo calcular el precio sin IVA: revise el IVA de venta y el PVP.",
  );
  const barcodeTypeCodes = catalogs.barcodeTypeOptions.map((b) => b.code);
  const form = useForm<PendingProductsFormValues>({
    resolver: zodResolver(pendingProductsFormSchema),
    defaultValues: { rows: [] },
  });
  const [rowErrors, setRowErrors] = useState<Map<string, string[]>>(new Map());
  const [generalErrors, setGeneralErrors] = useState<string[]>([]);
  const [levelsByItem, setLevelsByItem] = useState<LevelsByItem>({});
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set());
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
    // Una sola línea (abierta desde su fila de la compra): no hay nada que compactar.
    setExpanded(new Set(sources.length === 1 ? [sources[0].rowId] : []));
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

  const expandRows = (rowIds: string[]) =>
    // Síncrono: RHF enfoca el primer campo con error justo después, y debe estar visible.
    flushSync(() => setExpanded((prev) => new Set([...prev, ...rowIds])));
  const setRowExpanded = (rowId: string, value: boolean) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (value) next.add(rowId);
      else next.delete(rowId);
      return next;
    });

  const submit = form.handleSubmit(async (values) => {
    setRowErrors(new Map());
    setGeneralErrors([]);
    // El precio sin IVA que se persiste debe quedar > 0 con la precisión de la empresa.
    const unpriced = values.rows.filter((row) => lacksNetSalePrice(row, pricing));
    if (unpriced.length > 0) {
      expandRows(unpriced.map((row) => row.rowId));
      values.rows.forEach((row, index) => {
        if (!unpriced.includes(row)) return;
        form.setError(`rows.${index}.salePriceGross`, { message: noNetPriceMessage }, { shouldFocus: row === unpriced[0] });
      });
      return;
    }
    try {
      const result = await purchaseReceptionService.resolveLines(
        buildResolveRequest(values.rows, pricing),
      );
      if (!result.applied) {
        const grouped = groupRowErrors(result.errors);
        setRowErrors(grouped.byRow);
        setGeneralErrors(grouped.general);
        expandRows([...grouped.byRow.keys()]);
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
  }, (errors) => {
    // Ningún campo con error queda oculto: se expanden sus filas antes de enfocar el primero.
    const rowsWithFieldErrors = Object.keys(errors.rows ?? {}).map((i) => form.getValues(`rows.${Number(i)}.rowId`));
    expandRows(rowsWithFieldErrors.filter(Boolean));
  });

  const rows = useWatch({ control: form.control, name: "rows" }) ?? [];
  const selectedCount = rows.filter((r) => r.selected).length;
  const rowsWithErrors = rows.filter((r) => rowErrors.has(r.rowId)).length;
  const issuesByRow = pendingRowIssues(rows, pricing, noNetPriceMessage);
  // Pendiente = fila seleccionada que no pasa las validaciones del envío o que el servidor rechazó.
  const pendingCount = rows.filter((r) => r.selected && (issuesByRow.has(r.rowId) || rowErrors.has(r.rowId))).length;
  const allExpanded = rows.length > 0 && rows.every((r) => expanded.has(r.rowId));
  const counter = (label: string) => <span className="pbd-summary__item">{label}</span>;

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
          <span className="pbd-summary pbd-footer-status" aria-live="polite">
            {counter(t("purchases.pendingProducts.footer.selected", { count: selectedCount }))}
            {counter(t("purchases.pendingProducts.footer.ready", { count: selectedCount - pendingCount }))}
            {counter(t("purchases.pendingProducts.footer.pending", { count: pendingCount }))}
          </span>
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
        {/* One summary per rejection: row reasons stay on their rows; only reasons without a row go here. */}
        {(rowsWithErrors > 0 || generalErrors.length > 0) && (
          <ZHPageNotice
            variant={rowsWithErrors > 0 ? "warning" : "error"}
            message={rowsWithErrors > 0
              ? t("purchases.pendingProducts.rowsRejected", { count: rowsWithErrors })
              : generalErrors.join("\n")}
            detail={rowsWithErrors > 0 ? generalErrors.join("\n") : null}
          />
        )}
        <PendingProductsBulkBar catalogs={catalogs} sourcesById={sourcesById} pricing={pricing} />
        <div className="prl-rows" role="table" aria-label={t("purchases.pendingProducts.title", "Resolver productos pendientes")}>
          <div className="prl-row prl-row--head" role="row">
            <span role="columnheader" className="prl-row__check"><SelectAllCheckbox /></span>
            <span role="columnheader">{t("purchases.pendingProducts.col.xml", "Línea XML del proveedor")}</span>
            <span role="columnheader">{t("purchases.pendingProducts.col.action", "Acción")}</span>
            <span role="columnheader">{t("purchases.pendingProducts.col.product", "Producto ERP")}</span>
            <span role="columnheader" className="prl-row__toggle">
              <ZHBtn type="button" variant="ghost" size="sm"
                onClick={() => setExpanded(new Set(allExpanded ? [] : rows.map((r) => r.rowId)))}>
                {allExpanded
                  ? t("purchases.pendingProducts.collapseAll", "Contraer todo")
                  : t("purchases.pendingProducts.expandAll", "Expandir todo")}
              </ZHBtn>
            </span>
          </div>
          {rows.map((row, index) => (
            <PendingProductRow
              key={row.rowId}
              index={index}
              source={sourcesById.get(row.rowId)}
              sourcesById={sourcesById}
              catalogs={catalogs}
              pricing={pricing}
              levels={levelsByItem[row.linkItemId] ?? []}
              serverErrors={rowErrors.get(row.rowId) ?? []}
              issues={issuesByRow.get(row.rowId) ?? []}
              expanded={expanded.has(row.rowId)}
              onExpandedChange={(value) => setRowExpanded(row.rowId, value)}
              onItemLinked={(itemId) => void loadLevels(itemId, index)}
            />
          ))}
        </div>
      </FormProvider>
    </ZHModal>
  );
}

function SelectAllCheckbox() {
  const { t } = useI18n();
  const { control, setValue, getValues } = useFormContext<PendingProductsFormValues>();
  const rows = useWatch({ control, name: "rows" }) ?? [];
  const all = rows.length > 0 && rows.every((r) => r.selected);
  return (
    <input
      type="checkbox"
      aria-label={t("purchases.pendingProducts.selectAll", "Seleccionar todas")}
      checked={all}
      onChange={() =>
        setValue("rows", getValues("rows").map((row) => ({ ...row, selected: !all })), { shouldDirty: true })
      }
    />
  );
}
