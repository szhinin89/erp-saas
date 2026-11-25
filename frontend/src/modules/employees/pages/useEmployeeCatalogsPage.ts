import { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useDebounce } from "../../../hooks/useDebounce";
import {
  releaseDiscardApproval,
  useDiscardConfirmation,
  useUnsavedChangesGuard,
} from "../../../hooks/useUnsavedChangesGuard";
import { useAuthStore } from "../../../store/authStore";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { employeeErrorMessage, isConcurrencyConflict } from "../api/employeeService";
import { employeeCatalogService } from "../api/employeeCatalogService";
import { catalogEntrySchema, type CatalogEntryValues } from "../schemas/employeeSchemas";
import type { EmployeeCatalogEntry, EmployeeCatalogKind } from "../types/employee.types";

export const CATALOG_PAGE_SIZE = 25;

export type CatalogStatusFilter = "" | "active" | "inactive";

export function useEmployeeCatalogsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("employees.catalogs.view");
  const canManage = canShow("employees.catalogs.manage");
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);

  const [kind, setKind] = useState<EmployeeCatalogKind>("job-positions");
  const [view, setView] = useState<"list" | "editor">("list");
  const [searchText, setSearchText] = useState("");
  const [status, setStatus] = useState<CatalogStatusFilter>("");
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<EmployeeCatalogEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(canView);
  const [error, setError] = useState("");
  const [tick, setTick] = useState(0);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<EmployeeCatalogEntry | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const search = useDebounce(searchText, 300);

  const form = useForm<CatalogEntryValues>({
    resolver: zodResolver(catalogEntrySchema(t)),
    defaultValues: { name: "" },
  });
  const { isDirty } = form.formState;
  useUnsavedChangesGuard(editorOpen && isDirty);
  const confirmDiscard = useDiscardConfirmation();

  useEffect(() => {
    if (!canView) return;
    const controller = new AbortController();
    setLoading(true);
    setError("");
    employeeCatalogService
      .list(
        kind,
        {
          search: search.trim() || undefined,
          isActive: status === "" ? undefined : status === "active",
          skip: (page - 1) * CATALOG_PAGE_SIZE,
          take: CATALOG_PAGE_SIZE,
        },
        controller.signal,
      )
      .then((result) => {
        if (controller.signal.aborted) return;
        setItems(result.items);
        setTotalCount(result.totalCount);
        setLoading(false);
      })
      .catch((err) => {
        if (controller.signal.aborted) return;
        setItems([]);
        setTotalCount(0);
        setError(employeeErrorMessage(err, t("employees.catalogs.loadError", "No se pudo cargar el catálogo.")));
        setLoading(false);
      });
    return () => controller.abort();
  }, [canView, kind, search, status, page, tick, companySessionVersion, t]);

  const refetch = useCallback(() => setTick((v) => v + 1), []);

  const closeEditor = useCallback(() => {
    setEditorOpen(false);
    setEditing(null);
    setSaveError("");
    form.reset({ name: "" });
    setView("list");
  }, [form]);

  /** Ejecuta `action` solo si no hay cambios que perder o el usuario confirma descartarlos. */
  const guarded = useCallback(
    async (action: () => void) => {
      if (editorOpen && isDirty && !(await confirmDiscard())) return;
      action();
      releaseDiscardApproval();
    },
    [editorOpen, isDirty, confirmDiscard],
  );

  const changeKind = (next: EmployeeCatalogKind) => {
    if (next === kind) return;
    void guarded(() => {
      closeEditor();
      setKind(next);
      setItems([]);
      setSearchText("");
      setStatus("");
      setPage(1);
    });
  };

  const openEditor = (entry: EmployeeCatalogEntry | null) =>
    void guarded(() => {
      setEditing(entry);
      setSaveError("");
      form.reset({ name: entry?.name ?? "" });
      setEditorOpen(true);
      setView("editor");
    });

  const cancel = () => void guarded(closeEditor);

  const save = form.handleSubmit(async ({ name }) => {
    if (!canManage) return;
    setSaving(true);
    setSaveError("");
    try {
      if (editing) {
        await employeeCatalogService.rename(kind, editing.id, editing.rowVersion, name);
        message.success(t("employees.catalogs.updated", "Registro actualizado correctamente."));
      } else {
        await employeeCatalogService.create(kind, name);
        message.success(t("employees.catalogs.created", "Registro creado correctamente."));
      }
      closeEditor();
      refetch();
    } catch (err) {
      const generic = t("employees.catalogs.saveError", "No se pudo guardar el registro.");
      if (editing && isConcurrencyConflict(err)) {
        message.error(employeeErrorMessage(err, generic));
        refetch();
        try {
          // Se muestra lo actual; el cambio rechazado no se reaplica ni se reintenta.
          const fresh = await employeeCatalogService.getById(kind, editing.id);
          setEditing(fresh);
          form.reset({ name: fresh.name });
        } catch {
          closeEditor();
        }
      } else if (!applyServerErrors(err, form.setError, setSaveError)) {
        setSaveError(employeeErrorMessage(err, generic));
      }
    } finally {
      setSaving(false);
    }
  });

  const setActive = async (entry: EmployeeCatalogEntry, active: boolean) => {
    if (!canManage) return;
    setSaving(true);
    try {
      const updated = await employeeCatalogService.setActive(kind, entry.id, entry.rowVersion, active);
      message.success(
        active
          ? t("employees.catalogs.activated", "Registro activado.")
          : t("employees.catalogs.deactivated", "Registro desactivado."),
      );
      // El editor abierto sobre el mismo registro toma la versión nueva.
      if (editing?.id === entry.id) setEditing(updated);
      refetch();
    } catch (err) {
      message.error(
        employeeErrorMessage(err, t("employees.catalogs.toggleError", "No se pudo cambiar el estado.")),
      );
      if (isConcurrencyConflict(err)) refetch();
    } finally {
      setSaving(false);
    }
  };

  return {
    canView,
    canManage,
    kind,
    changeKind,
    view,
    setView,
    searchText,
    setSearch: (value: string) => {
      setSearchText(value);
      setPage(1);
    },
    status,
    setStatus: (value: CatalogStatusFilter) => {
      setStatus(value);
      setPage(1);
    },
    page,
    setPage,
    items,
    totalCount,
    loading,
    error,
    form,
    editorOpen,
    editing,
    saving,
    saveError,
    openEditor,
    cancel,
    save,
    setActive,
  };
}
