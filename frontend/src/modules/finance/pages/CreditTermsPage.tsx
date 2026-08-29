import { useCallback, useEffect, useState } from "react";

import { Badge } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import {
  ZhDecimalInput,
  ZhTextInput,
  ZhSelect,
} from "../../../components/zh/inputs";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import type {
  CreditTermDto,
  CreateCreditTermPayload,
  UpdateCreditTermPayload,
  InstallmentInput,
} from "../api/creditTermService";
import {
  CREDIT_TERM_MODES,
  creditTermModeName,
  creditTermService,
} from "../api/creditTermService";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";

import "../../../styles/shared/items-catalog.css";

type Tab = "resumen" | "listado" | "nuevo";

export function CreditTermsPage() {
  const [tab, setTab] = useState<Tab>("listado");
  const [items, setItems] = useState<CreditTermDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<CreditTermDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  // ── Form state ──
  const [fCode, setFCode] = useState("");
  const [fName, setFName] = useState("");
  const [fMode, setFMode] = useState(1);
  const [fDays, setFDays] = useState(0);
  const [fInstallments, setFInstallments] = useState<InstallmentInput[]>([]);

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await creditTermService.list(undefined, search || undefined));
    } catch {
      /* */
    }
    setLoading(false);
  }, [search]);

  useEffect(() => {
    fetchItems();
  }, [fetchItems]);

  const resetForm = () => {
    setFCode("");
    setFName("");
    setFMode(1);
    setFDays(0);
    setFInstallments([]);
    setEditing(null);
    setError("");
  };

  const startEdit = (ct: CreditTermDto) => {
    setEditing(ct);
    setFName(ct.name);
    setFMode(
      CREDIT_TERM_MODES.find((m) => m.label === ct.mode)?.value ??
        (parseInt(ct.mode) || 1),
    );
    setFDays(ct.totalDays);
    setFInstallments(
      ct.installments.map((i) => ({
        number: i.installmentNumber,
        daysOffset: i.daysOffset,
        percentage: i.percentage,
      })),
    );
    setTab("nuevo");
  };

  const handleSave = async () => {
    setError("");
    setSaving(true);
    try {
      const inst = fInstallments.length > 0 ? fInstallments : undefined;
      if (editing) {
        const p: UpdateCreditTermPayload = {
          id: editing.id,
          name: fName,
          mode: fMode,
          totalDays: fDays,
          installments: inst,
        };
        await creditTermService.update(editing.id, p);
      } else {
        const p: CreateCreditTermPayload = {
          code: fCode,
          name: fName,
          mode: fMode,
          totalDays: fDays,
          installments: inst,
        };
        await creditTermService.create(p);
      }
      resetForm();
      setTab("listado");
      fetchItems();
    } catch (e: unknown) {
      const error = e as {
        response?: {
          data?: {
            message?: { user?: string };
            data?: { errors?: string[] };
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

  const handleToggle = async (ct: CreditTermDto) => {
    const confirmed = await message.confirm({
      title: ct.isActive ? `Desactivar "${ct.name}"` : `Activar "${ct.name}"`,
      message: ct.isActive
        ? `"${ct.name}" dejará de estar disponible para nuevas operaciones a crédito. El histórico existente no se elimina.`
        : `"${ct.name}" volverá a estar disponible para nuevas operaciones a crédito.`,
      variant: ct.isActive ? "danger" : "warning",
      confirmLabel: ct.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;
    try {
      if (ct.isActive) {
        await creditTermService.disable(ct.id);
      } else {
        await creditTermService.enable(ct.id);
      }
      await fetchItems();
      message.success(
        ct.isActive ? "Plazo de crédito desactivado correctamente." : "Plazo de crédito activado correctamente.",
      );
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cambiar el estado del plazo de crédito." }),
      );
    }
  };

  // ── Installment helpers ──
  const addInstallment = () =>
    setFInstallments((prev) => [
      ...prev,
      { number: prev.length + 1, daysOffset: fDays, percentage: 0 },
    ]);
  const removeInstallment = (idx: number) =>
    setFInstallments((prev) =>
      prev
        .filter((_, i) => i !== idx)
        .map((inst, i) => ({ ...inst, number: i + 1 })),
    );
  const updateInstallment = (
    idx: number,
    field: keyof InstallmentInput,
    value: number,
  ) =>
    setFInstallments((prev) =>
      prev.map((inst, i) => (i === idx ? { ...inst, [field]: value } : inst)),
    );
  const installmentSum = fInstallments.reduce((s, i) => s + i.percentage, 0);

  const isFinancialStrict = fMode === 2;

  const tabs = [
    { id: "resumen" as Tab, label: "Resumen", icon: "bar_chart_4_bars" },
    { id: "listado" as Tab, label: "Listado", icon: "view_list" },
    {
      id: "nuevo" as Tab,
      label: editing ? "Editar" : "Nueva Condición",
      icon: editing ? "edit" : "add_box",
    },
  ];

  return (
    <ErpPageTemplate
      title="Condiciones de Crédito"
      subtitle="Administra los plazos y cuotas de pago."
    >
      <div className="prd-tabs">
        {tabs.map((t) => (
          <button
            key={t.id}
            className={`prd-tab-btn ${tab === t.id ? "prd-tab-btn--active" : ""}`}
            onClick={() => {
              if (t.id !== "nuevo") resetForm();
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
            <StatCard label="Total condiciones" value={items.length} />
            <StatCard
              label="Activas"
              value={items.filter((i) => i.isActive).length}
            />
            <StatCard
              label="Operativas"
              value={items.filter((i) => i.mode === "Operational").length}
            />
            <StatCard
              label="Financieras"
              value={items.filter((i) => i.mode === "FinancialStrict").length}
            />
          </div>
        </div>
      )}

      {/* LISTADO */}
      {tab === "listado" && (
        <div className="prd-section">
          <div className="prd-crud-toolbar">
            <ZhTextInput
              placeholder="Buscar..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <ZHBtn onClick={fetchItems} disabled={loading}>
              <span className="material-symbols-outlined zh-icon-lg">
                refresh
              </span>
            </ZHBtn>
          </div>
          {loading ? (
            <p>Cargando...</p>
          ) : (
            <table className="prd-crud-table">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Nombre</th>
                  <th>Modo</th>
                  <th>Días</th>
                  <th>Cuotas</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((ct) => (
                  <tr key={ct.id}>
                    <td className="prd-td-code">{ct.code}</td>
                    <td>{ct.name}</td>
                    <td>{creditTermModeName(ct.mode)}</td>
                    <td>{ct.totalDays}</td>
                    <td>{ct.installments.length}</td>
                    <td>
                      <Badge label={"Estado"} variant="neutral" />
                    </td>
                    <td className="prd-td-actions">
                      <ZHIconButton
                        icon="edit"
                        title="Editar"
                        variant="primary"
                        onClick={() => startEdit(ct)}
                      />
                      <ZHIconButton
                        icon={ct.isActive ? "toggle_off" : "toggle_on"}
                        title={ct.isActive ? "Desactivar" : "Activar"}
                        variant={ct.isActive ? "danger" : "success"}
                        onClick={() => handleToggle(ct)}
                      />
                    </td>
                  </tr>
                ))}
                {items.length === 0 && (
                  <tr className="prd-empty-row">
                    <td colSpan={7}>Sin condiciones de crédito.</td>
                  </tr>
                )}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* NUEVO / EDITAR */}
      {tab === "nuevo" && (
        <div className="prd-section">
          <h3 className="prd-crud-title">
            {editing ? `Editar: ${editing.code}` : "Nueva Condición de Crédito"}
          </h3>
          {error && <div className="prd-error-banner">{error}</div>}

          <div className="prd-crud-form-grid">
            {!editing && (
              <FormField label="Código" required>
                <ZhTextInput
                  mode="uppercase"
                  value={fCode}
                  onChange={(e) => setFCode(e.target.value)}
                  maxLength={20}
                  placeholder="Ej: CONTADO, NET30"
                />
              </FormField>
            )}
            <FormField label="Nombre" required>
              <ZhTextInput
                value={fName}
                onChange={(e) => setFName(e.target.value)}
                maxLength={120}
                placeholder="Contado / Crédito 30 días"
              />
            </FormField>
            <FormField label="Modo">
              <ZhSelect
                value={fMode}
                onChange={(e) => setFMode(Number(e.target.value))}
              >
                {CREDIT_TERM_MODES.map((m) => (
                  <option key={m.value} value={m.value}>
                    {m.label}
                  </option>
                ))}
              </ZhSelect>
            </FormField>
            {/* fDays: NO migrado — ver reporte 14A-2 (input type="number" con
                min nativo y sin Zod/RHF equivalente; ZhNumberInput no soporta
                min/max). */}
            <FormField label="Plazo total (días)">
              <input
                type="number"
                min={0}
                value={fDays}
                onChange={(e) => setFDays(Number(e.target.value))}
              />
            </FormField>
          </div>

          {/* Installments */}
          {isFinancialStrict && (
            <div className="zh-mt-16">
              <div className="zh-flex-between zh-mb-12">
                <h4>
                  Cuotas{" "}
                  <span className={`ct-installment-sum ${installmentSum === 100 ? "ct-installment-sum--ok" : "ct-installment-sum--error"}`}>
                    (Suma: {installmentSum.toFixed(2)}%)
                  </span>
                </h4>
                <ZHBtn onClick={addInstallment}>
                  <span className="material-symbols-outlined zh-icon-md">
                    add
                  </span>{" "}
                  Agregar cuota
                </ZHBtn>
              </div>
              {fInstallments.map((inst, idx) => (
                <div
                  key={idx}
                  className="zh-flex-end zh-gap-12 zh-mb-8 ct-installment-row"
                >
                  <span className="ct-installment-number">
                    #{inst.number}
                  </span>
                  {/* daysOffset: NO migrado — ver reporte 14A-2 (min/max
                      nativo con max dinámico por fila = fDays; sin Zod/RHF
                      equivalente que reemplace ese límite). */}
                  <FormField label="Días offset">
                    <input
                      type="number"
                      min={0}
                      max={fDays}
                      value={inst.daysOffset}
                      onChange={(e) =>
                        updateInstallment(
                          idx,
                          "daysOffset",
                          Number(e.target.value),
                        )
                      }
                      className="ct-installment-days-input"
                    />
                  </FormField>
                  <FormField label="Porcentaje (%)">
                    <ZhDecimalInput
                      decimals={2}
                      positiveOnly
                      defaultValue={inst.percentage}
                      onBlur={(e) =>
                        updateInstallment(
                          idx,
                          "percentage",
                          Number(e.target.value) || 0,
                        )
                      }
                      className="ct-installment-percent-input"
                    />
                  </FormField>
                  <ZHIconButton
                      icon="delete"
                      title="Eliminar cuota"
                      variant="danger"
                      onClick={() => removeInstallment(idx)}
                    />
                </div>
              ))}
              {fInstallments.length === 0 && (
                <p className="zh-text-muted">
                  Agrega al menos una cuota para el modo Financiero Estricto.
                </p>
              )}
            </div>
          )}

          <div className="prd-crud-actions">
            <ZHBtn
              onClick={handleSave}
              disabled={
                saving ||
                !fName.trim() ||
                (isFinancialStrict && installmentSum !== 100)
              }
            >
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
    </ErpPageTemplate>
  );
}

// ── Helpers ──────────────────────────────────────────────────────────────

function FormField({
  label,
  required,
  children,
}: {
  label: string;
  required?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="zh-field">
      {label && (
        <label className="zh-field-label">
          {label}
          {required && <span className="zh-field-required"> *</span>}
        </label>
      )}
      <div className="zh-field-control">{children}</div>
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="prd-stat-card">
      <div className="prd-stat-card__value">{value}</div>
      <div className="prd-stat-card__label">{label}</div>
    </div>
  );
}






