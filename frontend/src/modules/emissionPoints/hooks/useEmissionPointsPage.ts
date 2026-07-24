import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  emissionPointsService,
  type EmissionPointListItemDto,
  type EstablishmentLookupDto,
} from '../api/emissionPointsService';
import {
  emissionPointsPageSchema,
  emptyEmissionPointsPageForm,
  type EmissionPointsPageFormValues,
} from '../schemas/emissionPointsPageSchema';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { applyServerErrors } from '../../lib/validationErrors';
import { message } from '../../../lib/messages';

type CatalogActiveStatus = 'all' | 'active' | 'inactive';

export function useEmissionPointsPage() {
  const { canShow } = usePermissionsUi();
  const canView   = canShow('settings.emission-points.view');
  const canCreate = canShow('settings.emission-points.create');
  const canUpdate = canShow('settings.emission-points.update');
  const canDelete = canShow('settings.emission-points.delete');

  // ── Datos del listado ────────────────────────────────────────────────────
  const [items, setItems]     = useState<EmissionPointListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState('');
  const [search, setSearch]   = useState('');
  const [activeStatus, setActiveStatus] = useState<CatalogActiveStatus>('active');

  // ── Establecimientos para el selector ────────────────────────────────────
  const [establishments, setEstablishments]           = useState<EstablishmentLookupDto[]>([]);
  const [loadingEstablishments, setLoadingEstablishments] = useState(false);

  // ── Panel ────────────────────────────────────────────────────────────────
  const [panelOpen, setPanelOpen]       = useState(false);
  const [editingId, setEditingId]       = useState<string | null>(null);
  const [editingCode, setEditingCode]   = useState<string | null>(null);
  const [editingName, setEditingName]   = useState<string | null>(null);
  const [selectedId, setSelectedId]     = useState<string | null>(null);
  const [saving, setSaving]             = useState(false);
  const [saveError, setSaveError]       = useState('');

  const {
    register,
    control,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors },
  } = useForm<EmissionPointsPageFormValues>({
    resolver: zodResolver(emissionPointsPageSchema),
    defaultValues: emptyEmissionPointsPageForm(),
  });

  // ── Carga de datos ────────────────────────────────────────────────────────
  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await emissionPointsService.list(activeStatus, search || undefined));
    } catch {
      setError('Error al cargar los puntos de emisión.');
    } finally {
      setLoading(false);
    }
  }, [activeStatus, search]);

  const loadEstablishments = useCallback(async () => {
    setLoadingEstablishments(true);
    try {
      setEstablishments(await emissionPointsService.establishmentLookups());
    } catch {
      // selector mostrará vacío
    } finally {
      setLoadingEstablishments(false);
    }
  }, []);

  useEffect(() => { void fetchList(); }, [fetchList]);

  // ── Totales calculados ────────────────────────────────────────────────────
  const totals = {
    total:      items.length,
    active:     items.filter((i) => i.isActive).length,
    inactive:   items.filter((i) => !i.isActive).length,
    electronic: items.filter((i) => i.emissionType === 'Electronic').length,
    physical:   items.filter((i) => i.emissionType === 'Physical').length,
  };

  // ── Filtrado local ────────────────────────────────────────────────────────
  const filtered = search.trim()
    ? items.filter((i) =>
        i.code.toLowerCase().includes(search.toLowerCase()) ||
        (i.name?.toLowerCase().includes(search.toLowerCase()) ?? false) ||
        i.establishmentName.toLowerCase().includes(search.toLowerCase()),
      )
    : items;

  // ── Panel — abrir para crear ──────────────────────────────────────────────
  const openCreate = async () => {
    setEditingId(null);
    setEditingCode(null);
    setEditingName(null);
    setSelectedId(null);
    setSaveError('');
    reset(emptyEmissionPointsPageForm());
    await loadEstablishments();
    setPanelOpen(true);
  };

  // ── Panel — abrir para editar ─────────────────────────────────────────────
  const openEdit = async (item: EmissionPointListItemDto) => {
    setEditingId(item.id);
    setEditingCode(item.code);
    setEditingName(item.name ?? item.code);
    setSelectedId(item.id);
    setSaveError('');
    reset({
      establishmentId: item.establishmentId,
      code:            item.code,
      name:            item.name ?? '',
      emissionType:    item.emissionType,
      isDefault:       item.isDefault,
    });
    await loadEstablishments();
    setPanelOpen(true);
  };

  const closePanel = () => {
    setPanelOpen(false);
    setEditingId(null);
    setEditingCode(null);
    setEditingName(null);
    setSelectedId(null);
    setSaveError('');
  };

  // ── Guardar ───────────────────────────────────────────────────────────────
  const save = handleSubmit(async (form) => {
    setSaveError('');
    setSaving(true);
    try {
      if (editingId) {
        await emissionPointsService.update(editingId, {
          id:           editingId,
          name:         form.name || null,
          emissionType: form.emissionType,
          isDefault:    form.isDefault,
        });
        await fetchList();
        message.success('Punto de emisión actualizado correctamente.');
      } else {
        const created = await emissionPointsService.create({
          establishmentId: form.establishmentId,
          code:            form.code.trim(),
          name:            form.name || null,
          emissionType:    form.emissionType,
          isDefault:       form.isDefault,
        });
        await fetchList();
        setEditingId(created.id);
        setEditingCode(created.code);
        setEditingName(created.name ?? created.code);
        setSelectedId(created.id);
        reset({
          establishmentId: created.establishmentId,
          code:            created.code,
          name:            created.name ?? '',
          emissionType:    created.emissionType,
          isDefault:       created.isDefault,
        });
        message.success('Punto de emisión creado correctamente.');
      }
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setFieldError, (msg) => setSaveError(msg));
      if (!applied) setSaveError('Error al guardar el punto de emisión.');
    } finally {
      setSaving(false);
    }
  });

  // ── Toggle activo/inactivo ────────────────────────────────────────────────
  const toggleDisable = async (item: EmissionPointListItemDto) => {
    setError('');
    try {
      if (item.isActive) {
        if (!canDelete) return;
        await emissionPointsService.disable(item.id);
      } else {
        if (!canUpdate) return;
        await emissionPointsService.enable(item.id);
      }
      await fetchList();
    } catch {
      setError('Error al cambiar el estado del punto de emisión.');
    }
  };

  return {
    // Permisos
    canView,
    canCreate,
    canUpdate,
    canDelete,
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
    // Establecimientos
    establishments,
    loadingEstablishments,
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

export type EmissionPointsPageContext = ReturnType<typeof useEmissionPointsPage>;
