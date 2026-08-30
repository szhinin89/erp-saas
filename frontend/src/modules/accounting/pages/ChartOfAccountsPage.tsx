import { useCallback, useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PageShell, Badge } from "../../../components/PageShell";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { accountingApi, type AccountDto } from "../api/accountingApi";
import {
  ACCOUNT_NATURE_OPTIONS,
  ACCOUNT_TYPE_OPTIONS,
  createAccountSchema,
  editAccountSchema,
  emptyCreateAccountForm,
  type CreateAccountFormValues,
  type EditAccountFormValues,
} from "../schemas/accountSchema";

import "../../../styles/shared/items-catalog.css";
import "./ChartOfAccountsPage.css";

const ACCOUNT_TYPE_LABEL: Record<string, string> = Object.fromEntries(
  ACCOUNT_TYPE_OPTIONS.map((o) => [o.value, o.label]),
);
const ACCOUNT_NATURE_LABEL: Record<string, string> = Object.fromEntries(
  ACCOUNT_NATURE_OPTIONS.map((o) => [o.value, o.label]),
);

type Mode = "list" | "create" | "edit";

// ACCOUNTING-CHART-LIST-INTERACTIVITY-01: filtro rápido adicional — convive con
// búsqueda/tipo/estado existentes, nunca los reemplaza.
type QuickFilter = "all" | "group" | "posting" | "active" | "inactive";

const QUICK_FILTERS: { key: QuickFilter; label: string }[] = [
  { key: "all", label: "Todas" },
  { key: "group", label: "Agrupadoras" },
  { key: "posting", label: "Movimiento" },
  { key: "active", label: "Activas" },
  { key: "inactive", label: "Inactivas" },
];

const accountCodeSegments = (code: string) => code.split(".").filter(Boolean);

const compareAccountCode = (leftCode: string, rightCode: string) => {
  const leftSegments = accountCodeSegments(leftCode);
  const rightSegments = accountCodeSegments(rightCode);
  const segmentCount = Math.min(leftSegments.length, rightSegments.length);

  for (let index = 0; index < segmentCount; index += 1) {
    const leftSegment = leftSegments[index];
    const rightSegment = rightSegments[index];
    const leftIsNumeric = /^\d+$/.test(leftSegment);
    const rightIsNumeric = /^\d+$/.test(rightSegment);

    if (leftIsNumeric && rightIsNumeric) {
      const numericComparison = Number(leftSegment) - Number(rightSegment);
      if (numericComparison !== 0) return numericComparison;
    }

    const textComparison = leftSegment.localeCompare(rightSegment, undefined, {
      numeric: true,
      sensitivity: "base",
    });
    if (textComparison !== 0) return textComparison;
  }

  return leftSegments.length - rightSegments.length || leftCode.localeCompare(rightCode);
};

const getAccountVisualDepth = (code: string) => Math.max(accountCodeSegments(code).length - 1, 0);

const cleanAccountName = (name: string) =>
  name.replace(/^\s*(?:(?:L|[\u2502\u2514\u251c])\s+)+/, "");

interface AccountTreeNameCellProps {
  code: string;
  name: string;
  allowsPosting: boolean;
}

function AccountTreeNameCell({ code, name, allowsPosting }: AccountTreeNameCellProps) {
  const visualDepth = getAccountVisualDepth(code);
  const guides = Array.from({ length: visualDepth }, (_, index) => index);

  return (
    <span
      className={`coa-tree-name${allowsPosting ? "" : " coa-tree-name--group"}`}
      data-depth={visualDepth}
    >
      {visualDepth > 0 && (
        <span className="coa-tree-name__guides" aria-hidden="true">
          {guides.map((index) => (
            <span
              key={index}
              className={`coa-tree-name__guide${
                index === visualDepth - 1 ? " coa-tree-name__guide--branch" : ""
              }`}
            />
          ))}
        </span>
      )}
      <span className="coa-tree-name__label">{cleanAccountName(name)}</span>
    </span>
  );
}

/**
 * Plan de Cuentas (ACCOUNTING-CHART-OF-ACCOUNTS-02). Auditoría de reutilización: revisadas
 * `JournalEntriesPage.tsx`/`JournalEntryDetailPage.tsx` (mismo módulo, mismo patrón
 * PageShell+ZHCard+ZHDataTable ya establecido para Contabilidad) y `FinancialDestinationsPage.tsx`
 * (formulario RHF+Zod+ZHField/ZHGrid+applyServerErrors sobre una cuenta contable, catálogo
 * comparable). Reutiliza PageShell/ZHCard/ZHBtn/ZHIconButton/ZHDataTable/Badge/ZHPageNotice/
 * ZHField/ZHGrid/ZhTextInput/ZhSelect — sin componentes nuevos. Solo Create/Update/Enable/Disable:
 * sin edición de Code/AccountType/Nature (clasificación inmutable tras crear, ver accountSchema.ts)
 * y sin tocar el Posting Engine. ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C: pantalla principal,
 * accesible solo desde el menú — sin botones cruzados hacia Asientos contables/Reportes.
 */
export function ChartOfAccountsPage() {
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = useState<Mode>("list");
  const [editing, setEditing] = useState<AccountDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [quickFilter, setQuickFilter] = useState<QuickFilter>("all");
  const [levelFilter, setLevelFilter] = useState("");
  const [togglingAccountId, setTogglingAccountId] = useState<string | null>(null);

  const createForm = useForm<CreateAccountFormValues>({
    resolver: zodResolver(createAccountSchema),
    defaultValues: emptyCreateAccountForm(),
  });
  const editForm = useForm<EditAccountFormValues>({
    resolver: zodResolver(editAccountSchema),
    defaultValues: { name: "", parentAccountId: "", allowsPosting: true },
  });

  const fetchAccounts = useCallback(async () => {
    setLoading(true);
    try {
      const list = await accountingApi.listAccounts();
      setAccounts(list);
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo cargar el Plan de Cuentas." }));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchAccounts();
  }, [fetchAccounts]);

  const sortedAccounts = useMemo(
    () => [...accounts].sort((a, b) => compareAccountCode(a.code, b.code)),
    [accounts],
  );

  const filteredAccounts = useMemo(() => {
    const term = search.trim().toLowerCase();
    return sortedAccounts.filter((a) => {
      if (typeFilter && a.accountType !== typeFilter) return false;
      if (statusFilter === "active" && !a.isActive) return false;
      if (statusFilter === "inactive" && a.isActive) return false;
      if (quickFilter === "group" && a.allowsPosting) return false;
      if (quickFilter === "posting" && !a.allowsPosting) return false;
      if (quickFilter === "active" && !a.isActive) return false;
      if (quickFilter === "inactive" && a.isActive) return false;
      if (levelFilter !== "" && String(a.level) !== levelFilter) return false;
      if (term && !a.code.toLowerCase().includes(term) && !a.name.toLowerCase().includes(term))
        return false;
      return true;
    });
  }, [sortedAccounts, typeFilter, statusFilter, quickFilter, levelFilter, search]);

  const summary = useMemo(() => {
    const groupCount = accounts.filter((a) => !a.allowsPosting).length;
    const activeCount = accounts.filter((a) => a.isActive).length;
    return {
      total: accounts.length,
      group: groupCount,
      posting: accounts.length - groupCount,
      active: activeCount,
      inactive: accounts.length - activeCount,
      maxLevel: accounts.length > 0 ? Math.max(...accounts.map((a) => a.level)) : 0,
    };
  }, [accounts]);

  const availableLevels = useMemo(
    () => Array.from(new Set(accounts.map((a) => a.level))).sort((a, b) => a - b),
    [accounts],
  );

  const openCreate = () => {
    setEditing(null);
    createForm.reset(emptyCreateAccountForm());
    setSaveError("");
    setMode("create");
  };

  const openEdit = (a: AccountDto) => {
    setEditing(a);
    editForm.reset({
      name: a.name,
      parentAccountId: a.parentAccountId ?? "",
      allowsPosting: a.allowsPosting,
    });
    setSaveError("");
    setMode("edit");
  };

  const handleCancel = () => {
    setEditing(null);
    setSaveError("");
    setMode("list");
  };

  const onCreateValid = createForm.handleSubmit(async (values) => {
    setSaveError("");
    setSaving(true);
    try {
      await accountingApi.createAccount({
        code: values.code,
        name: values.name,
        parentAccountId: values.parentAccountId ? values.parentAccountId : null,
        accountType: values.accountType,
        nature: values.nature,
        allowsPosting: values.allowsPosting,
      });
      message.success("Cuenta creada correctamente.");
      setMode("list");
      void fetchAccounts();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, createForm.setError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(formatApiRequestError(err, { generic: "No se pudo crear la cuenta." }));
    } finally {
      setSaving(false);
    }
  });

  const onEditValid = editForm.handleSubmit(async (values) => {
    if (!editing) return;
    setSaveError("");
    setSaving(true);
    try {
      await accountingApi.updateAccount(editing.id, {
        id: editing.id,
        name: values.name,
        parentAccountId: values.parentAccountId ? values.parentAccountId : null,
        allowsPosting: values.allowsPosting,
      });
      message.success("Cuenta actualizada correctamente.");
      setMode("list");
      setEditing(null);
      void fetchAccounts();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, editForm.setError, (msg) => setSaveError(msg));
      if (!applied)
        setSaveError(formatApiRequestError(err, { generic: "No se pudo actualizar la cuenta." }));
    } finally {
      setSaving(false);
    }
  });

  // CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05: afecta si la cuenta puede usarse en nuevos
  // asientos/reglas de posteo — se confirma antes de ejecutar. No cambia validaciones ni lógica
  // contable del backend, solo agrega confirmación previa y bloqueo de doble submit.
  const handleToggleActive = async (a: AccountDto) => {
    if (togglingAccountId) return;

    const label = `${a.code} — ${a.name}`;
    const confirmed = await message.confirm({
      title: a.isActive ? `Desactivar cuenta "${label}"` : `Activar cuenta "${label}"`,
      message: a.isActive
        ? `La cuenta "${label}" no podrá usarse para nuevos asientos ni reglas de posteo. Los asientos ya registrados no se eliminan. Si hay reglas contables asociadas a esta cuenta, revísalas antes de continuar.`
        : `La cuenta "${label}" volverá a estar disponible para uso contable (nuevos asientos y reglas de posteo).`,
      variant: a.isActive ? "danger" : "warning",
      confirmLabel: a.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    setTogglingAccountId(a.id);
    try {
      if (a.isActive) await accountingApi.disableAccount(a.id);
      else await accountingApi.enableAccount(a.id);
      message.success(a.isActive ? "Cuenta desactivada correctamente." : "Cuenta activada correctamente.");
      void fetchAccounts();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: "No se pudo cambiar el estado de la cuenta." }));
    } finally {
      setTogglingAccountId(null);
    }
  };

  const columns: ZHDataTableColumn<AccountDto>[] = [
    {
      key: "code",
      header: "Código",
      render: (row) => <code className="prd-sku">{row.code}</code>,
    },
    {
      key: "name",
      header: "Nombre",
      render: (row) => (
        <AccountTreeNameCell code={row.code} name={row.name} allowsPosting={row.allowsPosting} />
      ),
    },
    {
      key: "accountType",
      header: "Tipo",
      render: (row) => ACCOUNT_TYPE_LABEL[row.accountType] ?? row.accountType,
    },
    {
      key: "nature",
      header: "Naturaleza",
      render: (row) => ACCOUNT_NATURE_LABEL[row.nature] ?? row.nature,
    },
    {
      key: "level",
      header: "Nivel",
      align: "center",
      render: (row) => row.level,
    },
    {
      key: "allowsPosting",
      header: "Permite movimiento",
      align: "center",
      render: (row) => (
        <Badge label={row.allowsPosting ? "Sí" : "No"} variant={row.allowsPosting ? "green" : "gray"} />
      ),
    },
    {
      key: "isActive",
      header: "Estado",
      render: (row) => (
        <Badge label={row.isActive ? "Activa" : "Inactiva"} variant={row.isActive ? "green" : "gray"} />
      ),
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (row) => (
        <div className="prd-row-actions">
          <ZHIconButton icon="edit" title="Editar" variant="ghost" onClick={() => openEdit(row)} />
          <ZHIconButton
            icon={row.isActive ? "toggle_on" : "toggle_off"}
            title={row.isActive ? "Desactivar" : "Activar"}
            variant="ghost"
            disabled={togglingAccountId === row.id}
            onClick={() => void handleToggleActive(row)}
          />
        </div>
      ),
    },
  ];

  const parentOptions = editing
    ? sortedAccounts.filter((a) => a.id !== editing.id)
    : sortedAccounts;

  return (
    <PageShell
      kicker="Contabilidad"
      title="Plan de cuentas"
      subtitle="Cuentas contables de la empresa — clasificación, jerarquía y estado"
      action={
        mode === "list" ? (
          <ZHBtn type="button" variant="primary" onClick={openCreate}>
            <span className="material-symbols-outlined">add</span>
            Nueva cuenta
          </ZHBtn>
        ) : undefined
      }
    >
      {mode === "list" && !loading && (
        <div className="pg-kpis">
          <ReportKpiCard
            layout="horizontal"
            icon="account_tree"
            tone="primary"
            label="Total cuentas"
            value={String(summary.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="folder"
            tone="secondary"
            label="Agrupadoras"
            value={String(summary.group)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="payments"
            tone="info"
            label="Movimiento"
            value={String(summary.posting)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="check_circle"
            tone="success"
            label="Activas"
            value={String(summary.active)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="block"
            tone="error"
            label="Inactivas"
            value={String(summary.inactive)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="stairs"
            tone="tertiary"
            label="Nivel máximo"
            value={String(summary.maxLevel)}
          />
        </div>
      )}

      {mode === "list" && (
        <ZHCard
          className="coa-list-card"
          title="Listado"
          actions={
            <div className="coa-list-filters">
              <ZhTextInput
                placeholder="Buscar por código o nombre..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <ZhSelect value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}>
                <option value="">Todos los tipos</option>
                {ACCOUNT_TYPE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
              <ZhSelect value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
                <option value="">Todos los estados</option>
                <option value="active">Activas</option>
                <option value="inactive">Inactivas</option>
              </ZhSelect>
              <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void fetchAccounts()} disabled={loading}>
                Actualizar
              </ZHBtn>
            </div>
          }
        >
          <div className="coa-list-secondary-row">
            <div
              className="coa-quick-filters"
              role="group"
              aria-label="Filtros rápidos de cuentas"
            >
              {QUICK_FILTERS.map((f) => (
                <button
                  key={f.key}
                  type="button"
                  className={`coa-chip${quickFilter === f.key ? " coa-chip--active" : ""}`}
                  aria-pressed={quickFilter === f.key}
                  onClick={() => setQuickFilter(f.key)}
                >
                  {f.label}
                </button>
              ))}
              <ZhSelect
                aria-label="Filtrar por nivel"
                value={levelFilter}
                onChange={(e) => setLevelFilter(e.target.value)}
              >
                <option value="">Todos los niveles</option>
                {availableLevels.map((lvl) => (
                  <option key={lvl} value={lvl}>
                    Nivel {lvl}
                  </option>
                ))}
              </ZhSelect>
            </div>
            <span className="coa-list-summary-text subtle">
              Mostrando {filteredAccounts.length} de {accounts.length} cuentas
            </span>
          </div>
          <ZHDataTable
            columns={columns}
            rows={filteredAccounts}
            rowKey={(row) => row.id}
            loading={loading}
            showRowNumber
            emptyMessage="No hay cuentas registradas."
          />
        </ZHCard>
      )}

      {mode === "create" && (
        <ZHCard title="Nueva cuenta">
          {saveError && <ZHPageNotice variant="error" message={saveError} />}
          <ZHGrid cols={2}>
            <ZHField label="Código" required fieldError={createForm.formState.errors.code?.message}>
              <ZhTextInput
                className="zh-input--upper"
                maxLength={30}
                disabled={saving}
                {...createForm.register("code")}
              />
            </ZHField>
            <ZHField label="Nombre" required fieldError={createForm.formState.errors.name?.message}>
              <ZhTextInput maxLength={150} disabled={saving} {...createForm.register("name")} />
            </ZHField>
            <ZHField
              label="Tipo de cuenta"
              required
              fieldError={createForm.formState.errors.accountType?.message}
            >
              <ZhSelect disabled={saving} {...createForm.register("accountType")}>
                {ACCOUNT_TYPE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            <ZHField label="Naturaleza" required fieldError={createForm.formState.errors.nature?.message}>
              <ZhSelect disabled={saving} {...createForm.register("nature")}>
                {ACCOUNT_NATURE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            <ZHField label="Cuenta padre" fieldError={createForm.formState.errors.parentAccountId?.message}>
              <ZhSelect disabled={saving} {...createForm.register("parentAccountId")}>
                <option value="">(cuenta raíz — sin padre)</option>
                {sortedAccounts.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.code} — {a.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            <ZHField label="Permite movimiento">
              <ZhSelect
                disabled={saving}
                {...createForm.register("allowsPosting", { setValueAs: (v) => v === "true" })}
              >
                <option value="true">Sí</option>
                <option value="false">No</option>
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
        </ZHCard>
      )}

      {mode === "edit" && editing && (
        <ZHCard title={`Editar cuenta — ${editing.code}`}>
          {saveError && <ZHPageNotice variant="error" message={saveError} />}
          <ZHGrid cols={2}>
            <ZHField label="Código" readOnly>
              <ZhTextInput className="zh-input--upper" value={editing.code} disabled />
            </ZHField>
            <ZHField label="Tipo" readOnly>
              <ZhTextInput value={ACCOUNT_TYPE_LABEL[editing.accountType] ?? editing.accountType} disabled />
            </ZHField>
            <ZHField label="Naturaleza" readOnly>
              <ZhTextInput value={ACCOUNT_NATURE_LABEL[editing.nature] ?? editing.nature} disabled />
            </ZHField>
            <ZHField label="Nombre" required fieldError={editForm.formState.errors.name?.message}>
              <ZhTextInput maxLength={150} disabled={saving} {...editForm.register("name")} />
            </ZHField>
            <ZHField label="Cuenta padre" fieldError={editForm.formState.errors.parentAccountId?.message}>
              <ZhSelect disabled={saving} {...editForm.register("parentAccountId")}>
                <option value="">(cuenta raíz — sin padre)</option>
                {parentOptions.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.code} — {a.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            <ZHField label="Permite movimiento">
              <ZhSelect
                disabled={saving}
                {...editForm.register("allowsPosting", { setValueAs: (v) => v === "true" })}
              >
                <option value="true">Sí</option>
                <option value="false">No</option>
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
        </ZHCard>
      )}
    </PageShell>
  );
}
