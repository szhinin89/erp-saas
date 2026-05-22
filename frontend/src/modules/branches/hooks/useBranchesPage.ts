import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import {
  branchService,
  type BranchDto,
  type BranchDetailDto,
  type GeographyItemDto,
} from '../api/branchService';
import { branchFormSchema, type BranchFormValues } from '../schemas/branchSchema';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

export const BRANCH_TYPES = ['Flagship Store', 'Boutique', 'Express', 'Almacén / Centro de Distribución'];

export function emptyBranchForm(): BranchFormValues {
  return {
    name: '',
    address: '',
    branchType: '',
    reference: '',
    phones: '',
    email: '',
    managerName: '',
    countryId: '',
    provinceId: '',
    cantonId: '',
    parishId: '',
    latitude: '',
    longitude: '',
    storageCapacity: null,
    dailySalesGoal: null,
    rechargeOption: '',
    isActive: true,
    isMainBranch: false,
  };
}

export function branchFormFromDto(d: BranchDto | BranchDetailDto): BranchFormValues {
  return {
    name: d.name,
    address: d.address,
    branchType: d.branchType ?? '',
    reference: d.reference ?? '',
    phones: d.phones ?? '',
    email: d.email ?? '',
    managerName: d.managerName ?? '',
    countryId: d.countryId ?? '',
    provinceId: d.provinceId ?? '',
    cantonId: d.cantonId ?? '',
    parishId: d.parishId ?? '',
    latitude: d.latitude ?? '',
    longitude: d.longitude ?? '',
    storageCapacity: d.storageCapacity ?? null,
    dailySalesGoal: d.dailySalesGoal ?? null,
    rechargeOption: d.rechargeOption ?? '',
    isActive: d.isActive,
    isMainBranch: d.isMainBranch,
  };
}

export function useBranchesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow('settings.branches.view');
  const canCreate = canShow('settings.branches.create');
  const canUpdate = canShow('settings.branches.update');
  const canDelete = canShow('settings.branches.delete');

  const [items, setItems] = useState<BranchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');

  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editCode, setEditCode] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const [countries, setCountries] = useState<GeographyItemDto[]>([]);
  const [provinces, setProvinces] = useState<GeographyItemDto[]>([]);
  const [cantons, setCantons] = useState<GeographyItemDto[]>([]);
  const [parishes, setParishes] = useState<GeographyItemDto[]>([]);
  const [loadingProvinces, setLoadingProvinces] = useState(false);
  const [loadingCantons, setLoadingCantons] = useState(false);
  const [loadingParishes, setLoadingParishes] = useState(false);

  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors },
  } = useForm<BranchFormValues>({
    resolver: zodResolver(branchFormSchema),
    defaultValues: emptyBranchForm(),
  });
  const formWatch = watch();

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await branchService.list('all'));
    } catch {
      setError(t('branches.error.load'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  useEffect(() => {
    branchService.countries().then(setCountries).catch(() => setCountries([]));
  }, []);

  const loadProvinces = useCallback(async (countryId: string) => {
    if (!countryId) {
      setProvinces([]);
      return;
    }
    setLoadingProvinces(true);
    try {
      setProvinces(await branchService.provinces(countryId));
    } catch {
      setProvinces([]);
    } finally {
      setLoadingProvinces(false);
    }
  }, []);

  const loadCantons = useCallback(async (provinceId: string) => {
    if (!provinceId) {
      setCantons([]);
      return;
    }
    setLoadingCantons(true);
    try {
      setCantons(await branchService.cantons(provinceId));
    } catch {
      setCantons([]);
    } finally {
      setLoadingCantons(false);
    }
  }, []);

  const loadParishes = useCallback(async (cantonId: string) => {
    if (!cantonId) {
      setParishes([]);
      return;
    }
    setLoadingParishes(true);
    try {
      setParishes(await branchService.parishes(cantonId));
    } catch {
      setParishes([]);
    } finally {
      setLoadingParishes(false);
    }
  }, []);

  const onCountryChange = async (countryId: string) => {
    setValue('provinceId', '');
    setValue('cantonId', '');
    setValue('parishId', '');
    setProvinces([]);
    setCantons([]);
    setParishes([]);
    await loadProvinces(countryId);
  };

  const onProvinceChange = async (provinceId: string) => {
    setValue('cantonId', '');
    setValue('parishId', '');
    setCantons([]);
    setParishes([]);
    await loadCantons(provinceId);
  };

  const onCantonChange = async (cantonId: string) => {
    setValue('parishId', '');
    setParishes([]);
    await loadParishes(cantonId);
  };

  const totals = useMemo(
    () => ({
      total: items.length,
      active: items.filter((x) => x.isActive).length,
      main: items.filter((x) => x.isMainBranch).length,
      inactive: items.filter((x) => !x.isActive).length,
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
        (x.managerName ?? '').toLowerCase().includes(q) ||
        (x.address ?? '').toLowerCase().includes(q),
    );
  }, [items, search]);

  const openCreateModal = () => {
    setEditingId(null);
    setEditCode(null);
    setSaveError('');
    const defaultCountry = countries.find((c) => c.id === 'EC') ? 'EC' : '';
    reset({ ...emptyBranchForm(), countryId: defaultCountry });
    setProvinces([]);
    setCantons([]);
    setParishes([]);
    if (defaultCountry) void loadProvinces(defaultCountry);
    setModalOpen(true);
  };

  const openEditModal = async (id: string) => {
    setSaveError('');
    try {
      const d = await branchService.getById(id);
      setEditingId(id);
      setEditCode(d.code);
      reset(branchFormFromDto(d));
      setProvinces([]);
      setCantons([]);
      setParishes([]);
      await loadProvinces(d.countryId ?? '');
      await loadCantons(d.provinceId ?? '');
      await loadParishes(d.cantonId ?? '');
      setModalOpen(true);
    } catch {
      setError(t('branches.error.loadOne'));
    }
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    setEditCode(null);
    setSaveError('');
  };

  const save = handleSubmit(async (form) => {
    setSaveError('');
    setSaving(true);
    try {
      const payload = {
        name: form.name.trim(),
        address: form.address.trim(),
        branchType: form.branchType || null,
        reference: form.reference || null,
        phones: form.phones || null,
        email: form.email || null,
        managerName: form.managerName || null,
        countryId: form.countryId || null,
        provinceId: form.provinceId || null,
        cantonId: form.cantonId || null,
        parishId: form.parishId || null,
        latitude: form.latitude || null,
        longitude: form.longitude || null,
        storageCapacity: form.storageCapacity ?? null,
        dailySalesGoal: form.dailySalesGoal ?? null,
        rechargeOption: form.rechargeOption || null,
        isActive: form.isActive,
        isMainBranch: form.isMainBranch,
      };
      if (editingId) {
        await branchService.update(editingId, { id: editingId, ...payload });
      } else {
        await branchService.create(payload);
      }
      await fetchList();
      closeModal();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('branches.error.save');
      setSaveError(msg);
    } finally {
      setSaving(false);
    }
  });

  const toggleDisable = async (row: BranchDto) => {
    setError('');
    try {
      if (row.isActive) {
        if (!canDelete) return;
        await branchService.disable(row.id);
      } else {
        if (!canUpdate) return;
        await branchService.enable(row.id);
      }
      await fetchList();
    } catch {
      setError(t('branches.error.toggle'));
    }
  };

  return {
    t,
    canView,
    canCreate,
    canUpdate,
    canDelete,
    items,
    loading,
    error,
    search,
    setSearch,
    totals,
    filtered,
    fetchList,
    modalOpen,
    editingId,
    editCode,
    saving,
    saveError,
    countries,
    provinces,
    cantons,
    parishes,
    loadingProvinces,
    loadingCantons,
    loadingParishes,
    register,
    control,
    errors,
    formWatch,
    onCountryChange,
    onProvinceChange,
    onCantonChange,
    openCreateModal,
    openEditModal,
    closeModal,
    save,
    toggleDisable,
  };
}

export type BranchesPageContext = ReturnType<typeof useBranchesPage>;
