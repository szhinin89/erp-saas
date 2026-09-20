import { Badge } from "../../../components/PageShell";
import {  useCallback, useEffect, useRef, useState } from "react";
import {  ZHBtn } from "../../../components/zh/ZHForm";
import {  ZHIconButton } from "../../../components/zh/ZHIconButton";
import {  ZHDrawer } from "../../../components/zh/ZHDrawer";
import {  ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import {  ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import {  ZhCurrencyInput } from "../../../components/zh/inputs/ZhCurrencyInput";
import {  formatMoney, parseDecimal } from "../../../lib/sanitizers";
import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import {  formatDateTime } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { itemPriceListFacade } from "../../items/facades/itemPriceListFacade";
import {  message } from "../../../lib/messages";
import {  itemLookupFacade } from "../../items/facades/itemLookupFacade";
import type { ItemDto } from "../../../types/items";
import { 
  priceListService,
  pricingSimulationService,
  pricingRuleService,
  RULE_TYPE_OPTIONS,
  formatRuleGeneral,
  type PriceListDto,
  type PriceListAssignedItemDto,
  type PricingRuleDto,
} from "../api/pricingService";

type ExceptionRow = PriceListAssignedItemDto & { rule: PricingRuleDto | null };

type DrawerProduct = {
  itemId: string;
  sku: string;
  itemName: string;
  baseSalePrice: number | null;
};

export function PriceListExceptionsTab({
  priceList,
  mode = "exceptions",
}: {
  priceList: PriceListDto;
  mode?: "products" | "exceptions";
}) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const [assigning, setAssigning] = useState(false);
  const [revision, setRevision] = useState(0);
  const assignItem = async (item: ItemDto) => {
    if (assigning || !canShow("items.edit") || !canShow("items.view")) return;
    setAssigning(true);
    setError("");
    try {
      const ids = await itemPriceListFacade.getPriceLists(item.id);
      await itemPriceListFacade.setPriceLists(item.id, [...new Set([...ids, priceList.id])]);
      await fetchAll();
    } catch (e) {
      setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
    } finally {
      setAssigning(false);
    }
  };
  const [rows, setRows] = useState<ExceptionRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerProduct, setDrawerProduct] = useState<DrawerProduct | null>(
    null,
  );
  const [drawerRule, setDrawerRule] = useState<PricingRuleDto | null>(null);

  const fetchAll = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [items, rules] = await Promise.all([
        priceListService.getAssignedItems(priceList.id),
        pricingRuleService.list(priceList.id),
      ]);
      const rulesByItem = new Map(
        rules.filter((r) => r.isActive).map((r) => [r.itemId, r]),
      );
      setRevision((value) => value + 1);
      setRows(
        items.map((i) => ({ ...i, rule: rulesByItem.get(i.itemId) ?? null })),
      );
    } catch (e: unknown) {
      setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
    }
    setLoading(false);
  }, [priceList.id, t]);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  const openForRow = (row: ExceptionRow) => {
    setDrawerProduct({
      itemId: row.itemId,
      sku: row.sku,
      itemName: row.itemName,
      baseSalePrice: row.baseSalePrice,
    });
    setDrawerRule(row.rule);
    setDrawerOpen(true);
  };

  const openNew = () => {
    if (loading || rows.length === 0 || error) return;
    setDrawerProduct(null);
    setDrawerRule(null);
    setDrawerOpen(true);
  };

  const handleRemove = async (rule: PricingRuleDto) => {
    const confirmed = await message.confirm({
      title: "Quitar excepción",
      message:
        "¿Quitar esta excepción? El producto volverá a usar la regla general de la lista.",
      variant: "warning",
      confirmLabel: "Quitar excepción",
      cancelLabel: "Cancelar",
    });
    if (!confirmed || !canShow("pricing.delete")) return;
    try {
      await pricingRuleService.remove(rule.id);
      await fetchAll();
      message.success("Excepción eliminada correctamente.");
    } catch (e: unknown) {
      setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
    }
  };

  const exceptionColumns: ZHDataTableColumn<ExceptionRow>[] = [
    { key: "product", header: t("pricing.ux.product"), render: (row) => row.itemName },
    { key: "sku", header: "SKU", render: (row) => <span className="prd-td-code">{row.sku}</span> },
    {
      key: "basePrice",
      align: "right",
      header: t("pricing.ux.base"),
      render: (row) => (row.baseSalePrice != null ? formatMoney(row.baseSalePrice, getPrecisionPolicy().salesUnitPriceDecimals) : "—"),
    },
    {
      key: "generalRule",
      header: t("pricing.ux.general"),
      render: () => formatRuleGeneral(priceList.ruleType, priceList.ruleValue, priceList.currencyCode, t),
    },
    {
      key: "exception",
      header: t("pricing.ux.exception"),
      render: (row) =>
        row.rule
          ? formatRuleGeneral(row.rule.ruleType, row.rule.ruleValue, priceList.currencyCode, t)
          : t("pricing.ux.none"),
    },
    {
      key: "calculatedPrice", header: t("pricing.ux.calculated"), align: "right",
      render: (row) => canShow("items.view") ? <CalculatedPrice key={`${row.itemId}-${revision}`} itemId={row.itemId} priceList={priceList} /> : "—",
    },
    {
      key: "status",
      header: t("pricing.ux.status"),
      render: (row) => (
        <Badge
          label={row.rule ? t("pricing.ux.exception") : t("pricing.ux.general")}
          variant={row.rule ? "success" : "neutral"}
        />
      ),
    },
    {
      key: "lastModified",
      header: t("pricing.ux.modified"),
      render: (row) =>
        row.rule?.lastModifiedAt
          ? `${formatDateTime(row.rule.lastModifiedAt)}${row.rule.lastModifiedByName ? ` — ${row.rule.lastModifiedByName}` : ""}`
          : "—",
    },
    {
      key: "actions",
      header: t("pricing.ux.actions"),
      render: (row) => (
        <div className="prd-td-actions">
          {canShow("pricing.create") && <ZHIconButton
            icon={row.rule ? "edit" : "add_circle"}
            title={row.rule ? t("pricing.ux.editException") : t("pricing.ux.createException")}
            variant="primary"
            onClick={() => openForRow(row)}
          />}
          {row.rule && canShow("pricing.delete") && (
            <ZHIconButton
              icon="delete"
              title={t("pricing.ux.deleteException")}
              variant="danger"
              onClick={() => handleRemove(row.rule!)}
            />
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="prd-section">
      {error && <div className="prd-error-banner">{error}</div>}

      <div className="prd-stat-grid">
        <div><strong>{t("pricing.ux.name")}</strong><p>{priceList.name}</p></div>
        <div><strong>{t("pricing.ux.general")}</strong><p>{formatRuleGeneral(priceList.ruleType, priceList.ruleValue, priceList.currencyCode, t)}</p></div>
        <div><strong>{t("pricing.ux.validity")}</strong><p>{priceList.validFrom ? formatDate(priceList.validFrom) : "—"} · {priceList.validUntil ? formatDate(priceList.validUntil) : "—"}</p></div>
        <div><strong>{t("pricing.ux.status")}</strong><p>{t(priceList.isActive ? "pricing.ux.active" : "pricing.ux.inactive")}</p></div>
        <div><strong>{t("pricing.ux.count")}</strong><p>{loading ? "—" : rows.length}</p></div>
      </div>
      <p className="zh-text-muted">{t("pricing.ux.note")}</p>
      {mode === "products" && canShow("items.edit") && canShow("items.view") && priceList.isActive && (
        <div className="zh-field">
          <label className="zh-field-label">{t("pricing.ux.assign")}</label>
          <p className="zh-text-muted">{t("pricing.ux.assignHint")}</p>
          {assigning ? <p>{t("pricing.ux.saving")}</p> : <RemoteItemPicker onSelect={(item) => void assignItem(item)} />}
        </div>
      )}
      {!loading && rows.length === 0 && <p role="status">{t("pricing.ux.assignFirst")}</p>}
      <div className="prd-crud-toolbar">
        {mode === "exceptions" && canShow("pricing.create") && <ZHBtn onClick={openNew} disabled={loading || rows.length === 0 || !!error}>
          <span className="material-symbols-outlined zh-icon-lg">add</span>
          {t("pricing.ux.newException")}
        </ZHBtn>}
        <ZHBtn onClick={fetchAll} disabled={loading}>
          <span className="material-symbols-outlined zh-icon-lg">refresh</span>
        </ZHBtn>
      </div>

      <ZHDataTable
        columns={mode === "products" ? exceptionColumns.filter((column) => !["lastModified", "actions", "sku"].includes(column.key)) : exceptionColumns}
        rows={mode === "exceptions" ? rows.filter((row) => row.rule) : rows}
        rowKey={(row) => row.itemId}
        loading={loading}
        showRowNumber
        emptyMessage={t(rows.length === 0 ? "pricing.ux.assignFirst" : "pricing.ux.noExceptions")}
      />

      <ExceptionDrawer
        products={rows}
        open={drawerOpen}
        priceList={priceList}
        product={drawerProduct}
        existingRule={drawerRule}
        onClose={() => setDrawerOpen(false)}
        onSaved={() => {
          setDrawerOpen(false);
          fetchAll();
        }}
      />
    </div>
  );
}

// ── Drawer de alta/edición ──────────────────────────────────────────────────

function ExceptionDrawer({
  open,
  products,
  priceList,
  product,
  existingRule,
  onClose,
  onSaved,
}: {
  open: boolean;
  products: DrawerProduct[];
  priceList: PriceListDto;
  product: DrawerProduct | null;
  existingRule: PricingRuleDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const pp = getPrecisionPolicy();
  const [selected, setSelected] = useState<DrawerProduct | null>(product);
  const [ruleType, setRuleType] = useState("");
  const [ruleValue, setRuleValue] = useState("");
  const [active, setActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  // "ExistsInactive": ya existe una excepción deshabilitada con esta clave — el backend no la
  // reactiva sola, se pide confirmación explícita antes de llamar a pricingRuleService.enable.
  const [confirmReactivate, setConfirmReactivate] = useState(false);
  // Valor de la excepción deshabilitada, expuesto por el backend solo cuando status ===
  // 'ExistsInactive' — se compara contra el precio base actual para no reactivar en
  // silencio un precio (típicamente FixedPrice) que quedó obsoleto mientras estaba inactiva.
  const [existingInactive, setExistingInactive] = useState<{
    ruleType: string;
    ruleValue: number;
  } | null>(null);

  useEffect(() => {
    if (!open) return;
    setSelected(product);
    setRuleType(existingRule?.ruleType ?? "");
    setRuleValue(existingRule ? String(existingRule.ruleValue) : "");
    setActive(existingRule ? existingRule.isActive : true);
    setError("");
    setConfirmReactivate(false);
    setExistingInactive(null);
  }, [open, product, existingRule]);

  const handleSave = async () => {
    setError("");
    if (!canShow("pricing.create")) return;
    if (!selected || !products.some((item) => item.itemId === selected.itemId)) {
      setError(t("pricing.ux.selectProduct"));
      return;
    }
    if (!ruleType) {
      setError(t("pricing.ux.selectRule"));
      return;
    }
    const parsed = parseDecimal(ruleValue);
    if (!ruleValue.trim() || Number.isNaN(parsed)) {
      setError(t("pricing.ux.validValue"));
      return;
    }

    setSaving(true);
    try {
      if (existingRule && !active) {
        if (!canShow("pricing.delete")) return;
        // Desactivar = quitar la excepción (única operación soportada por el backend).
        await pricingRuleService.remove(existingRule.id);
        onSaved();
      } else {
        const result = await pricingRuleService.set({
          priceListId: priceList.id,
          itemId: selected.itemId,
          ruleType,
          ruleValue: parsed,
        });
        if (result.status === "ExistsInactive") {
          // No es un error — es una decisión que le corresponde al usuario, no a un toast rojo.
          setExistingInactive(
            result.existingRuleType != null && result.existingRuleValue != null
              ? {
                  ruleType: result.existingRuleType,
                  ruleValue: result.existingRuleValue,
                }
              : null,
          );
          setConfirmReactivate(true);
        } else {
          onSaved();
        }
      }
    } catch (e: unknown) {
      setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
    }
    setSaving(false);
  };

  const handleReactivate = async () => {
    if (!selected || !canShow("pricing.update") || !products.some((item) => item.itemId === selected.itemId)) return;
    setSaving(true);
    setError("");
    try {
      await pricingRuleService.enable({
        priceListId: priceList.id,
        itemId: selected.itemId,
      });
      setConfirmReactivate(false);
      onSaved();
    } catch (e: unknown) {
      setError(formatApiRequestError(e, { generic: t("pricing.ux.error") }));
      setConfirmReactivate(false);
    }
    setSaving(false);
  };

  return (
    <ZHDrawer
      open={open}
      onClose={onClose}
      title={existingRule ? t("pricing.ux.editException") : t("pricing.ux.newException")}
      subtitle={priceList.name}
      footer={
        <>
          <ZHBtn onClick={onClose}>{t("pricing.ux.cancel")}</ZHBtn>
          <ZHBtn onClick={handleSave} disabled={saving || !selected || !ruleType || !ruleValue.trim() || !canShow("pricing.create")}>
            <span className="material-symbols-outlined zh-icon-lg">save</span>
            {saving ? t("pricing.ux.saving") : t("pricing.ux.save")}
          </ZHBtn>
        </>
      }
    >
      {error && <div className="prd-error-banner">{error}</div>}

      <div className="zh-field">
        <label className="zh-field-label" htmlFor="exception-product">{t("pricing.ux.listProduct")}</label>
        <ZhSelect id="exception-product" value={selected?.itemId ?? ""} disabled={!!product || saving}
          onChange={(event) => setSelected(products.find((item) => item.itemId === event.target.value) ?? null)}>
          <option value="">{t("pricing.ux.selectProduct")}</option>
          {products.map((item) => <option key={item.itemId} value={item.itemId}>{item.sku} · {item.itemName}</option>)}
        </ZhSelect>
      </div>

      {selected && (
        <div className="zh-field">
          <label className="zh-field-label">{t("pricing.ux.base")}</label>
          <div className="zh-field-control">
            <div className="prd-readonly-value">
              {selected.baseSalePrice != null
                ? `${priceList.currencyCode} ${formatMoney(selected.baseSalePrice, pp.salesUnitPriceDecimals)}`
                : t("pricing.ux.noBase")}
            </div>
          </div>
        </div>
      )}

      <div className="zh-field">
        <label className="zh-field-label">
          {t("pricing.ux.ruleType")} <span className="zh-field-required">*</span>
        </label>
        <div className="zh-field-control">
          <ZhSelect aria-label={t("pricing.ux.ruleType")}
            value={ruleType}
            onChange={(e) => {
              setRuleType(e.target.value);
              setRuleValue("");
            }}
          >
            {RULE_TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {t(`pricing.ux.option.${o.value || "none"}`)}
              </option>
            ))}
          </ZhSelect>
        </div>
      </div>

      {ruleType === "PercentDiscount" && (
        <div className="zh-field">
          <label className="zh-field-label">
            {t("pricing.ux.value")} (%) <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhDecimalInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              decimals={pp.percentageDecimals}
              positiveOnly
              placeholder="15"
            />
          </div>
        </div>
      )}
      {ruleType === "PercentMarkup" && (
        <div className="zh-field">
          <label className="zh-field-label">
            {t("pricing.ux.value")} (%) <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhDecimalInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              decimals={pp.percentageDecimals}
              positiveOnly
              placeholder="8"
            />
          </div>
        </div>
      )}
      {ruleType === "FixedPrice" && (
        <div className="zh-field">
          <label className="zh-field-label">
            {t("pricing.ux.value")} <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhCurrencyInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              currency={priceList.currencyCode}
              decimals={pp.salesUnitPriceDecimals}
              placeholder="25"
            />
          </div>
        </div>
      )}
      {ruleType === "FixedAdjustment" && (
        <div className="zh-field">
          <label className="zh-field-label">
            {t("pricing.ux.value")} ({priceList.currencyCode}){" "}
            <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhDecimalInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              decimals={pp.salesUnitPriceDecimals}
              placeholder="3"
            />
          </div>
        </div>
      )}

      {selected && canShow("items.view") && (
        <div role="status" className="zh-field">
          <p>{t("pricing.ux.summary")}</p>
          <p>{selected.baseSalePrice == null ? "—" : formatMoney(selected.baseSalePrice, pp.salesUnitPriceDecimals)}
            {" → "}{formatRuleGeneral(priceList.ruleType, priceList.ruleValue, priceList.currencyCode, t)}
            {" → "}{formatRuleGeneral(ruleType || null, ruleValue.trim() ? parseDecimal(ruleValue) : null, priceList.currencyCode, t)}
            {" → "}<CalculatedPrice key={`${selected.itemId}-${ruleType}-${ruleValue}-${active}`} itemId={selected.itemId} priceList={priceList}
              draft={ruleType && ruleValue.trim() && Number.isFinite(parseDecimal(ruleValue)) ? { priceListId: priceList.id, itemId: selected.itemId, ruleType, ruleValue: parseDecimal(ruleValue) } : undefined}
              incomplete={!ruleType || !ruleValue.trim() || !Number.isFinite(parseDecimal(ruleValue)) || !active} />
          </p>
          <p className="zh-text-muted">{t("pricing.ux.overrideHint")}</p>
        </div>
      )}

      {existingRule && canShow("pricing.delete") && (
        <div className="zh-field">
          <label className="zh-checkbox-label prd-checkbox-field">
            <input
              type="checkbox"
              checked={active}
              onChange={(e) => setActive(e.target.checked)}
            />
            Activo
          </label>
        </div>
      )}

      <ZHConfirmModal
        open={confirmReactivate}
        variant="default"
        title="Excepción deshabilitada"
        message={
          existingInactive
            ? `Esta excepción ya existe pero está desactivada, con el valor ` +
              `"${formatRuleGeneral(existingInactive.ruleType, existingInactive.ruleValue, priceList.currencyCode, t)}"` +
              (selected?.baseSalePrice != null
                ? ` (precio base actual del ítem: ${priceList.currencyCode} ${formatMoney(selected.baseSalePrice, pp.salesUnitPriceDecimals)}). `
                : ". ") +
              "Al reactivarla se aplicará ese valor tal cual — verifica que siga siendo correcto. ¿Desea reactivarla?"
            : "Esta excepción ya existe pero está desactivada. ¿Desea reactivarla?"
        }
        confirmLabel="Reactivar"
        cancelLabel="Cancelar"
        onConfirm={handleReactivate}
        onCancel={() => setConfirmReactivate(false)}
      />
    </ZHDrawer>
  );
}

// ── Buscador remoto de productos (SKU / Nombre) — sin cargar el catálogo completo ──

function RemoteItemPicker({ onSelect }: { onSelect: (item: ItemDto) => void }) {
  const { t } = useI18n();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ItemDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const [searchError, setSearchError] = useState("");
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node))
        setOpen(false);
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  useEffect(() => {
    let current = true;
    clearTimeout(debounceRef.current);
    if (query.trim().length < 2) {
      setResults([]);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      setLoading(true);
      setSearchError("");
      try {
        const res = await itemLookupFacade.search({
          search: query.trim(),
          isActive: true,
          pageSize: 10,
        });
        if (current) setResults(res.items);
      } catch (e) {
        if (current) { setResults([]); setSearchError(formatApiRequestError(e, { generic: t("pricing.ux.error") })); }
      }
      if (current) setLoading(false);
    }, 300);
    return () => { current = false; clearTimeout(debounceRef.current); };
  }, [query, t]);

  return (
    <div ref={wrapRef} className="zh-picker">
      {searchError && <p role="alert">{searchError}</p>}
      <ZhTextInput
        className="zh-input"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          if (query.length >= 2) setOpen(true);
        }}
        placeholder={t("pricing.ux.search")}
      />
      {open && query.length >= 2 && (
        <div className="zh-picker__dropdown">
          {loading && (
            <div className="zh-picker__empty">
              Buscando...
            </div>
          )}
          {!loading && results.length === 0 && (
            <div className="zh-picker__empty">
              Sin resultados para &ldquo;{query}&rdquo;
            </div>
          )}
          {results.map((item) => (
            <button
              key={item.id}
              type="button"
              className="zh-picker__result"
              onClick={() => {
                onSelect(item);
                setQuery("");
                setResults([]);
                setOpen(false);
              }}
            >
              <div className="zh-picker__result-main">
                <div className="zh-picker__result-name">
                  <span className="zh-picker__result-code">{item.sku}</span>
                  {item.shortName}
                </div>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}







function CalculatedPrice({ itemId, priceList, draft, incomplete = false }: {
  itemId: string;
  priceList: PriceListDto;
  draft?: import("../api/pricingService").SetPricingRulePayload;
  incomplete?: boolean;
}) {
  const { t } = useI18n();
  const [price, setPrice] = useState<number | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(!incomplete);
  useEffect(() => {
    if (incomplete) return;
    let current = true;
    const timeout = setTimeout(() => {
      pricingSimulationService.simulate(itemId, draft).then((results) => {
        if (current) setPrice(results.find((row) => row.priceListId === priceList.id)?.netPrice ?? null);
      }).catch((err: unknown) => {
        if (current) setError(formatApiRequestError(err, { generic: t("pricing.ux.error") }));
      }).finally(() => { if (current) setLoading(false); });
    }, draft ? 300 : 0);
    return () => { current = false; clearTimeout(timeout); };
  }, [itemId, priceList.id, draft, incomplete, t]);
  if (error) return <span title={error}>{t("pricing.ux.unavailable")}</span>;
  return <span>{loading ? t("pricing.ux.loading") : price == null ? t("pricing.ux.unavailable") : `${priceList.currencyCode} ${formatMoney(price, getPrecisionPolicy().salesUnitPriceDecimals)}`}</span>;
}
