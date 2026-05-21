import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import {
  catalogService,
  type CatalogItem,
  type ProductCategoryListItem,
  type ProductSubcategoryListItem,
} from '../api/catalogService';
import { formatApiError } from '../../lib/formatApiError';
import type { CatalogStructureModalForm, CatalogStructureModalType } from './catalogStructureTypes';

export function useCatalogStructurePage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);

  const canViewLines = isAdmin || hasPerm('inventory.product-lines.view');
  const canViewCategories = isAdmin || hasPerm('inventory.categories.view');
  const canViewSubcategories = isAdmin || hasPerm('inventory.subcategories.view');
  const canView = canViewLines && canViewCategories && canViewSubcategories;
  const canCreateLines = isAdmin || hasPerm('inventory.product-lines.create');
  const canCreateCategories = isAdmin || hasPerm('inventory.categories.create');
  const canCreateSubcategories = isAdmin || hasPerm('inventory.subcategories.create');
  const canUpdateLines = isAdmin || hasPerm('inventory.product-lines.update');
  const canDeleteLines = isAdmin || hasPerm('inventory.product-lines.delete');
  const canUpdateCategories = isAdmin || hasPerm('inventory.categories.update');
  const canDeleteCategories = isAdmin || hasPerm('inventory.categories.delete');
  const canUpdateSubcategories = isAdmin || hasPerm('inventory.subcategories.update');
  const canDeleteSubcategories = isAdmin || hasPerm('inventory.subcategories.delete');

  const [lines, setLines] = useState<CatalogItem[]>([]);
  const [categories, setCategories] = useState<ProductCategoryListItem[]>([]);
  const [subcategories, setSubcategories] = useState<ProductSubcategoryListItem[]>([]);
  const [linesLoading, setLinesLoading] = useState(false);
  const [catsLoading, setCatsLoading] = useState(false);
  const [subsLoading, setSubsLoading] = useState(false);

  const [selectedLineId, setSelectedLineId] = useState<string | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);

  const selectedLine = lines.find((l) => l.id === selectedLineId) ?? null;
  const selectedCategory = categories.find((c) => c.id === selectedCategoryId) ?? null;

  const [modal, setModal] = useState<CatalogStructureModalType | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [modalCats, setModalCats] = useState<ProductCategoryListItem[]>([]);

  const form = useForm<CatalogStructureModalForm>({
    defaultValues: { code: '', name: '', lineId: '', categoryId: '' },
  });

  const { handleSubmit, reset, watch, setValue, register, formState: { errors } } = form;
  const watchedLineId = watch('lineId');

  const loadLines = useCallback(async () => {
    setLinesLoading(true);
    try {
      setLines((await catalogService.productLines({ activeStatus: 'all' })) ?? []);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setLinesLoading(false);
    }
  }, []);

  const loadCategories = useCallback(async (lineId: string) => {
    setCatsLoading(true);
    try {
      setCategories((await catalogService.categories({ activeStatus: 'all', lineId })) ?? []);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setCatsLoading(false);
    }
  }, []);

  const loadSubcategories = useCallback(async (categoryId: string) => {
    setSubsLoading(true);
    try {
      setSubcategories((await catalogService.subcategories({ activeStatus: 'all', categoryId })) ?? []);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSubsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (canView) void loadLines();
  }, [canView, loadLines]);

  useEffect(() => {
    if (!selectedLineId) {
      setCategories([]);
      setSubcategories([]);
      return;
    }
    setSelectedCategoryId(null);
    setSubcategories([]);
    void loadCategories(selectedLineId);
  }, [selectedLineId, loadCategories]);

  useEffect(() => {
    if (!selectedCategoryId) {
      setSubcategories([]);
      return;
    }
    void loadSubcategories(selectedCategoryId);
  }, [selectedCategoryId, loadSubcategories]);

  useEffect(() => {
    if (!modal || (modal.kind !== 'create-subcategory' && modal.kind !== 'edit-subcategory')) return;
    if (!watchedLineId) {
      setModalCats([]);
      return;
    }
    catalogService
      .categories({ activeStatus: 'all', lineId: watchedLineId })
      .then((d) => setModalCats(d ?? []))
      .catch(() => setModalCats([]));
  }, [modal, watchedLineId]);

  const selectLine = (id: string) => {
    setSelectedLineId((prev) => (prev === id ? null : id));
  };

  const selectCategory = (id: string) => {
    setSelectedCategoryId((prev) => (prev === id ? null : id));
  };

  const openCreate = (kind: 'create-line' | 'create-category' | 'create-subcategory') => {
    setError('');
    reset({ code: '', name: '', lineId: selectedLineId ?? '', categoryId: selectedCategoryId ?? '' });
    setModal({ kind });
  };

  const openEdit = (
    item: CatalogItem | ProductCategoryListItem | ProductSubcategoryListItem,
    kind: CatalogStructureModalType['kind'],
  ) => {
    setError('');
    if (kind === 'edit-line') {
      const row = item as CatalogItem;
      reset({ code: row.code, name: row.name, lineId: '', categoryId: '' });
      setModal({ kind, item: row });
    } else if (kind === 'edit-category') {
      const row = item as ProductCategoryListItem;
      reset({ code: row.code, name: row.name, lineId: row.lineId, categoryId: '' });
      setModal({ kind, item: row });
    } else if (kind === 'edit-subcategory') {
      const row = item as ProductSubcategoryListItem;
      reset({ code: row.code, name: row.name, lineId: row.lineId, categoryId: row.categoryId });
      setModal({ kind, item: row });
    }
  };

  const closeModal = () => {
    setModal(null);
    setError('');
  };

  const onSubmit = handleSubmit(async (values) => {
    if (!modal) return;
    setSaving(true);
    setError('');
    try {
      const code = values.code.trim();
      const name = values.name.trim();

      switch (modal.kind) {
        case 'create-line':
          await catalogService.createProductLine({ code, name });
          await loadLines();
          break;
        case 'edit-line':
          await catalogService.updateProductLine(modal.item.id, { code, name });
          await loadLines();
          break;
        case 'create-category':
          await catalogService.createCategory({ code, name, lineId: values.lineId });
          if (selectedLineId) await loadCategories(selectedLineId);
          break;
        case 'edit-category':
          await catalogService.updateCategory(modal.item.id, { code, name, lineId: values.lineId });
          if (selectedLineId) await loadCategories(selectedLineId);
          break;
        case 'create-subcategory':
          await catalogService.createSubcategory({ code, name, categoryId: values.categoryId });
          if (selectedCategoryId) await loadSubcategories(selectedCategoryId);
          break;
        case 'edit-subcategory':
          await catalogService.updateSubcategory(modal.item.id, { code, name, categoryId: values.categoryId });
          if (selectedCategoryId) await loadSubcategories(selectedCategoryId);
          break;
      }
      closeModal();
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSaving(false);
    }
  });

  const toggleLine = async (item: CatalogItem) => {
    setSaving(true);
    try {
      if (item.isActive) await catalogService.disableProductLine(item.id);
      else await catalogService.enableProductLine(item.id);
      await loadLines();
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSaving(false);
    }
  };

  const toggleCategory = async (item: ProductCategoryListItem) => {
    setSaving(true);
    try {
      if (item.isActive) await catalogService.disableCategory(item.id);
      else await catalogService.enableCategory(item.id);
      if (selectedLineId) await loadCategories(selectedLineId);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSaving(false);
    }
  };

  const toggleSubcategory = async (item: ProductSubcategoryListItem) => {
    setSaving(true);
    try {
      if (item.isActive) await catalogService.disableSubcategory(item.id);
      else await catalogService.enableSubcategory(item.id);
      if (selectedCategoryId) await loadSubcategories(selectedCategoryId);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSaving(false);
    }
  };

  const modalTitle = () => {
    if (!modal) return '';
    const labels: Record<CatalogStructureModalType['kind'], string> = {
      'create-line': t('catalog.structure.primaryCreateLine'),
      'edit-line': t('catalog.structure.editLine'),
      'create-category': t('catalog.structure.primaryCreateCategory'),
      'edit-category': t('catalog.structure.editCategory'),
      'create-subcategory': t('catalog.structure.primaryCreateSubcategory'),
      'edit-subcategory': t('catalog.structure.editSubcategory'),
    };
    return labels[modal.kind] ?? '';
  };

  const showLineSelector =
    modal?.kind === 'create-category' ||
    modal?.kind === 'edit-category' ||
    modal?.kind === 'create-subcategory' ||
    modal?.kind === 'edit-subcategory';
  const showCategorySelector = modal?.kind === 'create-subcategory' || modal?.kind === 'edit-subcategory';

  return {
    t,
    canView,
    canCreateLines,
    canCreateCategories,
    canCreateSubcategories,
    canUpdateLines,
    canDeleteLines,
    canUpdateCategories,
    canDeleteCategories,
    canUpdateSubcategories,
    canDeleteSubcategories,
    lines,
    categories,
    subcategories,
    linesLoading,
    catsLoading,
    subsLoading,
    selectedLineId,
    selectedCategoryId,
    selectedLine,
    selectedCategory,
    modal,
    saving,
    error,
    modalCats,
    register,
    errors,
    watchedLineId,
    setValue,
    selectLine,
    selectCategory,
    openCreate,
    openEdit,
    closeModal,
    onSubmit,
    toggleLine,
    toggleCategory,
    toggleSubcategory,
    modalTitle,
    showLineSelector,
    showCategorySelector,
  };
}
