import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { catalogService, type ProductCategoryListItem } from '../api/catalogService';
import {
  catalogCategoryFormSchema,
  type CatalogCategoryFormValues,
} from '../../../schemas/catalog/catalogPagesFormsSchema';

export function useCategoriesCatalogPage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = isAdmin || hasPerm('inventory.categories.view');
  const canCreate = isAdmin || hasPerm('inventory.categories.create');

  const [lines, setLines] = useState<{ id: string; code: string; name: string }[]>([]);
  const [items, setItems] = useState<ProductCategoryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [filterLineId, setFilterLineId] = useState('');
  const [listQuery, setListQuery] = useState('');
  const [tab, setTab] = useState<'data' | 'list'>('data');

  const form = useForm<CatalogCategoryFormValues>({
    resolver: zodResolver(catalogCategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '' },
  });

  const { handleSubmit, reset, watch, formState: { errors } } = form;
  const formWatch = watch();

  const refresh = async () => {
    setError('');
    setLoading(true);
    try {
      const [li, cats] = await Promise.all([
        catalogService.productLines({ activeStatus: 'all' }),
        catalogService.categories({ activeStatus: 'all', lineId: filterLineId || undefined }),
      ]);
      setLines(li ?? []);
      setItems(cats ?? []);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canView) return;
    void Promise.resolve().then(refresh);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canView, filterLineId]);

  const lineLabel = (row: ProductCategoryListItem) => `${row.lineCode} — ${row.lineName}`;

  const listFiltered = useMemo(() => {
    if (!canView) return [];
    const q = listQuery.trim().toLowerCase();
    if (!q) return items;
    return items.filter((x) => `${x.code} ${x.name} ${x.lineCode} ${x.lineName}`.toLowerCase().includes(q));
  }, [canView, items, listQuery]);

  const onCreate = handleSubmit(async (formValues) => {
    setError('');
    setSaving(true);
    try {
      await catalogService.createCategory({
        code: formValues.code.trim(),
        name: formValues.name.trim(),
        lineId: formValues.lineId,
      });
      reset({ code: '', name: '', lineId: '' });
      await refresh();
      setTab('list');
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setSaving(false);
    }
  });

  return {
    t,
    canView,
    canCreate,
    lines,
    items,
    loading,
    saving,
    error,
    filterLineId,
    setFilterLineId,
    listQuery,
    setListQuery,
    tab,
    setTab,
    form,
    errors,
    formWatch,
    listFiltered,
    lineLabel,
    onCreate,
  };
}
