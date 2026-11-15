import { buildPurchaseLineIssues } from "../utils/purchaseLineIssues";
import { PurchaseFieldError } from "../components/PurchaseFieldError";
import { purchaseFieldErrors } from "../utils/purchaseFieldErrors";
import { canBreakDown } from "../utils/purchaseXmlBreakdown";
import { useState } from "react";
import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHLineAction } from "../../../components/zh/ZHLineAction";
import { ZHRowDeleteAction } from "../../../components/zh/ZHRowDeleteAction";
import { ZHLineCard } from "../../../components/zh/ZHLineCard";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHInputGroup } from "../../../components/zh/ZHInputGroup";
import { PurchaseLineIssuesTag } from "../components/PurchaseLineIssuesTag";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { usePrecisionPolicy } from "../../../hooks/usePrecisionPolicy";
import { resolvePrecisionDecimals } from "../../../lib/config/precisionPolicy.config";
import { useI18n } from "../../../i18n/i18n";
import { useOpenItemDetail } from "../../items/facades/itemDetailNavigationFacade";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";

type PurchaseLineDraft = PurchaseLineFormValues;
import { buildPurchaseLinePresentation, applyRegisteredQuantity, applyRegisteredUnitCost, headerInputKey, isUnchangedAtPrecision } from "../utils/purchaseLinePresentation";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";
import { MARGIN_BADGE_VARIANT, marginBadgeLabel, readinessActionLabel } from "../utils/purchaseLineCardLabels";
import { buildPurchaseLineHandlers } from "../utils/purchaseLineHandlers";
import { ProductLineActionMenu } from "./PurchaseLineActions";
import { PurchaseLineDetailBlocks } from "./PurchaseLineDetailBlocks";


/** Expansión efímera del detalle por clave de línea (solo presentación, no muta el formulario). */
export function usePurchaseLinesExpansion(lines: PurchaseLineDraft[]) {
  const [expanded, setExpanded] = useState<Set<number>>(() => new Set());
  const allExpanded = lines.length > 0 && lines.every(line => expanded.has(line._key));
  return {
    expanded,
    allExpanded,
    hasLines: lines.length > 0,
    toggleAll: () => setExpanded(new Set(allExpanded ? [] : lines.map(line => line._key))),
    setOne: (key: number, value: boolean) => setExpanded(current => {
      const next = new Set(current);
      if (value) next.add(key); else next.delete(key);
      return next;
    }),
  };
}
export type PurchaseLinesExpansion = ReturnType<typeof usePurchaseLinesExpansion>;

export function PurchaseExpandAllButton({ expansion }: { expansion: PurchaseLinesExpansion }) {
  const { t } = useI18n();
  if (!expansion.hasLines) return null;
  return (
    <ZHBtn type="button" variant="ghost" size="sm" className="pdl-toolbar__expand" onClick={expansion.toggleAll}>
      {expansion.allExpanded
        ? t("purchases.pendingProducts.collapseAll", "Contraer todo")
        : t("purchases.pendingProducts.expandAll", "Expandir todo")}
    </ZHBtn>
  );
}

/** Composición del detalle existente; la expansión la controla quien lo monta (barra de herramientas). */
export function PurchaseDetailLines({ ctx, expansion }: { ctx: ReturnType<typeof usePurchasesPage>; expansion: PurchaseLinesExpansion }) {
  return (
    <>
      {ctx.lines.map((line, idx) => (
        <PurchaseLineCard key={line._key} line={line} idx={idx} ctx={ctx}
          expanded={expansion.expanded.has(line._key)}
          onExpandedChange={value => expansion.setOne(line._key, value)} />
      ))}
    </>
  );
}

export function PurchaseLineCard({
  line: l,
  idx,
  ctx,
  expanded,
  onExpandedChange,
}: {
  line: PurchaseLineDraft;
  idx: number;
  ctx: ReturnType<typeof usePurchasesPage>;
  expanded?: boolean;
  onExpandedChange?: (value: boolean) => void;
}) {
  const { t } = useI18n();
  const lineLocked = ctx.fieldDisabled || !!l.xmlAllocation;
  // Origen o destino de una distribución de costos: no se elimina ni se desglosa (todo su grupo XML).
  const inCostDistribution = (line: PurchaseLineDraft) => !!line.lineId && ctx.costDistributionLineIds.has(line.lineId);
  const distributionLocked = inCostDistribution(l);
  const breakdownLocked = ctx.lines.some(x => x.purchaseReceptionLineId === l.purchaseReceptionLineId && inCostDistribution(x));
  const distributionLockReason = t("purchases.distributeCost.lineLocked",
    "La línea participa en una distribución de costos. Edite o retire la distribución antes de eliminarla o desglosarla.");
  const precisionPolicy = usePrecisionPolicy(); // presentación de línea (06)
  const viewMatchedItem = useOpenItemDetail();
  // Solo presentación: no modifica la línea ni el formulario al ocultar el detalle.
  const [locallyExpanded, setLocallyExpanded] = useState(false);
  const collapsed = !(expanded ?? locallyExpanded);
  const setCollapsed = (value: boolean) => {
    if (onExpandedChange) onExpandedChange(!value);
    else setLocallyExpanded(!value);
  };
  const fieldIssues = purchaseFieldErrors(ctx.errors?.lines?.[idx]);
  const errorId = (field: string) => `purchase-line-${l._key}-${field}-error`;
  const fieldMessage = (fields: string[]) => [...new Set(fieldIssues
    .filter(issue => fields.some(field => issue.path === field || issue.path.startsWith(`${field}.`)))
    .map(issue => issue.message))].join(" ") || undefined;
  const identityFields = ["description", "itemId"];
  const quantityFields = ["quantity", "quantityInBaseUom"];
  const costFields = ["unitPrice", "baseUnitPrice"];
  const taxFields = ["vatCode", "iceCode", "taxes"];
  const headerFields = [...identityFields, ...quantityFields, ...costFields, ...taxFields, "warehouseId"];
  const detailErrors = fieldIssues.filter(issue => !headerFields.some(field => issue.path === field || issue.path.startsWith(`${field}.`)));

  // PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01 — Base/IVA/ICE/Total de la cabecera
  // editable viven en `vm.editableHeader`, calculados en cada render: la base sigue la cantidad/
  // costo/descuento operativos actuales; en una línea con recepción XML, IVA/ICE/IRBPNR conservan
  // los importes fiscales del XML aunque cambien cantidad, costo o presentación (purchases.md § 2,
  // `resolveLineTaxes`), y la confirmación los concilia. Las líneas manuales calculan por tarifa.
  // El snapshot `vm.xml.*` se muestra aparte en "Producto recibido (XML)" y no cambia.
  const vm = buildPurchaseLinePresentation(l, precisionPolicy, t, ctx.vatRatesMap, ctx.iceRatesMap);
  const quantityDecimals = resolvePrecisionDecimals(precisionPolicy, "quantity");
  const purchasePriceDecimals = resolvePrecisionDecimals(precisionPolicy, "purchaseUnitPrice");
  const sub = vm.editableHeader.taxableBase;
  const vatAmt = vm.editableHeader.vatAmount;
  const iceAmt = vm.editableHeader.iceAmount;
  const irbpnrAmt = vm.editableHeader.irbpnrAmount;
  const total = vm.editableHeader.total;
  const ctxData = l.context;
  const vatPct = ctxData?.vatPercent ?? ctx.vatRatesMap[l.vatCode] ?? 0;
  const readiness = ctx.lineReadinessByKey[l._key];
  const readinessFields: Record<string, string[]> = {
    MISSING_ITEM: ["itemId", "description"], MISSING_PRESENTATION: ["packagingLevelId"],
    MISSING_WAREHOUSE: ["warehouseId"], INVALID_TAX: taxFields,
  };
  const readinessField = readinessFields[readiness?.status ?? ""];
  const readinessHasFieldError = readinessField && fieldMessage(readinessField);
  const primaryReadinessAction = readinessActionLabel(readiness?.primaryAction, t);
  const secondaryReadinessAction = readinessActionLabel(
    readiness?.secondaryAction,
    t,
  );
  const readinessActions = [primaryReadinessAction, secondaryReadinessAction]
    .filter(Boolean)
    .join(" / ");
  const showReadinessActions =
    readiness?.status !== "MISSING_ITEM" &&
    readiness?.status !== "MISSING_PRESENTATION" &&
    readinessActions;
  const packagingLevels = ctxData?.packagingLevels ?? [];
  const { handlePackagingChange, handleResetToSupplierOrigin, handleProductSelect, handleUnlinkProduct } =
    buildPurchaseLineHandlers({ line: l, ctx, vm, packagingLevels, baseUomCode: ctxData?.baseUomCode });
  const productName =
    ctxData?.shortName || l.description?.split(" — ")[1] || l.description;
  const detailId = `pdl-detail-${l._key}`;
  // ZH-DS-LINE-ISSUES-01 — incidencias de la línea, cada una una sola vez, para la
  // etiqueta anclada al borde de la tarjeta. Sin incidencias no hay etiqueta (sin badges de éxito).
  const lineIssues = buildPurchaseLineIssues({ vm, readiness, t, hasFieldErrors: fieldIssues.length > 0,
    readinessHasFieldError, showReadinessActions, lineLocked, onReset: handleResetToSupplierOrigin,
    onGoToField: () => {
      setCollapsed(false);
      requestAnimationFrame(() => {
        const line = document.getElementById(`purchase-line-${l._key}-fields`);
        (line?.querySelector<HTMLElement>('[aria-invalid="true"]:not(:disabled)') ?? line)?.focus();
      });
    },
  });
  // Franja operativa (no son estados de error): solo si hay equivalencia o nota de costo.
  const hasOperationalInfo =
    vm.registration.show && (!!vm.registration.presentationDetail || !!vm.registration.costNote);

  return (
    // DS-LINE-CARD-UNIFY-02: wrapper externo unificado (ZHLineCard) en vez de la caja local
    // `.zh-detail-line pdl-line-row` — mismo rail izquierdo aprobado (número, menú de producto,
    // duplicar, eliminar, colapso — plantilla DESIGN-SYSTEM-LINE-RAIL-04), pasado como prop
    // `rail`; el contenido de la línea (`.pdl-line`, sin cambios internos) sigue como children.
    <ZHLineCard
      className={`pdl-line-card${lineIssues.length > 0 ? " pdl-line-card--has-issues" : ""}`}
      rail={
        <>
          <span className="pdl-line-row__num">
            {String(idx + 1).padStart(2, "0")}
          </span>
          {canBreakDown(l) && (
            <ZHLineAction icon="call_split" variant="default" size="md" compact showText={false}
              title={breakdownLocked ? distributionLockReason : t(l.xmlAllocation ? "purchases.breakdown.edit" : "purchases.breakdown.action", l.xmlAllocation ? "Editar desglose" : "Desglosar")}
              label={t(l.xmlAllocation ? "purchases.breakdown.edit" : "purchases.breakdown.action", l.xmlAllocation ? "Editar desglose" : "Desglosar")}
              ariaLabel={t(l.xmlAllocation ? "purchases.breakdown.edit" : "purchases.breakdown.action", l.xmlAllocation ? "Editar desglose" : "Desglosar")}
              disabled={ctx.fieldDisabled || breakdownLocked} onClick={() => ctx.setBreakdownSourceId(l.purchaseReceptionLineId!)} />
          )}
          <ProductLineActionMenu
            line={l}
            ctx={ctx}
            disabled={lineLocked}
            onProductSelect={handleProductSelect}
            onViewProduct={() => viewMatchedItem(l.itemId as string)}
            onUnlinkProduct={handleUnlinkProduct}
          />
          <ZHLineAction
            icon="content_copy"
            variant="default"
            size="md"
            compact
            showText={false}
            title={t("purchases.lines.duplicate", "Duplicar")}
            label={t("purchases.lines.duplicate", "Duplicar")}
            disabled={lineLocked}
            onClick={() => ctx.duplicateLine(l._key)}
          />
          <ZHRowDeleteAction
            compact
            showText={false}
            title={distributionLocked ? distributionLockReason : t("purchases.lines.delete", "Eliminar")}
            label={t("purchases.lines.delete", "Eliminar")}
            ariaLabel={t("purchases.lines.delete", "Eliminar")}
            disabled={lineLocked || distributionLocked}
            onClick={() => ctx.removeLine(l._key)}
          />
        </>
      }
    >
      <div className="pdl-line" id={`purchase-line-${l._key}`} tabIndex={-1}>
      {lineIssues.length > 0 && <PurchaseLineIssuesTag issues={lineIssues} />}
      <div className="pdl-line__main" id={`purchase-line-${l._key}-fields`} tabIndex={-1}>
        <div className={`pdl-line__summary${l.itemId ? "" : " pdl-line__summary--no-warehouse"}`}>
          <div className="pdl-line__identity" role="group" aria-label={t("purchases.lines.product", "Producto")}
            aria-describedby={fieldMessage(identityFields) ? errorId("identity") : undefined}>
            {!l.itemId ? (
              <div className="pdl-line__product-name pdl-line__product-name--muted">
                {t(
                  "purchases.lines.noLinkedProduct",
                  "Sin producto vinculado",
                )}
              </div>
            ) : (
              <>
                <div className="pdl-line__product-name zh-row-title">
                  {productName}
                </div>
                {/* PURCHASE-LINE-HEADER-UX-01 — mismos datos/orden que antes (SKU · Cód.
                    proveedor), en pares label/valor: ZHFieldLabel size="sm" (global) para el
                    label + ZHDataValue variant="code" (global, mono documentado) para el
                    código — PURCHASES-DS-GLOBAL-COMPONENT-04. */}
                <div className="pdl-line__product-meta">
                  <span className="pdl-line__product-meta-item">
                    <ZHFieldLabel size="sm">SKU</ZHFieldLabel>
                    <ZHDataValue variant="code">{vm.item.sku}</ZHDataValue>
                  </span>
                  <ZHDataValue variant="muted">·</ZHDataValue>
                  <span className="pdl-line__product-meta-item">
                    <ZHFieldLabel size="sm">
                      {t("purchases.lines.supplierCode", "Cód. proveedor")}
                    </ZHFieldLabel>
                    <ZHDataValue variant="code">{vm.xml.supplierCode}</ZHDataValue>
                  </span>
                </div>
              </>
            )}
            <PurchaseFieldError id={errorId("identity")} message={fieldMessage(identityFields)} />
            {/* PURCHASE-LINE-COMPACT-02 — disclosure "Detalle" (ZH-DS-DISCLOSURE-01) dentro de la
                cabecera; el estado de la línea vive en la etiqueta anclada (ISSUES-TAG-01). */}
            <div className="pdl-line__identity-actions">
              <ZHBtn
                type="button"
                variant="secondary"
                size="sm"
                expanded={!collapsed}
                aria-controls={detailId}
                onClick={() => setCollapsed(!collapsed)}
              >
                {t("purchases.lines.detailToggle", "Detalle")}
              </ZHBtn>
            </div>
          </div>
          {/* PURCHASE-LINE-HEADER-UX-02 — bodega como columna operativa propia (criterio
              "Ubicación" de Ventas), en vez de un campo suelto debajo del producto. Mismo
              componente/handler/condición (l.itemId) que antes: solo cambió dónde se renderiza. */}
          {l.itemId && <div className="pdl-line__warehouse">
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.warehouse", "Bodega")}
                </ZHFieldLabel>
                <ZhSelect
                  className="pdl-line__wh-select-compact"
                  aria-label={t("purchases.lines.warehouse", "Bodega")}
                  aria-invalid={!!fieldMessage(["warehouseId"])} aria-describedby={fieldMessage(["warehouseId"]) ? errorId("warehouse") : undefined}
                  density="compact"
                  value={l.warehouseId ?? ctx.formWatch.globalWarehouseId ?? ""}
                  onChange={(e) =>
                    ctx.updateLineWarehouse(l._key, e.target.value || null)
                  }
                  disabled={ctx.fieldDisabled}
                >
                  <option value="">
                    {t("purchases.logistics.selectWarehouse", "— Seleccionar bodega —")}
                  </option>
                  {ctx.warehouses.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.code ? `${w.code} — ${w.name}` : w.name}
                    </option>
                  ))}
                </ZhSelect>
            <PurchaseFieldError id={errorId("warehouse")} message={fieldMessage(["warehouseId"])} />
          </div>}
          {/* PURCHASE-LINE-HEADER-UX-03 — cada métrica pasa a ser columna directa de
              .pdl-line__summary (antes agrupadas en .pdl-line__metrics) para que la cabecera sea
              un grid real de celdas (mismo criterio que .sf-product en Ventas: columnas con
              minmax() y separadores, no un bloque flex anidado) — mismos datos/orden/handlers. */}
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.quantity", "Cantidad")}
              </ZHFieldLabel>
              <ZhDecimalInput
                key={headerInputKey("qty", vm, l.packagingLevelId)}
                className="pdl-input pdl-input--qty"
                aria-label={t("purchases.lines.quantityToRegister", "Cantidad a registrar")}
                aria-invalid={!!fieldMessage(quantityFields)} aria-describedby={fieldMessage(quantityFields) ? errorId("quantity") : undefined}
                density="compact"
                precision="quantity"
                positiveOnly
                defaultValue={vm.inventory.baseQuantityValue}
                onBlur={(e) => {
                  // FASE 5 PURCHASE-UX — cantidad a registrar (unidad base), autoritativa: se
                  // guarda tal cual en quantityInBaseUom; la cantidad del documento se deriva.
                  // Blur sin edición real no toca la línea (evita round-trip por la escala visible).
                  const entered = Number(e.target.value) || 0;
                  if (isUnchangedAtPrecision(entered, vm.inventory.baseQuantityValue, quantityDecimals))
                    return;
                  const patch = applyRegisteredQuantity(l, vm.inventory.conversionFactorValue, entered);
                  if (!patch) return;
                  ctx.updateLine(l._key, "quantity", patch.quantity);
                  ctx.updateLine(l._key, "quantityInBaseUom", patch.quantityInBaseUom);
                  ctx.updateLine(l._key, "baseUnitPrice", patch.baseUnitPrice);
                }}
                disabled={lineLocked}
              />
              <PurchaseFieldError id={errorId("quantity")} message={fieldMessage(quantityFields)} />
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.unitCostShort", "Costo unitario")}
              </ZHFieldLabel>
              <ZHInputGroup className="pdl-line__metric-value" prefix="$">
                <ZhDecimalInput
                  key={headerInputKey("cost", vm, l.packagingLevelId)}
                  className="pdl-input pdl-input--cost"
                  aria-label={t("purchases.lines.unitCostToRegister", "Costo unitario a registrar")}
                  aria-invalid={!!fieldMessage(costFields)} aria-describedby={fieldMessage(costFields) ? errorId("cost") : undefined}
                  density="compact"
                  // Escala del precio de compra: es la que el backend persiste para el precio base.
                  precision="purchaseUnitPrice"
                  positiveOnly
                  defaultValue={vm.inventory.baseUnitCostValue}
                  onBlur={(e) => {
                    // FASE 5 PURCHASE-UX — costo unitario a registrar (neto de descuento, con
                    // flete/gastos asignados), autoritativo: se despeja el precio base y el precio
                    // por presentación se deriva. Blur sin edición real no toca la línea.
                    const entered = Number(e.target.value) || 0;
                    if (isUnchangedAtPrecision(entered, vm.inventory.baseUnitCostValue, purchasePriceDecimals))
                      return;
                    const patch = applyRegisteredUnitCost(l, vm.inventory.conversionFactorValue, entered);
                    if (!patch) return;
                    ctx.updateLine(l._key, "unitPrice", patch.unitPrice);
                    ctx.updateLine(l._key, "baseUnitPrice", patch.baseUnitPrice);
                  }}
                  disabled={lineLocked}
                />
              </ZHInputGroup>
              <PurchaseFieldError id={errorId("cost")} message={fieldMessage(costFields)} />
            </div>
            {/* Subtotal de producto (cantidad × costo − descuento, sin impuestos): no es la base
                imponible tributaria — esa vive en el XML como "Base imp. IVA" (puede incluir ICE). */}
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.summary.subtotal", "Subtotal")}
              </ZHFieldLabel>
              <div className="pdl-line__metric-value">
                <ZHMoneyValue value={sub} precision="money" />
              </div>
            </div>
            <div className="pdl-line__metric">
              <div className="pdl-line__tax">
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.vatShort", "IVA")} {vatPct}%
                </ZHFieldLabel>
                <div className="pdl-line__tax-amount" role="group" aria-label={t("purchases.lines.taxes", "Impuestos")}
                  aria-describedby={fieldMessage(taxFields) ? errorId("tax") : undefined}>
                  <ZHMoneyValue
                    value={vatAmt}
                    precision="tax"
                  />
                </div>
                <PurchaseFieldError id={errorId("tax")} message={fieldMessage(taxFields)} />
                {/* ICE/IRBPNR solo cuando aplican a la línea: un $0.00 fijo no aporta dato fiscal. */}
                {(iceAmt > 0 || !!l.iceCode) && (
                  <div>
                    <ZHDataValue variant="muted">
                      {t("purchases.lines.iceShort", "ICE")}:{" "}
                    </ZHDataValue>
                    <ZHMoneyValue
                      value={iceAmt}
                      precision="tax"
                      emphasis="muted"
                    />
                  </div>
                )}
                {irbpnrAmt > 0 && (
                  <div>
                    <ZHDataValue variant="muted">
                      IRBPNR:{" "}
                    </ZHDataValue>
                    <ZHMoneyValue
                      value={irbpnrAmt}
                      precision="tax"
                      emphasis="muted"
                    />
                  </div>
                )}
              </div>
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.lineTotal", "Total línea")}
              </ZHFieldLabel>
              <div className="pdl-line__total">
                <ZHMoneyValue
                  value={total}
                  precision="money"
                  emphasis="grand"
                />
              </div>
            </div>
            <div className="pdl-line__metric">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.marginShort", "Margen")}
              </ZHFieldLabel>
              <Badge
                variant={MARGIN_BADGE_VARIANT[vm.commercial.profitability.marginState]}
                size="md"
                label={marginBadgeLabel(vm.commercial.profitability, t)}
              />
            </div>
        </div>
      </div>

      {/* Franja operativa (no son estados de error), solo si existe: equivalencia de
          presentación y nota de costo. Las incidencias viven en la etiqueta anclada
          (ZH-DS-LINE-ISSUES-01). Cantidad/costo a registrar viven solo en la cabecera. */}
      {hasOperationalInfo && (
        <div className="pdl-line__status">
          {vm.registration.presentationDetail && (
            <ZHDataValue variant="muted">{vm.registration.presentationDetail}</ZHDataValue>
          )}
          {vm.registration.costNote && (
            <ZHDataValue variant="muted">{vm.registration.costNote}</ZHDataValue>
          )}
        </div>
      )}

      {/* Detalle expandible (botón "Detalle", ZH-DS-DISCLOSURE-01): 1. XML recibido — snapshot
          documental, única comparación explícita XML vs registro; en compra manual solo la nota.
          2. Presentación e inventario. 3. Información comercial. Cabecera, línea secundaria y
          avisos quedan siempre visibles, también colapsado. */}
      {!collapsed && (
      <PurchaseLineDetailBlocks ctx={ctx} line={l} vm={vm} readiness={readiness} detailId={detailId}
        detailErrors={detailErrors} packagingLevels={packagingLevels} lineLocked={lineLocked} errorId={errorId}
        fieldMessage={fieldMessage} onPackagingChange={handlePackagingChange} />
      )}
      </div>
    </ZHLineCard>
  );
}
