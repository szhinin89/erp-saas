import { CatalogSimplePage, type CatalogRow } from './CatalogSimplePage';
import { catalogService, type ProductCategoryListItem, type ProductSubcategoryListItem } from '../services/catalogService';
export { CatalogStructurePage } from './CatalogStructurePage';
import { useEffect, useMemo, useState } from 'react';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge, NoAccessPage } from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { useAuthStore } from '../store/authStore';
import { ZHBtn, ZHField, ZHGrid } from '../components/zh/ZHForm';
import { ZHGridRow, ZHSection } from '../components/zh/ZHLayout';

function mapBasic(items: { id: string; code: string; name: string; isActive: boolean }[]): CatalogRow[] {
  return (items ?? []).map((x) => ({ id: x.id, code: x.code, name: x.name, isActive: x.isActive }));
}

export function BrandsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.brands.title"
      viewPermissionKey="catalog.brands.view"
      createPermissionKey="catalog.brands.create"
      load={async () => mapBasic(await catalogService.brands(false))}
      create={async (p) => catalogService.createBrand({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function ProductTypesCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.productTypes.title"
      viewPermissionKey="catalog.productTypes.view"
      createPermissionKey="catalog.productTypes.create"
      load={async () => mapBasic(await catalogService.productTypes(false))}
      create={async (p) => catalogService.createProductType({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function UnitsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.units.title"
      viewPermissionKey="catalog.units.view"
      createPermissionKey="catalog.units.create"
      load={async () => mapBasic(await catalogService.units(false))}
      create={async (p) => catalogService.createUnit({ code: String(p.code ?? ''), name: String(p.name ?? ''), symbol: undefined })}
    />
  );
}

export function TariffsCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.tariffs.title"
      viewPermissionKey="catalog.tariffs.view"
      createPermissionKey="catalog.tariffs.create"
      load={async () => mapBasic(await catalogService.tariffs(false))}
      create={async (p) => catalogService.createTariff({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function ProductLinesCatalogPage() {
  return (
    <CatalogSimplePage
      titleKey="catalog.productLines.title"
      viewPermissionKey="catalog.productLines.view"
      createPermissionKey="catalog.productLines.create"
      load={async () => mapBasic(await catalogService.productLines({ activeStatus: 'all' }))}
      create={async (p) => catalogService.createProductLine({ code: String(p.code ?? ''), name: String(p.name ?? '') })}
    />
  );
}

export function TaxRatesCatalogPage() {
  const fields = useMemo(
    () => [
      { key: 'code', labelKey: 'common.code', placeholderKey: 'common.codePlaceholder', type: 'text' as const },
      { key: 'name', labelKey: 'common.name', placeholderKey: 'common.namePlaceholder', type: 'text' as const },
      {
        key: 'type',
        labelKey: 'catalog.taxRates.type',
        placeholderKey: 'catalog.taxRates.type',
        type: 'select' as const,
        options: [
          { value: 'VAT', label: 'IVA' },
          { value: 'Excise', label: 'ICE' },
          { value: 'Other', label: 'OTRO' },
        ],
      },
      { key: 'percentage', labelKey: 'catalog.taxRates.percentage', placeholderKey: 'catalog.taxRates.percentagePlaceholder', type: 'number' as const },
    ],
    []
  );

  return (
    <CatalogSimplePage
      titleKey="catalog.taxRates.title"
      viewPermissionKey="catalog.taxRates.view"
      createPermissionKey="catalog.taxRates.create"
      fields={fields}
      load={async () => mapBasic(await catalogService.taxRates(false))}
      create={async (p) =>
        catalogService.createTaxRate({
          code: String(p.code ?? ''),
          name: String(p.name ?? ''),
          type: (String(p.type ?? 'VAT') as 'VAT' | 'Excise' | 'Other'),
          percentage: Number(p.percentage ?? 0),
        })
      }
    />
  );
}

export function CategoriesCatalogPage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = isAdmin || hasPerm('catalog.categories.view');
  const canCreate = isAdmin || hasPerm('catalog.categories.create');

  const [lines, setLines] = useState<{ id: string; code: string; name: string }[]>([]);
  const [items, setItems] = useState<ProductCategoryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const [filterLineId, setFilterLineId] = useState('');
  const [form, setForm] = useState({ code: '', name: '', lineId: '' });

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

  if (!canView) {
    return <NoAccessPage title={t('catalog.categories.title')} />;
  }

  const lineLabel = (row: ProductCategoryListItem) => `${row.lineCode} — ${row.lineName}`;

  const onCreate = async () => {
    setError('');
    setSaving(true);
    try {
      await catalogService.createCategory({
        code: form.code.trim(),
        name: form.name.trim(),
        lineId: form.lineId,
      });
      setForm({ code: '', name: '', lineId: '' });
      await refresh();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageShell
      kicker={t('app.nav.group.catalog')}
      title={t('catalog.categories.title')}
      action={
        canCreate ? (
          <ZHBtn variant="primary" type="button" onClick={() => void onCreate()} disabled={saving || loading || !form.code.trim() || !form.name.trim() || !form.lineId}>
            {saving ? t('common.saving') : t('common.create')}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        <ZHGridRow cols={2}>
            <ZHField label={t('catalog.categories.line')}>
              <select value={filterLineId} onChange={(e) => setFilterLineId(e.target.value)} disabled={loading}>
                <option value="">{t('common.select')}</option>
                {lines.map((x) => (
                  <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                ))}
              </select>
            </ZHField>
        </ZHGridRow>

        {canCreate ? (
          <ZHSection top={10}>
            <ZHGrid cols={3}>
              <ZHField label={t('common.code')}>
                <input value={form.code} onChange={(e) => setForm((s) => ({ ...s, code: e.target.value }))} disabled={saving || loading} placeholder={t('common.codePlaceholder')} />
              </ZHField>
              <ZHField label={t('common.name')}>
                <input value={form.name} onChange={(e) => setForm((s) => ({ ...s, name: e.target.value }))} disabled={saving || loading} placeholder={t('common.namePlaceholder')} />
              </ZHField>
              <ZHField label={t('catalog.categories.line')}>
                <select value={form.lineId} onChange={(e) => setForm((s) => ({ ...s, lineId: e.target.value }))} disabled={saving || loading}>
                  <option value="">{t('common.select')}</option>
                  {lines.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
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

        {error && <ErrorState message={error} />}
        {loading ? (
          <LoadingState />
        ) : items.length === 0 ? (
          <EmptyState message={t('common.noData')} />
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
              {items.map((x) => (
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
      </TableCard>
    </PageShell>
  );
}

export function SubcategoriesCatalogPage() {
  const { t } = useI18n();
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = isAdmin || hasPerm('catalog.subcategories.view');
  const canCreate = isAdmin || hasPerm('catalog.subcategories.create');

  const [lines, setLines] = useState<{ id: string; code: string; name: string }[]>([]);
  const [categories, setCategories] = useState<ProductCategoryListItem[]>([]);
  const [items, setItems] = useState<ProductSubcategoryListItem[]>([]);
  const [formCategories, setFormCategories] = useState<ProductCategoryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const [filterLineId, setFilterLineId] = useState('');
  const [filterCategoryId, setFilterCategoryId] = useState('');
  const [form, setForm] = useState({ code: '', name: '', lineId: '', categoryId: '' });

  const onFilterLineChange = (lineId: string) => {
    setFilterLineId(lineId);
    setFilterCategoryId('');
  };

  const onFormLineChange = (lineId: string) => {
    setForm((s) => ({ ...s, lineId, categoryId: '' }));
  };

  useEffect(() => {
    let cancelled = false;
    if (!form.lineId) {
      const id = window.setTimeout(() => {
        if (!cancelled) setFormCategories([]);
      }, 0);
      return () => {
        cancelled = true;
        window.clearTimeout(id);
      };
    }
    void catalogService
      .categories({ activeStatus: 'all', lineId: form.lineId })
      .then((c) => {
        if (!cancelled) setFormCategories(c ?? []);
      })
      .catch(() => {
        if (!cancelled) setFormCategories([]);
      });
    return () => {
      cancelled = true;
    };
  }, [form.lineId]);

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

  if (!canView) {
    return <NoAccessPage title={t('catalog.subcategories.title')} />;
  }

  const onCreate = async () => {
    setError('');
    setSaving(true);
    try {
      const selectedCat = formCategories.find((c) => c.id === form.categoryId);
      if (!selectedCat) throw new Error('Categoría requerida');

      await catalogService.createSubcategory({
        code: form.code.trim(),
        name: form.name.trim(),
        categoryId: form.categoryId,
      });
      setForm({ code: '', name: '', lineId: '', categoryId: '' });
      await refresh();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? (err as Error)?.message ?? t('common.errorGeneric');
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageShell
      kicker={t('app.nav.group.catalog')}
      title={t('catalog.subcategories.title')}
      action={
        canCreate ? (
          <ZHBtn
            variant="primary"
            type="button"
            onClick={() => void onCreate()}
            disabled={saving || loading || !form.code.trim() || !form.name.trim() || !form.lineId || !form.categoryId}
          >
            {saving ? t('common.saving') : t('common.create')}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        <ZHGridRow cols={2}>
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

        {canCreate ? (
          <ZHSection top={10}>
            <ZHGrid cols={3}>
              <ZHField label={t('common.code')}>
                <input value={form.code} onChange={(e) => setForm((s) => ({ ...s, code: e.target.value }))} disabled={saving || loading} placeholder={t('common.codePlaceholder')} />
              </ZHField>
              <ZHField label={t('common.name')}>
                <input value={form.name} onChange={(e) => setForm((s) => ({ ...s, name: e.target.value }))} disabled={saving || loading} placeholder={t('common.namePlaceholder')} />
              </ZHField>
              <ZHField label={t('catalog.categories.line')}>
                <select value={form.lineId} onChange={(e) => onFormLineChange(e.target.value)} disabled={saving || loading}>
                  <option value="">{t('common.select')}</option>
                  {lines.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </ZHField>
              <ZHField label={t('catalog.subcategories.category')}>
                <select value={form.categoryId} onChange={(e) => setForm((s) => ({ ...s, categoryId: e.target.value }))} disabled={saving || loading || !form.lineId}>
                  <option value="">{t('common.select')}</option>
                  {formCategories.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
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

        {error && <ErrorState message={error} />}
        {loading ? (
          <LoadingState />
        ) : items.length === 0 ? (
          <EmptyState message={t('common.noData')} />
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
              {items.map((x) => (
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
      </TableCard>
    </PageShell>
  );
}
