import { useCallback, useEffect, useMemo, useState } from "react";
import { NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import {
  formatApiRequestError,
  parseValidationErrors,
} from "../../lib/apiError";
import {
  accountingApi,
  type AccountDto,
} from "../../accounting/api/accountingApi";
import {
  expenseCategoryService,
  type ExpenseCategoryNodeLevel,
  type ExpenseCategoryTreeNodeDto,
} from "../api/expenseCategoryService";
import {
  ExpenseCategoryFormPanel,
  type ExpenseCategoryFieldErrors,
  type ExpenseCategoryFormMode,
  type ExpenseCategoryFormState,
} from "../components/ExpenseCategoryFormPanel";
import { ExpenseCategoryTree } from "../components/ExpenseCategoryTree";
import "../styles/expense-categories.css";

const PERMISSIONS = {
  view: "expenses.catalog.view",
  create: "expenses.catalog.create",
  update: "expenses.catalog.update",
  activate: "expenses.catalog.activate",
  deactivate: "expenses.catalog.deactivate",
} as const;

const EMPTY_FORM: ExpenseCategoryFormState = {
  code: "",
  name: "",
  description: "",
  accountingAccountId: "",
};

const FIELD_MAP: Record<string, keyof ExpenseCategoryFormState> = {
  code: "code",
  Code: "code",
  name: "name",
  Name: "name",
  description: "description",
  Description: "description",
  accountingAccountId: "accountingAccountId",
  AccountingAccountId: "accountingAccountId",
};

function accountIsValidExpense(account: AccountDto): boolean {
  return (
    account.isActive &&
    account.allowsPosting &&
    account.accountType.toLowerCase() === "expense"
  );
}

function findNode(
  nodes: ExpenseCategoryTreeNodeDto[],
  id: string | null,
): ExpenseCategoryTreeNodeDto | null {
  if (!id) return null;
  for (const node of nodes) {
    if (node.id === id) return node;
    const child = findNode(node.children, id);
    if (child) return child;
  }
  return null;
}

function hasActiveDescendant(node: ExpenseCategoryTreeNodeDto): boolean {
  return node.children.some((child) => child.isActive || hasActiveDescendant(child));
}

function toForm(node: ExpenseCategoryTreeNodeDto): ExpenseCategoryFormState {
  return {
    code: node.code,
    name: node.name,
    description: node.description ?? "",
    accountingAccountId: node.accountingAccountId ?? "",
  };
}

function collectApiFieldErrors(error: unknown): ExpenseCategoryFieldErrors {
  const validation = parseValidationErrors(error);
  if (!validation) return {};

  return Object.entries(validation).reduce<ExpenseCategoryFieldErrors>(
    (acc, [field, messages]) => {
      const mapped = FIELD_MAP[field];
      const first = messages[0];
      if (mapped && first) acc[mapped] = first;
      return acc;
    },
    {},
  );
}

export function ExpenseCategoriesPage() {
  const { has } = usePermissionsUi();
  const canView = has(PERMISSIONS.view);
  const canCreate = has(PERMISSIONS.create);
  const canUpdate = has(PERMISSIONS.update);
  const canActivate = has(PERMISSIONS.activate);
  const canDeactivate = has(PERMISSIONS.deactivate);

  const [tree, setTree] = useState<ExpenseCategoryTreeNodeDto[]>([]);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [selected, setSelected] =
    useState<ExpenseCategoryTreeNodeDto | null>(null);
  const [mode, setMode] = useState<ExpenseCategoryFormMode>("idle");
  const [draftLevel, setDraftLevel] =
    useState<ExpenseCategoryNodeLevel | null>(null);
  const [draftParent, setDraftParent] =
    useState<ExpenseCategoryTreeNodeDto | null>(null);
  const [form, setForm] = useState<ExpenseCategoryFormState>(EMPTY_FORM);
  const [fieldErrors, setFieldErrors] =
    useState<ExpenseCategoryFieldErrors>({});
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);

  const accountsById = useMemo(
    () => new Map(accounts.map((account) => [account.id, account])),
    [accounts],
  );

  const expenseAccounts = useMemo(
    () => accounts.filter(accountIsValidExpense),
    [accounts],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setPageError(null);
    try {
      const [nodes, allAccounts] = await Promise.all([
        expenseCategoryService.getTree(true),
        accountingApi.listAccounts(),
      ]);
      setTree(nodes);
      setAccounts(allAccounts);
      setSelected((current) =>
        current ? findNode(nodes, current.id) ?? current : current,
      );
    } catch (error) {
      setPageError(
        formatApiRequestError(error, {
          generic: "No se pudo cargar el catalogo de gastos.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  if (!canView) {
    return <NoAccessPage title="Catalogo de gastos" />;
  }

  const resetForm = () => {
    setMode("idle");
    setDraftLevel(null);
    setDraftParent(null);
    setForm(EMPTY_FORM);
    setFieldErrors({});
  };

  const handleSelect = async (node: ExpenseCategoryTreeNodeDto) => {
    setSelected(node);
    setMode("idle");
    setFieldErrors({});
    try {
      const detail = await expenseCategoryService.getById(node.id);
      setSelected({ ...node, ...detail, children: node.children });
    } catch {
      setSelected(node);
    }
  };

  const handleCreateType = () => {
    setSelected(null);
    setDraftParent(null);
    setDraftLevel("Type");
    setForm(EMPTY_FORM);
    setFieldErrors({});
    setMode("create");
  };

  const handleCreateChild = (
    parent: ExpenseCategoryTreeNodeDto,
    level: ExpenseCategoryNodeLevel,
  ) => {
    if (!parent.isActive) {
      message.error("Seleccione un padre activo para crear un nodo hijo.");
      return;
    }
    if (level === "Category" && parent.level !== "Type") {
      message.error("La categoria debe crearse dentro de un tipo de gasto.");
      return;
    }
    if (level === "Subcategory" && parent.level !== "Category") {
      message.error(
        "La subcategoria debe crearse dentro de una categoria de gasto.",
      );
      return;
    }

    setSelected(parent);
    setDraftParent(parent);
    setDraftLevel(level);
    setForm(EMPTY_FORM);
    setFieldErrors({});
    setMode("create");
  };

  const handleEdit = (node: ExpenseCategoryTreeNodeDto) => {
    setSelected(node);
    setDraftParent(findNode(tree, node.parentId));
    setDraftLevel(node.level);
    setForm(toForm(node));
    setFieldErrors({});
    setMode("edit");
  };

  const validateForm = (): boolean => {
    const nextErrors: ExpenseCategoryFieldErrors = {};
    const level = draftLevel;

    if (!form.code.trim()) nextErrors.code = "Ingrese el codigo.";
    if (!form.name.trim()) nextErrors.name = "Ingrese el nombre.";

    if (mode === "create") {
      if (level === "Category" && draftParent?.level !== "Type") {
        nextErrors.name = "Seleccione un tipo activo como padre.";
      }
      if (level === "Subcategory" && draftParent?.level !== "Category") {
        nextErrors.name = "Seleccione una categoria activa como padre.";
      }
    }

    if (level === "Subcategory") {
      if (!form.accountingAccountId) {
        nextErrors.accountingAccountId =
          "La subcategoria requiere una cuenta contable.";
      } else {
        const account = accountsById.get(form.accountingAccountId);
        if (!account || !accountIsValidExpense(account)) {
          nextErrors.accountingAccountId =
            "La cuenta debe estar activa, permitir movimiento y ser de tipo gasto.";
        }
      }
    }

    setFieldErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = async () => {
    if (!draftLevel || !validateForm()) return;

    setSaving(true);
    setFieldErrors({});
    try {
      const common = {
        code: form.code.trim(),
        name: form.name.trim(),
        description: form.description.trim() || null,
        accountingAccountId:
          draftLevel === "Subcategory" ? form.accountingAccountId : null,
      };

      if (mode === "create") {
        await expenseCategoryService.create({
          ...common,
          level: draftLevel,
          parentId: draftParent?.id ?? null,
        });
        message.success("Nodo del catalogo creado correctamente.");
      } else if (mode === "edit" && selected) {
        await expenseCategoryService.update(selected.id, common);
        message.success("Nodo del catalogo actualizado correctamente.");
      }

      resetForm();
      await load();
    } catch (error) {
      const apiErrors = collectApiFieldErrors(error);
      if (Object.keys(apiErrors).length > 0) setFieldErrors(apiErrors);
      message.error(
        formatApiRequestError(error, {
          generic: "No se pudo guardar el nodo del catalogo.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const handleToggleActive = async (node: ExpenseCategoryTreeNodeDto) => {
    if (node.isActive && hasActiveDescendant(node)) {
      message.error("No se puede desactivar un nodo con hijos activos.");
      return;
    }

    setSaving(true);
    try {
      if (node.isActive) {
        await expenseCategoryService.deactivate(node.id);
        message.success("Nodo desactivado correctamente.");
      } else {
        await expenseCategoryService.activate(node.id);
        message.success("Nodo activado correctamente.");
      }
      await load();
    } catch (error) {
      message.error(
        formatApiRequestError(error, {
          generic: node.isActive
            ? "No se pudo desactivar el nodo."
            : "No se pudo activar el nodo.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const canSave = mode === "create" ? canCreate : mode === "edit" && canUpdate;

  return (
    <PageShell
      kicker="Gastos"
      title="Catalogo de gastos"
      subtitle="Administra la jerarquia Tipo de gasto, Categoria y Subcategoria con cuenta contable destino."
    >
      {pageError && (
        <ZHPageNotice
          variant="error"
          message="No se pudo preparar la pantalla"
          detail={pageError}
        />
      )}

      <div className="exp-cat-layout">
        <ZHCard bodyClassName="exp-cat-card-body">
          <ExpenseCategoryTree
            nodes={tree}
            accountsById={accountsById}
            selectedId={selected?.id ?? null}
            loading={loading}
            canCreate={canCreate}
            canUpdate={canUpdate}
            canActivate={canActivate}
            canDeactivate={canDeactivate}
            onSelect={handleSelect}
            onCreateType={handleCreateType}
            onCreateChild={handleCreateChild}
            onEdit={handleEdit}
            onToggleActive={handleToggleActive}
          />
        </ZHCard>

        <ZHCard bodyClassName="exp-cat-card-body">
          <ExpenseCategoryFormPanel
            mode={mode}
            level={draftLevel}
            parent={draftParent}
            selected={selected}
            form={form}
            fieldErrors={fieldErrors}
            accounts={expenseAccounts}
            accountsById={accountsById}
            saving={saving}
            canSave={canSave}
            onChange={(patch) => setForm((current) => ({ ...current, ...patch }))}
            onSubmit={handleSubmit}
            onCancel={resetForm}
          />
        </ZHCard>
      </div>
    </PageShell>
  );
}

export default ExpenseCategoriesPage;
