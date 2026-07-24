import { useState } from 'react';
import { Controller, useFieldArray, useFormContext } from 'react-hook-form';
import { ZHBtn, ZHField, ZHGrid } from '../../../../components/zh/ZHForm';
import { SupplierPicker } from '../../../purchases/components/SupplierPicker';
import { useMarkPrimaryField } from '../../hooks/useMarkPrimaryField';
import type { CreateItemFormValues } from '../../schemas/createItemSchema';

type Props = {
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
};

export function SupplierCodesSection({ t, disabled }: Props) {
  const { register, control, watch, setValue, formState: { errors } } = useFormContext<CreateItemFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'supplierCodes' });
  const supplierCodes = watch('supplierCodes') ?? [];
  const [open, setOpen] = useState(fields.length > 0);

  const listErrorMessage = errors.supplierCodes?.message;
  const listError = typeof listErrorMessage === 'string' ? listErrorMessage : null;

  const markPrimary = useMarkPrimaryField(setValue, 'supplierCodes', fields.length);

  return (
    <section className="zh-form-section">
      <button
        type="button"
        className={`items-accordion-header${open ? ' items-accordion-header--open' : ''}`}
        aria-expanded={open}
        onClick={() => setOpen(o => !o)}
      >
        <span className="items-accordion-header__title">
          {t('items.supplierCodes.title', 'Códigos de proveedor (opcional)')}
        </span>
        <span className="material-symbols-outlined items-accordion-header__chevron">expand_more</span>
      </button>

      {open && (
        <div className="items-accordion-body">
          <p className="zh-form-section-desc">
            {t('items.supplierCodes.sectionDesc', 'Código con el que cada proveedor identifica este ítem en sus propios documentos. Se usa en Compras para resolver el código correcto según el proveedor de la factura.')}
          </p>
          <p className="zh-field-hint">
            {t('items.supplierCodes.hint', 'No es obligatorio para crear el ítem. Puede agregarse también más adelante desde Compras.')}
          </p>

          {listError && (
            <p className="zh-field-hint zh-field-hint--error">{t(listError, listError)}</p>
          )}

          {fields.map((field, index) => (
            <ZHGrid cols={3} key={field.id}>
              <ZHField
                label={t('items.supplierCodes.supplier', 'Proveedor')}
                required
                fieldError={errors.supplierCodes?.[index]?.supplierId?.message ? t(errors.supplierCodes[index]!.supplierId!.message!, errors.supplierCodes[index]!.supplierId!.message!) : null}
              >
                <Controller
                  control={control}
                  name={`supplierCodes.${index}.supplierId`}
                  render={({ field: rhfField }) => (
                    <SupplierPicker
                      value={rhfField.value || null}
                      onChange={(supplier) => rhfField.onChange(supplier?.id ?? '')}
                      disabled={disabled}
                    />
                  )}
                />
              </ZHField>
              <ZHField
                label={t('items.supplierCodes.code', 'Código')}
                required
                fieldError={errors.supplierCodes?.[index]?.code?.message ? t(errors.supplierCodes[index]!.code!.message!, errors.supplierCodes[index]!.code!.message!) : null}
              >
                <input {...register(`supplierCodes.${index}.code`)} placeholder={t('items.supplierCodes.codePlaceholder', 'PROV-001')} disabled={disabled} />
              </ZHField>
              <ZHField label={t('items.supplierCodes.primary', 'Principal')}>
                <div className="items-row-actions">
                  <ZHBtn
                    type="button"
                    variant={supplierCodes[index]?.isPrimary ? 'primary' : 'ghost'}
                    size="sm"
                    disabled={disabled || !!supplierCodes[index]?.isPrimary}
                    onClick={() => markPrimary(index)}
                  >
                    {supplierCodes[index]?.isPrimary
                      ? t('items.supplierCodes.isPrimary', 'Principal')
                      : t('items.supplierCodes.markPrimary', 'Marcar como principal')}
                  </ZHBtn>
                  <ZHBtn type="button" variant="ghost" size="sm" onClick={() => remove(index)} disabled={disabled}>
                    {t('common.remove', 'Quitar')}
                  </ZHBtn>
                </div>
              </ZHField>
            </ZHGrid>
          ))}

          <ZHBtn
            type="button"
            variant="secondary"
            size="sm"
            disabled={disabled}
            onClick={() => append({ code: '', isPrimary: false, supplierId: '' })}
          >
            {t('items.supplierCodes.add', 'Agregar código de proveedor')}
          </ZHBtn>
        </div>
      )}
    </section>
  );
}
