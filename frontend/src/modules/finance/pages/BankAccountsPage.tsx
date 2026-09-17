import { useCallback, useEffect, useState } from "react";

import { Badge } from "../../../components/PageShell";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { apiGet } from "../../lib/apiEnvelope";
import { bankService, type BankDto } from "../../settings/banks/api/bankService";
import { bankAccountService, type CompanyBankAccountDto } from "../api/bankAccountService";
import {
  createBankAccountSchema,
  editBankAccountSchema,
  emptyCreateBankAccountForm,
  type CreateBankAccountFormValues,
  type EditBankAccountFormValues,
} from "../schemas/bankAccountSchema";

import "../../../styles/shared/items-catalog.css";

interface AccountOption {
  id: string;
  code: string;
  name: string;
  allowsPosting: boolean;
  isActive: boolean;
}

const ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Checking: "Corriente",
  Savings: "Ahorros",
  Other: "Otro",
};

/**
 * TREASURY-BANK-ACCOUNTS-01: CRUD básico + activar/desactivar de cuentas bancarias de empresa
 * (Tesorería → Bancos → Cuentas bancarias). Banco siempre seleccionado desde el catálogo maestro
 * `Bank` (BANK-CATALOG-01) — nunca texto libre. Sigue el patrón `ConfigTabsLayout` obligatorio
 * (Master Configuration UI CLOSED), mismo criterio que `FinancialDestinationsPage.tsx`.
 */
export function BankAccountsPage() {
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");
  const [items, setItems] = useState<CompanyBankAccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState<CompanyBankAccountDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [banks, setBanks] = useState<BankDto[]>([]);
  const [accounts, setAccounts] = useState<AccountOption[]>([]);

  const createForm = useForm<CreateBankAccountFormValues>({
    resolver: zodResolver(createBankAccountSchema),
    defaultValues: emptyCreateBankAccountForm(),
  });

  const editForm = useForm<EditBankAccountFormValues>({
    resolver: zodResolver(editBankAccountSchema),
    defaultValues: { displayName: "", accountingAccountId: "" },
  });

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const all = await bankAccountService.list();
      setItems(all);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el listado de cuentas bancarias." }),
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchItems();
    bankService
      .list(true)
      .then(setBanks)
      .catch(() => setBanks([]));
    apiGet<AccountOption[]>("/api/v1/accounting/accounts")
      .then((list) => setAccounts(list.filter((a) => a.allowsPosting && a.isActive)))
      .catch(() => setAccounts([]));
  }, [fetchItems]);

  const bankName = (bankId: string) => {
    const bank = banks.find((b) => b.id === bankId);
    return bank ? bank.name : bankId;
  };

  const accountLabel = (accountingAccountId: string) => {
    const account = accounts.find((a) => a.id === accountingAccountId);
    return account ? `${account.code} — ${account.name}` : accountingAccountId;
  };

  const openCreate = () => {
    setEditing(null);
    createForm.reset(emptyCreateBankAccountForm());
    setSaveError("");
    setActiveTab("editor");
  };

  const openEdit = (a: CompanyBankAccountDto) => {
    setEditing(a);
    editForm.reset({ displayName: a.displayName, accountingAccountId: a.accountingAccountId });
    setSaveError("");
    setActiveTab("editor");
  };

  const handleCancel = () => {
    setEditing(null);
    setSaveError("");
    setActiveTab("list");
  };

  const onCreateValid = createForm.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      await bankAccountService.create({
        bankId: values.bankId,
        accountType: values.accountType,
        accountNumber: values.accountNumber,
        displayName: values.displayName,
        accountingAccountId: values.accountingAccountId,
      });
      message.success("Cuenta bancaria creada correctamente.");
      setActiveTab("list");
      void fetchItems();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, createForm.setError, (msg) => setSaveError(msg));
      if (!applied) {
        setSaveError(formatApiRequestError(err, { generic: "No se pudo crear la cuenta bancaria." }));
      }
    } finally {
      setSaving(false);
    }
  });

  const onEditValid = editForm.handleSubmit(async (values) => {
    if (!editing) return;
    setSaveError("");
    setSaving(true);
    try {
      await bankAccountService.update(editing.id, {
        displayName: values.displayName,
        accountingAccountId: values.accountingAccountId,
      });
      message.success("Cuenta bancaria actualizada correctamente.");
      setActiveTab("list");
      setEditing(null);
      void fetchItems();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, editForm.setError, (msg) => setSaveError(msg));
      if (!applied) {
        setSaveError(
          formatApiRequestError(err, { generic: "No se pudo actualizar la cuenta bancaria." }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  const handleToggle = async (a: CompanyBankAccountDto) => {
    if (togglingId) return;
    const confirmed = await message.confirm({
      title: a.isActive ? `Desactivar "${a.displayName}"` : `Activar "${a.displayName}"`,
      message: a.isActive
        ? `"${a.displayName}" dejará de estar disponible para su uso. El histórico existente no se elimina.`
        : `"${a.displayName}" volverá a estar disponible para su uso.`,
      variant: a.isActive ? "danger" : "warning",
      confirmLabel: a.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;
    setTogglingId(a.id);
    try {
      if (a.isActive) await bankAccountService.disable(a.id);
      else await bankAccountService.enable(a.id);
      message.success(a.isActive ? "Cuenta desactivada." : "Cuenta activada.");
      void fetchItems();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo cambiar el estado." }));
    } finally {
      setTogglingId(null);
    }
  };

  const editorLabel = editing ? "Editar Cuenta Bancaria" : "Nueva Cuenta Bancaria";

  const columns: ZHDataTableColumn<CompanyBankAccountDto>[] = [
    { key: "bank", header: "Banco", render: (a) => bankName(a.bankId) },
    {
      key: "type",
      header: "Tipo",
      render: (a) => ACCOUNT_TYPE_LABELS[a.accountType] ?? a.accountType,
    },
    { key: "number", header: "Número", render: (a) => <code className="prd-sku">{a.accountNumber}</code> },
    { key: "alias", header: "Alias", render: (a) => a.displayName },
    { key: "account", header: "Cuenta contable", render: (a) => accountLabel(a.accountingAccountId) },
    {
      key: "status",
      header: "Estado",
      render: (a) => (
        <Badge label={a.isActive ? "Activo" : "Inactivo"} variant={a.isActive ? "success" : "neutral"} />
      ),
    },
    {
      key: "actions",
      header: "Acciones",
      render: (a) => (
        <div className="prd-row-actions">
          <ZHBtn type="button" variant="ghost" size="sm" onClick={() => openEdit(a)}>
            <span className="material-symbols-outlined zh-icon-lg">edit</span>
          </ZHBtn>
          <ZHBtn
            type="button"
            variant="ghost"
            size="sm"
            disabled={togglingId === a.id}
            onClick={() => void handleToggle(a)}
            title={a.isActive ? "Desactivar" : "Activar"}
          >
            <span className="material-symbols-outlined zh-icon-lg">
              {a.isActive ? "toggle_on" : "toggle_off"}
            </span>
          </ZHBtn>
        </div>
      ),
    },
  ];

  const listContent = (
    <ZHDataTable
      columns={columns}
      rows={items}
      rowKey={(a) => a.id}
      loading={loading}
      showRowNumber
      rowClassName={(a) => (a.isActive ? undefined : "prd-row--inactive")}
      emptyMessage="Sin cuentas bancarias registradas."
    />
  );

  const editorContent = editing ? (
    <div className="pg-section-body">
      {saveError && <ZHPageNotice variant="error" message={saveError} />}
      <ZHGrid cols={2}>
        <ZHField label="Banco" readOnly>
          <ZhTextInput value={bankName(editing.bankId)} disabled />
        </ZHField>
        <ZHField label="Tipo" readOnly>
          <ZhTextInput
            value={ACCOUNT_TYPE_LABELS[editing.accountType] ?? editing.accountType}
            disabled
          />
        </ZHField>
        <ZHField label="Número de cuenta" readOnly>
          <ZhTextInput value={editing.accountNumber} disabled />
        </ZHField>
        <ZHField
          label="Alias / Nombre visible"
          required
          fieldError={editForm.formState.errors.displayName?.message}
        >
          <ZhTextInput
            className="zh-input"
            maxLength={200}
            disabled={saving}
            {...editForm.register("displayName")}
          />
        </ZHField>
        <ZHField
          label="Cuenta contable"
          required
          fieldError={editForm.formState.errors.accountingAccountId?.message}
        >
          <ZhSelect
            className="zh-input"
            disabled={saving}
            {...editForm.register("accountingAccountId")}
          >
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.code} — {a.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <div className="prd-crud-actions">
        <ZHBtn variant="primary" size="md" onClick={() => void onEditValid()} disabled={saving}>
          {saving ? "Guardando..." : "Actualizar"}
        </ZHBtn>
        <ZHBtn variant="ghost" size="md" onClick={handleCancel}>
          Cancelar
        </ZHBtn>
      </div>
    </div>
  ) : (
    <div className="pg-section-body">
      {saveError && <ZHPageNotice variant="error" message={saveError} />}
      <ZHGrid cols={2}>
        <ZHField label="Banco" required fieldError={createForm.formState.errors.bankId?.message}>
          <ZhSelect className="zh-input" disabled={saving} {...createForm.register("bankId")}>
            <option value="">Seleccione un banco</option>
            {banks.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField
          label="Tipo de cuenta"
          required
          fieldError={createForm.formState.errors.accountType?.message}
        >
          <ZhSelect className="zh-input" disabled={saving} {...createForm.register("accountType")}>
            <option value="Checking">Corriente</option>
            <option value="Savings">Ahorros</option>
            <option value="Other">Otro</option>
          </ZhSelect>
        </ZHField>
        <ZHField
          label="Número de cuenta"
          required
          fieldError={createForm.formState.errors.accountNumber?.message}
        >
          <ZhTextInput
            className="zh-input"
            maxLength={50}
            disabled={saving}
            {...createForm.register("accountNumber")}
          />
        </ZHField>
        <ZHField
          label="Alias / Nombre visible"
          required
          fieldError={createForm.formState.errors.displayName?.message}
        >
          <ZhTextInput
            className="zh-input"
            maxLength={200}
            disabled={saving}
            {...createForm.register("displayName")}
          />
        </ZHField>
        <ZHField
          label="Cuenta contable"
          required
          fieldError={createForm.formState.errors.accountingAccountId?.message}
        >
          <ZhSelect
            className="zh-input"
            disabled={saving}
            {...createForm.register("accountingAccountId")}
          >
            <option value="">Seleccione una cuenta</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.code} — {a.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <div className="prd-crud-actions">
        <ZHBtn variant="primary" size="md" onClick={() => void onCreateValid()} disabled={saving}>
          {saving ? "Guardando..." : "Crear"}
        </ZHBtn>
        <ZHBtn variant="ghost" size="md" onClick={handleCancel}>
          Cancelar
        </ZHBtn>
      </div>
    </div>
  );

  return (
    <ErpPageTemplate
      kicker="Tesorería"
      title="Cuentas Bancarias"
      action={
        <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
          <span className="material-symbols-outlined">add</span>
          Nueva cuenta
        </ZHBtn>
      }
    >
      <ConfigTabsLayout
        activeTab={activeTab}
        onTabChange={setActiveTab}
        editorLabel={editorLabel}
        editorIcon={editing ? "edit" : "add_box"}
        listContent={listContent}
        editorContent={editorContent}
      />
    </ErpPageTemplate>
  );
}
