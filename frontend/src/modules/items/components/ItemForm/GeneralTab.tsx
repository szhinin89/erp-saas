import type { UseFormReturn } from 'react-hook-form';
import { ZHField, ZHFormSection, ZHGrid } from '../../../../components/zh/ZHForm';
import type { CreateItemFormValues } from '../../schemas/createItemSchema';
import type { AttributeGroupDto } from '../../../../types/items';

type Props = {
  form: UseFormReturn<CreateItemFormValues>;
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  isEditMode: boolean;
  sriUomOptions: { code: string; name: string }[];
  categoryOptions: { id: string; name: string; depth: number }[];
  brandOptions:    { id: string; name: string }[];
};

export function GeneralTab({ form, t, disabled, isEditMode, sriUomOptions, categoryOptions, brandOptions }: Props) {
  const { register, formState: { errors } } = form;
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);

  return (
    <>
      <ZHFormSection title={t('items.section.identity', 'Identificación')}>
        <ZHGrid cols={3}>
          {!isEditMode && (
            <ZHField label={t('items.form.sku', 'SKU *')} required fieldError={fe(errors.sku?.message)}>
              <input {...register('sku')} placeholder="CAMISA-ROJA" disabled={disabled} style={{ textTransform: 'uppercase' }} />
            </ZHField>
          )}
          <ZHField label={t('items.form.shortName', 'Nombre corto *')} required fieldError={fe(errors.shortName?.message)}>
            <input {...register('shortName')} placeholder="Camisa Roja L" disabled={disabled} />
          </ZHField>
          <ZHField label={t('items.form.purchaseCode', 'Código de compra')} fieldError={fe(errors.purchaseCode?.message)}>
            <input {...register('purchaseCode')} placeholder="PROV-001" disabled={disabled} />
          </ZHField>
        </ZHGrid>
        <ZHField label={t('items.form.description', 'Descripción *')} required fieldError={fe(errors.description?.message)}>
          <input {...register('description')} placeholder="Descripción completa del ítem" disabled={disabled} />
        </ZHField>
        <ZHField label={t('items.form.observations', 'notes')}>
          <textarea {...register('observations')} rows={2} disabled={disabled} />
        </ZHField>
      </ZHFormSection>

      <ZHFormSection title={t('items.section.classification', 'Clasificación')}>
        <ZHGrid cols={3}>
          <ZHField label={t('items.form.itemType', 'Tipo *')} required fieldError={fe(errors.itemType?.message)}>
            <select {...register('itemType')} disabled={disabled || isEditMode}>
              <option value="Physical">{t('items.type.physical', 'Físico')}</option>
              <option value="Service">{t('items.type.service', 'Servicio')}</option>
              <option value="Digital">{t('items.type.digital', 'Digital')}</option>
              <option value="Kit">{t('items.type.kit', 'Kit')}</option>
              <option value="Bundle">{t('items.type.bundle', 'Bundle')}</option>
            </select>
          </ZHField>
          <ZHField label={t('items.form.uom', 'UOM base *')} required fieldError={fe(errors.defaultUomCode?.message)}>
            <select {...register('defaultUomCode')} disabled={disabled}>
              <option value="">{t('common.selectOption', '— Seleccionar —')}</option>
              {sriUomOptions.map(u => (
                <option key={u.code} value={u.code}>{u.code} — {u.name}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('items.form.brand', 'Marca')}>
            <select {...register('brandId')} disabled={disabled}>
              <option value="">{t('common.none', 'Sin marca')}</option>
              {brandOptions.map(b => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('items.form.category', 'Categoría')}>
            <select {...register('categoryNodeId')} disabled={disabled}>
              <option value="">{t('common.none', 'Sin categoría')}</option>
              {categoryOptions.map(c => (
                <option key={c.id} value={c.id}>
                  {'  '.repeat(c.depth)}{c.name}
                </option>
              ))}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>
    </>
  );
}
