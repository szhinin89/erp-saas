import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../../i18n/i18n';
import { branchService, type BranchListItemDto } from '../../../branches/api/branchService';
import { warehouseService, type WarehouseDto } from '../api/warehouseService';
import { applyServerErrors } from '../../../lib/validationErrors';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { message } from '../../../../lib/messages';
import {
  warehouseSchema,
  defaultWarehouseValues,
  type WarehouseFormValues,
} from '../../../../schemas/inventory/warehouseSchema';

export function fromWarehouseDto(d: WarehouseDto): WarehouseFormValues {
  return {
    branchId: d.branchId,
    name: d.name,
    storageType: d.storageType ?? '',
    address: d.address ?? '',
    phone: d.phone ?? '',
    email: d.email ?? '',
    manager: d.manager ?? '',
    latitude: d.latitude ?? '',
    longitude: d.longitude ?? '',
    capacity: d.capacity ?? null,
    dailyDispatchGoal: d.dailyDispatchGoal ?? null,
  };
}

export function useWarehousesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();

  const canView = canShow('inventory.warehouses.view');
  const canCreate = canShow('inventory.warehouses.create');
  const canUpdate = canShow('inventory.warehouses.update');
  const canDelete = canShow('inventory.warehouses.delete');

  const [items, setItems] = useState<WarehouseDto[]>([]);
  const [branches, setBranches] = useState<BranchListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');

  const [panelOpen, setPanelOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editCode, setEditCode] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const form = useForm<WarehouseFormValues>({
    resolver: zodResolver(warehouseSchema),
    defaultValues: defaultWarehouseValues,
  });

  const { handleSubmit, reset, formState: { errors } } = form;

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems((await warehouseService.list('all')) ?? []);
    } catch {
      setError(t('common.errorPrefix'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  useEffect(() => {
    branchService.list('active').then(setBranches).catch(() => setBranches([]));
  }, []);

  const totals = useMemo(
    () => ({
      total: items.length,
      active: items.filter((x) => x.isActive).length,
      inactive: items.filter((x) => !x.isActive).length,
      branches: [...new Set(items.map((x) => x.branchId))].length,
    }),
    [items],
  );

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (x) =>
        x.name.toLowerCase().includes(q) ||
        (x.code ?? '').toLowerCase().includes(q) ||
        (x.manager ?? '').toLowerCase().includes(q) ||
        (x.address ?? '').toLowerCase().includes(q),
    );
  }, [items, search]);

  const branchName = (id: string) => branches.find((b) => b.id === id)?.name ?? id;

  const openCreate = () => {
    setEditingId(null);
    setEditCode(null);
    setSelectedId(null);
    setSaveError('');
    reset(defaultWarehouseValues);
    setPanelOpen(true);
  };

  const openEdit = async (id: string) => {
    setSaveError('');
    try {
      const d = await warehouseService.getById(id);
      if (!d) return;
      setEditingId(id);
      setEditCode(d.code);
      setSelectedId(id);
      reset(fromWarehouseDto(d));
      setPanelOpen(true);
    } catch {
      setError(t('common.errorPrefix'));
    }
  };

  const closePanel = () => {
    setPanelOpen(false);
    setEditingId(null);
    setEditCode(null);
    setSelectedId(null);
    setSaveError('');
  };

  const save = handleSubmit(async (formValues) => {
    setSaveError('');
    setSaving(true);
    try {
      const payload = {
        branchId: formValues.branchId,
        name: formValues.name.trim(),
        storageType: formValues.storageType || null,
        address: formValues.address || null,
        phone: formValues.phone || null,
        email: formValues.email || null,
        manager: formValues.manager || null,
        latitude: formValues.latitude || null,
        longitude: formValues.longitude || null,
        capacity: formValues.capacity ?? null,
        dailyDispatchGoal: formValues.dailyDispatchGoal ?? null,
      };
      if (editingId) {
        await warehouseService.update(editingId, { id: editingId, ...payload });
        await fetchList();
        message.success(t('warehouses.updated.success', 'Bodega actualizada correctamente.'));
      } else {
        const created = await warehouseService.create(payload);
        await fetchList();
        setEditingId(created.id);
        setEditCode(created.code);
        setSelectedId(created.id);
        reset(fromWarehouseDto(created));
        message.success(t('warehouses.created.success', 'Bodega creada correctamente.'));
      }
    } catch (err: unknown) {
      const applied = applyServerErrors(err, form.setError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(t('common.errorPrefix'));
    } finally {
      setSaving(false);
    }
  });

  const toggleStatus = async (row: WarehouseDto) => {
    setError('');
    try {
      if (row.isActive) {
        if (!canDelete) return;
        await warehouseService.disable(row.id);
      } else {
        if (!canUpdate) return;
        await warehouseService.enable(row.id);
      }
      await fetchList();
    } catch {
      setError(t('common.errorPrefix'));
    }
  };

  return {
    t,
    canView,
    canCreate,
    canUpdate,
    canDelete,
    items,
    branches,
    loading,
    error,
    search,
    setSearch,
    filtered,
    totals,
    panelOpen,
    editingId,
    editCode,
    selectedId,
    saving,
    saveError,
    form,
    errors,
    branchName,
    fetchList,
    openCreate,
    openEdit,
    closePanel,
    save,
    toggleStatus,
  };
}
