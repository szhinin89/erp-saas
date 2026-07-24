import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  cajaService,
  type CashRegisterDto,
  type CashRegisterActiveStatus,
  type EmissionPointLookupForBranchDto,
} from '../../caja/api/cajaService';
import { branchService, type BranchListItemDto } from '../../branches/api/branchService';
import { warehouseService, type WarehouseDto } from '../../inventory/warehouses/api/warehouseService';
import {
  cashRegistersPageSchema,
  emptyCashRegistersPageForm,
  type CashRegistersPageFormValues,
} from '../schemas/cashRegistersPageSchema';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { applyServerErrors } from '../../lib/validationErrors';
import { formatApiRequestError } from '../../lib/apiError';
import { message } from '../../../lib/messages';

export function useCashRegistersPage() {
  const { canShow } = usePermissionsUi();
  const canView = canShow('caja.view');
  const canManage = canShow('caja.manage');

  // ── Datos del listado ────────────────────────────────────────────────────
  const [items, setItems] = useState<CashRegisterDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [activeStatus, setActiveStatus] = useState<CashRegisterActiveStatus>('active');

  // ── Sucursales para el selector ───────────────────────────────────────────
  const [branches, setBranches] = useState<BranchListItemDto[]>([]);
  const [loadingBranches, setLoadingBranches] = useState(false);

  // ── Puntos de emisión de la sucursal elegida (cascada) ───────────────────
  const [emissionPoints, setEmissionPoints] = useState<EmissionPointLookupForBranchDto[]>([]);
  const [loadingEmissionPoints, setLoadingEmissionPoints] = useState(false);

  // ── Bodegas de la sucursal elegida (cascada) — para "Bodega por defecto" ──
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [loadingWarehouses, setLoadingWarehouses] = useState(false);

  // ── Panel ────────────────────────────────────────────────────────────────
  const [panelOpen, setPanelOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [editingName, setEditingName] = useState<string | null>(null);
  // Bandera calculada exclusivamente por el backend (CashRegisterDto.hasHistory) — el frontend
  // solo la refleja para bloquear Punto de Emisión, nunca decide por su cuenta si la caja "fue usada".
  const [editingHasHistory, setEditingHasHistory] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    setError: setFieldError,
    formState: { errors },
  } = useForm<CashRegistersPageFormValues>({
    resolver: zodResolver(cashRegistersPageSchema),
    defaultValues: emptyCashRegistersPageForm(),
  });

  const watchedBranchId = watch('branchId');

  // ── Carga de datos ────────────────────────────────────────────────────────
  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await cajaService.listAllCashRegisters(activeStatus, search || undefined));
    } catch {
      setError('Error al cargar las cajas.');
    } finally {
      setLoading(false);
    }
  }, [activeStatus, search]);

  const loadBranches = useCallback(async () => {
    setLoadingBranches(true);
    try {
      setBranches(await branchService.list('active'));
    } catch {
      // selector mostrará vacío
    } finally {
      setLoadingBranches(false);
    }
  }, []);

  const loadEmissionPoints = useCallback(async (branchId: string) => {
    if (!branchId) {
      setEmissionPoints([]);
      return;
    }
    setLoadingEmissionPoints(true);
    try {
      setEmissionPoints(await cajaService.emissionPointLookupsByBranch(branchId));
    } catch {
      setEmissionPoints([]);
    } finally {
      setLoadingEmissionPoints(false);
    }
  }, []);

  const loadWarehouses = useCallback(async (branchId: string) => {
    if (!branchId) {
      setWarehouses([]);
      return;
    }
    setLoadingWarehouses(true);
    try {
      setWarehouses(await warehouseService.list('active', undefined, branchId));
    } catch {
      setWarehouses([]);
    } finally {
      setLoadingWarehouses(false);
    }
  }, []);

  useEffect(() => { void fetchList(); }, [fetchList]);

  // Recarga la cascada de Puntos de Emisión/Bodegas cada vez que cambia la Sucursal elegida en el form.
  useEffect(() => {
    if (panelOpen) {
      void loadEmissionPoints(watchedBranchId);
      void loadWarehouses(watchedBranchId);
    }
  }, [watchedBranchId, panelOpen, loadEmissionPoints, loadWarehouses]);

  // ── Totales calculados ────────────────────────────────────────────────────
  const totals = {
    total: items.length,
    active: items.filter((i) => i.isActive).length,
    inactive: items.filter((i) => !i.isActive).length,
  };

  // ── Filtrado local ────────────────────────────────────────────────────────
  const filtered = search.trim()
    ? items.filter((i) =>
        i.code.toLowerCase().includes(search.toLowerCase()) ||
        i.name.toLowerCase().includes(search.toLowerCase()) ||
        i.branchName.toLowerCase().includes(search.toLowerCase()),
      )
    : items;

  // Establecimiento derivado del Punto de Emisión elegido — solo informativo, nunca seleccionable.
  const selectedEmissionPointId = watch('emissionPointId');
  const derivedEstablishmentCode =
    emissionPoints.find((ep) => ep.id === selectedEmissionPointId)?.establishmentCode ?? null;

  // ── Panel — abrir para crear ──────────────────────────────────────────────
  const openCreate = async () => {
    setEditingId(null);
    setEditingCode(null);
    setEditingName(null);
    setEditingHasHistory(false);
    setSelectedId(null);
    setSaveError('');
    setEmissionPoints([]);
    setWarehouses([]);
    reset(emptyCashRegistersPageForm());
    await loadBranches();
    setPanelOpen(true);
  };

  // ── Panel — abrir para editar ─────────────────────────────────────────────
  const openEdit = async (item: CashRegisterDto) => {
    setEditingId(item.id);
    setEditingCode(item.code);
    setEditingName(item.name);
    setEditingHasHistory(item.hasHistory);
    setSelectedId(item.id);
    setSaveError('');
    reset({
      branchId: item.branchId,
      code: item.code,
      name: item.name,
      emissionPointId: item.emissionPointId ?? '',
      notes: item.notes ?? '',
      defaultWarehouseId: item.defaultWarehouseId ?? '',
      defaultCustomerId: item.defaultCustomerId ?? '',
    });
    await loadBranches();
    await loadEmissionPoints(item.branchId);
    await loadWarehouses(item.branchId);
    setPanelOpen(true);
  };

  const closePanel = () => {
    setPanelOpen(false);
    setEditingId(null);
    setEditingCode(null);
    setEditingName(null);
    setEditingHasHistory(false);
    setSelectedId(null);
    setSaveError('');
  };

  // ── Guardar ───────────────────────────────────────────────────────────────
  const save = handleSubmit(async (form) => {
    setSaveError('');
    setSaving(true);
    try {
      if (editingId) {
        await cajaService.updateCashRegister(editingId, {
          id: editingId,
          name: form.name.trim(),
          emissionPointId: form.emissionPointId || null,
          notes: form.notes || null,
          defaultWarehouseId: form.defaultWarehouseId || null,
          defaultCustomerId: form.defaultCustomerId || null,
        });
        await fetchList();
        message.success('Caja actualizada correctamente.');
      } else {
        const created = await cajaService.createCashRegister({
          branchId: form.branchId,
          code: form.code.trim(),
          name: form.name.trim(),
          emissionPointId: form.emissionPointId || null,
          notes: form.notes || null,
          defaultWarehouseId: form.defaultWarehouseId || null,
          defaultCustomerId: form.defaultCustomerId || null,
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
          emissionPointId: created.emissionPointId ?? '',
          notes: created.notes ?? '',
          defaultWarehouseId: created.defaultWarehouseId ?? '',
          defaultCustomerId: created.defaultCustomerId ?? '',
        });
        message.success('Caja creada correctamente.');
      }
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setFieldError, (msg) => setSaveError(msg));
      if (!applied) setSaveError('Error al guardar la caja.');
    } finally {
      setSaving(false);
    }
  });

  // ── Toggle activo/inactivo ────────────────────────────────────────────────
  const toggleDisable = async (item: CashRegisterDto) => {
    setError('');
    if (!canManage) return;
    try {
      if (item.isActive) {
        await cajaService.disableCashRegister(item.id);
      } else {
        await cajaService.enableCashRegister(item.id);
      }
      await fetchList();
    } catch (err: unknown) {
      // El backend expone el motivo exacto (ej. "tiene una sesión de caja abierta") — se
      // muestra tal cual, sin genericizarlo, para que el usuario sepa qué corregir.
      setError(formatApiRequestError(err, { generic: 'Error al cambiar el estado de la caja.' }));
    }
  };

  return {
    // Permisos
    canView,
    canManage,
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
    editingHasHistory,
    selectedId,
    saving,
    saveError,
    // Sucursales / Puntos de emisión
    branches,
    loadingBranches,
    emissionPoints,
    loadingEmissionPoints,
    derivedEstablishmentCode,
    warehouses,
    loadingWarehouses,
    // Formulario
    register,
    control,
    setValue,
    watch,
    errors,
    // Acciones
    openCreate,
    openEdit,
    closePanel,
    save,
    toggleDisable,
  };
}

export type CashRegistersPageContext = ReturnType<typeof useCashRegistersPage>;
