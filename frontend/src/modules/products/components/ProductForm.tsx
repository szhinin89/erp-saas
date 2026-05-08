import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import type { CatalogItem } from '../../../services/catalogService';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../../../components/zh/ZHForm';
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
    setValue,
    watch,
    formState: { errors },
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema),
    defaultValues: defaultProductValues,
  });

  const selectedLineId = watch('lineId');
  const selectedCategoryId = watch('categoryId');

  useEffect(() => {
    setValue('categoryId', EMPTY_GUID, { shouldDirty: true, shouldValidate: true });
    setValue('subcategoryId', EMPTY_GUID, { shouldDirty: true, shouldValidate: true });
  }, [selectedLineId, setValue]);

  useEffect(() => {
    setValue('subcategoryId', EMPTY_GUID, { shouldDirty: true, shouldValidate: true });
  }, [selectedCategoryId, setValue]);

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
      <ZHFormSection title={t('products.section.general')}>
        <ZHGrid cols={2}>
          <ZHField label={t('products.form.saleCode')} required fieldError={showFieldError(errors.saleCode?.message)}>
            <input id="saleCode" disabled={loading} {...register('saleCode')} />
          </ZHField>
          <ZHField label={t('products.form.shortName')} required fieldError={showFieldError(errors.shortName?.message)}>
            <input id="shortName" disabled={loading} {...register('shortName')} />
          </ZHField>
          <ZHField label={t('products.form.description')} required fieldError={showFieldError(errors.description?.message)}>
            <input id="description" disabled={loading} {...register('description')} />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      <ZHFormSection title={t('products.form.classification')}>
        <ZHGrid cols={2}>
          <ZHField label={t('products.form.line')} fieldError={showFieldError(errors.lineId?.message)}>
            <select id="lineId" disabled={loading} {...register('lineId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.lines ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.category')} fieldError={showFieldError(errors.categoryId?.message)}>
            <select id="categoryId" disabled={loading} {...register('categoryId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {filteredCategories.map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.subcategory')} fieldError={showFieldError(errors.subcategoryId?.message)}>
            <select id="subcategoryId" disabled={loading} {...register('subcategoryId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {filteredSubcategories.map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.unit')} fieldError={showFieldError(errors.unitOfMeasureId?.message)}>
            <select id="unitOfMeasureId" disabled={loading} {...register('unitOfMeasureId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.units ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.brand')} fieldError={showFieldError(errors.brandId?.message)}>
            <select id="brandId" disabled={loading} {...register('brandId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.brands ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.productType')} fieldError={showFieldError(errors.productTypeId?.message)}>
            <select id="productTypeId" disabled={loading} {...register('productTypeId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.productTypes ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.tariff')} fieldError={showFieldError(errors.tariffId?.message)}>
            <select id="tariffId" disabled={loading} {...register('tariffId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.tariffs ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.saleTax')} fieldError={showFieldError(errors.saleTaxId?.message)}>
            <select id="saleTaxId" disabled={loading} {...register('saleTaxId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.taxRates ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.purchaseTax')} fieldError={showFieldError(errors.purchaseTaxId?.message)}>
            <select id="purchaseTaxId" disabled={loading} {...register('purchaseTaxId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.taxRates ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      <div className="zh-form-actions-row zh-form-actions-row--end">
        <ZHBtn type="submit" variant="primary" size="md" disabled={loading}>
          {loading ? t('common.saving') : t('products.modal.create.submit')}
        </ZHBtn>
      </div>
    </form>
  );
}
