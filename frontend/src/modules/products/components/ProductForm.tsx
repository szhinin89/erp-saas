import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import type { CatalogItem } from '../../../services/catalogService';
import { defaultProductValues, productSchema, type ProductFormValues } from '../schemas/productSchema';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

type ProductFormCatalogs = {
  lines: CatalogItem[];
  categories: CatalogItem[];
  subcategories: CatalogItem[];
  units: CatalogItem[];
  brands: CatalogItem[];
  productTypes: CatalogItem[];
  taxRates: CatalogItem[];
  tariffs: CatalogItem[];
};

type Props = {
  t: (key: string, fallback?: string) => string;
  catalogs: ProductFormCatalogs | null | undefined;
  loading: boolean;
  onSubmit: (values: ProductFormValues) => Promise<void>;
};

export function ProductForm({ t, catalogs, loading, onSubmit }: Props) {
  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema),
    defaultValues: defaultProductValues,
  });

  const selectedLineId = watch('lineId');
  const selectedCategoryId = watch('categoryId');

  const filteredCategories = (catalogs?.categories ?? []).filter((item) => {
    if (selectedLineId === EMPTY_GUID) return false;
    return 'lineId' in item ? item.lineId === selectedLineId : true;
  });

  const filteredSubcategories = (catalogs?.subcategories ?? []).filter((item) => {
    if (selectedCategoryId === EMPTY_GUID) return false;
    return 'categoryId' in item ? item.categoryId === selectedCategoryId : true;
  });

  const showFieldError = (message?: string) => (message ? t(message) : null);

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <div>
        <label htmlFor="saleCode">{t('products.form.saleCode')}</label>
        <input id="saleCode" disabled={loading} {...register('saleCode')} />
        <small>{showFieldError(errors.saleCode?.message)}</small>
      </div>

      <div>
        <label htmlFor="shortName">{t('products.form.shortName')}</label>
        <input id="shortName" disabled={loading} {...register('shortName')} />
        <small>{showFieldError(errors.shortName?.message)}</small>
      </div>

      <div>
        <label htmlFor="description">{t('products.form.description')}</label>
        <input id="description" disabled={loading} {...register('description')} />
        <small>{showFieldError(errors.description?.message)}</small>
      </div>

      <div>
        <label htmlFor="lineId">{t('products.form.line')}</label>
        <select id="lineId" disabled={loading} {...register('lineId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {(catalogs?.lines ?? []).map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
        <small>{showFieldError(errors.lineId?.message)}</small>
      </div>

      <div>
        <label htmlFor="categoryId">{t('products.form.category')}</label>
        <select id="categoryId" disabled={loading} {...register('categoryId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {filteredCategories.map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
        <small>{showFieldError(errors.categoryId?.message)}</small>
      </div>

      <div>
        <label htmlFor="subcategoryId">{t('products.form.subcategory')}</label>
        <select id="subcategoryId" disabled={loading} {...register('subcategoryId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {filteredSubcategories.map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
        <small>{showFieldError(errors.subcategoryId?.message)}</small>
      </div>

      <div>
        <label htmlFor="unitOfMeasureId">{t('products.form.unit')}</label>
        <select id="unitOfMeasureId" disabled={loading} {...register('unitOfMeasureId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {(catalogs?.units ?? []).map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="brandId">{t('products.form.brand')}</label>
        <select id="brandId" disabled={loading} {...register('brandId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {(catalogs?.brands ?? []).map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="productTypeId">{t('products.form.productType')}</label>
        <select id="productTypeId" disabled={loading} {...register('productTypeId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {(catalogs?.productTypes ?? []).map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="tariffId">{t('products.form.tariff')}</label>
        <select id="tariffId" disabled={loading} {...register('tariffId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {(catalogs?.tariffs ?? []).map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="saleTaxId">{t('products.form.saleTax')}</label>
        <select id="saleTaxId" disabled={loading} {...register('saleTaxId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {(catalogs?.taxRates ?? []).map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
      </div>

      <div>
        <label htmlFor="purchaseTaxId">{t('products.form.purchaseTax')}</label>
        <select id="purchaseTaxId" disabled={loading} {...register('purchaseTaxId')}>
          <option value={EMPTY_GUID}>{t('common.select')}</option>
          {(catalogs?.taxRates ?? []).map((item) => (
            <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
          ))}
        </select>
      </div>

      <button type="submit" disabled={loading}>
        {t('products.modal.create.submit')}
      </button>
    </form>
  );
}
