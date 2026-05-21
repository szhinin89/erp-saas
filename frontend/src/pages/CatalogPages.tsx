import { CatalogSimplePage, type CatalogRow } from './CatalogSimplePage';
import { catalogService, type ProductCategoryListItem, type ProductSubcategoryListItem } from '../services/catalogService';
export { CatalogStructurePage } from './CatalogStructurePage';
import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { PageShell, EmptyState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { Card } from '../components/ui/Card';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import { ZHBtn, ZHField, ZHGrid } from '../components/zh/ZHForm';
import { ZHGridRow, ZHSection } from '../components/zh/ZHLayout';
import { SearchBar } from '../components/ui';
import {
  catalogCategoryFormSchema,
  catalogSubcategoryFormSchema,
  type CatalogCategoryFormValues,
  type CatalogSubcategoryFormValues,
} from '../schemas/catalog/catalogPagesFormsSchema';

function mapBasic(items: { id: string; code: string; name: string; isActive: boolean }[]): CatalogRow[] {
  return (items ?? []).map((x) => ({ id: x.id, code: x.code, name: x.name, isActive: x.isActive }));
}

export function BrandsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.brands.title"
      listTabLabelKey="catalog.brands.tabList"
      primaryCreateKey="catalog.brands.primaryCreate"
      viewPermissionKey="inventory.brands.view"
      createPermissionKey="inventory.brands.create"
      auditEntityType="Brand"
      load={async () => mapBasic(await catalogService.brands(false))}
      create={async (p) => catalogService.createBrand({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function ProductTypesCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.productTypes.title"
      listTabLabelKey="catalog.productTypes.tabList"
      primaryCreateKey="catalog.productTypes.primaryCreate"
      viewPermissionKey="inventory.product-types.view"
      createPermissionKey="inventory.product-types.create"
      load={async () => mapBasic(await catalogService.productTypes(false))}
      create={async (p) => catalogService.createProductType({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function UnitsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.units.title"
      listTabLabelKey="catalog.units.tabList"
      primaryCreateKey="catalog.units.primaryCreate"
      viewPermissionKey="inventory.units.view"
      createPermissionKey="inventory.units.create"
      load={async () => mapBasic(await catalogService.units(false))}
      create={async (p) => catalogService.createUnit({ code: String(p.code ?? ''), name: String(p.name ?? ''), symbol: undefined })}
    />
  );
}

export function TariffsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.tariffs.title"
      listTabLabelKey="catalog.tariffs.tabList"
      primaryCreateKey="catalog.tariffs.primaryCreate"
      viewPermissionKey="inventory.tariffs.view"
      createPermissionKey="inventory.tariffs.create"
      load={async () => mapBasic(await catalogService.tariffs(false))}
      create={async (p) => catalogService.createTariff({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function ProductLinesCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.productLines.title"
      listTabLabelKey="catalog.productLines.tabList"
      primaryCreateKey="catalog.productLines.primaryCreate"
      viewPermissionKey="inventory.product-lines.view"
      createPermissionKey="inventory.product-lines.create"
      load={async () => mapBasic(await catalogService.productLines({ activeStatus: 'all' }))}
      create={async (p) => catalogService.createProductLine({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

// TaxRatesCatalogPage eliminada: las tarifas SRI vienen de sri_vat_rate (datos oficiales pre-cargados,
// no editables por el subscriber). Se configuran en el producto al momento de crearlo/editarlo.

export function CategoriesCatalogPage() {
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
  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<CatalogCategoryFormValues>({
    resolver: zodResolver(catalogCategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '' },
  });
  const formWatch = watch();
  const [tab, setTab] = useState<'data' | 'list'>('data');

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
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
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
    return items.filter((x) =>
      `${x.code} ${x.name} ${x.lineCode} ${x.lineName}`.toLowerCase().includes(q)
    );
  }, [canView, items, listQuery]);

  if (!canView) {
    return <NoAccessPage title={t('catalog.categories.title')} />;
  }

  const onCreate = handleSubmit(async (form) => {
    setError('');
    setSaving(true);
    try {
      await catalogService.createCategory({
        code: form.code.trim(),
        name: form.name.trim(),
        lineId: form.lineId,
      });
      reset({ code: '', name: '', lineId: '' });
      await refresh();
      setTab('list');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setSaving(false);
    }
  });

  return (
    <PageShell
      kicker={t('app.nav.group.inventario')}
      title={t('catalog.categories.title')}
      action={
        canCreate && tab === 'data' ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => void onCreate()}
            disabled={saving || loading || !formWatch.code.trim() || !formWatch.name.trim() || !formWatch.lineId}
          >
            {saving ? t('common.saving') : t('catalog.categories.primaryCreate')}
          </ZHBtn>
        ) : undefined
      }
    >
      <Card>
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'list' ? 'is-active' : ''} onClick={() => setTab('list')}>
            {t('catalog.categories.tabList')}
          </button>
        </div>

        {tab === 'data' && (
          <>
            {canCreate ? (
              <ZHSection top={10}>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errors.code?.message}>
                    <input disabled={saving || loading} placeholder={t('common.codePlaceholder')} {...register('code')} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errors.name?.message}>
                    <input disabled={saving || loading} placeholder={t('common.namePlaceholder')} {...register('name')} />
                  </ZHField>
                  <ZHField label={t('catalog.categories.line')} fieldError={errors.lineId?.message}>
                    <select disabled={saving || loading} {...register('lineId')}>
                      <option value="">{t('common.select')}</option>
                      {lines.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.code} — {x.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                </ZHGrid>
              </ZHSection>
            ) : (
              <ZHSection top={10}>
                <div className="empty-state">{t('common.readOnly')}</div>
              </ZHSection>
            )}
          </>
        )}

        {tab === 'list' && (
          <>
            <ZHGridRow cols={1} className="zh-mb-12">
              <ZHField label={t('catalog.categories.line')}>
                <select value={filterLineId} onChange={(e) => setFilterLineId(e.target.value)} disabled={loading}>
                  <option value="">{t('common.select')}</option>
                  {lines.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </ZHField>
            </ZHGridRow>
            <div className="zh-mb-12">
              <SearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => setListQuery('')}
                filterValues={{}}
                placeholder={t('common.zhList.searchPlaceholder')}
                resultCount={listFiltered.length}
                entityLabel={t('common.zhList.entityLabel')}
                loading={loading}
                actionLabel={canCreate ? t('catalog.categories.listNewAction') : undefined}
                onAction={canCreate ? () => setTab('data') : undefined}
              />
            </div>
            {loading ? (
              <LoadingState />
            ) : items.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : listFiltered.length === 0 ? (
              <EmptyState message={t('common.listTab.noMatch')} />
            ) : (
              <table className="table zh-mt-12">
                <thead>
                  <tr>
                    <th>{t('common.code')}</th>
                    <th>{t('common.name')}</th>
                    <th>{t('catalog.categories.line')}</th>
                    <th>{t('common.status')}</th>
                  </tr>
                </thead>
                <tbody>
                  {listFiltered.map((x) => (
                    <tr key={x.id}>
                      <td>{x.code}</td>
                      <td>{x.name}</td>
                      <td>{lineLabel(x)}</td>
                      <td><Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </Card>
    </PageShell>
  );
}

export function SubcategoriesCatalogPage() {
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
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors },
  } = useForm<CatalogSubcategoryFormValues>({
    resolver: zodResolver(catalogSubcategoryFormSchema),
    defaultValues: { code: '', name: '', lineId: '', categoryId: '' },
  });
  const formLineId = watch('lineId');
  const formWatchSub = watch();
  const lineIdField = register('lineId');
  const [subTab, setSubTab] = useState<'data' | 'list'>('data');

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
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
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

  const listFilteredSub = useMemo(() => {
    if (!canView) return [];
    const q = listQuery.trim().toLowerCase();
    if (!q) return items;
    return items.filter((x) =>
      `${x.code} ${x.name} ${x.lineCode} ${x.lineName} ${x.categoryCode} ${x.categoryName}`.toLowerCase().includes(q)
    );
  }, [canView, items, listQuery]);

  if (!canView) {
    return <NoAccessPage title={t('catalog.subcategories.title')} />;
  }

  const onCreate = handleSubmit(async (form) => {
    setError('');
    setSaving(true);
    try {
      const selectedCat = formCategories.find((c) => c.id === form.categoryId);
      if (!selectedCat) {
        setError(t('catalog.subcategories.validation.categoryRequired'));
        setSaving(false);
        return;
      }

      await catalogService.createSubcategory({
        code: form.code.trim(),
        name: form.name.trim(),
        categoryId: form.categoryId,
      });
      reset({ code: '', name: '', lineId: '', categoryId: '' });
      await refresh();
      setSubTab('list');
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

  return (
    <PageShell
      kicker={t('app.nav.group.inventario')}
      title={t('catalog.subcategories.title')}
      action={
        canCreate && subTab === 'data' ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => void onCreate()}
            disabled={
              saving ||
              loading ||
              !formWatchSub.code.trim() ||
              !formWatchSub.name.trim() ||
              !formWatchSub.lineId ||
              !formWatchSub.categoryId
            }
          >
            {saving ? t('common.saving') : t('catalog.subcategories.primaryCreate')}
          </ZHBtn>
        ) : undefined
      }
    >
      <Card>
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={subTab === 'data' ? 'is-active' : ''} onClick={() => setSubTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={subTab === 'list' ? 'is-active' : ''} onClick={() => setSubTab('list')}>
            {t('catalog.subcategories.tabList')}
          </button>
        </div>

        {subTab === 'data' && (
          <>
            {canCreate ? (
              <ZHSection top={10}>
                <ZHGrid cols={3}>
                  <ZHField label={t('common.code')} fieldError={errors.code?.message}>
                    <input disabled={saving || loading} placeholder={t('common.codePlaceholder')} {...register('code')} />
                  </ZHField>
                  <ZHField label={t('common.name')} fieldError={errors.name?.message}>
                    <input disabled={saving || loading} placeholder={t('common.namePlaceholder')} {...register('name')} />
                  </ZHField>
                  <ZHField label={t('catalog.categories.line')} fieldError={errors.lineId?.message}>
                    <select
                      disabled={saving || loading}
                      name={lineIdField.name}
                      onBlur={lineIdField.onBlur}
                      ref={lineIdField.ref}
                      onChange={(e) => {
                        void lineIdField.onChange(e);
                        setValue('categoryId', '', { shouldValidate: true, shouldDirty: true });
                      }}
                    >
                      <option value="">{t('common.select')}</option>
                      {lines.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.code} — {x.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField label={t('catalog.subcategories.category')} fieldError={errors.categoryId?.message}>
                    <select disabled={saving || loading || !formWatchSub.lineId} {...register('categoryId')}>
                      <option value="">{t('common.select')}</option>
                      {formCategories.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.code} — {x.name}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                </ZHGrid>
              </ZHSection>
            ) : (
              <ZHSection top={10}>
                <div className="empty-state">{t('common.readOnly')}</div>
              </ZHSection>
            )}
          </>
        )}

        {subTab === 'list' && (
          <>
            <ZHGridRow cols={2} className="zh-mb-12">
              <ZHField label={t('catalog.categories.line')}>
                <select value={filterLineId} onChange={(e) => onFilterLineChange(e.target.value)} disabled={loading}>
                  <option value="">{t('common.select')}</option>
                  {lines.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </ZHField>
              <ZHField label={t('catalog.subcategories.category')}>
                <select value={filterCategoryId} onChange={(e) => setFilterCategoryId(e.target.value)} disabled={loading || !filterLineId}>
                  <option value="">{t('common.select')}</option>
                  {categories.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </ZHField>
            </ZHGridRow>
            <div className="zh-mb-12">
              <SearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => setListQuery('')}
                filterValues={{}}
                placeholder={t('common.zhList.searchPlaceholder')}
                resultCount={listFilteredSub.length}
                entityLabel={t('common.zhList.entityLabel')}
                loading={loading}
                actionLabel={canCreate ? t('catalog.subcategories.listNewAction') : undefined}
                onAction={canCreate ? () => setSubTab('data') : undefined}
              />
            </div>
            {loading ? (
              <LoadingState />
            ) : items.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : listFilteredSub.length === 0 ? (
              <EmptyState message={t('common.listTab.noMatch')} />
            ) : (
              <table className="table zh-mt-12">
                <thead>
                  <tr>
                    <th>{t('common.code')}</th>
                    <th>{t('common.name')}</th>
                    <th>{t('catalog.categories.line')}</th>
                    <th>{t('catalog.subcategories.category')}</th>
                    <th>{t('common.status')}</th>
                  </tr>
                </thead>
                <tbody>
                  {listFilteredSub.map((x) => (
                    <tr key={x.id}>
                      <td>{x.code}</td>
                      <td>{x.name}</td>
                      <td>{`${x.lineCode} — ${x.lineName}`}</td>
                      <td>{`${x.categoryCode} — ${x.categoryName}`}</td>
                      <td><Badge label={x.isActive ? t('common.active') : t('common.inactive')} variant={x.isActive ? 'green' : 'gray'} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </Card>
    </PageShell>
  );
}
