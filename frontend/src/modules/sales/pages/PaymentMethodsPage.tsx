import { useCallback, useEffect, useState } from "react";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { Badge } from "../../../components/PageShell";
import type {
  PaymentMethodDto,
  PaymentMethodDetailType,
  CreatePaymentMethodPayload,
  UpdatePaymentMethodPayload,
} from "../api/paymentMethodService";
import { paymentMethodService } from "../api/paymentMethodService";
import "../../../styles/shared/items-catalog.css";

type Tab = "listado" | "nuevo";

const DETAIL_TYPE_OPTIONS: { value: PaymentMethodDetailType; label: string }[] =
  [
    { value: "None", label: "Ninguno (sin detalle adicional)" },
    { value: "Card", label: "Tarjeta" },
    { value: "Transfer", label: "Transferencia" },
    { value: "Check", label: "Cheque" },
  ];

export function PaymentMethodsPage() {
  const [tab, setTab] = useState<Tab>("listado");
  const [items, setItems] = useState<PaymentMethodDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<PaymentMethodDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [fCode, setFCode] = useState("");
  const [fName, setFName] = useState("");
  const [fRequiresRef, setFRequiresRef] = useState(false);
  const [fIsCredit, setFIsCredit] = useState(false);
  const [fSortOrder, setFSortOrder] = useState(0);
  const [fDetailType, setFDetailType] =
    useState<PaymentMethodDetailType>("None");

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const all = await paymentMethodService.list(false);
      const filtered = search
        ? all.filter(
            (pm) =>
              pm.code.toLowerCase().includes(search.toLowerCase()) ||
              pm.name.toLowerCase().includes(search.toLowerCase()),
          )
        : all;
      setItems(filtered);
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
    setFRequiresRef(false);
    setFIsCredit(false);
    setFSortOrder(0);
    setFDetailType("None");
    setEditing(null);
    setError("");
  };

  const startEdit = (pm: PaymentMethodDto) => {
    setEditing(pm);
    setFName(pm.name);
    setFRequiresRef(pm.requiresReference);
    setFIsCredit(pm.isCreditAllowed);
    setFSortOrder(pm.sortOrder);
    setFDetailType(pm.detailType);
    setTab("nuevo");
  };

  const handleSave = async () => {
    setError("");
    setSaving(true);
    try {
      if (editing) {
        const p: UpdatePaymentMethodPayload = {
          id: editing.id,
          name: fName,
          requiresReference: fRequiresRef,
          isCreditAllowed: fIsCredit,
          sortOrder: fSortOrder,
          detailType: fDetailType,
        };
        await paymentMethodService.update(editing.id, p);
      } else {
        const p: CreatePaymentMethodPayload = {
          code: fCode,
          name: fName,
          requiresReference: fRequiresRef,
          isCreditAllowed: fIsCredit,
          sortOrder: fSortOrder,
          detailType: fDetailType,
        };
        await paymentMethodService.create(p);
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

  const handleToggle = async (pm: PaymentMethodDto) => {
    try {
      await paymentMethodService.toggle(pm.id);
      fetchItems();
    } catch {
      /* */
    }
  };

  const tabs = [
    { id: "listado" as Tab, label: "Listado", icon: "view_list" },
    {
      id: "nuevo" as Tab,
      label: editing ? "Editar" : "Nuevo MÃƒÂ©todo",
      icon: editing ? "edit" : "add_box",
    },
  ];

  return (
    <ErpPageTemplate
      title="MÃƒÂ©todos de Pago"
      subtitle="Formas de cobro y pago disponibles en la empresa."
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
              placeholder="Buscar por cÃƒÂ³digo o nombre..."
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
                  <th>CÃƒÂ³digo</th>
                  <th>Nombre</th>
                  <th>Requiere Ref.</th>
                  <th>CrÃƒÂ©dito</th>
                  <th>Detalle</th>
                  <th>Orden</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((pm) => (
                  <tr key={pm.id}>
                    <td className="prd-td-code">{pm.code}</td>
                    <td>{pm.name}</td>
                    <td>
                      <Badge
                        variant={pm.requiresReference ? "info" : "neutral"}
                        label={pm.requiresReference ? "SÃƒÂ­" : "No"}
                      />
                    </td>
                    <td>
                      <Badge
                        variant={pm.isCreditAllowed ? "warning" : "neutral"}
                        label={pm.isCreditAllowed ? "SÃƒÂ­" : "No"}
                      />
                    </td>
                    <td>
                      {DETAIL_TYPE_OPTIONS.find(
                        (o) => o.value === pm.detailType,
                      )?.label ?? pm.detailType}
                    </td>
                    <td>{pm.sortOrder}</td>
                    <td>
                      <Badge
                        variant={pm.isActive ? "success" : "error"}
                        label={pm.isActive ? "Activo" : "Inactivo"}
                      />
                    </td>
                    <td className="prd-td-actions">
                      <ZHIconButton
                        icon="edit"
                        title="Editar"
                        variant="primary"
                        onClick={() => startEdit(pm)}
                      />
                      <ZHIconButton
                        icon={pm.isActive ? "toggle_off" : "toggle_on"}
                        title={pm.isActive ? "Desactivar" : "Activar"}
                        variant={pm.isActive ? "danger" : "success"}
                        onClick={() => handleToggle(pm)}
                      />
                    </td>
                  </tr>
                ))}
                {items.length === 0 && (
                  <tr className="prd-empty-row">
                    <td colSpan={8}>Sin mÃƒÂ©todos de pago registrados.</td>
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
            {editing ? `Editar: ${editing.code}` : "Nuevo MÃƒÂ©todo de Pago"}
          </h3>
          {error && <div className="prd-error-banner">{error}</div>}

          <div className="prd-crud-form-grid">
            {!editing && (
              <div className="zh-field">
                <label className="zh-field-label">
                  CÃƒÂ³digo <span className="zh-field-required">*</span>
                </label>
                <div className="zh-field-control">
                  <input
                    value={fCode}
                    onChange={(e) => setFCode(e.target.value.toUpperCase())}
                    maxLength={20}
                    placeholder="EFECTIVO, TARJETA, TRANSFERENCIA"
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
                  maxLength={100}
                  placeholder="Efectivo / Tarjeta de crÃƒÂ©dito"
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Orden de visualizaciÃƒÂ³n</label>
              <div className="zh-field-control">
                <input
                  type="number"
                  min={0}
                  value={fSortOrder}
                  onChange={(e) => setFSortOrder(Number(e.target.value))}
                />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Esquema de detalle</label>
              <div className="zh-field-control">
                <select
                  value={fDetailType}
                  onChange={(e) =>
                    setFDetailType(e.target.value as PaymentMethodDetailType)
                  }
                >
                  {DETAIL_TYPE_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          <div className="pf-collapsible zh-mt-16 pm-flags-panel">
            <label className="zh-checkbox-label pm-checkbox-option">
              <input
                type="checkbox"
                checked={fRequiresRef}
                onChange={(e) => setFRequiresRef(e.target.checked)}
              />
              <div>
                <div className="pm-checkbox-title">Requiere referencia</div>
                <div className="zh-text-muted zh-text-xs">
                  Solicita datos adicionales (nro. comprobante, banco, etc.)
                </div>
              </div>
            </label>

            <label className="zh-checkbox-label pm-checkbox-option">
              <input
                type="checkbox"
                checked={fIsCredit}
                onChange={(e) => setFIsCredit(e.target.checked)}
              />
              <div>
                <div className="pm-checkbox-title">Permite crÃƒÂ©dito</div>
                <div className="zh-text-muted zh-text-xs">
                  Habilita la simulaciÃƒÂ³n de cuotas al usarse
                </div>
              </div>
            </label>
          </div>

          <div className="prd-crud-actions">
            <ZHBtn
              onClick={handleSave}
              disabled={saving || !fName.trim() || (!editing && !fCode.trim())}
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

