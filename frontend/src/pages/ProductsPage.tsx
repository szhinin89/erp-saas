import { useMemo, useRef, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { productService, type ProductImageInput } from '../services/productService';
import { formatApiError } from '../modules/lib/formatApiError';
import type { Product } from '../types/product';
import { useAsync } from '../hooks/useAsync';
import {
  PageShell, TableCard, EmptyState, LoadingState, Badge, NoAccessPage,
} from '../components/PageShell';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { catalogService } from '../services/catalogService';
import { useAuthStore } from '../store/authStore';
import { ZHBtn, ZHFormSection, ZHGrid, ZHField, ZHToggle } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHColSpan } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import { ZHDirtyBar } from '../components/zh/ZHDirtyBar';
import { EntityAuditPanel } from '../components/EntityAuditPanel';
import { productCreateFormSchema, type ProductCreateFormValues } from '../schemas/catalog/productSchema';
import './ProductsPage.css';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const MAX_IMAGE_BYTES = 600 * 1024;
const LIST_LIMIT = 20;

type ProductFormTab = 'general' | 'images' | 'list' | 'audit';

type ImageRow = {
  id: string;
  url: string;
  altText: string;
  isMain: boolean;
  isEcommerce: boolean;
};

function newImageRow(): ImageRow {
  const id =
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `img-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
  return { id, url: '', altText: '', isMain: false, isEcommerce: false };
}

function readFileAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve(String(r.result ?? ''));
    r.onerror = () => reject(new Error('read'));
    r.readAsDataURL(file);
  });
}

function ProductsRecentListBlock(props: {
  t: (key: string, fallback?: string) => string;
  loading: boolean;
  error: string | null;
  products: Product[] | null | undefined;
  recentRows: Product[];
  listQuery: string;
  setListQuery: (v: string) => void;
  lineLabel: (lineId: string) => string;
  canCreate: boolean;
  onGoToCreate?: () => void;
  onViewAudit?: (productId: string) => void;
}) {
  const {
    t,
    loading,
    error,
    products,
    recentRows,
    listQuery,
    setListQuery,
    lineLabel,
    canCreate,
    onGoToCreate,
    onViewAudit,
  } = props;

  if (error) return <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} />;

  return (
    <>
      <div className="zh-mb-12">
        <ZHSearchBar
          searchQuery={listQuery}
          onSearch={setListQuery}
          onClearAll={() => setListQuery('')}
          filterValues={{}}
          placeholder={t('products.list.searchPlaceholder')}
          resultCount={recentRows.length}
          entityLabel={t('products.list.entityLabel')}
          loading={loading}
          actionLabel={canCreate && onGoToCreate ? t('products.list.newAction') : undefined}
          onAction={canCreate ? onGoToCreate : undefined}
        />
      </div>
      {loading ? (
        <LoadingState />
      ) : !products || products.length === 0 ? (
        <EmptyState message={t('products.empty')} />
      ) : recentRows.length === 0 ? (
        <EmptyState message={t('common.listTab.noMatch')} />
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>{t('products.table.code')}</th>
              <th>{t('products.table.name')}</th>
              <th>{t('products.table.description')}</th>
              <th>{t('products.table.line')}</th>
              <th>{t('products.table.createdAt')}</th>
              <th>{t('products.table.type')}</th>
              <th>{t('products.table.sale')}</th>
              <th>{t('products.table.status')}</th>
              {onViewAudit ? <th>{t('common.formTab.audit')}</th> : null}
            </tr>
          </thead>
          <tbody>
            {recentRows.map((p) => (
              <tr key={p.id}>
                <td><span className="mono">{p.saleCode}</span></td>
                <td>{p.shortName}</td>
                <td className="subtle">{p.description}</td>
                <td>{lineLabel(p.lineId)}</td>
                <td>{new Date(p.createdAt).toLocaleString('es-EC', { dateStyle: 'short', timeStyle: 'short' })}</td>
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
                {onViewAudit ? (
                  <td>
                    <ZHBtn variant="ghost" size="xs" type="button" onClick={() => onViewAudit(p.id)}>
                      {t('common.formTab.audit')}
                    </ZHBtn>
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

const emptyProductForm = (): ProductCreateFormValues => ({
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
  const canView = isAdmin || hasPerm('inventario.products.view');
  const canCreate = isAdmin || hasPerm('inventario.products.create');
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

  const savedProductSnapshot = useRef<ProductCreateFormValues>(emptyProductForm());
  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isDirty: productFormDirty },
  } = useForm<ProductCreateFormValues>({
    resolver: zodResolver(productCreateFormSchema),
    defaultValues: emptyProductForm(),
  });
  const lineIdWatch = watch('lineId');
  const categoryIdWatch = watch('categoryId');
  const productWatch = watch();
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');
  const formRef = useRef<HTMLFormElement>(null);
  const [formTab, setFormTab] = useState<ProductFormTab>('general');
  const [imageRows, setImageRows] = useState<ImageRow[]>([]);
  const [savedImageRows, setSavedImageRows] = useState<ImageRow[]>([]);
  const [listQuery, setListQuery] = useState('');
  const [auditProductId, setAuditProductId] = useState('');
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);

  const openProductAudit = (productId: string) => {
    setAuditProductId(productId);
    setAuditRefreshKey((k) => k + 1);
    if (canCreate) setFormTab('audit');
  };

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
    () => (catalogs?.categories ?? []).filter((c) => lineIdWatch !== EMPTY_GUID && c.lineId === lineIdWatch),
    [catalogs?.categories, lineIdWatch]
  );
  const filteredSubcategories = useMemo(
    () =>
      (catalogs?.subcategories ?? []).filter(
        (s) => categoryIdWatch !== EMPTY_GUID && s.categoryId === categoryIdWatch
      ),
    [catalogs?.subcategories, categoryIdWatch]
  );

  const recentProductsList = useMemo(() => {
    const arr = products ?? [];
    const q = listQuery.trim().toLowerCase();
    const filtered = q
      ? arr.filter((p) => {
          const hay = `${p.saleCode} ${p.shortName} ${p.description} ${p.id} ${p.purchaseCode ?? ''}`.toLowerCase();
          return hay.includes(q);
        })
      : arr;
    return [...filtered]
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .slice(0, LIST_LIMIT);
  }, [products, listQuery]);

  const imagesDirty = useMemo(
    () => JSON.stringify(imageRows) !== JSON.stringify(savedImageRows),
    [imageRows, savedImageRows]
  );

  const isDirty = productFormDirty || imagesDirty;

  const discardChanges = () => {
    reset({ ...savedProductSnapshot.current });
    setImageRows(savedImageRows.map((r) => ({ ...r })));
    setFormError('');
  };

  const patchImageRow = (rowId: string, patch: Partial<ImageRow>) => {
    setImageRows((rows) => rows.map((r) => (r.id === rowId ? { ...r, ...patch } : r)));
  };

  const setImageMain = (rowId: string, checked: boolean) => {
    setImageRows((rows) =>
      rows.map((r) => {
        if (!checked) return r.id === rowId ? { ...r, isMain: false } : r;
        return { ...r, isMain: r.id === rowId };
      })
    );
  };

  const setImageEcommerce = (rowId: string, checked: boolean) => {
    setImageRows((rows) =>
      rows.map((r) => {
        if (!checked) return r.id === rowId ? { ...r, isEcommerce: false } : r;
        return { ...r, isEcommerce: r.id === rowId };
      })
    );
  };

  const removeImageRow = (rowId: string) => {
    setImageRows((rows) => rows.filter((r) => r.id !== rowId));
  };

  const assignImageFile = async (rowId: string, file: File | undefined) => {
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      setFormError(t('products.images.notImageFile'));
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      setFormError(t('products.images.fileTooLarge'));
      return;
    }
    setFormError('');
    try {
      const url = await readFileAsDataUrl(file);
      patchImageRow(rowId, { url });
    } catch {
      setFormError(t('common.errorGeneric'));
    }
  };

  const submitProduct = handleSubmit(async (form) => {
    if (!canCreate) return;
    setFormError('');

    const filled = imageRows.filter((r) => r.url.trim());
    if (filled.filter((r) => r.isMain).length > 1) {
      setFormError(t('products.images.oneMain'));
      return;
    }
    if (filled.filter((r) => r.isEcommerce).length > 1) {
      setFormError(t('products.images.oneEcommerce'));
      return;
    }

    const images: ProductImageInput[] = filled.map((r, i) => ({
      url: r.url.trim(),
      altText: r.altText.trim() || null,
      isMain: r.isMain,
      isEcommerce: r.isEcommerce,
      sortOrder: i,
    }));

    setSaving(true);
    try {
      const created = await productService.create({
        ...form,
        images: images.length > 0 ? images : undefined,
      });
      await refetch();
      const cleared = emptyProductForm();
      reset(cleared);
      savedProductSnapshot.current = cleared;
      setImageRows([]);
      setSavedImageRows([]);
      setAuditProductId(created.id);
      setAuditRefreshKey((k) => k + 1);
      setFormTab('audit');
    } catch (err: unknown) {
      setFormError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  });

  if (!canView) {
    return <NoAccessPage title={t('products.title')} />;
  }

  return (
    <PageShell
      kicker={`${t('app.nav.group.inventario')} · ${t('products.title')}`}
      title={canCreate ? t('products.modal.create.title') : t('products.title')}
      subtitle={canCreate ? undefined : t('common.readOnly')}
      action={
        canCreate && formTab === 'general' ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            disabled={
              saving ||
              !productWatch.saleCode.trim() ||
              !productWatch.shortName.trim() ||
              !productWatch.description.trim()
            }
            onClick={() => formRef.current?.requestSubmit()}
          >
            {saving ? t('common.saving') : t('products.modal.create.submit')}
          </ZHBtn>
        ) : undefined
      }
    >
      {canCreate && (
        <ZHFormCard
          ref={formRef}
          hideHeader
          title={t('products.modal.create.title')}
          subtitle={t('products.form.saleCode')}
          onSubmit={submitProduct}
        >
          {formError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={formError} /> : null}

          <div className="zh-form-tabs" role="tablist">
            <button
              type="button"
              className={formTab === 'general' ? 'is-active' : ''}
              onClick={() => setFormTab('general')}
            >
              {t('common.formTab.data')}
            </button>
            <button
              type="button"
              className={formTab === 'images' ? 'is-active' : ''}
              onClick={() => setFormTab('images')}
            >
              {t('products.tab.images')}
            </button>
            <button
              type="button"
              className={formTab === 'list' ? 'is-active' : ''}
              onClick={() => setFormTab('list')}
            >
              {t('products.tabList')}
            </button>
            <button
              type="button"
              className={formTab === 'audit' ? 'is-active' : ''}
              onClick={() => setFormTab('audit')}
            >
              {t('common.formTab.audit')}
            </button>
          </div>

          {formTab === 'general' && (
            <>
          <ZHFormSection title={t('products.section.general')}>
            <ZHGrid cols={2}>
              <ZHField label={t('products.form.saleCode')} required fieldError={errors.saleCode?.message}>
                <input id="saleCode" placeholder={t('products.form.saleCode.placeholder')} disabled={saving} {...register('saleCode')} />
              </ZHField>
              <ZHField label={t('products.form.purchaseCode')} fieldError={errors.purchaseCode?.message}>
                <input
                  id="purchaseCode"
                  placeholder={t('products.form.purchaseCode.placeholder')}
                  disabled={saving}
                  {...register('purchaseCode')}
                />
              </ZHField>

              <ZHColSpan span={2}>
                <ZHField label={t('products.form.shortName')} required fieldError={errors.shortName?.message}>
                  <input id="shortName" placeholder={t('products.form.shortName.placeholder')} disabled={saving} {...register('shortName')} />
                </ZHField>
              </ZHColSpan>

              <ZHColSpan span={2}>
                <ZHField label={t('products.form.description')} required fieldError={errors.description?.message}>
                  <input id="description" placeholder={t('products.form.description.placeholder')} disabled={saving} {...register('description')} />
                </ZHField>
              </ZHColSpan>
            </ZHGrid>
          </ZHFormSection>

                <ZHFormSection title={t('products.form.classification')}>
                  <ZHGrid cols={2}>
                    <ZHField label={t('products.form.line')} fieldError={errors.lineId?.message}>
                      <select
                        id="lineId"
                        disabled={saving}
                        {...register('lineId', {
                          onChange: () => {
                            setValue('categoryId', EMPTY_GUID, { shouldDirty: true, shouldValidate: true });
                            setValue('subcategoryId', EMPTY_GUID, { shouldDirty: true, shouldValidate: true });
                          },
                        })}
                      >
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.lines ?? []).map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.brand')} fieldError={errors.brandId?.message}>
                      <select id="brandId" disabled={saving} {...register('brandId')}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.brands ?? []).map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.category')} fieldError={errors.categoryId?.message}>
                      <select
                        id="categoryId"
                        disabled={saving || lineIdWatch === EMPTY_GUID}
                        {...register('categoryId', {
                          onChange: () => {
                            setValue('subcategoryId', EMPTY_GUID, { shouldDirty: true, shouldValidate: true });
                          },
                        })}
                      >
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {filteredCategories.map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.subcategory')} fieldError={errors.subcategoryId?.message}>
                      <select id="subcategoryId" disabled={saving || categoryIdWatch === EMPTY_GUID} {...register('subcategoryId')}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {filteredSubcategories.map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.productType')} fieldError={errors.productTypeId?.message}>
                      <select id="productTypeId" disabled={saving} {...register('productTypeId')}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.productTypes ?? []).map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.unit')} fieldError={errors.unitOfMeasureId?.message}>
                      <select id="unitOfMeasureId" disabled={saving} {...register('unitOfMeasureId')}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.units ?? []).map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                  </ZHGrid>
                </ZHFormSection>

                <ZHFormSection title={t('products.form.taxes')}>
                  <ZHGrid cols={2}>
                    <ZHField label={t('products.form.saleTax')} fieldError={errors.saleTaxId?.message}>
                      <select id="saleTaxId" disabled={saving} {...register('saleTaxId')}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.taxRates ?? []).map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHField label={t('products.form.purchaseTax')} fieldError={errors.purchaseTaxId?.message}>
                      <select id="purchaseTaxId" disabled={saving} {...register('purchaseTaxId')}>
                        <option value={EMPTY_GUID}>{t('common.select')}</option>
                        {(catalogs?.taxRates ?? []).map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.code} — {x.name}
                          </option>
                        ))}
                      </select>
                    </ZHField>
                    <ZHColSpan span={2}>
                      <ZHField label={t('products.form.tariff')} fieldError={errors.tariffId?.message}>
                        <select id="tariffId" disabled={saving} {...register('tariffId')}>
                          <option value={EMPTY_GUID}>{t('common.select')}</option>
                          {(catalogs?.tariffs ?? []).map((x) => (
                            <option key={x.id} value={x.id}>
                              {x.code} — {x.name}
                            </option>
                          ))}
                        </select>
                      </ZHField>
                    </ZHColSpan>
                  </ZHGrid>
                </ZHFormSection>

                <ZHFormSection title={t('products.form.behavior')}>
                  <ZHGrid cols={1}>
                    <Controller
                      name="isService"
                      control={control}
                      render={({ field }) => (
                        <ZHToggle
                          label={t('products.form.isService')}
                          description={t('products.form.isService')}
                          value={field.value}
                          onChange={field.onChange}
                          disabled={saving}
                        />
                      )}
                    />
                    <Controller
                      name="isForSale"
                      control={control}
                      render={({ field }) => (
                        <ZHToggle
                          label={t('products.form.isForSale')}
                          description={t('products.form.isForSale')}
                          value={field.value}
                          onChange={field.onChange}
                          disabled={saving}
                        />
                      )}
                    />
                    <Controller
                      name="availableOnWeb"
                      control={control}
                      render={({ field }) => (
                        <ZHToggle
                          label={t('products.form.availableOnWeb')}
                          description={t('products.form.availableOnWeb')}
                          value={field.value}
                          onChange={field.onChange}
                          disabled={saving}
                        />
                      )}
                    />
                    <Controller
                      name="availableOnMobile"
                      control={control}
                      render={({ field }) => (
                        <ZHToggle
                          label={t('products.form.availableOnMobile')}
                          description={t('products.form.availableOnMobile')}
                          value={field.value}
                          onChange={field.onChange}
                          disabled={saving}
                        />
                      )}
                    />
                  </ZHGrid>
                </ZHFormSection>
            </>
          )}

          {formTab === 'images' && (
            <ZHFormSection title={t('products.images.section')}>
              <p className="subtle zh-mb-12">{t('products.images.hint')}</p>
              {imageRows.length === 0 ? (
                <p className="subtle zh-mb-12">{t('common.noData')}</p>
              ) : null}
              {imageRows.map((row) => (
                <div key={row.id} className="products-image-row">
                  {row.url ? (
                    <img src={row.url} alt="" className="products-image-preview" />
                  ) : (
                    <div className="products-image-preview" aria-hidden />
                  )}
                  <div className="products-image-fields">
                    <ZHField label={t('products.images.url')}>
                      <input
                        value={row.url}
                        onChange={(e) => patchImageRow(row.id, { url: e.target.value })}
                        placeholder={t('products.images.urlPlaceholder')}
                        disabled={saving}
                      />
                    </ZHField>
                    <ZHField label={t('products.images.alt')}>
                      <input
                        value={row.altText}
                        onChange={(e) => patchImageRow(row.id, { altText: e.target.value })}
                        disabled={saving}
                      />
                    </ZHField>
                    <div className="products-image-actions">
                      <input
                        type="file"
                        accept="image/*"
                        disabled={saving}
                        onChange={(e) => {
                          const f = e.target.files?.[0];
                          void assignImageFile(row.id, f);
                          e.target.value = '';
                        }}
                      />
                    </div>
                    <div className="products-image-checks">
                      <label>
                        <input
                          type="checkbox"
                          checked={row.isMain}
                          onChange={(e) => setImageMain(row.id, e.target.checked)}
                          disabled={saving}
                        />
                        {t('products.images.main')}
                      </label>
                      <label>
                        <input
                          type="checkbox"
                          checked={row.isEcommerce}
                          onChange={(e) => setImageEcommerce(row.id, e.target.checked)}
                          disabled={saving}
                        />
                        {t('products.images.ecommerce')}
                      </label>
                    </div>
                  </div>
                  <ZHBtn variant="ghost" size="md" type="button" onClick={() => removeImageRow(row.id)} disabled={saving}>
                    {t('products.images.remove')}
                  </ZHBtn>
                </div>
              ))}
              <ZHBtn
                variant="secondary"
                size="md"
                type="button"
                onClick={() => setImageRows((rows) => [...rows, newImageRow()])}
                disabled={saving}
              >
                {t('products.images.addRow')}
              </ZHBtn>
            </ZHFormSection>
          )}

          {formTab === 'list' && (
            <ZHFormSection title={t('products.tabList')}>
              <p className="subtle zh-mb-12">{t('products.list.subtitle')}</p>
              <ProductsRecentListBlock
                t={t}
                loading={loading}
                error={error}
                products={products ?? null}
                recentRows={recentProductsList}
                listQuery={listQuery}
                setListQuery={setListQuery}
                lineLabel={(id) => show(maps.lines, id)}
                canCreate={canCreate}
                onGoToCreate={() => setFormTab('general')}
                onViewAudit={openProductAudit}
              />
            </ZHFormSection>
          )}

          {formTab === 'audit' && (
            <ZHFormSection title={t('common.formTab.audit')}>
              {!auditProductId ? (
                <EmptyState message={t('audit.pickRow')} />
              ) : (
                <EntityAuditPanel
                  entityType="Product"
                  entityId={auditProductId}
                  refreshKey={auditRefreshKey}
                />
              )}
            </ZHFormSection>
          )}

          <ZHDirtyBar
            visible={isDirty && formTab === 'general'}
            loading={saving}
            onDiscard={discardChanges}
            onSave={() => {
              formRef.current?.requestSubmit();
            }}
            saveLabel={saving ? t('common.saving') : t('products.modal.create.submit')}
          />
        </ZHFormCard>
      )}

      {canCreate && error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
      {canCreate && loading && formTab !== 'list' && formTab !== 'audit' ? <LoadingState /> : null}

      {!canCreate && (
        <TableCard>
          <div className="zh-card-section-title">{t('products.tabList')}</div>
          <p className="subtle zh-mb-12">{t('products.list.subtitle')}</p>
          <ProductsRecentListBlock
            t={t}
            loading={loading}
            error={error}
            products={products ?? null}
            recentRows={recentProductsList}
            listQuery={listQuery}
            setListQuery={setListQuery}
            lineLabel={(id) => show(maps.lines, id)}
            canCreate={false}
            onViewAudit={openProductAudit}
          />
          {auditProductId ? (
            <ZHFormSection title={t('common.formTab.audit')}>
              <EntityAuditPanel
                entityType="Product"
                entityId={auditProductId}
                refreshKey={auditRefreshKey}
              />
            </ZHFormSection>
          ) : null}
        </TableCard>
      )}
    </PageShell>
  );
}
