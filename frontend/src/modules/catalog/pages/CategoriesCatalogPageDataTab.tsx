import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import { ZHField, ZHGrid } from '../../../components/zh/ZHForm';
import { ZHSection } from '../../../components/zh/ZHLayout';
import type { CatalogCategoryFormValues } from '../../../schemas/catalog/catalogPagesFormsSchema';

type CategoriesCatalogPageDataTabProps = {
  t: (key: string) => string;
  canCreate: boolean;
  saving: boolean;
  loading: boolean;
  lines: { id: string; code: string; name: string }[];
  register: UseFormRegister<CatalogCategoryFormValues>;
  errors: FieldErrors<CatalogCategoryFormValues>;
};

export function CategoriesCatalogPageDataTab({
  t,
  canCreate,
  saving,
  loading,
  lines,
  register,
  errors,
}: CategoriesCatalogPageDataTabProps) {
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
  );
}
