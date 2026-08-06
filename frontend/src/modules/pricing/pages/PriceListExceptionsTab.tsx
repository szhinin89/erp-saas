import { useCallback, useEffect, useRef, useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDrawer } from "../../../components/zh/ZHDrawer";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhCurrencyInput } from "../../../components/zh/inputs/ZhCurrencyInput";
import { formatMoney, parseDecimal } from "../../../lib/sanitizers";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { formatApiError } from "../../lib/formatApiError";
import { itemService } from "../../items/api/itemService";
import type { ItemDto } from "../../../types/items";
import {
  priceListService,
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
}: {
  priceList: PriceListDto;
}) {
  const [rows, setRows] = useState<ExceptionRow[]>([]);
  const [loading, setLoading] = useState(false);
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
      setRows(
        items.map((i) => ({ ...i, rule: rulesByItem.get(i.itemId) ?? null })),
      );
    } catch (e: unknown) {
      setError(formatApiError(e));
    }
    setLoading(false);
  }, [priceList.id]);

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
    setDrawerProduct(null);
    setDrawerRule(null);
    setDrawerOpen(true);
  };

  const handleRemove = async (rule: PricingRuleDto) => {
    if (
      !window.confirm(
        "Â¿Quitar esta excepciÃ³n? El producto volverÃ¡ a usar la regla general de la lista.",
      )
    )
      return;
    try {
      await pricingRuleService.remove(rule.id);
      fetchAll();
    } catch (e: unknown) {
      setError(formatApiError(e));
    }
  };

  return (
    <div className="prd-section">
      {error && <div className="prd-error-banner">{error}</div>}

      <div className="prd-crud-toolbar">
        <ZHBtn onClick={openNew}>
          <span className="material-symbols-outlined zh-icon-lg">add</span>
          Nueva excepciÃ³n
        </ZHBtn>
        <ZHBtn onClick={fetchAll} disabled={loading}>
          <span className="material-symbols-outlined zh-icon-lg">refresh</span>
        </ZHBtn>
      </div>

      {loading ? (
        <p>Cargando...</p>
      ) : (
        <div className="prd-table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>Producto</th>
                <th>SKU</th>
                <th>Precio Base</th>
                <th>Regla General</th>
                <th>ExcepciÃ³n</th>
                <th>Estado</th>
                <th>Ãšltima modificaciÃ³n</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.itemId}>
                  <td>{row.itemName}</td>
                  <td className="prd-td-code">{row.sku}</td>
                  <td>
                    {row.baseSalePrice != null
                      ? formatMoney(row.baseSalePrice, 2)
                      : "â€”"}
                  </td>
                  <td>
                    {formatRuleGeneral(
                      priceList.ruleType,
                      priceList.ruleValue,
                      priceList.currencyCode,
                    )}
                  </td>
                  <td>
                    {row.rule
                      ? formatRuleGeneral(
                          row.rule.ruleType,
                          row.rule.ruleValue,
                          priceList.currencyCode,
                        )
                      : "Sin excepciÃ³n"}
                  </td>
                  <td>
                    <span
                      className={`prd-status-badge ${row.rule ? "prd-status-badge--active" : ""}`}
                    >
                      {row.rule ? "ExcepciÃ³n" : "General"}
                    </span>
                  </td>
                  <td>
                    {row.rule?.lastModifiedAt
                      ? `${formatDateTime(row.rule.lastModifiedAt)}${row.rule.lastModifiedByName ? ` â€” ${row.rule.lastModifiedByName}` : ""}`
                      : "â€”"}
                  </td>
                  <td className="prd-td-actions">
                    <ZHIconButton
                      icon={row.rule ? "edit" : "add_circle"}
                      title={row.rule ? "Editar excepciÃ³n" : "Crear excepciÃ³n"}
                      variant="primary"
                      onClick={() => openForRow(row)}
                    />
                    {row.rule && (
                      <ZHIconButton
                        icon="delete"
                        title="Eliminar excepciÃ³n"
                        variant="danger"
                        onClick={() => handleRemove(row.rule!)}
                      />
                    )}
                  </td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr className="prd-empty-row">
                  <td colSpan={8}>
                    Esta lista todavÃ­a no tiene productos asignados. AsÃ­gnalos
                    desde el formulario del Ãtem.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      <ExceptionDrawer
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

// â”€â”€ Drawer de alta/ediciÃ³n â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function ExceptionDrawer({
  open,
  priceList,
  product,
  existingRule,
  onClose,
  onSaved,
}: {
  open: boolean;
  priceList: PriceListDto;
  product: DrawerProduct | null;
  existingRule: PricingRuleDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [selected, setSelected] = useState<DrawerProduct | null>(product);
  const [ruleType, setRuleType] = useState("");
  const [ruleValue, setRuleValue] = useState("");
  const [active, setActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  // "ExistsInactive": ya existe una excepciÃ³n deshabilitada con esta clave â€” el backend no la
  // reactiva sola, se pide confirmaciÃ³n explÃ­cita antes de llamar a pricingRuleService.enable.
  const [confirmReactivate, setConfirmReactivate] = useState(false);
  // Valor de la excepciÃ³n deshabilitada, expuesto por el backend solo cuando status ===
  // 'ExistsInactive' â€” se compara contra el precio base actual para no reactivar en
  // silencio un precio (tÃ­picamente FixedPrice) que quedÃ³ obsoleto mientras estaba inactiva.
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
    if (!selected) {
      setError("Selecciona un producto.");
      return;
    }
    if (!ruleType) {
      setError("Selecciona un tipo de regla.");
      return;
    }
    const parsed = parseDecimal(ruleValue);
    if (!ruleValue.trim() || Number.isNaN(parsed)) {
      setError("Ingresa un valor vÃ¡lido.");
      return;
    }

    setSaving(true);
    try {
      if (existingRule && !active) {
        // Desactivar = quitar la excepciÃ³n (Ãºnica operaciÃ³n soportada por el backend).
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
          // No es un error â€” es una decisiÃ³n que le corresponde al usuario, no a un toast rojo.
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
      setError(formatApiError(e));
    }
    setSaving(false);
  };

  const handleReactivate = async () => {
    if (!selected) return;
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
      setError(formatApiError(e));
      setConfirmReactivate(false);
    }
    setSaving(false);
  };

  return (
    <ZHDrawer
      open={open}
      onClose={onClose}
      title={existingRule ? "Editar excepciÃ³n" : "Nueva excepciÃ³n"}
      subtitle={priceList.name}
      footer={
        <>
          <ZHBtn onClick={onClose}>Cancelar</ZHBtn>
          <ZHBtn onClick={handleSave} disabled={saving}>
            <span className="material-symbols-outlined zh-icon-lg">save</span>
            {saving ? "Guardando..." : "Guardar"}
          </ZHBtn>
        </>
      }
    >
      {error && <div className="prd-error-banner">{error}</div>}

      {/* Producto: solo lectura si ya viene preseleccionado desde la tabla; bÃºsqueda remota si es nuevo. */}
      {selected ? (
        <div className="zh-field">
          <label className="zh-field-label">Producto</label>
          <div className="zh-field-control">
            <div className="prd-readonly-value">
              <code className="prd-sku">{selected.sku}</code>{" "}
              {selected.itemName}
            </div>
          </div>
        </div>
      ) : (
        <div className="zh-field">
          <label className="zh-field-label">
            Producto <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <RemoteItemPicker
              onSelect={(item) =>
                setSelected({
                  itemId: item.id,
                  sku: item.sku,
                  itemName: item.shortName,
                  baseSalePrice: item.baseSalePrice,
                })
              }
            />
          </div>
        </div>
      )}

      {selected && (
        <div className="zh-field">
          <label className="zh-field-label">Precio Base</label>
          <div className="zh-field-control">
            <div className="prd-readonly-value">
              {selected.baseSalePrice != null
                ? `${priceList.currencyCode} ${formatMoney(selected.baseSalePrice, 2)}`
                : "Sin precio base"}
            </div>
          </div>
        </div>
      )}

      <div className="zh-field">
        <label className="zh-field-label">
          Tipo de regla <span className="zh-field-required">*</span>
        </label>
        <div className="zh-field-control">
          <select
            value={ruleType}
            onChange={(e) => {
              setRuleType(e.target.value);
              setRuleValue("");
            }}
          >
            {RULE_TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {ruleType === "PercentDiscount" && (
        <div className="zh-field">
          <label className="zh-field-label">
            Valor (%) <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhDecimalInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              decimals={2}
              positiveOnly
              placeholder="15"
            />
          </div>
        </div>
      )}
      {ruleType === "PercentMarkup" && (
        <div className="zh-field">
          <label className="zh-field-label">
            Valor (%) <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhDecimalInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              decimals={2}
              positiveOnly
              placeholder="8"
            />
          </div>
        </div>
      )}
      {ruleType === "FixedPrice" && (
        <div className="zh-field">
          <label className="zh-field-label">
            Valor <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhCurrencyInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              currency={priceList.currencyCode}
              decimals={2}
              placeholder="25"
            />
          </div>
        </div>
      )}
      {ruleType === "FixedAdjustment" && (
        <div className="zh-field">
          <label className="zh-field-label">
            Valor ({priceList.currencyCode}){" "}
            <span className="zh-field-required">*</span>
          </label>
          <div className="zh-field-control">
            <ZhDecimalInput
              value={ruleValue}
              onChange={(e) => setRuleValue(e.target.value)}
              decimals={2}
              placeholder="3"
            />
          </div>
        </div>
      )}

      {existingRule && (
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
        title="ExcepciÃ³n deshabilitada"
        message={
          existingInactive
            ? `Esta excepciÃ³n ya existe pero estÃ¡ desactivada, con el valor ` +
              `"${formatRuleGeneral(existingInactive.ruleType, existingInactive.ruleValue, priceList.currencyCode)}"` +
              (selected?.baseSalePrice != null
                ? ` (precio base actual del Ã­tem: ${priceList.currencyCode} ${formatMoney(selected.baseSalePrice, 2)}). `
                : ". ") +
              "Al reactivarla se aplicarÃ¡ ese valor tal cual â€” verifica que siga siendo correcto. Â¿Desea reactivarla?"
            : "Esta excepciÃ³n ya existe pero estÃ¡ desactivada. Â¿Desea reactivarla?"
        }
        confirmLabel="Reactivar"
        cancelLabel="Cancelar"
        onConfirm={handleReactivate}
        onCancel={() => setConfirmReactivate(false)}
      />
    </ZHDrawer>
  );
}

// â”€â”€ Buscador remoto de productos (SKU / Nombre) â€” sin cargar el catÃ¡logo completo â”€â”€

function RemoteItemPicker({ onSelect }: { onSelect: (item: ItemDto) => void }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ItemDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
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
    clearTimeout(debounceRef.current);
    if (query.trim().length < 2) {
      setResults([]);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      setLoading(true);
      try {
        const res = await itemService.getAll({
          search: query.trim(),
          isActive: true,
          pageSize: 10,
        });
        setResults(res.items);
      } catch {
        setResults([]);
      }
      setLoading(false);
    }, 300);
    return () => clearTimeout(debounceRef.current);
  }, [query]);

  return (
    <div ref={wrapRef} className="zh-picker">
      <input
        className="zh-input"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          if (query.length >= 2) setOpen(true);
        }}
        placeholder="Buscar por SKU o nombre..."
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

