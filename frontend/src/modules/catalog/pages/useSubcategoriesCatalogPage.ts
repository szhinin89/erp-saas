import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import {
  catalogService,
  type ProductCategoryListItem,
  type ProductSubcategoryListItem,
} from '../api/catalogService';
import {
  catalogSubcategoryFormSchema,
  type CatalogSubcategoryFormValues,
} from '../../../schemas/catalog/catalogPagesFormsSchema';

export function useSubcategoriesCatalogPage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = isAdmin || hasPerm('inventory.subcategories.view');
  const canCreate = isAdmin || hasPerm('inventory.subcategories.create');

  const [lines, setLines] = useState<{ id: string; code: string; name: string }[]>([]);
  const [categories, setCategories] = useState<ProductCategoryListItem[]>([]);
  const [items, setItems] = useState<ProductSubcategoryListItem[]>([]);
  const [formCategories, setFormCategories] = useState<ProductCategoryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [filterLineId, setFilterLineId] = useState('');
  const [filterCategoryId, setFilterCategoryId] = useState('');
  const [listQuery, setListQuery] = useState('');
  const [tab, setTab] = useState<'data' | 'list'>('data');

  const form = useForm<CatalogSubcategoryFormValues>({
    resolver: zodResolver(catalogSubcategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '', categoryId: '' },
  });

  const { handleSubmit, reset, watch, setValue, register, formState: { errors } } = form;
  const formLineId = watch('lineId');
  const formWatch = watch();
  const lineIdField = register('lineId');

  const onFilterLineChange = (lineId: string) => {
    setFilterLineId(lineId);
    setFilterCategoryId('');
  };

  useEffect(() => {
    let cancelled = false;
    if (!formLineId) {
      const id = window.setTimeout(() => {
        if (!cancelled) setFormCategories([]);
      }, 0);
      return () => {
        cancelled = true;
        window.clearTimeout(id);
      };
    }
    void catalogService
      .categories({ activeStatus: 'all', lineId: formLineId })
      .then((c) => {
        if (!cancelled) setFormCategories(c ?? []);
      })
      .catch(() => {
        if (!cancelled) setFormCategories([]);
      });
    return () => {
      cancelled = true;
    };
  }, [formLineId]);

  const refresh = async () => {
    setError('');
    setLoading(true);
    try {
      const li = await catalogService.productLines({ activeStatus: 'all' });
      setLines(li ?? []);

      const cats = filterLineId ? await catalogService.categories({ activeStatus: 'all', lineId: filterLineId }) : [];
      setCategories(cats ?? []);

      const subs = filterCategoryId
        ? await catalogService.subcategories({ activeStatus: 'all', categoryId: filterCategoryId })
        : [];
      setItems(subs ?? []);
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
  }, [canView, filterLineId, filterCategoryId]);

  const listFiltered = useMemo(() => {
    if (!canView) return [];
    const q = listQuery.trim().toLowerCase();
    if (!q) return items;
    return items.filter((x) =>
      `${x.code} ${x.name} ${x.lineCode} ${x.lineName} ${x.categoryCode} ${x.categoryName}`.toLowerCase().includes(q),
    );
  }, [canView, items, listQuery]);

  const onCreate = handleSubmit(async (formValues) => {
    setError('');
    setSaving(true);
    try {
      const selectedCat = formCategories.find((c) => c.id === formValues.categoryId);
      if (!selectedCat) {
        setError(t('catalog.subcategories.validation.categoryRequired'));
        setSaving(false);
        return;
      }

      await catalogService.createSubcategory({
        code: formValues.code.trim(),
        name: formValues.name.trim(),
        categoryId: formValues.categoryId,
      });
      reset({ code: '', name: '', lineId: '', categoryId: '' });
      await refresh();
      setTab('list');
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        (err as Error)?.message ??
        t('common.errorGeneric');
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
    categories,
    items,
    formCategories,
    loading,
    saving,
    error,
    filterLineId,
    filterCategoryId,
    setFilterCategoryId,
    listQuery,
    setListQuery,
    tab,
    setTab,
    errors,
    formWatch,
    lineIdField,
    setValue,
    register,
    listFiltered,
    onFilterLineChange,
    onCreate,
  };
}
