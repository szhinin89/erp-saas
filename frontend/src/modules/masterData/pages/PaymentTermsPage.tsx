import { useCallback, useEffect, useState } from "react";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import type {
  PaymentTermDto,
  CreatePaymentTermPayload,
  UpdatePaymentTermPayload,
} from "../api/paymentTermService";
import { paymentTermService } from "../api/paymentTermService";
import "../../../styles/shared/items-catalog.css";

type Tab = "listado" | "nuevo";

export function PaymentTermsPage() {
  const [tab, setTab] = useState<Tab>("listado");
  const [items, setItems] = useState<PaymentTermDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<PaymentTermDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [fCode, setFCode] = useState("");
  const [fName, setFName] = useState("");
  const [fInstallments, setFInstallments] = useState(1);
  const [fDays, setFDays] = useState(0);

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await paymentTermService.list(search || undefined));
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
    setFInstallments(1);
    setFDays(0);
    setEditing(null);
    setError("");
  };

  const startEdit = (pt: PaymentTermDto) => {
    setEditing(pt);
    setFName(pt.name);
    setFInstallments(pt.installments);
    setFDays(pt.daysBetweenInstallments);
    setTab("nuevo");
  };

  const handleSave = async () => {
    setError("");
    setSaving(true);
    try {
      if (editing) {
        const p: UpdatePaymentTermPayload = {
          id: editing.id,
          name: fName,
          installments: fInstallments,
          daysBetweenInstallments: fDays,
        };
        await paymentTermService.update(editing.id, p);
      } else {
        const p: CreatePaymentTermPayload = {
          code: fCode,
          name: fName,
          installments: fInstallments,
          daysBetweenInstallments: fDays,
        };
        await paymentTermService.create(p);
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

  const handleToggle = async (pt: PaymentTermDto) => {
    try {
      if (pt.isActive) {
        await paymentTermService.disable(pt.id);
      } else {
        await paymentTermService.enable(pt.id);
      }
      fetchItems();
    } catch {
      /* */
    }
  };

  const previewSummary =
    fInstallments === 1 && fDays === 0
      ? "Contado"
      : fInstallments === 1
        ? `${fDays} días`
        : `${fInstallments}x${fDays}`;

  const previewTotalDays =
    fInstallments <= 1 ? fDays : (fInstallments - 1) * fDays;

  const tabs = [
    { id: "listado" as Tab, label: "Listado", icon: "view_list" },
    {
      id: "nuevo" as Tab,
      label: editing ? "Editar" : "Nueva Condición",
      icon: editing ? "edit" : "add_box",
    },
  ];

  return (
    <ErpPageTemplate
      title="Condiciones de Pago"
      subtitle="Plazos y cuotas para proveedores."
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

      {tab === "listado" && (
        <div className="prd-section">
          <div className="prd-crud-toolbar">
            <input
              type="text"
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
          {loading ? (
            <p>Cargando...</p>
          ) : (
            <table className="prd-crud-table">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Nombre</th>
                  <th>Cuotas</th>
                  <th>Días entre cuotas</th>
                  <th>Total días</th>
                  <th>Resumen</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((pt) => (
                  <tr key={pt.id}>
                    <td className="prd-td-code">{pt.code}</td>
                    <td>{pt.name}</td>
                    <td>{pt.installments}</td>
                    <td>{pt.daysBetweenInstallments}</td>
                    <td>{pt.totalDays}</td>
                    <td>
                      <span className="pf-badge pf-badge--info">
                        {pt.summary}
                      </span>
                    </td>
                    <td>
                      <span
                        className={`prd-status-badge ${pt.isActive ? "prd-status-badge--active" : "prd-status-badge--inactive"}`}
                      >
                        {pt.isActive ? "Activa" : "Inactiva"}
                      </span>
                    </td>
                    <td className="prd-td-actions">
                      <ZHIconButton
                        icon="edit"
                        title="Editar"
                        variant="primary"
                        onClick={() => startEdit(pt)}
                      />
                      <ZHIconButton
                          icon={pt.isActive ? "toggle_off" : "toggle_on"}
                          title={pt.isActive ? "Desactivar" : "Activar"}
                          variant={pt.isActive ? "danger" : "success"}
                          onClick={() => handleToggle(pt)}
                        />
                    </td>
                  </tr>
                ))}
                {items.length === 0 && (
                  <tr className="prd-empty-row">
                    <td colSpan={8}>Sin condiciones de pago.</td>
                  </tr>
                )}
              </tbody>
            </table>
          )}
        </div>
      )}

      {tab === "nuevo" && (
        <div className="prd-section">
          <h3 className="prd-crud-title">
            {editing ? `Editar: ${editing.code}` : "Nueva Condición de Pago"}
          </h3>
          {error && <div className="prd-error-banner">{error}</div>}

          <div className="prd-crud-form-grid">
            {!editing && (
              <div className="zh-field">
                <label className="zh-field-label">
                  Código <span className="zh-field-required">*</span>
                </label>
                <div className="zh-field-control">
                  <input
                    value={fCode}
                    onChange={(e) => setFCode(e.target.value.toUpperCase())}
                    maxLength={20}
                    placeholder="CONTADO, NET30, 3X30"
                  />
                </div>
              </div>
            )}
            <div className="zh-field">
              <label className="zh-field-label">
                Nombre <span className="zh-field-required">*</span>
              </label>
              <div className="zh-field-control">
                <input
                  value={fName}
                  onChange={(e) => setFName(e.target.value)}
                  maxLength={120}
                  placeholder="Contado / Crédito 30 días"
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">
                Número de cuotas <span className="zh-field-required">*</span>
              </label>
              <div className="zh-field-control">
                <input
                  type="number"
                  min={1}
                  max={60}
                  value={fInstallments}
                  onChange={(e) => setFInstallments(Number(e.target.value))}
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">
                Días entre cuotas <span className="zh-field-required">*</span>
              </label>
              <div className="zh-field-control">
                <input
                  type="number"
                  min={0}
                  value={fDays}
                  onChange={(e) => setFDays(Number(e.target.value))}
                />
              </div>
            </div>
          </div>

          {/* Preview */}
          <div className="pf-collapsible md-payment-term-preview">
            <div className="pf-collapsible__body md-payment-term-preview__body">
              <ZHField density="compact" label="Resumen">
                <div className="md-payment-term-preview__summary">
                  {previewSummary}
                </div>
              </ZHField>
              <ZHField density="compact" label="Total días">
                <div className="md-payment-term-preview__total">
                  {previewTotalDays}
                </div>
              </ZHField>
            </div>
          </div>

          <div className="prd-crud-actions">
            <ZHBtn
              onClick={handleSave}
              disabled={
                saving ||
                !fName.trim() ||
                fInstallments < 1 ||
                (!editing && !fCode.trim())
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

