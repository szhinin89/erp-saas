import type { FieldErrors, UseFormRegister, UseFormSetValue } from 'react-hook-form';
import { ZHField, ZHGrid } from '../../../components/zh/ZHForm';
import { ZHSection } from '../../../components/zh/ZHLayout';
import type { ProductCategoryListItem } from '../api/catalogService';
import type { CatalogSubcategoryFormValues } from '../../../schemas/catalog/catalogPagesFormsSchema';

type SubcategoriesCatalogPageDataTabProps = {
  t: (key: string) => string;
  canCreate: boolean;
  saving: boolean;
  loading: boolean;
  lines: { id: string; code: string; name: string }[];
  formCategories: ProductCategoryListItem[];
  formWatch: CatalogSubcategoryFormValues;
  lineIdField: ReturnType<UseFormRegister<CatalogSubcategoryFormValues>>;
  setValue: UseFormSetValue<CatalogSubcategoryFormValues>;
  register: UseFormRegister<CatalogSubcategoryFormValues>;
  errors: FieldErrors<CatalogSubcategoryFormValues>;
};

export function SubcategoriesCatalogPageDataTab({
  t,
  canCreate,
  saving,
  loading,
  lines,
  formCategories,
  formWatch,
  lineIdField,
  setValue,
  register,
  errors,
}: SubcategoriesCatalogPageDataTabProps) {
  if (!canCreate) {
    return (
      <ZHSection top={10}>
        <div className="empty-state">{t('common.readOnly')}</div>
      </ZHSection>
    );
  }

  return (
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
          <select disabled={saving || loading || !formWatch.lineId} {...register('categoryId')}>
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
  );
}
