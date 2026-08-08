import { useEffect, useRef } from "react";
import { useSearchParams } from "react-router-dom";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import {
  Badge,
  NoAccessPage,
  type BadgeVariant,
} from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHTabBar } from "../../../../components/zh/ZHTabBar";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { formatDate } from "../../../../lib/formatters/dateFormatters";
import { formatMoneyWithSymbol, formatMoney } from "../../../../lib/sanitizers";
import { getDecimalConfig } from "../../../../lib/config/decimal.config";
import { useInventoryInvestigationPage } from "../hooks/useInventoryInvestigationPage";
import type { InitialDocument } from "../hooks/useInventoryInvestigationPage";
import { KardexMovementDetailModal } from "../components/KardexMovementDetailModal";
import "../../../../styles/shared/items-catalog.css";
import "../../../../styles/shared/erp-form-core.css";

const MOVEMENT_TYPE_LABELS: Record<string, string> = {
  PurchaseEntry: "Entrada por Compra",
  SaleExit: "Salida por Venta",
  PositiveAdjust: "Ajuste Positivo",
  NegativeAdjust: "Ajuste Negativo",
  TransferEntry: "Entrada por Transferencia",
  TransferExit: "Salida por Transferencia",
  PurchaseReturn: "Devolución a Proveedor",
  SaleReturn: "Devolución de Cliente",
  SupplierCreditNote: "Nota de Crédito Proveedor",
  SupplierDebitNote: "Nota de Débito Proveedor",
};

function movementBadgeVariant(typeName: string): BadgeVariant {
  if (
    typeName.includes("Entry") ||
    typeName === "PositiveAdjust" ||
    typeName === "SaleReturn"
  )
    return "success";
  if (
    typeName.includes("Exit") ||
    typeName === "NegativeAdjust" ||
    typeName === "PurchaseReturn"
  )
    return "error";
  return "info";
}

export function KardexPage() {
  const { canShow } = usePermissionsUi();
  const canView = canShow("inventory.stock.view");
  const [searchParams, setSearchParams] = useSearchParams();
  const initialProductId = searchParams.get("productId") ?? undefined;
  const docId = searchParams.get("docId");
  const docTypeParam = searchParams.get("docType");
  const initialDocument: InitialDocument | undefined =
    docId &&
    (docTypeParam === "PurchaseInvoice" || docTypeParam === "SalesInvoice")
      ? { id: docId, docType: docTypeParam }
      : undefined;
  const initialFiltersRef = useRef({
    warehouseId: searchParams.get("warehouseId") ?? undefined,
    dateFrom: searchParams.get("dateFrom") ?? undefined,
    dateTo: searchParams.get("dateTo") ?? undefined,
    movementTypeFilter: searchParams.get("movementType") ?? undefined,
  });

  const ctx = useInventoryInvestigationPage(
    initialProductId,
    initialDocument,
    initialFiltersRef.current,
  );

  useEffect(() => {
    void ctx.loadWarehouses();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (initialProductId) void ctx.runSearch();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialProductId]);
  useEffect(() => {
    if (ctx.selectedDoc) void ctx.runSearch();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ctx.selectedDoc?.id]);

  // Mantiene la URL sincronizada con los filtros activos para que "volver" desde el
  // documento origen (o el expediente) restaure exactamente el mismo reporte filtrado.
  useEffect(() => {
    if (ctx.searchMode !== "product" || !ctx.selectedProductId) return;
    const next = new URLSearchParams();
    next.set("productId", ctx.selectedProductId);
    if (ctx.selectedWarehouseId)
      next.set("warehouseId", ctx.selectedWarehouseId);
    if (ctx.dateFrom) next.set("dateFrom", ctx.dateFrom);
    if (ctx.dateTo) next.set("dateTo", ctx.dateTo);
    if (ctx.movementTypeFilter)
      next.set("movementType", ctx.movementTypeFilter);
    setSearchParams(next, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    ctx.searchMode,
    ctx.selectedProductId,
    ctx.selectedWarehouseId,
    ctx.dateFrom,
    ctx.dateTo,
    ctx.movementTypeFilter,
  ]);

  if (!canView)
    return <NoAccessPage title="Centro de Investigación de Inventario" />;

  const qty = getDecimalConfig().quantity;
  const cost = getDecimalConfig().purchaseUnitPrice;
  const total = getDecimalConfig().totalAmount;

  return (
    <ErpPageTemplate
      title="Centro de Investigación de Inventario"
      subtitle="Trazabilidad completa de movimientos de inventario — Compras, Ventas y ajustes en un solo lugar."
    >
      <ZHTabBar
        tabs={[{ id: "kardex", label: "Kardex", icon: "history" }]}
        activeTab={ctx.tab}
        onChange={() => {
          /* única pestaña activa hoy */
        }}
        ariaLabel="Secciones del centro de investigación"
      />

      <div className="prd-section">
        {/* ── Búsqueda rápida: modo explícito ─────────────────────────── */}
        <div className="pf-mini-card kdx-filter-card">
          <div className="kdx-tabs-actions">
            <button
              type="button"
              className={`prd-tab-btn ${ctx.searchMode === "product" ? "prd-tab-btn--active" : ""}`}
              onClick={() => ctx.setSearchMode("product")}
            >
              <span
                className="material-symbols-outlined zh-icon-md"
              >
                inventory_2
              </span>
              Buscar por Producto
            </button>
            <button
              type="button"
              className={`prd-tab-btn ${ctx.searchMode === "document" ? "prd-tab-btn--active" : ""}`}
              onClick={() => ctx.setSearchMode("document")}
            >
              <span
                className="material-symbols-outlined zh-icon-md"
              >
                description
              </span>
              Buscar por Documento
            </button>
          </div>

          {ctx.searchMode === "product" ? (
            <div
              className="kdx-filter-row"
            >
              <ZHField
                density="compact"
                label="Producto"
                className="kdx-field-product"
              >
                <input
                  type="text"
                  placeholder="Buscar por SKU o nombre..."
                  value={
                    ctx.selectedProduct
                      ? `${ctx.selectedProduct.sku} — ${ctx.selectedProduct.shortName}`
                      : ctx.productQuery
                  }
                  onChange={(e) => {
                    ctx.clearSelectedProduct();
                    void ctx.searchProducts(e.target.value);
                  }}
                />
                {ctx.productResults.length > 0 && (
                  <div className="pf-picker-dropdown">
                    {ctx.productResults.map((item) => (
                      <button
                        key={item.id}
                        type="button"
                        className="pf-picker-item"
                        onClick={() => ctx.selectProduct(item)}
                      >
                        <div className="pf-picker-item__main">
                          <div className="pf-picker-item__name">
                            <span className="pf-picker-item__sku">
                              {item.sku}
                            </span>
                            {item.shortName}
                          </div>
                        </div>
                      </button>
                    ))}
                  </div>
                )}
              </ZHField>
              <ZHField
                density="compact"
                label="Bodega (opcional)"
                className="kdx-field-warehouse"
              >
                <select
                  value={ctx.selectedWarehouseId}
                  onChange={(e) => ctx.setSelectedWarehouseId(e.target.value)}
                >
                  <option value="">— Todas las bodegas —</option>
                  {ctx.warehouses.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.code ? `${w.code} — ${w.name}` : w.name}
                    </option>
                  ))}
                </select>
              </ZHField>
              <ZHField density="compact" label="Desde" className="kdx-field-date">
                <input
                  type="date"
                  value={ctx.dateFrom}
                  onChange={(e) => ctx.setDateFrom(e.target.value)}
                />
              </ZHField>
              <ZHField density="compact" label="Hasta" className="kdx-field-date">
                <input
                  type="date"
                  value={ctx.dateTo}
                  onChange={(e) => ctx.setDateTo(e.target.value)}
                />
              </ZHField>
              <ZHBtn
                type="button"
                variant="primary"
                onClick={() => void ctx.runSearch()}
                disabled={!ctx.selectedProductId}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  search
                </span>
                Buscar
              </ZHBtn>
            </div>
          ) : (
            <div
              className="kdx-filter-row"
            >
              <ZHField
                density="compact"
                label="Tipo de documento"
                className="kdx-field-type"
              >
                <select
                  value={ctx.docSubType}
                  onChange={(e) =>
                    ctx.setDocSubType(e.target.value as typeof ctx.docSubType)
                  }
                >
                  <option value="PurchaseInvoice">Factura de Compra</option>
                  <option value="SalesInvoice">Factura de Venta</option>
                </select>
              </ZHField>
              <ZHField
                density="compact"
                label="Número de documento"
                className="kdx-field-document"
              >
                <input
                  type="text"
                  placeholder="Ej. 001-001-000000123"
                  value={
                    ctx.selectedDoc ? ctx.selectedDoc.label : ctx.docNumberQuery
                  }
                  onChange={(e) => {
                    ctx.clearSelectedDocument();
                    ctx.setDocNumberQuery(e.target.value);
                  }}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") void ctx.searchDocuments();
                  }}
                />
                {ctx.docMatches.length > 0 && (
                  <div className="pf-picker-dropdown">
                    {ctx.docMatches.map((d) => (
                      <button
                        key={d.id}
                        type="button"
                        className="pf-picker-item"
                        onClick={() => ctx.selectDocument(d)}
                      >
                        <div className="pf-picker-item__main">
                          <div className="pf-picker-item__name">{d.label}</div>
                        </div>
                      </button>
                    ))}
                  </div>
                )}
              </ZHField>
              <ZHBtn
                type="button"
                variant="secondary"
                onClick={() => void ctx.searchDocuments()}
                disabled={ctx.docSearching || !ctx.docNumberQuery.trim()}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  manage_search
                </span>
                {ctx.docSearching ? "Buscando..." : "Resolver documento"}
              </ZHBtn>
              <ZHBtn
                type="button"
                variant="primary"
                onClick={() => void ctx.runSearch()}
                disabled={!ctx.selectedDoc}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  search
                </span>
                Ver movimientos
              </ZHBtn>
            </div>
          )}
        </div>

        {/* ── Resumen superior (solo modo Producto con resultados) ────── */}
        {ctx.searchMode === "product" && ctx.summary && (
          <div className="pf-header-cards-row kdx-summary-row">
            <SummaryCard
              icon="inventory_2"
              label="Stock Actual"
              value={`${formatMoney(ctx.summary.quantity, qty)} und.`}
            />
            <SummaryCard
              icon="payments"
              label="Costo Promedio"
              value={formatMoneyWithSymbol(ctx.summary.averageCost, cost)}
            />
            <SummaryCard
              icon="account_balance_wallet"
              label="Valor de Inventario"
              value={formatMoneyWithSymbol(ctx.summary.totalStockValue, total)}
            />
            <SummaryCard
              icon="update"
              label="Último Movimiento"
              value={
                ctx.lastMovement
                  ? `${MOVEMENT_TYPE_LABELS[ctx.lastMovement.movementTypeName] ?? ctx.lastMovement.movementTypeName} — ${formatDate(ctx.lastMovement.effectiveDate)}`
                  : "—"
              }
            />
            <SummaryCard
              icon="format_list_numbered"
              label="Cantidad de Movimientos"
              value={`${ctx.movements.length}`}
            />
          </div>
        )}

        {/* ── Filtro de tipo de movimiento (client-side, sobre lo ya cargado) + exportaciones ── */}
        {ctx.movements.length > 0 && (
          <div
            className="kdx-toolbar"
          >
            <select
              value={ctx.movementTypeFilter}
              onChange={(e) => ctx.setMovementTypeFilter(e.target.value)}
            >
              <option value="">Todos los tipos de movimiento</option>
              {Object.entries(MOVEMENT_TYPE_LABELS).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
            <div className="kdx-toolbar-actions">
              <ZHBtn type="button" variant="secondary" disabled title="Próximamente">
                <span className="material-symbols-outlined zh-icon-md">
                  grid_on
                </span>
                Excel
              </ZHBtn>
              <ZHBtn type="button" variant="secondary" disabled title="Próximamente">
                <span className="material-symbols-outlined zh-icon-md">
                  picture_as_pdf
                </span>
                PDF
              </ZHBtn>
              <ZHBtn type="button" variant="secondary" disabled title="Próximamente">
                <span className="material-symbols-outlined zh-icon-md">
                  print
                </span>
                Imprimir
              </ZHBtn>
            </div>
          </div>
        )}

        {/* ── Tabla principal ──────────────────────────────────────────── */}
        {ctx.movementsLoading ? (
          <p>Cargando movimientos...</p>
        ) : (
          <table className="table table--compact table--neutral">
            <thead>
              <tr>
                <th>Seq.</th>
                <th>Fecha Efectiva</th>
                <th>Tipo</th>
                <th>Documento</th>
                <th className="zh-text-align-right">Entrada</th>
                <th className="zh-text-align-right">Salida</th>
                <th className="zh-text-align-right">Saldo</th>
                <th className="zh-text-align-right">Costo Unit.</th>
                <th className="zh-text-align-right">Costo Promedio</th>
                <th className="zh-text-align-right">Valor Inventario</th>
                <th>Usuario</th>
                <th className="zh-text-align-center">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {ctx.movements.map((m) => (
                <tr key={m.id}>
                  <td className="kdx-mono kdx-mono--strong">
                    #{m.sequenceNumber}
                  </td>
                  <td>{formatDate(m.effectiveDate)}</td>
                  <td>
                    <Badge
                      variant={movementBadgeVariant(m.movementTypeName)}
                      label={
                        MOVEMENT_TYPE_LABELS[m.movementTypeName] ??
                        m.movementTypeName
                      }
                    />
                  </td>
                  <td
                    className="kdx-secondary-text"
                  >
                    {m.reference ?? "—"}
                  </td>
                  <td
                    className="zh-table-cell--num kdx-positive"
                  >
                    {m.quantity > 0 ? formatMoney(m.quantity, qty) : "—"}
                  </td>
                  <td
                    className="zh-table-cell--num kdx-negative"
                  >
                    {m.quantity < 0 ? formatMoney(-m.quantity, qty) : "—"}
                  </td>
                  <td className="zh-table-cell--num kdx-value-strong">
                    {formatMoney(m.resultQuantity, qty)}
                  </td>
                  <td className="zh-table-cell--num">
                    {m.unitCost != null
                      ? formatMoneyWithSymbol(m.unitCost, cost)
                      : "—"}
                  </td>
                  <td className="zh-table-cell--num">
                    {formatMoneyWithSymbol(m.runningAverageCost, cost)}
                  </td>
                  <td className="zh-table-cell--num">
                    {formatMoneyWithSymbol(m.runningStockValue, total)}
                  </td>
                  <td className="kdx-small-text">{m.createdByName ?? "—"}</td>
                  <td className="zh-text-align-center">
                    <ZHIconButton
                      icon="folder_open"
                      variant="ghost"
                      title="Ver expediente"
                      onClick={() => void ctx.openDetail(m.id)}
                    />
                  </td>
                </tr>
              ))}
              {ctx.movements.length === 0 && (
                <tr>
                  <td colSpan={12} className="zh-table-empty">
                    {ctx.searchMode === "product"
                      ? "Busque un producto para ver su historial de Kardex."
                      : "Resuelva un documento para ver los movimientos que generó."}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>

      <KardexMovementDetailModal
        open={ctx.detailOpen}
        loading={ctx.detailLoading}
        detail={ctx.detail}
        onClose={ctx.closeDetail}
        onNavigate={ctx.goToRelated}
        movementTypeLabels={MOVEMENT_TYPE_LABELS}
      />
    </ErpPageTemplate>
  );
}

function SummaryCard({
  icon,
  label,
  value,
}: {
  icon: string;
  label: string;
  value: string;
}) {
  return (
    <div className="pf-mini-card">
      <div className="pf-mini-card__body kdx-summary-body">
        <span
          className="material-symbols-outlined zh-icon-xl kdx-summary-icon"
        >
          {icon}
        </span>
        <div
          className="kdx-summary-label"
        >
          {label}
        </div>
        <div className="kdx-summary-value">
          {value}
        </div>
      </div>
    </div>
  );
}
