import { useCallback, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import {
  branchService,
  type BranchListItemDto,
  type BranchDetailDto,
  type GeographyItemDto,
} from '../api/branchService';
import { branchFormSchema, type BranchFormValues } from '../schemas/branchSchema';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { applyServerErrors } from '../../lib/validationErrors';
import { message } from '../../../lib/messages';

export function emptyBranchForm(): BranchFormValues {
  return {
    name: '',
    description: '',
    isMainBranch: false,
    address: '',
    countryId: '',
    provinceId: '',
    cantonId: '',
    parishId: '',
    reference: '',
    postalCode: '',
    latitude: '',
    longitude: '',
    phone: '',
    secondaryPhone: '',
    email: '',
    website: '',
    managerName: '',
    managerPosition: '',
    managerEmail: '',
    managerPhone: '',
    isActive: true,
    openingDate: '',
    internalNotes: '',
  };
}

export function branchFormFromDto(d: BranchListItemDto | BranchDetailDto): BranchFormValues {
  const detail = d as Partial<BranchDetailDto>;
  return {
    name: d.name,
    description: detail.description ?? '',
    isMainBranch: d.isMainBranch,
    address: d.address,
    countryId: d.countryId ?? '',
    provinceId: d.provinceId ?? '',
    cantonId: d.cantonId ?? '',
    parishId: d.parishId ?? '',
    reference: detail.reference ?? '',
    postalCode: detail.postalCode ?? '',
    latitude: detail.latitude ?? '',
    longitude: detail.longitude ?? '',
    phone: d.phone ?? '',
    secondaryPhone: detail.secondaryPhone ?? '',
    email: d.email ?? '',
    website: detail.website ?? '',
    managerName: d.managerName ?? '',
    managerPosition: detail.managerPosition ?? '',
    managerEmail: detail.managerEmail ?? '',
    managerPhone: detail.managerPhone ?? '',
    isActive: d.isActive,
    openingDate: detail.openingDate ?? '',
    internalNotes: detail.internalNotes ?? '',
  };
}

export function useBranchesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow('settings.branches.view');
  const canCreate = canShow('settings.branches.create');
  const canUpdate = canShow('settings.branches.update');
  const canDelete = canShow('settings.branches.delete');

  const [items, setItems] = useState<BranchListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');

  const [panelOpen, setPanelOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editCode, setEditCode] = useState<string | null>(null);
  const [editName, setEditName] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
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
    setError: setFieldError,
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
    if (!countryId) { setProvinces([]); return; }
    setLoadingProvinces(true);
    try { setProvinces(await branchService.provinces(countryId)); }
    catch { setProvinces([]); }
    finally { setLoadingProvinces(false); }
  }, []);

  const loadCantons = useCallback(async (provinceId: string) => {
    if (!provinceId) { setCantons([]); return; }
    setLoadingCantons(true);
    try { setCantons(await branchService.cantons(provinceId)); }
    catch { setCantons([]); }
    finally { setLoadingCantons(false); }
  }, []);

  const loadParishes = useCallback(async (cantonId: string) => {
    if (!cantonId) { setParishes([]); return; }
    setLoadingParishes(true);
    try { setParishes(await branchService.parishes(cantonId)); }
    catch { setParishes([]); }
    finally { setLoadingParishes(false); }
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

  const openCreate = () => {
    setEditingId(null);
    setEditCode(null);
    setEditName(null);
    setSelectedId(null);
    setSaveError('');
    const defaultCountry = countries.find((c) => c.id === 'EC') ? 'EC' : '';
    reset({ ...emptyBranchForm(), countryId: defaultCountry });
    setProvinces([]);
    setCantons([]);
    setParishes([]);
    if (defaultCountry) void loadProvinces(defaultCountry);
    setPanelOpen(true);
  };

  const openEdit = async (id: string) => {
    setSaveError('');
    try {
      const d = await branchService.getById(id);
      setEditingId(id);
      setEditCode(d.code);
      setEditName(d.name);
      setSelectedId(id);
      reset(branchFormFromDto(d));
      setProvinces([]);
      setCantons([]);
      setParishes([]);
      await loadProvinces(d.countryId ?? '');
      await loadCantons(d.provinceId ?? '');
      await loadParishes(d.cantonId ?? '');
      setPanelOpen(true);
    } catch {
      setError(t('branches.error.loadOne'));
    }
  };

  const closePanel = () => {
    setPanelOpen(false);
    setEditingId(null);
    setEditCode(null);
    setEditName(null);
    setSelectedId(null);
    setSaveError('');
  };

  const save = handleSubmit(async (form) => {
    setSaveError('');
    setSaving(true);
    try {
      const payload = {
        name: form.name.trim(),
        address: form.address.trim(),
        description: form.description || null,
        reference: form.reference || null,
        postalCode: form.postalCode || null,
        phone: form.phone || null,
        secondaryPhone: form.secondaryPhone || null,
        email: form.email || null,
        website: form.website || null,
        managerName: form.managerName || null,
        managerPosition: form.managerPosition || null,
        managerEmail: form.managerEmail || null,
        managerPhone: form.managerPhone || null,
        countryId: form.countryId || null,
        provinceId: form.provinceId || null,
        cantonId: form.cantonId || null,
        parishId: form.parishId || null,
        latitude: form.latitude || null,
        longitude: form.longitude || null,
        openingDate: form.openingDate || null,
        internalNotes: form.internalNotes || null,
        isActive: form.isActive,
        isMainBranch: form.isMainBranch,
      };
      if (editingId) {
        const updated = await branchService.update(editingId, { id: editingId, ...payload });
        await fetchList();
        setEditName(updated.name);
        message.success('Sucursal actualizada correctamente.');
      } else {
        const created = await branchService.create(payload);
        await fetchList();
        setEditingId(created.id);
        setEditCode(created.code);
        setEditName(created.name);
        setSelectedId(created.id);
        message.success('Sucursal creada correctamente.');
      }
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setFieldError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(t('branches.error.save'));
    } finally {
      setSaving(false);
    }
  });

  const toggleDisable = async (row: BranchListItemDto) => {
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
    panelOpen,
    editingId,
    editCode,
    editName,
    selectedId,
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
    openCreate,
    openEdit,
    closePanel,
    save,
    toggleDisable,
  };
}

export type BranchesPageContext = ReturnType<typeof useBranchesPage>;
