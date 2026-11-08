import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { NoAccessPage, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import {
  accountLookupFacade,
  type AccountDto,
} from "../../accounting/facades/accountLookupFacade";
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
import {
  accountIsValidExpense,
  collectApiFieldErrors,
  containerIdsWithChildren,
  CREATE_TITLE,
  EDIT_TITLE,
  EMPTY_FORM,
  findNode,
  formatPath,
  formsEqual,
  hasActiveDescendant,
  pathTo,
  ROOT_SUBTITLE,
  toForm,
} from "../utils/expenseCategoryModel";
import "../styles/expense-categories.css";

const PERMISSIONS = {
  view: "expenses.catalog.view",
  create: "expenses.catalog.create",
  update: "expenses.catalog.update",
  activate: "expenses.catalog.activate",
  deactivate: "expenses.catalog.deactivate",
} as const;

const FORM_ID = "expense-category-form";

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
  const [collapsedIds, setCollapsedIds] = useState<ReadonlySet<string>>(
    () => new Set(),
  );
  const [mode, setMode] = useState<ExpenseCategoryFormMode>("idle");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draftLevel, setDraftLevel] =
    useState<ExpenseCategoryNodeLevel | null>(null);
  const [draftParent, setDraftParent] =
    useState<ExpenseCategoryTreeNodeDto | null>(null);
  const [form, setForm] = useState<ExpenseCategoryFormState>(EMPTY_FORM);
  const [initialForm, setInitialForm] =
    useState<ExpenseCategoryFormState>(EMPTY_FORM);
  const [fieldErrors, setFieldErrors] =
    useState<ExpenseCategoryFieldErrors>({});
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);

  /** Control que abrió el modal; recibe el foco al cerrarlo. */
  const openerRef = useRef<HTMLElement | null>(null);
  /** Evita apilar confirmaciones de descarte (Escape repetido mientras se pregunta). */
  const discardPromptOpenRef = useRef(false);

  const accountsById = useMemo(
    () => new Map(accounts.map((account) => [account.id, account])),
    [accounts],
  );

  const expenseAccounts = useMemo(
    () => accounts.filter(accountIsValidExpense),
    [accounts],
  );

  const revealPath = useCallback(
    (nodes: ExpenseCategoryTreeNodeDto[], id: string | null) => {
      const ancestors = pathTo(nodes, id).slice(0, -1);
      if (ancestors.length === 0) return;
      setCollapsedIds((current) => {
        if (!ancestors.some((node) => current.has(node.id))) return current;
        const next = new Set(current);
        ancestors.forEach((node) => next.delete(node.id));
        return next;
      });
    },
    [],
  );

  const load = useCallback(
    async (selectId?: string) => {
      setLoading(true);
      setPageError(null);
      try {
        const [nodes, allAccounts] = await Promise.all([
          expenseCategoryService.getTree(true),
          accountLookupFacade.listAccounts(),
        ]);
        setTree(nodes);
        setAccounts(allAccounts);
        setSelected((current) => {
          const targetId = selectId ?? current?.id ?? null;
          return findNode(nodes, targetId) ?? current;
        });
        if (selectId) revealPath(nodes, selectId);
      } catch (error) {
        setPageError(
          formatApiRequestError(error, {
            generic: "No se pudo cargar el catálogo de gastos.",
          }),
        );
      } finally {
        setLoading(false);
      }
    },
    [revealPath],
  );

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  // ZHModal enfoca su primer control (la X del encabezado); al abrir el editor se lleva el
  // foco al primer campo. Este efecto del padre se ejecuta después del efecto del modal.
  const editorMounted = mode !== "idle";
  useEffect(() => {
    if (!editorMounted) return;
    document
      .getElementById(FORM_ID)
      ?.querySelector<HTMLElement>("input:not([disabled])")
      ?.focus();
  }, [editorMounted]);

  if (!canView) {
    return <NoAccessPage title="Catálogo de gastos" />;
  }

  const editorOpen = mode !== "idle" && draftLevel !== null;
  const isDirty = editorOpen && !formsEqual(form, initialForm);

  const openEditor = (
    opener: HTMLElement,
    next: {
      mode: Exclude<ExpenseCategoryFormMode, "idle">;
      level: ExpenseCategoryNodeLevel;
      parent: ExpenseCategoryTreeNodeDto | null;
      editingId: string | null;
      form: ExpenseCategoryFormState;
    },
  ) => {
    openerRef.current = opener;
    setDraftParent(next.parent);
    setDraftLevel(next.level);
    setEditingId(next.editingId);
    setForm(next.form);
    setInitialForm(next.form);
    setFieldErrors({});
    setMode(next.mode);
  };

  const closeEditor = () => {
    setMode("idle");
    setEditingId(null);
    setDraftLevel(null);
    setDraftParent(null);
    setForm(EMPTY_FORM);
    setInitialForm(EMPTY_FORM);
    setFieldErrors({});

    const opener = openerRef.current;
    openerRef.current = null;
    window.setTimeout(() => {
      if (opener?.isConnected) opener.focus();
    }, 0);
  };

  /** Cierre por Cancelar, X o Escape: bloqueado durante el guardado; confirma si hay cambios. */
  const requestClose = async () => {
    if (saving || discardPromptOpenRef.current) return;
    if (isDirty) {
      discardPromptOpenRef.current = true;
      const discard = await message
        .confirm({
          title: "Descartar cambios",
          message:
            "Hay cambios sin guardar en este formulario. Si continúa, se perderán.",
          variant: "warning",
          confirmLabel: "Descartar cambios",
          cancelLabel: "Seguir editando",
        })
        .finally(() => {
          discardPromptOpenRef.current = false;
        });
      if (!discard) return;
    }
    closeEditor();
  };

  const handleSelect = (node: ExpenseCategoryTreeNodeDto) => {
    setSelected(node);
  };

  const handleToggleExpanded = (node: ExpenseCategoryTreeNodeDto) => {
    setCollapsedIds((current) => {
      const next = new Set(current);
      if (next.has(node.id)) next.delete(node.id);
      else next.add(node.id);
      return next;
    });
  };

  const handleExpandAll = () => setCollapsedIds(new Set());

  /** Contrae todo salvo la ruta del nodo seleccionado, que permanece visible. */
  const handleCollapseAll = () => {
    const keepOpen = new Set(
      pathTo(tree, selected?.id ?? null)
        .slice(0, -1)
        .map((node) => node.id),
    );
    setCollapsedIds(
      new Set(containerIdsWithChildren(tree).filter((id) => !keepOpen.has(id))),
    );
  };

  const handleCreateType = (event: React.MouseEvent<HTMLButtonElement>) => {
    setSelected(null);
    openEditor(event.currentTarget, {
      mode: "create",
      level: "Type",
      parent: null,
      editingId: null,
      form: EMPTY_FORM,
    });
  };

  const handleCreateChild = (
    parent: ExpenseCategoryTreeNodeDto,
    level: ExpenseCategoryNodeLevel,
    event: React.MouseEvent<HTMLButtonElement>,
  ) => {
    if (!parent.isActive) {
      message.error("Seleccione un padre activo para crear un nodo hijo.");
      return;
    }
    if (level === "Category" && parent.level !== "Type") {
      message.error("La categoría debe crearse dentro de un tipo de gasto.");
      return;
    }
    if (level === "Subcategory" && parent.level !== "Category") {
      message.error(
        "La subcategoría debe crearse dentro de una categoría de gasto.",
      );
      return;
    }

    setSelected(parent);
    openEditor(event.currentTarget, {
      mode: "create",
      level,
      parent,
      editingId: null,
      form: EMPTY_FORM,
    });
  };

  const handleEdit = (
    node: ExpenseCategoryTreeNodeDto,
    event: React.MouseEvent<HTMLButtonElement>,
  ) => {
    setSelected(node);
    openEditor(event.currentTarget, {
      mode: "edit",
      level: node.level,
      parent: findNode(tree, node.parentId),
      editingId: node.id,
      form: toForm(node),
    });
  };

  const validateForm = (): boolean => {
    const nextErrors: ExpenseCategoryFieldErrors = {};
    const level = draftLevel;

    if (!form.code.trim()) nextErrors.code = "Ingrese el código.";
    if (!form.name.trim()) nextErrors.name = "Ingrese el nombre.";

    if (mode === "create") {
      if (level === "Category" && draftParent?.level !== "Type") {
        nextErrors.name = "Seleccione un tipo activo como padre.";
      }
      if (level === "Subcategory" && draftParent?.level !== "Category") {
        nextErrors.name = "Seleccione una categoría activa como padre.";
      }
    }

    if (level === "Subcategory") {
      if (!form.accountingAccountId) {
        nextErrors.accountingAccountId =
          "La subcategoría requiere una cuenta contable.";
      } else {
        const account = accountsById.get(form.accountingAccountId);
        if (!account || !accountIsValidExpense(account)) {
          nextErrors.accountingAccountId =
            "La cuenta debe estar activa, permitir movimientos y ser de tipo gasto.";
        }
      }
    }

    setFieldErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = async () => {
    if (saving || !draftLevel || !validateForm()) return;

    setSaving(true);
    setFieldErrors({});
    let savedId: string | null = null;
    try {
      const common = {
        code: form.code.trim(),
        name: form.name.trim(),
        description: form.description.trim() || null,
        accountingAccountId:
          draftLevel === "Subcategory" ? form.accountingAccountId : null,
      };

      if (mode === "create") {
        const created = await expenseCategoryService.create({
          ...common,
          level: draftLevel,
          parentId: draftParent?.id ?? null,
        });
        savedId = created?.id ?? null;
        message.success("Nodo del catálogo creado correctamente.");
      } else if (mode === "edit" && editingId) {
        await expenseCategoryService.update(editingId, common);
        savedId = editingId;
        message.success("Nodo del catálogo actualizado correctamente.");
      }
    } catch (error) {
      const apiErrors = collectApiFieldErrors(error);
      if (Object.keys(apiErrors).length > 0) setFieldErrors(apiErrors);
      message.error(
        formatApiRequestError(error, {
          generic: "No se pudo guardar el nodo del catálogo.",
        }),
      );
      return;
    } finally {
      setSaving(false);
    }

    closeEditor();
    await load(savedId ?? undefined);
  };

  const handleToggleActive = async (node: ExpenseCategoryTreeNodeDto) => {
    if (saving) return;
    if (node.isActive && hasActiveDescendant(node)) {
      message.error("No se puede desactivar un nodo con hijos activos.");
      return;
    }

    const confirmed = await message.confirm({
      title: node.isActive
        ? `Desactivar "${node.name}"`
        : `Activar "${node.name}"`,
      message: node.isActive
        ? `"${node.name}" dejará de estar disponible para registrar nuevos gastos. Los documentos de gasto ya registrados con esta categoría no se modifican.`
        : `"${node.name}" volverá a estar disponible para registrar nuevos gastos.`,
      variant: node.isActive ? "danger" : "warning",
      confirmLabel: node.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

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

  const modalTitle = draftLevel
    ? (mode === "edit" ? EDIT_TITLE : CREATE_TITLE)[draftLevel]
    : "";
  const parentPath = formatPath(pathTo(tree, draftParent?.id ?? null));
  let modalSubtitle = ROOT_SUBTITLE;
  if (parentPath) {
    modalSubtitle =
      mode === "edit" ? `Ubicación: ${parentPath}` : `Dentro de: ${parentPath}`;
  }

  return (
    <PageShell
      kicker="Gastos"
      title="Catálogo de gastos"
      subtitle="Administra la jerarquía Tipo de gasto, Categoría y Subcategoría con cuenta contable destino."
    >
      {pageError && (
        <ZHPageNotice
          variant="error"
          message="No se pudo preparar la pantalla"
          detail={pageError}
        />
      )}

      <ZHCard bodyClassName="exp-cat-card-body">
        <ExpenseCategoryTree
          nodes={tree}
          accountsById={accountsById}
          selectedId={selected?.id ?? null}
          collapsedIds={collapsedIds}
          loading={loading}
          canCreate={canCreate}
          canUpdate={canUpdate}
          canActivate={canActivate}
          canDeactivate={canDeactivate}
          onSelect={handleSelect}
          onToggleExpanded={handleToggleExpanded}
          onExpandAll={handleExpandAll}
          onCollapseAll={handleCollapseAll}
          onCreateType={handleCreateType}
          onCreateChild={handleCreateChild}
          onEdit={handleEdit}
          onToggleActive={handleToggleActive}
        />
      </ZHCard>

      {editorOpen && draftLevel && (
        <ZHModal
          open
          size="md"
          title={modalTitle}
          subtitle={modalSubtitle}
          closeOnBackdrop={false}
          onClose={() => void requestClose()}
          footer={
            <>
              <ZHBtn
                type="button"
                variant="ghost"
                onClick={() => void requestClose()}
                disabled={saving}
              >
                Cancelar
              </ZHBtn>
              <ZHBtn
                type="submit"
                form={FORM_ID}
                variant="primary"
                disabled={saving || !canSave}
              >
                {saving ? "Guardando..." : "Guardar"}
              </ZHBtn>
            </>
          }
        >
          <ExpenseCategoryFormPanel
            formId={FORM_ID}
            level={draftLevel}
            form={form}
            fieldErrors={fieldErrors}
            accounts={expenseAccounts}
            saving={saving}
            onChange={(patch) => setForm((current) => ({ ...current, ...patch }))}
            onSubmit={() => void handleSubmit()}
          />
        </ZHModal>
      )}
    </PageShell>
  );
}

export default ExpenseCategoriesPage;
