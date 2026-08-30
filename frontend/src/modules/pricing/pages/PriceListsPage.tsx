import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { Badge } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhCurrencyInput } from "../../../components/zh/inputs/ZhCurrencyInput";
import {
  ZhSelect,
  ZhTextInput,
  ZhDateInput,
} from "../../../components/zh/inputs";
import { parseDecimal } from "../../../lib/sanitizers";
import type {
  PriceListDto,
  CreatePriceListPayload,
  UpdatePriceListPayload,
} from "../api/pricingService";
import {
  priceListService,
  RULE_TYPE_OPTIONS,
  formatRuleGeneral,
} from "../api/pricingService";
import { PriceListExceptionsTab } from "./PriceListExceptionsTab";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";

import "../../../styles/shared/items-catalog.css";

type Tab = "resumen" | "listado" | "nuevo" | "excepciones";

export function PriceListsPage() {
  const [tab, setTab] = useState<Tab>("listado");
  const [items, setItems] = useState<PriceListDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<PriceListDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);

  // ── Form state ──
  const [fCode, setFCode] = useState("");
  const [fName, setFName] = useState("");
  const [fCurrency, setFCurrency] = useState("USD");
  const [fFrom, setFFrom] = useState("");
  const [fUntil, setFUntil] = useState("");
  const [fRuleType, setFRuleType] = useState("");
  const [fRuleValue, setFRuleValue] = useState("");

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const res = await priceListService.list(undefined, search || undefined);
      setItems(res);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudieron cargar las listas de precios." }),
      );
    }
    setLoading(false);
  }, [search]);

  useEffect(() => {
    fetchItems();
  }, [fetchItems]);

  const resetForm = () => {
    setFCode("");
    setFName("");
    setFCurrency("USD");
    setFFrom("");
    setFUntil("");
    setFRuleType("");
    setFRuleValue("");
    setEditing(null);
    setError("");
  };

  const startEdit = (pl: PriceListDto) => {
    setEditing(pl);
    setFName(pl.name);
    setFCurrency(pl.currencyCode);
    setFFrom(pl.validFrom ?? "");
    setFUntil(pl.validUntil ?? "");
    setFRuleType(pl.ruleType ?? "");
    setFRuleValue(pl.ruleValue != null ? String(pl.ruleValue) : "");
    setTab("nuevo");
  };

  const handleSave = async () => {
    setError("");

    // Regla general: si hay tipo seleccionado, el valor es obligatorio y debe ser numérico.
    let ruleType: string | null = null;
    let ruleValue: number | null = null;
    if (fRuleType) {
      const parsed = parseDecimal(fRuleValue);
      if (!fRuleValue.trim() || Number.isNaN(parsed)) {
        setError(
          "Debe ingresar un valor válido para la regla general seleccionada.",
        );
        return;
      }
      ruleType = fRuleType;
      ruleValue = parsed;
    }

    setSaving(true);
    try {
      if (editing) {
        // isDefault no se edita desde este catálogo — único punto de escritura:
        // Configuración → Ventas (priceListService.setDefault). Se reenvía sin cambios.
        const p: UpdatePriceListPayload = {
          id: editing.id,
          name: fName,
          currencyCode: fCurrency,
          isDefault: editing.isDefault,
          validFrom: fFrom || null,
          validUntil: fUntil || null,
          ruleType,
          ruleValue,
        };
        await priceListService.update(editing.id, p);
      } else {
        // Las listas nuevas nunca nacen predeterminadas — se promueven desde Configuración → Ventas.
        const p: CreatePriceListPayload = {
          code: fCode,
          name: fName,
          currencyCode: fCurrency,
          isDefault: false,
          validFrom: fFrom || null,
          validUntil: fUntil || null,
          ruleType,
          ruleValue,
        };
        await priceListService.create(p);
      }
      resetForm();
      setTab("listado");
      fetchItems();
    } catch (e: unknown) {
      const error = e as {
        response?: {
          data?: {
            message?: {
              user?: string;
            };
            data?: {
              errors?: string[];
            };
          };
        };
        message?: string;
      };

      const msg =
        error.response?.data?.message?.user ??
        error.response?.data?.data?.errors?.[0] ??
        error.message ??
        "Error al guardar.";
      setError(msg);
    }
    setSaving(false);
  };

  const handleToggle = async (pl: PriceListDto) => {
    if (togglingId) return;
    const confirmed = await message.confirm({
      title: pl.isActive ? `Desactivar "${pl.name}"` : `Activar "${pl.name}"`,
      message: pl.isActive
        ? `"${pl.name}" dejará de estar disponible para nuevas ventas. El histórico existente no se elimina.`
        : `"${pl.name}" volverá a estar disponible para nuevas ventas.`,
      variant: pl.isActive ? "danger" : "warning",
      confirmLabel: pl.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;
    setTogglingId(pl.id);
    try {
      if (pl.isActive) await priceListService.disable(pl.id);
      else await priceListService.enable(pl.id);
      await fetchItems();
      message.success(
        pl.isActive ? "Lista de precios desactivada correctamente." : "Lista de precios activada correctamente.",
      );
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cambiar el estado de la lista de precios." }),
      );
    } finally {
      setTogglingId(null);
    }
  };

  // ── Tab definitions ──
  // "Excepciones" solo tiene sentido con una lista ya cargada — administración de PricingRule
  // arranca siempre desde una PriceList concreta, nunca desde el Item (ver AI-RULES).
  const tabs: { id: Tab; label: string; icon: string }[] = [
    { id: "resumen", label: "Resumen", icon: "bar_chart_4_bars" },
    { id: "listado", label: "Listado", icon: "view_list" },
    {
      id: "nuevo",
      label: editing ? "Editar Lista" : "Nueva Lista",
      icon: editing ? "edit" : "add_box",
    },
    ...(editing
      ? [{ id: "excepciones" as Tab, label: "Excepciones", icon: "rule" }]
      : []),
  ];

  const preservesEditing = (id: Tab) => id === "nuevo" || id === "excepciones";

  const priceListColumns: ZHDataTableColumn<PriceListDto>[] = [
    { key: "code", header: "Código", render: (pl) => <span className="prd-td-code">{pl.code}</span> },
    { key: "name", header: "Nombre", render: (pl) => pl.name },
    { key: "currency", header: "Moneda", render: (pl) => pl.currencyCode },
    {
      key: "generalRule",
      header: "Regla General",
      render: (pl) => formatRuleGeneral(pl.ruleType, pl.ruleValue, pl.currencyCode),
    },
    {
      key: "default",
      header: "Default",
      render: (pl) => (pl.isDefault ? <Badge label="Predeterminada" variant="success" /> : null),
    },
    {
      // ZH-LISTING-GLOBAL-STANDARD-06: antes mostraba el texto literal "Estado" en vez del
      // estado real (mismo bug corregido en FinancialDestinationsPage — FINANCIAL-DESTINATIONS-STATUS-FIX-01).
      key: "status",
      header: "Estado",
      render: (pl) => <Badge label={pl.isActive ? "Activo" : "Inactivo"} variant={pl.isActive ? "success" : "neutral"} />,
    },
    {
      key: "actions",
      header: "Acciones",
      align: "right",
      render: (pl) => (
        <div className="prd-td-actions">
          <ZHIconButton
            icon="edit"
            title={`Editar lista de precios ${pl.code}`}
            ariaLabel={`Editar lista de precios ${pl.code}`}
            variant="primary"
            onClick={() => startEdit(pl)}
          />
          <ZHIconButton
            icon={pl.isActive ? "toggle_off" : "toggle_on"}
            title={pl.isActive ? `Desactivar lista de precios ${pl.code}` : `Activar lista de precios ${pl.code}`}
            ariaLabel={pl.isActive ? `Desactivar lista de precios ${pl.code}` : `Activar lista de precios ${pl.code}`}
            variant={pl.isActive ? "danger" : "success"}
            disabled={togglingId === pl.id}
            onClick={() => void handleToggle(pl)}
          />
        </div>
      ),
    },
  ];

  return (
    <ErpPageTemplate
      title="Listas de Precios"
      subtitle="Administra las listas de precios de la empresa."
    >
      {/* Tabs */}
      <div className="prd-tabs">
        {tabs.map((t) => (
          <button
            key={t.id}
            className={`prd-tab-btn ${tab === t.id ? "prd-tab-btn--active" : ""}`}
            onClick={() => {
              if (!preservesEditing(t.id)) resetForm();
              setTab(t.id);
            }}
          >
            <span className="material-symbols-outlined zh-icon-lg">
              {t.icon}
            </span>
            {t.label}
          </button>
        ))}
      </div>

      {/* RESUMEN */}
      {tab === "resumen" && (
        <div className="prd-section">
          <div className="prd-stat-grid">
            <StatCard label="Total listas" value={items.length} />
            <StatCard
              label="Activas"
              value={items.filter((i) => i.isActive).length}
            />
            <StatCard
              label="Predeterminada"
              value={items.find((i) => i.isDefault)?.name ?? "—"}
            />
          </div>
        </div>
      )}

      {/* LISTADO */}
      {tab === "listado" && (
        <div className="prd-section">
          <div className="prd-crud-toolbar">
            <ZhTextInput
              placeholder="Buscar por código o nombre..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <ZHBtn onClick={fetchItems} disabled={loading}>
              <span className="material-symbols-outlined zh-icon-lg">
                refresh
              </span>
            </ZHBtn>
          </div>

          <ZHDataTable
            columns={priceListColumns}
            rows={items}
            rowKey={(pl) => pl.id}
            loading={loading}
            showRowNumber
            emptyMessage='No hay listas de precios. Crea la primera en la pestaña "Nueva Lista".'
          />
        </div>
      )}

      {/* NUEVO / EDITAR */}
      {tab === "nuevo" && (
        <div className="prd-section">
          <h3 className="prd-crud-title">
            {editing ? `Editar: ${editing.code}` : "Nueva Lista de Precios"}
          </h3>
          {error && <div className="prd-error-banner">{error}</div>}

          <div className="zh-grid zh-grid--2 prd-form-grid">
            {!editing && (
              <div className="zh-field">
                <label className="zh-field-label">
                  Código <span className="zh-field-required">*</span>
                </label>
                <div className="zh-field-control">
                  <ZhTextInput
                    value={fCode}
                    onChange={(e) => setFCode(e.target.value.toUpperCase())}
                    maxLength={20}
                    placeholder="Ej: DEFAULT, MAYORISTA"
                    className="zh-input--upper"
                  />
                </div>
              </div>
            )}
            <div className="zh-field">
              <label className="zh-field-label">
                Nombre <span className="zh-field-required">*</span>
              </label>
              <div className="zh-field-control">
                <ZhTextInput
                  value={fName}
                  onChange={(e) => setFName(e.target.value)}
                  maxLength={120}
                  placeholder="Lista de precios general"
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Moneda</label>
              <div className="zh-field-control">
                <ZhTextInput
                  value={fCurrency}
                  onChange={(e) => setFCurrency(e.target.value.toUpperCase())}
                  maxLength={3}
                  placeholder="USD"
                  className="zh-input--upper"
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Vigencia desde</label>
              <div className="zh-field-control">
                <ZhDateInput
                  value={fFrom}
                  onChange={(e) => setFFrom(e.target.value)}
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Vigencia hasta</label>
              <div className="zh-field-control">
                <ZhDateInput
                  value={fUntil}
                  onChange={(e) => setFUntil(e.target.value)}
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Regla General</label>
              <div className="zh-field-control">
                <ZhSelect
                  value={fRuleType}
                  onChange={(e) => {
                    setFRuleType(e.target.value);
                    setFRuleValue("");
                  }}
                >
                  {RULE_TYPE_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </ZhSelect>
              </div>
            </div>
            {fRuleType === "PercentDiscount" && (
              <div className="zh-field">
                <label className="zh-field-label">
                  Valor (%) <span className="zh-field-required">*</span>
                </label>
                <div className="zh-field-control">
                  <ZhDecimalInput
                    value={fRuleValue}
                    onChange={(e) => setFRuleValue(e.target.value)}
                    decimals={2}
                    positiveOnly
                    placeholder="15"
                  />
                </div>
              </div>
            )}
            {fRuleType === "PercentMarkup" && (
              <div className="zh-field">
                <label className="zh-field-label">
                  Valor (%) <span className="zh-field-required">*</span>
                </label>
                <div className="zh-field-control">
                  <ZhDecimalInput
                    value={fRuleValue}
                    onChange={(e) => setFRuleValue(e.target.value)}
                    decimals={2}
                    positiveOnly
                    placeholder="8"
                  />
                </div>
              </div>
            )}
            {fRuleType === "FixedPrice" && (
              <div className="zh-field">
                <label className="zh-field-label">
                  Valor <span className="zh-field-required">*</span>
                </label>
                <div className="zh-field-control">
                  <ZhCurrencyInput
                    value={fRuleValue}
                    onChange={(e) => setFRuleValue(e.target.value)}
                    currency={fCurrency}
                    decimals={2}
                    placeholder="25"
                  />
                </div>
              </div>
            )}
            {fRuleType === "FixedAdjustment" && (
              <div className="zh-field">
                <label className="zh-field-label">
                  Valor ({fCurrency}){" "}
                  <span className="zh-field-required">*</span>
                </label>
                <div className="zh-field-control">
                  <ZhDecimalInput
                    value={fRuleValue}
                    onChange={(e) => setFRuleValue(e.target.value)}
                    decimals={2}
                    placeholder="3"
                  />
                </div>
              </div>
            )}
            <div className="zh-field">
              <label className="zh-field-label">Lista predeterminada</label>
              <div className="zh-field-control">
                {editing?.isDefault ? (
                  <Badge label="Predeterminada" variant="success" />
                ) : (
                  <span className="zh-text-muted zh-text-xs">No</span>
                )}
              </div>
            </div>
          </div>

          <p className="zh-text-muted zh-text-xs zh-mt-8">
            La lista predeterminada se asigna desde{" "}
            <Link to="/settings/company" className="zh-link">
              Configuración → Ventas
            </Link>
            .
          </p>

          <div className="prd-crud-actions">
            <ZHBtn onClick={handleSave} disabled={saving || !fName.trim()}>
              <span className="material-symbols-outlined zh-icon-lg">save</span>
              {saving ? "Guardando..." : editing ? "Actualizar" : "Crear"}
            </ZHBtn>
            <ZHBtn
              onClick={() => {
                resetForm();
                setTab("listado");
              }}
            >
              Cancelar
            </ZHBtn>
          </div>
        </div>
      )}

      {/* EXCEPCIONES (PricingRule) — solo con una lista cargada */}
      {tab === "excepciones" && editing && (
        <PriceListExceptionsTab priceList={editing} />
      )}
    </ErpPageTemplate>
  );
}

// ── Helpers ──────────────────────────────────────────────────────────────

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="prd-stat-card">
      <div className="prd-stat-card__value">{value}</div>
      <div className="prd-stat-card__label">{label}</div>
    </div>
  );
}





