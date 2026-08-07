import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import type { SalesInvoiceDetailDto } from "../api/salesService";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import { invoiceItemSearchService } from "../api/invoiceItemSearchService";
import type { InvoiceItemSearchResultDto } from "../api/invoiceItemSearchService";
import type { WarehouseDto } from "../../inventory/warehouses/api/warehouseService";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhWarehouseSelector } from "../../../components/zh/inputs/ZhWarehouseSelector";
import type { ItemWarehouseAvailabilityDto } from "../../inventory/stock/api/stockService";
import { Badge } from "../../../components/PageShell";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney } from "../../../lib/sanitizers";
import { lineNet, calcLineTax } from "../utils/salesCalc";
import "../styles/sales-product-card.css";

// ── Resaltado de coincidencias ────────────────────────────────────────────────
function highlightMatch(text: string, query: string): ReactNode {
  const q = query.trim();
  if (!q) return text;
  const idx = text.toLowerCase().indexOf(q.toLowerCase());
  if (idx === -1) return text;
  return (
    <>
      {text.slice(0, idx)}
      <mark className="sf-search-highlight">
        {text.slice(idx, idx + q.length)}
      </mark>
      {text.slice(idx + q.length)}
    </>
  );
}

export type LineWithKey = SalesLineFormValues;

interface SalesInvoiceDetailsSectionProps {
  lines: LineWithKey[];
  backendLines?: SalesInvoiceDetailDto[];
  readOnly: boolean;
  disabled: boolean;
  onRemoveLine: (key: number) => void;
  onUpdateLine: (key: number, field: string, value: unknown) => void;
  onAddItemLine: (item: InvoiceItemSearchResultDto) => Promise<void>;
  onUpdateLineWarehouse: (
    key: number,
    warehouseId: string,
    option?: ItemWarehouseAvailabilityDto,
  ) => void;
  warehouses: WarehouseDto[];
  selectedWarehouseId: string;
  onWarehouseChange: (id: string) => void;
  vatRates?: Record<string, number>;
  /** Cambia (se incrementa) cada vez que el buscador de productos debe recibir foco — UX retail. */
  focusSignal?: number;
}

export function SalesInvoiceDetailsSection({
  lines,
  backendLines,
  readOnly,
  disabled,
  onRemoveLine,
  onUpdateLine,
  onAddItemLine,
  onUpdateLineWarehouse,
  warehouses,
  selectedWarehouseId,
  onWarehouseChange,
  vatRates,
  focusSignal,
}: SalesInvoiceDetailsSectionProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<InvoiceItemSearchResultDto[]>([]);
  const [open, setOpen] = useState(false);
  const [focusIdx, setFocusIdx] = useState(-1);
  const [loading, setLoading] = useState(false);
  const [selecting, setSelecting] = useState(false);
  const searchRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const resultRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);
  const searchVersionRef = useRef(0);

  // UX retail: foco automático al buscador de productos al entrar a "Nueva
  // Venta" y después de cada "Nueva venta" tras emitir (ver productSearchFocusKey en useSalesPage.ts).
  useEffect(() => {
    if (!disabled) searchInputRef.current?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusSignal]);

  // Atajo global F2: reenfoca el buscador de productos desde cualquier parte de la página.
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "F2" && !disabled) {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [disabled]);

  // Mantiene visible el resultado resaltado al navegar la lista con teclado.
  useEffect(() => {
    if (focusIdx >= 0)
      resultRefs.current[focusIdx]?.scrollIntoView({ block: "nearest" });
  }, [focusIdx]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    if (!open || query.length < 2) {
      setResults([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    debounceRef.current = setTimeout(async () => {
      const version = ++searchVersionRef.current;
      try {
        const res = await invoiceItemSearchService.search({
          q: query.trim(),
          warehouseId: selectedWarehouseId || undefined,
          pageSize: 10,
        });
        if (version === searchVersionRef.current) {
          setResults(res);
          setFocusIdx(-1);
        }
      } catch {
        if (version === searchVersionRef.current) setResults([]);
      } finally {
        if (version === searchVersionRef.current) setLoading(false);
      }
    }, 300);
    return () => clearTimeout(debounceRef.current);
  }, [query, open, selectedWarehouseId]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const selectItem = useCallback(
    async (item: InvoiceItemSearchResultDto) => {
      setOpen(false);
      setQuery("");
      setResults([]);
      setSelecting(true);
      try {
        await onAddItemLine(item);
      } finally {
        setSelecting(false);
      }
    },
    [onAddItemLine],
  );

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      if (!open || results.length === 0) return;
      e.preventDefault();
      setFocusIdx((i) => Math.min(i + 1, results.length - 1));
      return;
    }
    if (e.key === "ArrowUp") {
      if (!open || results.length === 0) return;
      e.preventDefault();
      setFocusIdx((i) => Math.max(i - 1, 0));
      return;
    }
    if (e.key === "Escape") {
      setOpen(false);
      return;
    }
    if (e.key !== "Enter") return;
    e.preventDefault();
    if (query.trim().length < 2) return;

    // Navegación por teclado (usuario ya resaltó un resultado): respeta esa elección.
    if (open && focusIdx >= 0 && results[focusIdx]) {
      void selectItem(results[focusIdx]);
      return;
    }
    // Resultados ya cargados (búsqueda con debounce ya resuelta): agrega el primero —
    // caso típico de lector de código de barras USB (código + Enter).
    if (open && !loading && results.length > 0) {
      void selectItem(results[0]);
      return;
    }
    // El debounce de 300ms aún no resolvió (Enter llegó antes) — se fuerza una
    // búsqueda inmediata para no perder el escaneo.
    void (async () => {
      clearTimeout(debounceRef.current);
      setLoading(true);
      const version = ++searchVersionRef.current;
      try {
        const res = await invoiceItemSearchService.search({
          q: query.trim(),
          warehouseId: selectedWarehouseId || undefined,
          pageSize: 10,
        });
        if (version !== searchVersionRef.current) return;
        setResults(res);
        if (res.length > 0) void selectItem(res[0]);
      } catch {
        if (version === searchVersionRef.current) setResults([]);
      } finally {
        if (version === searchVersionRef.current) setLoading(false);
      }
    })();
  };

  const dc = getDecimalConfig();

  const vatLabel = (code: string) => {
    if (!code) return "Sin IVA configurado";
    const rate = vatRates?.[code];
    if (rate !== undefined) return rate === 0 ? "IVA 0%" : `IVA ${rate}%`;
    // Catálogo aún no cargado — nunca se infiere el porcentaje desde el código.
    return "Cargando...";
  };

  return (
    <>
      {/* Search bar */}
      <div className="sf-searchbar">
        <div className="sf-searchbar__input-wrap" ref={searchRef}>
          <span className="material-symbols-outlined sf-searchbar__icon">
            barcode_scanner
          </span>
          <input
            ref={searchInputRef}
            className={`sf-searchbar__input${selecting ? " sf-searchbar__input--selecting" : ""}`}
            type="text"
            placeholder={
              selecting
                ? "Cargando producto…"
                : "Escribe el nombre del producto o escanea el código... (F2)"
            }
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setOpen(true);
            }}
            onFocus={() => {
              if (query.length >= 2) setOpen(true);
            }}
            onKeyDown={handleKeyDown}
            disabled={disabled || selecting}
          />
          {open && query.length >= 2 && (
            <div className="sf-search-dropdown">
              {loading ? (
                <div className="sf-search-loading">
                  <span className="sf-search-spinner" />
                  <span>Buscando…</span>
                </div>
              ) : results.length === 0 ? (
                <div className="pdl-search__empty">
                  Sin resultados para &ldquo;{query}&rdquo;
                </div>
              ) : (
                results.map((item, i) => {
                  const stockVal = item.availableStock ?? 0;
                  const stockClass = !item.tracksStock
                    ? ""
                    : stockVal <= 0
                      ? "sf-result__stock--danger"
                      : stockVal <= 5
                        ? "sf-result__stock--warning"
                        : "sf-result__stock--ok";
                  return (
                    <button
                      key={item.id}
                      type="button"
                      ref={(el) => {
                        resultRefs.current[i] = el;
                      }}
                      className={`sf-result${i === focusIdx ? " sf-result--focused" : ""}`}
                      onClick={() => void selectItem(item)}
                      onMouseEnter={() => setFocusIdx(i)}
                    >
                      {/* Col 1: SKU */}
                      <div className="sf-result__sku-col">
                        <span className="sf-result__sku">{item.sku}</span>
                      </div>

                      {/* Col 2: Producto */}
                      <div className="sf-result__info">
                        <div className="sf-result__name">
                          {highlightMatch(item.description, query)}
                        </div>
                        <div className="sf-result__meta">
                          {item.productFamilyName && (
                            <span className="sf-result__family">
                              {item.productFamilyName}
                            </span>
                          )}
                          {item.productFamilyName && (
                            <span className="sf-result__meta-sep">•</span>
                          )}
                          <span className="sf-result__uom">
                            {item.uomAbbrev}
                          </span>
                        </div>
                      </div>

                      {/* Col 3: Inventario */}
                      <div className="sf-result__col sf-result__col--inv">
                        {item.tracksStock ? (
                          <>
                            {item.warehouseName && (
                              <span className="sf-result__col-label">
                                {item.warehouseName}
                              </span>
                            )}
                            <span className={`sf-result__stock ${stockClass}`}>
                              {item.availableStock != null
                                ? item.availableStock.toFixed(dc.quantity)
                                : "—"}
                            </span>
                            <span className="sf-result__col-sub">
                              disponible
                            </span>
                          </>
                        ) : (
                          <span className="sf-result__value sf-result__value--muted">
                            No aplica
                          </span>
                        )}
                      </div>

                      {/* Col 4: Comercial */}
                      <div className="sf-result__col sf-result__col--comm">
                        {item.averageCost != null && item.averageCost > 0 && (
                          <div className="sf-result__price-row">
                            <span className="sf-result__price-lbl">Costo</span>
                            <span className="sf-result__price-val sf-result__price-val--cost">
                              $
                              {formatMoney(
                                item.averageCost,
                                dc.purchaseUnitPrice,
                              )}
                            </span>
                          </div>
                        )}
                        {item.salePriceWithoutTax != null ? (
                          <div className="sf-result__price-row">
                            <span className="sf-result__price-lbl">PVP</span>
                            <span className="sf-result__price-val sf-result__price-val--pvp">
                              $
                              {formatMoney(
                                item.salePriceWithoutTax,
                                dc.salesUnitPrice,
                              )}
                            </span>
                          </div>
                        ) : (
                          <span className="sf-result__value sf-result__value--muted">
                            Sin precio
                          </span>
                        )}
                        {item.finalSalePrice != null && (
                          <div className="sf-result__price-row">
                            <span className="sf-result__price-lbl">Final</span>
                            <span className="sf-result__price-val sf-result__price-val--final">
                              $
                              {formatMoney(
                                item.finalSalePrice,
                                dc.salesUnitPrice,
                              )}
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Col 5: Impuestos */}
                      <div className="sf-result__col sf-result__col--tax">
                        <span className="sf-result__tax-line">
                          {item.vatDisplay}
                        </span>
                        <span className="sf-result__tax-line sf-result__tax-line--ice">
                          {item.iceDisplay}
                        </span>
                      </div>
                    </button>
                  );
                })
              )}
            </div>
          )}
        </div>
        <ZhWarehouseSelector
          value={selectedWarehouseId || null}
          onChange={(id) => onWarehouseChange(id)}
          fallbackWarehouses={warehouses}
          disabled={disabled || selecting}
          placeholder="Seleccione bodega"
        />
      </div>

      {/* Column headers */}
      <div className="sf-columns">
        <span>Producto</span>
        <span>Precio Lista</span>
        <span>Precio Negociado</span>
        <span>Stock</span>
        <span>Cant.</span>
        <span>Subtotal</span>
      </div>

      {/* Product lines */}
      <div className="sf-products">
        {lines.map((l, idx) => (
          <SalesProductCard
            key={l._key}
            line={l}
            backendLine={backendLines?.[idx]}
            disabled={disabled}
            readOnly={readOnly}
            vatLabel={vatLabel(l.vatCode)}
            vatRates={vatRates}
            warehouses={warehouses}
            selectedWarehouseId={selectedWarehouseId}
            onUpdate={onUpdateLine}
            onUpdateWarehouse={onUpdateLineWarehouse}
            onRemove={onRemoveLine}
          />
        ))}
        {lines.length === 0 && (
          <div className="sf-products-empty">
            <span className="material-symbols-outlined sf-products-empty__icon">
              add_shopping_cart
            </span>
            <p>Busca un producto arriba para agregarlo a la factura</p>
          </div>
        )}
      </div>
    </>
  );
}

function SalesProductCard({
  line,
  backendLine,
  disabled,
  readOnly,
  vatLabel,
  vatRates,
  warehouses,
  selectedWarehouseId,
  onUpdate,
  onUpdateWarehouse,
  onRemove,
}: {
  line: LineWithKey;
  backendLine?: SalesInvoiceDetailDto;
  disabled: boolean;
  readOnly: boolean;
  vatLabel: string;
  vatRates?: Record<string, number>;
  warehouses: WarehouseDto[];
  selectedWarehouseId: string;
  onUpdate: (key: number, field: string, value: unknown) => void;
  onUpdateWarehouse: (
    key: number,
    warehouseId: string,
    option?: ItemWarehouseAvailabilityDto,
  ) => void;
  onRemove: (key: number) => void;
}) {
  const dc = getDecimalConfig();
  const previewNet = lineNet(line);
  const previewTax = calcLineTax(line, vatRates);
  const total =
    backendLine?.taxInclusiveTotal ??
    previewNet + previewTax.vat + previewTax.ice;

  const sku = line._sku ?? backendLine?.snapshotSku ?? "";
  const name = line._name ?? backendLine?.snapshotItemName ?? line.description;
  const cost = line._cost;
  const pvp = line._pvp;
  const stockQty = line._stockQty;
  const stockWarehouse = line._stockWarehouse;

  return (
    <div className="sf-product">
      {/* Col 1: Product Info */}
      <div className="sf-product__info">
        <div className="sf-product__info-row">
          {!readOnly && (
            <ZHIconButton
              icon="delete"
              variant="danger"
              title="Eliminar línea"
              onClick={() => onRemove(line._key)}
              disabled={disabled}
            />
          )}
          <div className="sf-product__text">
            {sku && <div className="sf-product__code">{sku}</div>}
            <div className="sf-product__name" title={name}>
              {name}
            </div>
            <span className="sf-product__tax-tag">{vatLabel}</span>
          </div>
        </div>
      </div>

      {/* Col 2: Price List */}
      <div className="sf-product__pricelist">
        {cost != null && (
          <div className="sf-product__pricelist-row">
            <span className="sf-product__pricelist-label">Costo:</span>
            <span className="sf-product__pricelist-value">
              ${formatMoney(cost, dc.purchaseUnitPrice)}
            </span>
          </div>
        )}
        {pvp != null && (
          <div className="sf-product__pricelist-row">
            <span
              className="sf-product__pricelist-label sales-invoice-details-pvp-label"
            >
              PVP:
            </span>
            <span className="sf-product__pricelist-value sf-product__pricelist-value--bold">
              ${formatMoney(pvp, dc.salesUnitPrice)}
            </span>
          </div>
        )}
        {cost == null && pvp == null && (
          <span className="sales-invoice-details-empty-value">—</span>
        )}
      </div>

      {/* Col 3: Discount + Invoiced Price */}
      <div className="sf-product__negotiated">
        <div className="sf-product__disc-block">
          <span className="sf-product__disc-label">Dto. %</span>
          <ZhDecimalInput
            className="sf-product__disc-input"
            decimals={dc.percentage}
            positiveOnly
            defaultValue={line.discountPct ?? 0}
            onBlur={(e) =>
              onUpdate(
                line._key,
                "discountPct",
                Math.min(100, Math.max(0, Number(e.target.value) || 0)),
              )
            }
            disabled={disabled}
          />
        </div>
        <div className="sf-product__price-block">
          <span className="sf-product__price-label">Precio Facturado</span>
          <div className="sf-product__price-wrap">
            <span className="sf-product__price-currency">$</span>
            <ZhDecimalInput
              className="sf-product__price-input"
              decimals={dc.salesUnitPrice}
              positiveOnly
              defaultValue={line.unitPrice}
              onBlur={(e) =>
                onUpdate(line._key, "unitPrice", Number(e.target.value) || 0)
              }
              disabled={disabled}
            />
          </div>
        </div>
      </div>

      {/* Col 4: Stock */}
      <div className="sf-product__stock-box">
        <span className="material-symbols-outlined sf-product__stock-icon">
          assignment
        </span>
        <div className="sf-product__stock-data">
          <div
            className={`sf-product__stock-qty ${stockQty == null ? "sf-product__stock-qty--empty" : ""}`}
          >
            {stockQty != null ? stockQty : "—"}
            {stockQty != null && (
              <span className="sf-product__stock-uom"> UDS</span>
            )}
            {line._tracksStock && stockQty != null && (
              <Badge
                label={
                  stockQty <= 0
                    ? "Sin stock"
                    : stockQty <= 5
                      ? "Stock bajo"
                      : "Disponible"
                }
                variant={
                  stockQty <= 0 ? "red" : stockQty <= 5 ? "orange" : "green"
                }
                size="md"
              />
            )}
          </div>
          {line._tracksStock && !readOnly ? (
            <ZhWarehouseSelector
              value={line.warehouseId ?? null}
              onChange={(id, option) =>
                onUpdateWarehouse(line._key, id, option)
              }
              itemId={line.itemId}
              fallbackWarehouses={warehouses}
              defaultWarehouseId={selectedWarehouseId}
              disabled={disabled}
              placeholder="Seleccione bodega"
            />
          ) : line._tracksStock ? (
            <div className="sf-product__stock-wh">
              {warehouses.find((w) => w.id === line.warehouseId)?.name ??
                stockWarehouse ??
                "—"}
            </div>
          ) : null}
        </div>
      </div>

      {/* Col 5: Quantity */}
      <div className="sf-product__qty">
        <ZhDecimalInput
          className="sf-product__qty-input"
          decimals={dc.quantity}
          positiveOnly
          defaultValue={line.quantity}
          onBlur={(e) =>
            onUpdate(line._key, "quantity", Number(e.target.value) || 1)
          }
          disabled={disabled}
        />
      </div>

      {/* Col 6: Subtotal */}
      <div className="sf-product__subtotal">
        <div className="sf-product__base">
          Base: ${formatMoney(previewNet, dc.totalAmount)}
        </div>
        <div className="sf-product__total-amount">
          ${formatMoney(total, dc.totalAmount)}
        </div>
        <div className="sf-product__tax-incl">IMP. INCLUIDOS</div>
      </div>
    </div>
  );
}
