import { useFieldArray, useFormContext } from 'react-hook-form';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../../../../components/zh/ZHForm';
import { useMarkPrimaryField } from '../../hooks/useMarkPrimaryField';
import type { CreateItemFormValues } from '../../schemas/createItemSchema';

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  barcodeTypeOptions: { code: string; name: string }[];
};

export function BarcodeListEditor({ t, disabled, barcodeTypeOptions }: Props) {
  const { register, control, watch, setValue, formState: { errors } } = useFormContext<CreateItemFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'barcodes' });
  const barcodes = watch('barcodes') ?? [];

  const barcodesError = errors.barcodes;
  const listError = typeof barcodesError?.message === 'string' ? barcodesError.message : null;

  const markPrimary = useMarkPrimaryField(setValue, 'barcodes', fields.length);

  return (
    <ZHFormSection
      title={t('items.barcodes.title', 'Códigos de barras')}
      description={t('items.barcodes.sectionDesc', 'Al menos un código es obligatorio. El código marcado como "Principal" es el que se usa por defecto al escanear o buscar el ítem.')}
    >
      {listError && (
        <p className="zh-field-hint zh-field-hint--error">{t(listError, listError)}</p>
      )}

      {fields.map((field, index) => (
        <ZHGrid cols={3} key={field.id}>
          <ZHField
            label={t('items.barcodes.code', 'Código')}
            required
            fieldError={errors.barcodes?.[index]?.code?.message ? t(errors.barcodes[index]!.code!.message!, errors.barcodes[index]!.code!.message!) : null}
          >
            <input {...register(`barcodes.${index}.code`)} placeholder={t('items.barcodes.codePlaceholder', '7501234567890')} disabled={disabled} />
          </ZHField>
          <ZHField
            label={t('items.barcodes.type', 'Tipo')}
            required
            fieldError={errors.barcodes?.[index]?.barcodeType?.message ? t(errors.barcodes[index]!.barcodeType!.message!, errors.barcodes[index]!.barcodeType!.message!) : null}
          >
            <select {...register(`barcodes.${index}.barcodeType`)} disabled={disabled}>
              <option value="">{t('common.selectOption', '— Seleccionar —')}</option>
              {barcodeTypeOptions.map(bt => (
                <option key={bt.code} value={bt.code}>{bt.name}</option>
              ))}
            </select>
          </ZHField>
          <ZHField label={t('items.barcodes.primary', 'Principal')}>
            <div className="items-row-actions">
              <ZHBtn
                type="button"
                variant={barcodes[index]?.isPrimary ? 'primary' : 'ghost'}
                size="sm"
                disabled={disabled || !!barcodes[index]?.isPrimary}
                onClick={() => markPrimary(index)}
              >
                {barcodes[index]?.isPrimary
                  ? t('items.barcodes.isPrimary', 'Principal')
                  : t('items.barcodes.markPrimary', 'Marcar como principal')}
              </ZHBtn>
              {fields.length > 1 && (
                <ZHBtn type="button" variant="ghost" size="sm" onClick={() => remove(index)} disabled={disabled}>
                  {t('common.remove', 'Quitar')}
                </ZHBtn>
              )}
            </div>
          </ZHField>
        </ZHGrid>
      ))}

      <ZHBtn
        type="button"
        variant="secondary"
        size="sm"
        disabled={disabled}
        onClick={() => append({ code: '', barcodeType: '', isPrimary: fields.length === 0 })}
      >
        {t('items.barcodes.add', 'Agregar código de barras')}
      </ZHBtn>
    </ZHFormSection>
  );
}
