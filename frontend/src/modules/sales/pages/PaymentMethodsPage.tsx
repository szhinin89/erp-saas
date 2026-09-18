import { useCallback, useEffect, useState } from "react";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhTextInput, ZhSelect } from "../../../components/zh/inputs";
import { Badge } from "../../../components/PageShell";
import type { PaymentMethodDto } from "../api/paymentMethodService";
import { paymentMethodService } from "../api/paymentMethodService";
import { sriLookupFacade } from "../../items/facades/sriLookupFacade";
import { accountingApi } from "../../accounting/api/accountingApi";
import { useAsync } from "../../../hooks/useAsync";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import "../../../styles/shared/items-catalog.css";

// DESTINOS-CONTABLES-COBROS-VENTAS-01: esta pantalla vive únicamente bajo Contabilidad >
// Configuración > Destinos contables > Cobros de ventas (único punto de acceso, ver
// AccountingModule.SalesCollectionDestinations) — ya no existe un acceso "Métodos de Pago" con
// CRUD completo del catálogo, así que la UI se enfoca exclusivamente en la asignación de cuenta
// contable por forma de cobro. Nombre/orden/SRI/requiere-referencia/permite-crédito son atributos
// del catálogo tenant-wide (PaymentMethod, gestionado por su propio ciclo de vida) y aquí se
// muestran solo de referencia, nunca editables. No se agrega endpoint ni pantalla nueva: mismo
// GET /api/v1/payment-methods y PUT /api/v1/payment-methods/{id}/account de siempre.
type Tab = "listado" | "cuenta";

export function PaymentMethodsPage() {
  const [tab, setTab] = useState<Tab>("listado");
  const [items, setItems] = useState<PaymentMethodDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<PaymentMethodDto | null>(null);
  const [togglingId, setTogglingId] = useState<string | null>(null);

  const [fAccountingAccountId, setFAccountingAccountId] = useState("");
  const [savingAccount, setSavingAccount] = useState(false);

  // SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01: catálogo real sri_payment_method — nunca códigos
  // hardcodeados en el frontend.
  const sriPaymentMethodsState = useAsync(() => sriLookupFacade.paymentMethods());
  const sriPaymentMethods = sriPaymentMethodsState.data ?? [];

  // SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — reutiliza accountingApi.listAccounts() (ya usado
  // por ChartOfAccountsPage) en vez de crear un fetch nuevo. Solo cuentas postables/activas son
  // seleccionables — mismo criterio que PostingAccountGuard en backend.
  const accountsState = useAsync(() => accountingApi.listAccounts());
  const postableAccounts = (accountsState.data ?? []).filter(
    (a) => a.isActive && a.allowsPosting,
  );

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
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudieron cargar las formas de cobro." }),
      );
    }
    setLoading(false);
  }, [search]);

  useEffect(() => {
    fetchItems();
  }, [fetchItems]);

  const resetForm = () => {
    setFAccountingAccountId("");
    setEditing(null);
  };

  const startConfigureAccount = (pm: PaymentMethodDto) => {
    if (pm.accountSource !== "PaymentMethodAccount") return;
    setEditing(pm);
    setFAccountingAccountId(pm.accountingAccountId ?? "");
    setTab("cuenta");
  };

  // SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01 — comando independiente del catálogo de métodos
  // (la cuenta es Company-scoped, distinto ciclo de vida que PaymentMethod, que es tenant-wide).
  const handleSaveAccount = async () => {
    if (!editing || !fAccountingAccountId) return;
    setSavingAccount(true);
    try {
      await paymentMethodService.setAccount(editing.id, fAccountingAccountId);
      await fetchItems();
      message.success("Cuenta contable configurada correctamente.");
      resetForm();
      setTab("listado");
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: "No se pudo configurar la cuenta contable.",
        }),
      );
    } finally {
      setSavingAccount(false);
    }
  };

  const handleToggle = async (pm: PaymentMethodDto) => {
    if (togglingId) return;
    const confirmed = await message.confirm({
      title: pm.isActive ? `Desactivar "${pm.name}"` : `Activar "${pm.name}"`,
      message: pm.isActive
        ? `"${pm.name}" dejará de estar disponible para nuevas operaciones (POS, ventas, cobros). El histórico existente no se elimina.`
        : `"${pm.name}" volverá a estar disponible para nuevas operaciones.`,
      variant: pm.isActive ? "danger" : "warning",
      confirmLabel: pm.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;
    setTogglingId(pm.id);
    try {
      await paymentMethodService.toggle(pm.id);
      await fetchItems();
      message.success(
        pm.isActive ? "Método de pago desactivado correctamente." : "Método de pago activado correctamente.",
      );
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cambiar el estado del método de pago." }),
      );
    } finally {
      setTogglingId(null);
    }
  };

  const tabs =
    tab === "cuenta" && editing
      ? [
          { id: "listado" as Tab, label: "Listado", icon: "view_list" },
          { id: "cuenta" as Tab, label: "Configurar cuenta", icon: "account_balance" },
        ]
      : [{ id: "listado" as Tab, label: "Listado", icon: "view_list" }];

  const sriLabel = (code: string | null) => {
    if (!code)
      return (
        <span className="zh-text-muted zh-text-xs">
          Sin mapear (usa default de empresa)
        </span>
      );
    const sri = sriPaymentMethods.find((s) => s.code === code);
    return `${code} — ${sri?.name ?? "?"}`;
  };

  const paymentMethodColumns: ZHDataTableColumn<PaymentMethodDto>[] = [
    {
      key: "name",
      header: "Forma de cobro",
      render: (pm) => (
        <>
          <span className="prd-td-code">{pm.code}</span>
          {" — "}
          <span>{pm.name}</span>
        </>
      ),
    },
    {
      key: "sriPaymentMethodCode",
      header: "Código SRI",
      render: (pm) => sriLabel(pm.sriPaymentMethodCode),
    },
    {
      key: "requiresReference",
      header: "Requiere Ref.",
      render: (pm) => <Badge variant={pm.requiresReference ? "info" : "neutral"} label={pm.requiresReference ? "Sí" : "No"} />,
    },
    {
      key: "accountingAccountId",
      header: "Cuenta Contable",
      render: (pm) => {
        switch (pm.accountSource) {
          case "CashRegister":
            return <span className="zh-text-muted zh-text-xs">Según caja registradora</span>;
          case "CompanyBankAccount":
            return <span className="zh-text-muted zh-text-xs">Según cuenta bancaria</span>;
          case "AccountingRule":
            return <span className="zh-text-muted zh-text-xs">Según regla contable</span>;
          case "PaymentMethodAccount": {
            if (!pm.accountingAccountId)
              return <Badge variant="error" label="Sin configurar" />;
            const acc = (accountsState.data ?? []).find(
              (a) => a.id === pm.accountingAccountId,
            );
            return acc ? `${acc.code} — ${acc.name}` : pm.accountingAccountId;
          }
        }
      },
    },
    {
      key: "status",
      header: "Estado",
      render: (pm) => <Badge variant={pm.isActive ? "success" : "error"} label={pm.isActive ? "Activo" : "Inactivo"} />,
    },
    {
      key: "actions",
      header: "Acciones",
      align: "right",
      render: (pm) => (
        <div className="prd-td-actions">
          {pm.accountSource === "PaymentMethodAccount" && (
            <ZHIconButton
              icon="account_balance"
              title={`Configurar cuenta contable de ${pm.name}`}
              ariaLabel={`Configurar cuenta contable de ${pm.name}`}
              variant="primary"
              onClick={() => startConfigureAccount(pm)}
            />
          )}
          <ZHIconButton
            icon={pm.isActive ? "toggle_off" : "toggle_on"}
            title={pm.isActive ? `Desactivar método de pago ${pm.code}` : `Activar método de pago ${pm.code}`}
            ariaLabel={pm.isActive ? `Desactivar método de pago ${pm.code}` : `Activar método de pago ${pm.code}`}
            variant={pm.isActive ? "danger" : "success"}
            disabled={togglingId === pm.id}
            onClick={() => void handleToggle(pm)}
          />
        </div>
      ),
    },
  ];

  return (
    <ErpPageTemplate
      title="Cobros de ventas"
      subtitle="Configura la cuenta contable que recibirá el débito por cada forma de cobro."
    >
      <ZHTabBar
        tabs={tabs}
        activeTab={tab}
        onChange={(id) => {
          if (id !== "cuenta") resetForm();
          setTab(id);
        }}
      />

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
            columns={paymentMethodColumns}
            rows={items}
            rowKey={(pm) => pm.id}
            loading={loading}
            showRowNumber
            emptyMessage="Sin formas de cobro registradas."
          />
        </div>
      )}

      {tab === "cuenta" && editing && (
        <div className="prd-section">
          <h3 className="prd-crud-title">Configurar cuenta contable: {editing.name}</h3>

          <div className="prd-crud-form-grid">
            <div className="zh-field">
              <label className="zh-field-label">Código</label>
              <div className="zh-field-control">
                <span className="prd-td-code">{editing.code}</span>
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Nombre</label>
              <div className="zh-field-control">{editing.name}</div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Requiere referencia</label>
              <div className="zh-field-control">
                <Badge variant={editing.requiresReference ? "info" : "neutral"} label={editing.requiresReference ? "Sí" : "No"} />
              </div>
            </div>
            <div className="zh-field">
              <label className="zh-field-label">Código SRI</label>
              <div className="zh-field-control">{sriLabel(editing.sriPaymentMethodCode)}</div>
            </div>
          </div>

          <div className="prd-section zh-mt-16">
            <p className="zh-text-muted zh-text-xs">
              Cuenta contable que recibirá el débito del dinero cobrado
              cuando se use esta forma de cobro en una venta. Sin esta
              configuración, ninguna venta con esta forma de cobro podrá
              emitirse.
            </p>
            <div className="prd-crud-form-grid">
              <div className="zh-field">
                <label className="zh-field-label">Cuenta contable</label>
                <div className="zh-field-control">
                  <ZhSelect
                    value={fAccountingAccountId}
                    onChange={(e) => setFAccountingAccountId(e.target.value)}
                    disabled={accountsState.loading}
                  >
                    <option value="">— Sin configurar —</option>
                    {postableAccounts.map((acc) => (
                      <option key={acc.id} value={acc.id}>
                        {acc.code} — {acc.name}
                      </option>
                    ))}
                  </ZhSelect>
                </div>
              </div>
            </div>
            <div className="prd-crud-actions">
              <ZHBtn
                onClick={handleSaveAccount}
                disabled={
                  savingAccount ||
                  !fAccountingAccountId ||
                  fAccountingAccountId === editing.accountingAccountId
                }
              >
                <span className="material-symbols-outlined zh-icon-lg">
                  save
                </span>
                {savingAccount ? "Guardando..." : "Guardar cuenta contable"}
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
        </div>
      )}
    </ErpPageTemplate>
  );
}
