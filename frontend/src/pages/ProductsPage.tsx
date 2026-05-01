import { useMemo, useState, type FormEvent } from 'react';
import { productService } from '../services/productService';
import { useAsync } from '../hooks/useAsync';
import {
  PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge, NoAccessPage,
} from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { catalogService } from '../services/catalogService';
import { useAuthStore } from '../store/authStore';
import { ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHFormActions, ZHToggle } from '../components/zh/ZHForm';
import { ZHColSpan } from '../components/zh/ZHLayout';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import { ZHDirtyBar } from '../components/zh/ZHDirtyBar';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

const emptyProductForm = () => ({
  saleCode: '',
  purchaseCode: '',
  shortName: '',
  description: '',
  lineId: EMPTY_GUID,
  categoryId: EMPTY_GUID,
  subcategoryId: EMPTY_GUID,
  unitOfMeasureId: EMPTY_GUID,
  brandId: EMPTY_GUID,
  productTypeId: EMPTY_GUID,
  tariffId: EMPTY_GUID,
  saleTaxId: EMPTY_GUID,
  purchaseTaxId: EMPTY_GUID,
  isService: false,
  isForSale: true,
  availableOnWeb: false,
  availableOnMobile: false,
});

export function ProductsPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const canView = isAdmin || hasPerm('catalog.products.view');
  const canCreate = isAdmin || hasPerm('catalog.products.create');
  const { data: products, loading, error, refetch } = useAsync(productService.getAll);
  const { data: catalogs } = useAsync(async () => {
    const [lines, categories, subcategories, brands, productTypes, units, taxRates, tariffs] = await Promise.all([
      catalogService.productLines({ activeStatus: 'all' }),
      catalogService.categories({ activeStatus: 'all' }),
      catalogService.subcategories({ activeStatus: 'all' }),
      catalogService.brands(false),
      catalogService.productTypes(false),
      catalogService.units(false),
      catalogService.taxRates(false),
      catalogService.tariffs(false),
    ]);
    return { lines, categories, subcategories, brands, productTypes, units, taxRates, tariffs };
  });

  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');
  const [form, setForm] = useState(() => emptyProductForm());
  const [savedForm, setSavedForm] = useState(() => emptyProductForm());
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');

  const maps = useMemo(() => {
    const toMap = (arr: { id: string; code: string; name: string }[] | undefined) =>
      new Map((arr ?? []).map((x) => [x.id, `${x.code} — ${x.name}`] as const));
    return {
      lines: toMap(catalogs?.lines),
      categories: toMap(catalogs?.categories),
      subcategories: toMap(catalogs?.subcategories),
      brands: toMap(catalogs?.brands),
      productTypes: toMap(catalogs?.productTypes),
      units: toMap(catalogs?.units),
      taxRates: toMap(catalogs?.taxRates),
      tariffs: toMap(catalogs?.tariffs),
    };
  }, [catalogs]);

  const show = (m: Map<string, string>, id: string) => {
    if (!id || id === '00000000-0000-0000-0000-000000000000') return '—';
    return m.get(id) ?? id;
  };

  const filteredCategories = useMemo(
    () => (catalogs?.categories ?? []).filter((c) => form.lineId !== EMPTY_GUID && c.lineId === form.lineId),
    [catalogs?.categories, form.lineId]
  );
  const filteredSubcategories = useMemo(
    () => (catalogs?.subcategories ?? []).filter((s) => form.categoryId !== EMPTY_GUID && s.categoryId === form.categoryId),
    [catalogs?.subcategories, form.categoryId]
  );

  const set = (field: keyof typeof form, value: unknown) => setForm((f) => ({ ...f, [field]: value }));

  const isDirty = useMemo(
    () => JSON.stringify(form) !== JSON.stringify(savedForm),
    [form, savedForm]
  );

  const discardChanges = () => {
    setForm(savedForm);
    setFormError('');
  };

  const onLineChange = (lineId: string) => {
    setForm((f) => ({
      ...f,
      lineId,
      categoryId: EMPTY_GUID,
      subcategoryId: EMPTY_GUID,
    }));
  };

  const onCategoryChange = (categoryId: string) => {
    setForm((f) => ({
      ...f,
      categoryId,
      subcategoryId: EMPTY_GUID,
    }));
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!canCreate) return;
    setFormError('');
    setSaving(true);
    try {
      await productService.create(form);
      await refetch();
      const cleared = emptyProductForm();
      setForm(cleared);
      setSavedForm(cleared);
    } catch (err: unknown) {
      setFormError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('common.errorGeneric')
      );
    } finally {
      setSaving(false);
    }
  };

  if (!canView) {
    return <NoAccessPage title={t('products.title')} />;
  }

  return (
    <PageShell
      kicker={`${t('app.nav.group.catalog')} · ${t('products.title')}`}
      title={canCreate ? t('products.modal.create.title') : t('products.title')}
      subtitle={canCreate ? undefined : t('common.readOnly')}
    >
      {canCreate && (
        <ZHFormCard
          hideHeader
          title={t('products.modal.create.title')}
          subtitle={t('products.form.saleCode')}
          onSubmit={handleSubmit}
        >
          <input type="hidden" name="tenantId" value={tenantId} />
          {formError ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={formError} /> : null}

          <ZHFormSection title={t('products.section.general')}>
            <ZHGrid cols={2}>
              <ZHField label={t('products.form.saleCode')} required>
                <input
                  id="saleCode"
                  value={form.saleCode}
                  onChange={(e) => set('saleCode', e.target.value)}
                  placeholder={t('products.form.saleCode.placeholder')}
                  required
                  disabled={saving}
                />
              </ZHField>
              <ZHField label={t('products.form.purchaseCode')}>
                <input
                  id="purchaseCode"
                  value={form.purchaseCode ?? ''}
                  onChange={(e) => set('purchaseCode', e.target.value)}
                  placeholder={t('products.form.purchaseCode.placeholder')}
                  disabled={saving}
                />
              </ZHField>

              <ZHColSpan span={2}>
                <ZHField label={t('products.form.shortName')} required>
                  <input
                    id="shortName"
                    value={form.shortName}
                    onChange={(e) => set('shortName', e.target.value)}
                    placeholder={t('products.form.shortName.placeholder')}
                    required
                    disabled={saving}
                  />
                </ZHField>
              </ZHColSpan>

              <ZHColSpan span={2}>
                <ZHField label={t('products.form.description')} required>
                  <input
                    id="description"
                    value={form.description}
                    onChange={(e) => set('description', e.target.value)}
                    placeholder={t('products.form.description.placeholder')}
                    required
                    disabled={saving}
                  />
                </ZHField>
              </ZHColSpan>
            </ZHGrid>
          </ZHFormSection>

                <ZHFormSection title={t('products.form.classification')}>
                  <ZHGrid cols={2}>
                    <ZHField label={t('products.form.line')}>
                      <select id="lineId" value={form.lineId} onChange={(e) => onLineChange(e.target.value)} disabled={saving}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.lines ?? []).map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.brand')}>
                      <select id="brandId" value={form.brandId} onChange={(e) => set('brandId', e.target.value)} disabled={saving}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.brands ?? []).map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.category')}>
                      <select
                        id="categoryId"
                        value={form.categoryId}
                        onChange={(e) => onCategoryChange(e.target.value)}
                        disabled={saving || form.lineId === EMPTY_GUID}
                      >
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {filteredCategories.map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.subcategory')}>
                      <select
                        id="subcategoryId"
                        value={form.subcategoryId}
                        onChange={(e) => set('subcategoryId', e.target.value)}
                        disabled={saving || form.categoryId === EMPTY_GUID}
                      >
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {filteredSubcategories.map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.productType')}>
                      <select id="productTypeId" value={form.productTypeId} onChange={(e) => set('productTypeId', e.target.value)} disabled={saving}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.productTypes ?? []).map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.unit')}>
                      <select id="unitOfMeasureId" value={form.unitOfMeasureId} onChange={(e) => set('unitOfMeasureId', e.target.value)} disabled={saving}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.units ?? []).map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                  </ZHGrid>
                </ZHFormSection>

                <ZHFormSection title={t('products.form.taxes')}>
                  <ZHGrid cols={2}>
                    <ZHField label={t('products.form.saleTax')}>
                      <select id="saleTaxId" value={form.saleTaxId} onChange={(e) => set('saleTaxId', e.target.value)} disabled={saving}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.taxRates ?? []).map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.purchaseTax')}>
                      <select id="purchaseTaxId" value={form.purchaseTaxId} onChange={(e) => set('purchaseTaxId', e.target.value)} disabled={saving}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.taxRates ?? []).map((x) => (
                          <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHColSpan span={2}>
                      <ZHField label={t('products.form.tariff')}>
                        <select id="tariffId" value={form.tariffId} onChange={(e) => set('tariffId', e.target.value)} disabled={saving}>
                          <option value={EMPTY_GUID}>{t('common.select')}</option>
                          {(catalogs?.tariffs ?? []).map((x) => (
                            <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                          ))}
                        </select>
                      </ZHField>
                    </ZHColSpan>
                  </ZHGrid>
                </ZHFormSection>

                <ZHFormSection title={t('products.form.behavior')}>
                  <ZHGrid cols={1}>
                    <ZHToggle
                      label={t('products.form.isService')}
                      description={t('products.form.isService')}
                      value={form.isService}
                      onChange={(v) => set('isService', v)}
                      disabled={saving}
                    />
                    <ZHToggle
                      label={t('products.form.isForSale')}
                      description={t('products.form.isForSale')}
                      value={form.isForSale}
                      onChange={(v) => set('isForSale', v)}
                      disabled={saving}
                    />
                    <ZHToggle
                      label={t('products.form.availableOnWeb')}
                      description={t('products.form.availableOnWeb')}
                      value={form.availableOnWeb}
                      onChange={(v) => set('availableOnWeb', v)}
                      disabled={saving}
                    />
                    <ZHToggle
                      label={t('products.form.availableOnMobile')}
                      description={t('products.form.availableOnMobile')}
                      value={form.availableOnMobile}
                      onChange={(v) => set('availableOnMobile', v)}
                      disabled={saving}
                    />
                  </ZHGrid>
                </ZHFormSection>

          <ZHFormActions
            onCancel={undefined}
            onDraft={undefined}
            onSave={undefined}
            hideCancel
            hideDraft
            disableDraft
            disableSave={saving || !form.saleCode.trim() || !form.shortName.trim() || !form.description.trim()}
            saveButtonType="submit"
            labels={{
              cancel: t('common.cancel'),
              draft: t('common.saveDraft') ?? 'Guardar borrador',
              save: saving ? t('common.saving') : t('products.modal.create.submit'),
            }}
          />

          <ZHDirtyBar
            visible={isDirty}
            loading={saving}
            onDiscard={discardChanges}
            onSave={() => {
              const formEl = document.querySelector('form');
              (formEl as HTMLFormElement | null)?.requestSubmit?.();
            }}
            saveLabel={saving ? t('common.saving') : t('common.save')}
          />
        </ZHFormCard>
      )}

      {loading && <LoadingState />}
      {error   && <ErrorState message={error} />}
      {!loading && !error && products?.length === 0 && (
        <EmptyState message={t('products.empty')} />
      )}
      {!loading && !error && products && products.length > 0 && (
        <TableCard>
          <table>
            <thead>
              <tr>
                <th>{t('products.table.code')}</th>
                <th>{t('products.table.name')}</th>
                <th>{t('products.table.description')}</th>
                <th>{t('products.table.line')}</th>
                <th>{t('products.table.brand')}</th>
                <th>{t('products.table.productType')}</th>
                <th>{t('products.table.unit')}</th>
                <th>{t('products.table.category')}</th>
                <th>{t('products.table.subcategory')}</th>
                <th>{t('products.table.saleTax')}</th>
                <th>{t('products.table.purchaseTax')}</th>
                <th>{t('products.table.tariff')}</th>
                <th>{t('products.table.type')}</th>
                <th>{t('products.table.sale')}</th>
                <th>{t('products.table.status')}</th>
              </tr>
            </thead>
            <tbody>
              {products.map((p) => (
                <tr key={p.id}>
                  <td><span className="mono">{p.saleCode}</span></td>
                  <td>{p.shortName}</td>
                  <td className="subtle">{p.description}</td>
                  <td>{show(maps.lines, p.lineId)}</td>
                  <td>{show(maps.brands, p.brandId)}</td>
                  <td>{show(maps.productTypes, p.productTypeId)}</td>
                  <td>{show(maps.units, p.unitOfMeasureId)}</td>
                  <td>{show(maps.categories, p.categoryId)}</td>
                  <td>{show(maps.subcategories, p.subcategoryId)}</td>
                  <td>{show(maps.taxRates, p.saleTaxId)}</td>
                  <td>{show(maps.taxRates, p.purchaseTaxId)}</td>
                  <td>{show(maps.tariffs, p.tariffId)}</td>
                  <td>
                    <Badge
                      label={p.isService ? t('products.badge.service') : t('products.badge.product')}
                      variant={p.isService ? 'blue' : 'gray'}
                    />
                  </td>
                  <td>
                    <Badge
                      label={p.isForSale ? t('common.yes') : t('common.no')}
                      variant={p.isForSale ? 'green' : 'gray'}
                    />
                  </td>
                  <td>
                    <Badge
                      label={p.isActive ? t('common.active') : t('common.inactive')}
                      variant={p.isActive ? 'green' : 'red'}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableCard>
      )}
    </PageShell>
  );
}
