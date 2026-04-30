import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Modal } from './Modal';
import { productService, type CreateProductRequest } from '../services/productService';
import { useI18n } from '../i18n/i18n';
import { catalogService, type CatalogItem } from '../services/catalogService';

interface Props {
  onClose: () => void;
  onCreated: () => void;
}

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

const EMPTY: CreateProductRequest = {
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
};

export function CreateProductModal({ onClose, onCreated }: Props) {
  const { t } = useI18n();
  const [form, setForm]       = useState<CreateProductRequest>(EMPTY);
  const [error, setError]     = useState('');
  const [loading, setLoading] = useState(false);

  const [loadingCatalogs, setLoadingCatalogs] = useState(true);
  const [brands, setBrands] = useState<CatalogItem[]>([]);
  const [productTypes, setProductTypes] = useState<CatalogItem[]>([]);
  const [units, setUnits] = useState<CatalogItem[]>([]);
  const [taxRates, setTaxRates] = useState<CatalogItem[]>([]);
  const [tariffs, setTariffs] = useState<CatalogItem[]>([]);
  const [lines, setLines] = useState<CatalogItem[]>([]);
  const [categories, setCategories] = useState<CatalogItem[]>([]);
  const [subcategories, setSubcategories] = useState<CatalogItem[]>([]);

  const set = (field: keyof CreateProductRequest, value: unknown) =>
    setForm((f) => ({ ...f, [field]: value }));

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoadingCatalogs(true);
        const [
          b, pt, u, tr, ta, li,
        ] = await Promise.all([
          catalogService.brands(true),
          catalogService.productTypes(true),
          catalogService.units(true),
          catalogService.taxRates(true),
          catalogService.tariffs(true),
          catalogService.productLines({ activeStatus: 'active' }),
        ]);
        if (cancelled) return;
        setBrands(b ?? []);
        setProductTypes(pt ?? []);
        setUnits(u ?? []);
        setTaxRates(tr ?? []);
        setTariffs(ta ?? []);
        setLines(li ?? []);
      } catch {
        // Si falla, igual deja el formulario (se verá vacío)
      } finally {
        if (!cancelled) setLoadingCatalogs(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  // Categories dependen de lineId
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const lineId = form.lineId;
      if (!lineId || lineId === EMPTY_GUID) {
        setCategories([]);
        set('categoryId', EMPTY_GUID);
        setSubcategories([]);
        set('subcategoryId', EMPTY_GUID);
        return;
      }
      try {
        const cats = await catalogService.categories({ activeStatus: 'active', lineId });
        if (cancelled) return;
        setCategories(cats ?? []);
        // Si la categoría actual no pertenece, resetea
        if (!cats?.some((c) => c.id === form.categoryId)) {
          set('categoryId', EMPTY_GUID);
          setSubcategories([]);
          set('subcategoryId', EMPTY_GUID);
        }
      } catch {
        if (!cancelled) setCategories([]);
      }
    })();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form.lineId]);

  // Subcategories dependen de categoryId
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const categoryId = form.categoryId;
      if (!categoryId || categoryId === EMPTY_GUID) {
        setSubcategories([]);
        set('subcategoryId', EMPTY_GUID);
        return;
      }
      try {
        const subs = await catalogService.subcategories({ activeStatus: 'active', categoryId });
        if (cancelled) return;
        setSubcategories(subs ?? []);
        if (!subs?.some((s) => s.id === form.subcategoryId)) {
          set('subcategoryId', EMPTY_GUID);
        }
      } catch {
        if (!cancelled) setSubcategories([]);
      }
    })();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form.categoryId]);

  const selectDisabled = loading || loadingCatalogs;

  const catalogHint = useMemo(() => {
    if (!loadingCatalogs) return null;
    return (
      <p style={{ margin: '0 0 10px', fontSize: 12, color: '#6b7280' }}>
        {t('common.loading')}
      </p>
    );
  }, [loadingCatalogs, t]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await productService.create(form);
      onCreated();
      onClose();
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { error?: string } } })
          ?.response?.data?.error ?? t('products.modal.create.error')
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal title={t('products.modal.create.title')} onClose={onClose} width={560}>
      <form onSubmit={handleSubmit}>
        <div className="form-grid form-grid--2col">
          <div className="field">
            <label htmlFor="saleCode">{t('products.form.saleCode')}</label>
            <input
              id="saleCode"
              value={form.saleCode}
              onChange={(e) => set('saleCode', e.target.value)}
              placeholder={t('products.form.saleCode.placeholder')}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="purchaseCode">{t('products.form.purchaseCode')}</label>
            <input
              id="purchaseCode"
              value={form.purchaseCode ?? ''}
              onChange={(e) => set('purchaseCode', e.target.value)}
              placeholder={t('products.form.purchaseCode.placeholder')}
            />
          </div>

          <div className="field field--span2">
            <label htmlFor="shortName">{t('products.form.shortName')}</label>
            <input
              id="shortName"
              value={form.shortName}
              onChange={(e) => set('shortName', e.target.value)}
              placeholder={t('products.form.shortName.placeholder')}
              required
            />
          </div>

          <div className="field field--span2">
            <label htmlFor="description">{t('products.form.description')}</label>
            <input
              id="description"
              value={form.description}
              onChange={(e) => set('description', e.target.value)}
              placeholder={t('products.form.description.placeholder')}
              required
            />
          </div>

          <div className="field field--span2" style={{ borderTop: '1px solid #f0f0f0', paddingTop: 12 }}>
            <p style={{ margin: '0 0 10px', fontSize: 12, color: '#6b7280' }}>
              {t('products.form.classification')}
            </p>
            {catalogHint}
            <div className="form-grid form-grid--2col">
              <div className="field">
                <label htmlFor="lineId">{t('products.form.line')}</label>
                <select
                  id="lineId"
                  value={form.lineId}
                  onChange={(e) => set('lineId', e.target.value)}
                  disabled={selectDisabled}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {lines.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor="brandId">{t('products.form.brand')}</label>
                <select
                  id="brandId"
                  value={form.brandId}
                  onChange={(e) => set('brandId', e.target.value)}
                  disabled={selectDisabled}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {brands.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor="categoryId">{t('products.form.category')}</label>
                <select
                  id="categoryId"
                  value={form.categoryId}
                  onChange={(e) => set('categoryId', e.target.value)}
                  disabled={selectDisabled || form.lineId === EMPTY_GUID}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {categories.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor="subcategoryId">{t('products.form.subcategory')}</label>
                <select
                  id="subcategoryId"
                  value={form.subcategoryId}
                  onChange={(e) => set('subcategoryId', e.target.value)}
                  disabled={selectDisabled || form.categoryId === EMPTY_GUID}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {subcategories.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor="productTypeId">{t('products.form.productType')}</label>
                <select
                  id="productTypeId"
                  value={form.productTypeId}
                  onChange={(e) => set('productTypeId', e.target.value)}
                  disabled={selectDisabled}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {productTypes.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor="unitOfMeasureId">{t('products.form.unit')}</label>
                <select
                  id="unitOfMeasureId"
                  value={form.unitOfMeasureId}
                  onChange={(e) => set('unitOfMeasureId', e.target.value)}
                  disabled={selectDisabled}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {units.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          <div className="field field--span2" style={{ borderTop: '1px solid #f0f0f0', paddingTop: 12 }}>
            <p style={{ margin: '0 0 10px', fontSize: 12, color: '#6b7280' }}>
              {t('products.form.taxes')}
            </p>
            {catalogHint}
            <div className="form-grid form-grid--2col">
              <div className="field">
                <label htmlFor="saleTaxId">{t('products.form.saleTax')}</label>
                <select
                  id="saleTaxId"
                  value={form.saleTaxId}
                  onChange={(e) => set('saleTaxId', e.target.value)}
                  disabled={selectDisabled}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {taxRates.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor="purchaseTaxId">{t('products.form.purchaseTax')}</label>
                <select
                  id="purchaseTaxId"
                  value={form.purchaseTaxId}
                  onChange={(e) => set('purchaseTaxId', e.target.value)}
                  disabled={selectDisabled}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {taxRates.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>

              <div className="field field--span2">
                <label htmlFor="tariffId">{t('products.form.tariff')}</label>
                <select
                  id="tariffId"
                  value={form.tariffId}
                  onChange={(e) => set('tariffId', e.target.value)}
                  disabled={selectDisabled}
                >
                  <option value={EMPTY_GUID}>{t('common.select')}</option>
                  {tariffs.map((x) => (
                    <option key={x.id} value={x.id}>{x.code} — {x.name}</option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          <div className="field field--span2" style={{ borderTop: '1px solid #f0f0f0', paddingTop: 12 }}>
            <p style={{ margin: '0 0 10px', fontSize: 12, color: '#6b7280' }}>
              {t('products.form.behavior')}
            </p>
            <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
              {([
                ['isService', t('products.form.isService')],
                ['isForSale', t('products.form.isForSale')],
                ['availableOnWeb', t('products.form.availableOnWeb')],
                ['availableOnMobile', t('products.form.availableOnMobile')],
              ] as [keyof CreateProductRequest, string][]).map(([key, label]) => (
                <div key={key} className="field field--inline">
                  <input
                    id={key}
                    type="checkbox"
                    checked={form[key] as boolean}
                    onChange={(e) => set(key, e.target.checked)}
                  />
                  <label htmlFor={key}>{label}</label>
                </div>
              ))}
            </div>
          </div>
        </div>

        {error && <p className="form-error" style={{ marginTop: 14 }}>{error}</p>}

        <div className="form-actions">
          <button type="button" className="btn btn--ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? t('common.saving') : t('products.modal.create.submit')}
          </button>
        </div>
      </form>
    </Modal>
  );
}
