import { useCallback, useEffect, useState } from "react";

import { Badge } from "../../../components/PageShell";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { apiGet } from "../../lib/apiEnvelope";
import {
  financialDestinationService,
  type CompanyFinancialDestinationDto,
} from "../api/financialDestinationService";
import {
  createFinancialDestinationSchema,
  editFinancialDestinationSchema,
  emptyCreateFinancialDestinationForm,
  type CreateFinancialDestinationFormValues,
  type EditFinancialDestinationFormValues,
} from "../schemas/financialDestinationSchema";

import "../../../styles/shared/items-catalog.css";

interface AccountOption {
  id: string;
  code: string;
  name: string;
  allowsPosting: boolean;
  isActive: boolean;
}

interface CashRegisterOption {
  id: string;
  code: string;
  name: string;
}

/**
 * P0-02 Fase 13 â€” administraciÃ³n limitada de `CompanyFinancialDestination` (Â§6.4ter): alta con
 * los 8 campos estructurales (inmutables tras crear), ediciÃ³n limitada a
 * Name/AccountingAccountId/IsActive. Sigue el patrÃ³n `ConfigTabsLayout` obligatorio (Master
 * Configuration UI CLOSED, mismo criterio que `ItemTypesPage.tsx`) â€” modernizado con RHF+Zod
 * (F-V1/F-V2), a diferencia del `useState` manual de `ItemTypesPage.tsx` (no repetir esa deuda,
 * mismo criterio que Fase 12). Sin botÃ³n de eliminaciÃ³n fÃ­sica â€” solo activar/desactivar.
 */
export function FinancialDestinationsPage() {
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");
  const [items, setItems] = useState<CompanyFinancialDestinationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState<CompanyFinancialDestinationDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [accounts, setAccounts] = useState<AccountOption[]>([]);
  const [cashRegisters, setCashRegisters] = useState<CashRegisterOption[]>([]);

  const createForm = useForm<CreateFinancialDestinationFormValues>({
    resolver: zodResolver(createFinancialDestinationSchema),
    defaultValues: emptyCreateFinancialDestinationForm(),
  });
  const destinationTypeCode = createForm.watch("destinationTypeCode");

  const editForm = useForm<EditFinancialDestinationFormValues>({
    resolver: zodResolver(editFinancialDestinationSchema),
    defaultValues: { name: "", accountingAccountId: "" },
  });

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const all = await financialDestinationService.list();
      setItems(all);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar el listado de destinos financieros." }),
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchItems();
    apiGet<AccountOption[]>("/api/v1/accounting/accounts")
      .then((list) => setAccounts(list.filter((a) => a.allowsPosting && a.isActive)))
      .catch(() => setAccounts([]));
    apiGet<CashRegisterOption[]>("/api/v1/cash-registers/all?activeOnly=true")
      .then(setCashRegisters)
      .catch(() => setCashRegisters([]));
  }, [fetchItems]);

  const openCreate = () => {
    setEditing(null);
    createForm.reset(emptyCreateFinancialDestinationForm());
    setSaveError("");
    setActiveTab("editor");
  };

  const openEdit = (d: CompanyFinancialDestinationDto) => {
    setEditing(d);
    editForm.reset({ name: d.name, accountingAccountId: d.accountingAccountId });
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
      await financialDestinationService.create({
        code: values.code,
        name: values.name,
        destinationTypeCode: values.destinationTypeCode,
        accountingAccountId: values.accountingAccountId,
        currencyCode: values.currencyCode,
        cashRegisterId: values.cashRegisterId || null,
        bankInstitutionCode: values.bankInstitutionCode || null,
        bankAccountIdentifierNormalized: values.bankAccountIdentifierNormalized || null,
      });
      message.success("Destino financiero creado correctamente.");
      setActiveTab("list");
      void fetchItems();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, createForm.setError, (msg) => setSaveError(msg));
      if (!applied) {
        setSaveError(
          formatApiRequestError(err, { generic: "No se pudo crear el destino financiero." }),
        );
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
      if (values.name !== editing.name) await financialDestinationService.rename(editing.id, values.name);
      if (values.accountingAccountId !== editing.accountingAccountId)
        await financialDestinationService.changeAccountingAccount(
          editing.id,
          values.accountingAccountId,
        );
      message.success("Destino financiero actualizado correctamente.");
      setActiveTab("list");
      setEditing(null);
      void fetchItems();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, editForm.setError, (msg) => setSaveError(msg));
      if (!applied) {
        setSaveError(
          formatApiRequestError(err, { generic: "No se pudo actualizar el destino financiero." }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  const handleToggle = async (d: CompanyFinancialDestinationDto) => {
    try {
      await financialDestinationService.setActive(d.id, !d.isActive);
      message.success(d.isActive ? "Destino desactivado." : "Destino activado.");
      void fetchItems();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo cambiar el estado." }));
    }
  };

  const editorLabel = editing ? "Editar Destino Financiero" : "Nuevo Destino Financiero";

  const listContent = (
    <div className="table-scroll">
      {loading ? (
        <p>Cargando...</p>
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>CÃ³digo</th>
              <th>Nombre</th>
              <th>Tipo</th>
              <th>Moneda</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {items.map((d) => (
              <tr key={d.id} className={d.isActive ? undefined : "prd-row--inactive"}>
                <td>
                  <code className="prd-sku">{d.code}</code>
                </td>
                <td>{d.name}</td>
                <td>{d.destinationTypeCode === "BankAccount" ? "Cuenta bancaria" : "Caja"}</td>
                <td>{d.currencyCode}</td>
                <td>
                  <Badge label={"Estado"} variant="neutral" />
                </td>
                <td>
                  <div className="prd-row-actions">
                    <ZHBtn type="button" variant="ghost" size="sm" onClick={() => openEdit(d)}>
                      <span className="material-symbols-outlined zh-icon-lg">edit</span>
                    </ZHBtn>
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => void handleToggle(d)}
                      title={d.isActive ? "Desactivar" : "Activar"}
                    >
                      <span className="material-symbols-outlined zh-icon-lg">
                        {d.isActive ? "toggle_on" : "toggle_off"}
                      </span>
                    </ZHBtn>
                  </div>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr className="prd-empty-row">
                <td colSpan={6}>Sin destinos financieros registrados.</td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  );

  const editorContent = editing ? (
    <div className="pg-section-body">
      {saveError && <ZHPageNotice variant="error" message={saveError} />}
      <ZHGrid cols={2}>
        <ZHField label="CÃ³digo" readOnly>
          <input className="zh-input--upper" value={editing.code} disabled />
        </ZHField>
        <ZHField label="Tipo" readOnly>
          <input
            value={editing.destinationTypeCode === "BankAccount" ? "Cuenta bancaria" : "Caja"}
            disabled
          />
        </ZHField>
        <ZHField label="Nombre" required fieldError={editForm.formState.errors.name?.message}>
          <input
            className="zh-input"
            maxLength={200}
            disabled={saving}
            {...editForm.register("name")}
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
                {a.code} â€” {a.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <div className="prd-crud-actions">
        <ZHBtn
          variant="primary"
          size="md"
          onClick={() => void onEditValid()}
          disabled={saving}
        >
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
        <ZHField label="CÃ³digo" required fieldError={createForm.formState.errors.code?.message}>
          <input
            className="zh-input--upper"
            maxLength={30}
            disabled={saving}
            {...createForm.register("code")}
          />
        </ZHField>
        <ZHField label="Nombre" required fieldError={createForm.formState.errors.name?.message}>
          <input
            className="zh-input"
            maxLength={200}
            disabled={saving}
            {...createForm.register("name")}
          />
        </ZHField>
        <ZHField
          label="Tipo de destino"
          required
          fieldError={createForm.formState.errors.destinationTypeCode?.message}
        >
          <ZhSelect
            className="zh-input"
            disabled={saving}
            {...createForm.register("destinationTypeCode")}
          >
            <option value="BankAccount">Cuenta bancaria</option>
            <option value="CashRegister">Caja</option>
          </ZhSelect>
        </ZHField>
        <ZHField label="Moneda" required fieldError={createForm.formState.errors.currencyCode?.message}>
          <input
            className="zh-input--upper"
            maxLength={3}
            disabled={saving}
            {...createForm.register("currencyCode")}
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
                {a.code} â€” {a.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>

        {destinationTypeCode === "CashRegister" && (
          <ZHField
            label="Caja asociada"
            required
            fieldError={createForm.formState.errors.cashRegisterId?.message}
          >
            <ZhSelect
              className="zh-input"
              disabled={saving}
              {...createForm.register("cashRegisterId")}
            >
              <option value="">Seleccione una caja</option>
              {cashRegisters.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.code} â€” {c.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        )}

        {destinationTypeCode === "BankAccount" && (
          <>
            <ZHField
              label="InstituciÃ³n bancaria"
              required
              fieldError={createForm.formState.errors.bankInstitutionCode?.message}
            >
              <input
                className="zh-input"
                maxLength={20}
                disabled={saving}
                {...createForm.register("bankInstitutionCode")}
              />
            </ZHField>
            <ZHField
              label="NÃºmero de cuenta"
              required
              fieldError={createForm.formState.errors.bankAccountIdentifierNormalized?.message}
            >
              <input
                className="zh-input"
                maxLength={50}
                disabled={saving}
                {...createForm.register("bankAccountIdentifierNormalized")}
              />
            </ZHField>
          </>
        )}
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
      kicker="Finance"
      title="Destinos Financieros"
      action={
        <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
          <span className="material-symbols-outlined">add</span>
          Nuevo destino
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





