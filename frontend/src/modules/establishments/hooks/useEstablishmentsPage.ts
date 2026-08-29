import { useCallback, useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useSearchParams } from "react-router-dom";
import {
  establishmentService,
  type EstablishmentListItemDto,
} from "../api/establishmentService";
import {
  branchLookupFacade,
  type BranchListItemDto,
} from "../../branches/facades/branchLookupFacade";
import {
  establishmentSchema,
  emptyEstablishmentForm,
  type EstablishmentFormValues,
} from "../schemas/establishmentPageSchema";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";

type ActiveStatus = "all" | "active" | "inactive";

export function useEstablishmentsPage() {
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.establishments.view");
  const canCreate = canShow("settings.establishments.create");
  const canUpdate = canShow("settings.establishments.update");
  const canDisable = canShow("settings.establishments.disable");

  const [searchParams] = useSearchParams();
  const branchIdFilter = searchParams.get("branchId") ?? undefined;

  // ── Listado ──────────────────────────────────────────────────────────────
  const [items, setItems] = useState<EstablishmentListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [activeStatus, setActiveStatus] = useState<ActiveStatus>("active");

  // ── Sucursales para selector ──────────────────────────────────────────────
  const [branches, setBranches] = useState<BranchListItemDto[]>([]);
  const [loadingBranches, setLoadingBranches] = useState(false);

  // ── Panel ────────────────────────────────────────────────────────────────
  const [panelOpen, setPanelOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [editingName, setEditingName] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [togglingId, setTogglingId] = useState<string | null>(null);

  const {
    register,
    control,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors },
  } = useForm<EstablishmentFormValues>({
    resolver: zodResolver(establishmentSchema),
    defaultValues: emptyEstablishmentForm(branchIdFilter),
  });

  // ── Carga de datos ────────────────────────────────────────────────────────
  const fetchList = useCallback(async () => {
    setError("");
    setLoading(true);
    try {
      setItems(
        await establishmentService.list(
          activeStatus,
          branchIdFilter,
          search || undefined,
        ),
      );
    } catch {
      setError("Error al cargar los establecimientos.");
    } finally {
      setLoading(false);
    }
  }, [activeStatus, branchIdFilter, search]);

  const loadBranches = useCallback(async () => {
    setLoadingBranches(true);
    try {
      setBranches(await branchLookupFacade.list("active"));
    } catch {
      // selector mostrará vacío
    } finally {
      setLoadingBranches(false);
    }
  }, []);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  // ── Totales ───────────────────────────────────────────────────────────────
  const totals = {
    total: items.length,
    active: items.filter((i) => i.isActive).length,
    inactive: items.filter((i) => !i.isActive).length,
    main: items.filter((i) => i.isMain).length,
  };

  // ── Filtrado local ────────────────────────────────────────────────────────
  const filtered = search.trim()
    ? items.filter(
        (i) =>
          i.code.toLowerCase().includes(search.toLowerCase()) ||
          i.name.toLowerCase().includes(search.toLowerCase()) ||
          i.address.toLowerCase().includes(search.toLowerCase()) ||
          (i.branchName?.toLowerCase().includes(search.toLowerCase()) ?? false),
      )
    : items;

  // ── Panel — abrir para crear ──────────────────────────────────────────────
  const openCreate = async () => {
    setEditingId(null);
    setEditingCode(null);
    setEditingName(null);
    setSelectedId(null);
    setSaveError("");
    reset(emptyEstablishmentForm(branchIdFilter));
    await loadBranches();
    setPanelOpen(true);
  };

  // ── Panel — abrir para editar ─────────────────────────────────────────────
  const openEdit = async (item: EstablishmentListItemDto) => {
    setEditingId(item.id);
    setEditingCode(item.code);
    setEditingName(item.name);
    setSelectedId(item.id);
    setSaveError("");
    reset({
      branchId: item.branchId,
      code: item.code,
      name: item.name,
      address: item.address,
      phone: item.phone ?? "",
      isMain: item.isMain,
    });
    await loadBranches();
    setPanelOpen(true);
  };

  const closePanel = () => {
    setPanelOpen(false);
    setEditingId(null);
    setEditingCode(null);
    setEditingName(null);
    setSelectedId(null);
    setSaveError("");
  };

  // ── Guardar ───────────────────────────────────────────────────────────────
  const save = handleSubmit(async (form) => {
    setSaveError("");
    setSaving(true);
    try {
      if (editingId) {
        await establishmentService.update(editingId, {
          id: editingId,
          name: form.name,
          address: form.address,
          phone: form.phone || null,
          isMain: form.isMain,
        });
        await fetchList();
        message.success("Establecimiento actualizado correctamente.");
      } else {
        const created = await establishmentService.create({
          branchId: form.branchId || null,
          code: form.code.trim(),
          name: form.name,
          address: form.address,
          phone: form.phone || null,
          isMain: form.isMain,
        });
        await fetchList();
        setEditingId(created.id);
        setEditingCode(created.code);
        setEditingName(created.name);
        setSelectedId(created.id);
        reset({
          branchId: created.branchId,
          code: created.code,
          name: created.name,
          address: created.address,
          phone: created.phone ?? "",
          isMain: created.isMain,
        });
        message.success("Establecimiento creado correctamente.");
      }
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setFieldError, (msg) =>
        setSaveError(msg),
      );
      if (!applied) setSaveError("Error al guardar el establecimiento.");
    } finally {
      setSaving(false);
    }
  });

  // ── Toggle activo/inactivo ────────────────────────────────────────────────
  const toggleDisable = async (item: EstablishmentListItemDto) => {
    if (togglingId) return;
    if (item.isActive) {
      if (!canDisable) return;
    } else if (!canUpdate) {
      return;
    }
    const confirmed = await message.confirm({
      title: item.isActive ? `Desactivar "${item.name}"` : `Activar "${item.name}"`,
      message: item.isActive
        ? `"${item.name}" dejará de estar disponible para nuevas operaciones y emisión documental. El histórico existente no se elimina.`
        : `"${item.name}" volverá a estar disponible para nuevas operaciones.`,
      variant: item.isActive ? "danger" : "warning",
      confirmLabel: item.isActive ? "Desactivar" : "Activar",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;
    setError("");
    setTogglingId(item.id);
    try {
      if (item.isActive) {
        await establishmentService.disable(item.id);
      } else {
        await establishmentService.enable(item.id);
      }
      await fetchList();
      message.success(
        item.isActive
          ? "Establecimiento desactivado correctamente."
          : "Establecimiento activado correctamente.",
      );
    } catch (err: unknown) {
      const msg = formatApiRequestError(err, {
        generic: "Error al cambiar el estado del establecimiento.",
      });
      setError(msg);
      message.error(msg);
    } finally {
      setTogglingId(null);
    }
  };

  return {
    // Permisos
    canView,
    canCreate,
    canUpdate,
    canDisable,
    // Filtro de sucursal (desde URL)
    branchIdFilter,
    // Listado
    items,
    filtered,
    loading,
    error,
    search,
    setSearch,
    activeStatus,
    setActiveStatus,
    totals,
    fetchList,
    // Panel
    panelOpen,
    editingId,
    editingCode,
    editingName,
    selectedId,
    saving,
    saveError,
    togglingId,
    // Sucursales
    branches,
    loadingBranches,
    // Formulario
    register,
    control,
    errors,
    // Acciones
    openCreate,
    openEdit,
    closePanel,
    save,
    toggleDisable,
  };
}

export type EstablishmentsPageContext = ReturnType<
  typeof useEstablishmentsPage
>;
