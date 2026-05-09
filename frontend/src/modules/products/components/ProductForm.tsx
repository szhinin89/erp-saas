import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import type { CatalogItem } from '../../../services/catalogService';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../../../components/zh/ZHForm';
import { defaultProductValues, productSchema, type ProductFormValues } from '../schemas/productSchema';
import type { Product } from '../../../types/product';

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
  onSubmit: (values: any) => Promise<void>;
  editMode?: boolean;
  existingProduct?: Product | null;
  onCancelEdit?: () => void;
};

export function ProductForm({ t, catalogs, loading, onSubmit, editMode = false, existingProduct, onCancelEdit }: Props) {
  const {
    register,
    handleSubmit,
    control,
    setValue,
    watch,
    formState: { errors },
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema) as any,
    defaultValues: defaultProductValues,
  });

  const { fields: barcodeFields, append, remove } = useFieldArray({
    control,
    name: 'barcodes',
  });

  const barcodeTypes = [
    { value: 1, label: t('products.form.barcodeType.ean13') },
    { value: 2, label: t('products.form.barcodeType.ean8') },
    { value: 3, label: t('products.form.barcodeType.qr') },
    { value: 4, label: t('products.form.barcodeType.code128') },
    { value: 5, label: t('products.form.barcodeType.internal') },
    { value: 99, label: t('products.form.barcodeType.other') },
  ];

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

  // Load existing product data in edit mode
  useEffect(() => {
    if (editMode && existingProduct) {
      // Reset form with existing data
      Object.keys(defaultProductValues).forEach((key) => {
        const value = (existingProduct as any)[key];
        if (value !== undefined) {
          setValue(key as keyof ProductFormValues, value);
        }
      });

      // Load barcodes
      if (existingProduct.barcodes && existingProduct.barcodes.length > 0) {
        // Clear existing barcodes
        barcodeFields.forEach((_, index) => remove(index));
        // Add existing barcodes
        existingProduct.barcodes.forEach((barcode) => {
          append({ code: barcode.code, type: barcode.type });
        });
      }
    }
  }, [editMode, existingProduct, setValue, append, remove, barcodeFields]);

  const showFieldError = (message?: string) => (message ? t(message) : null);

  // Validate barcode duplicates
  const validateBarcodeDuplicates = (barcodes: Array<{ code: string; type: number }>) => {
    const codes = barcodes.map(b => b.code.trim()).filter(code => code.length > 0);
    const duplicates = codes.filter((code, index) => codes.indexOf(code) !== index);
    return duplicates.length === 0 ? null : `Código de barras duplicado: ${duplicates[0]}`;
  };

  const barcodeDuplicateError = validateBarcodeDuplicates(watch('barcodes') || []);

  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      {/* Identificación */}
      <ZHFormSection title={t('products.section.general')}>
        <ZHGrid cols={2}>
          <ZHField label={t('products.form.saleCode')} required fieldError={showFieldError(errors.saleCode?.message)}>
            <input id="saleCode" disabled={loading} {...register('saleCode')} />
          </ZHField>
          <ZHField label={t('products.form.purchaseCode')} fieldError={showFieldError(errors.purchaseCode?.message)}>
            <input id="purchaseCode" disabled={loading} {...register('purchaseCode')} />
          </ZHField>
          <ZHField label={t('products.form.shortName')} required fieldError={showFieldError(errors.shortName?.message)}>
            <input id="shortName" disabled={loading} {...register('shortName')} />
          </ZHField>
          <ZHField label={t('products.form.description')} required fieldError={showFieldError(errors.description?.message)}>
            <input id="description" disabled={loading} {...register('description')} />
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={1}>
          <ZHField label={t('products.form.observations')} fieldError={showFieldError(errors.observations?.message)}>
            <textarea
              id="observations"
              disabled={loading}
              rows={3}
              {...register('observations')}
              placeholder={t('products.form.observationsPlaceholder')}
            />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      {/* Códigos de barras */}
      <ZHFormSection title={t('products.section.barcodes')}>
        {barcodeDuplicateError && (
          <div className="zh-form-error zh-mb-4">
            {barcodeDuplicateError}
          </div>
        )}
        <div className="zh-mb-4">
          <ZHBtn type="button" variant="secondary" disabled={loading} onClick={() => append({ code: '', type: 1 })}>
            {t('products.form.addBarcode')}
          </ZHBtn>
        </div>
        {barcodeFields.length === 0 ? (
          <p className="subtle">{t('products.form.barcodesEmpty')}</p>
        ) : null}
        {barcodeFields.map((field, index) => (
          <ZHGrid cols={3} key={field.id}>
            <ZHField
              label={t('products.form.barcodeCode')}
              required
              fieldError={showFieldError(errors.barcodes?.[index]?.code?.message)}
            >
              <input
                id={`barcodeCode-${index}`}
                disabled={loading}
                {...register(`barcodes.${index}.code` as const)}
              />
            </ZHField>
            <ZHField
              label={t('products.form.barcodeType')}
              required
              fieldError={showFieldError(errors.barcodes?.[index]?.type?.message)}
            >
              <select
                id={`barcodeType-${index}`}
                disabled={loading}
                {...register(`barcodes.${index}.type` as const, { valueAsNumber: true })}
              >
                <option value="">{t('common.select')}</option>
                {barcodeTypes.map((type) => (
                  <option key={type.value} value={type.value}>
                    {type.label}
                  </option>
                ))}
              </select>
            </ZHField>
            <div className="zh-flex zh-items-end zh-gap-3">
              <ZHBtn type="button" variant="ghost" disabled={loading} onClick={() => remove(index)}>
                {t('products.form.removeBarcode')}
              </ZHBtn>
            </div>
          </ZHGrid>
        ))}
      </ZHFormSection>

      {/* Clasificación */}
      <ZHFormSection title={t('products.form.classification')}>
        <ZHGrid cols={3}>
          <ZHField label={t('products.form.line')} required fieldError={showFieldError(errors.lineId?.message)}>
            <select id="lineId" disabled={loading} {...register('lineId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.lines ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.category')} required fieldError={showFieldError(errors.categoryId?.message)}>
            <select id="categoryId" disabled={loading} {...register('categoryId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {filteredCategories.map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.subcategory')} required fieldError={showFieldError(errors.subcategoryId?.message)}>
            <select id="subcategoryId" disabled={loading} {...register('subcategoryId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {filteredSubcategories.map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      {/* Catálogos */}
      <ZHFormSection title={t('products.section.catalogs')}>
        <ZHGrid cols={2}>
          <ZHField label={t('products.form.unit')} required fieldError={showFieldError(errors.unitOfMeasureId?.message)}>
            <select id="unitOfMeasureId" disabled={loading} {...register('unitOfMeasureId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.units ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.brand')} required fieldError={showFieldError(errors.brandId?.message)}>
            <select id="brandId" disabled={loading} {...register('brandId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.brands ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.productType')} required fieldError={showFieldError(errors.productTypeId?.message)}>
            <select id="productTypeId" disabled={loading} {...register('productTypeId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.productTypes ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('products.form.tariff')} required fieldError={showFieldError(errors.tariffId?.message)}>
            <select id="tariffId" disabled={loading} {...register('tariffId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.tariffs ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      {/* Impuestos */}
      <ZHFormSection title={t('products.section.taxes')}>
        <ZHGrid cols={2}>
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
          <ZHField label={t('products.form.exciseTax')} fieldError={showFieldError(errors.exciseTaxId?.message)}>
            <select id="exciseTaxId" disabled={loading} {...register('exciseTaxId')}>
              <option value={EMPTY_GUID}>{t('common.select')}</option>
              {(catalogs?.taxRates ?? []).map((item) => (
                <option key={item.id} value={item.id}>{`${item.code} - ${item.name}`}</option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={3}>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('appliesVatOnSale')} />
            <span>{t('products.form.appliesVatOnSale')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('appliesVatOnPurchase')} />
            <span>{t('products.form.appliesVatOnPurchase')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('appliesExciseTax')} />
            <span>{t('products.form.appliesExciseTax')}</span>
          </label>
        </ZHGrid>
      </ZHFormSection>

      {/* Comportamiento de Stock */}
      <ZHFormSection title={t('products.section.stockBehavior')}>
        <ZHGrid cols={3}>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('isService')} />
            <span>{t('products.form.isService')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('tracksStock')} />
            <span>{t('products.form.tracksStock')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('tracksLot')} />
            <span>{t('products.form.tracksLot')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('tracksSeries')} />
            <span>{t('products.form.tracksSeries')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('hasRecipe')} />
            <span>{t('products.form.hasRecipe')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('stockWithDecimal')} />
            <span>{t('products.form.stockWithDecimal')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('saleWithDecimal')} />
            <span>{t('products.form.saleWithDecimal')}</span>
          </label>
        </ZHGrid>
        <ZHGrid cols={1}>
          <ZHField label={t('products.form.maxItemDiscountPercent')} fieldError={showFieldError(errors.maxItemDiscountPercent?.message)}>
            <input
              id="maxItemDiscountPercent"
              type="number"
              min="0"
              max="100"
              step="0.01"
              disabled={loading}
              {...register('maxItemDiscountPercent', { valueAsNumber: true })}
            />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      {/* Canales de Venta */}
      <ZHFormSection title={t('products.section.salesChannels')}>
        <ZHGrid cols={3}>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('availableOnWeb')} />
            <span>{t('products.form.availableOnWeb')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('availableOnMobile')} />
            <span>{t('products.form.availableOnMobile')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('isEcommerceActive')} />
            <span>{t('products.form.isEcommerceActive')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('isFavorite')} />
            <span>{t('products.form.isFavorite')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('isForSale')} />
            <span>{t('products.form.isForSale')}</span>
          </label>
        </ZHGrid>
      </ZHFormSection>

      {/* Variantes */}
      <ZHFormSection title={t('products.section.variants')}>
        <ZHGrid cols={2}>
          <ZHField label={t('products.form.baseColor')} fieldError={showFieldError(errors.baseColor?.message)}>
            <input id="baseColor" disabled={loading} {...register('baseColor')} />
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('hasMultipleColors')} />
            <span>{t('products.form.hasMultipleColors')}</span>
          </label>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('hasSizes')} />
            <span>{t('products.form.hasSizes')}</span>
          </label>
        </ZHGrid>
      </ZHFormSection>

      {/* Aranceles */}
      <ZHFormSection title={t('products.section.tariffs')}>
        <ZHGrid cols={1}>
          <label className="zh-checkbox">
            <input type="checkbox" disabled={loading} {...register('handlesTariff')} />
            <span>{t('products.form.handlesTariff')}</span>
          </label>
        </ZHGrid>
      </ZHFormSection>

      <div className="zh-form-actions-row zh-form-actions-row--end">
        {editMode && onCancelEdit && (
          <ZHBtn type="button" variant="ghost" size="md" onClick={onCancelEdit} disabled={loading}>
            {t('common.cancel')}
          </ZHBtn>
        )}
        <ZHBtn type="submit" variant="primary" size="md" disabled={loading || !!barcodeDuplicateError}>
          {loading
            ? t('common.saving')
            : editMode
              ? t('products.modal.edit.submit', 'Actualizar Producto')
              : t('products.modal.create.submit')
          }
        </ZHBtn>
      </div>
    </form>
  );
}
