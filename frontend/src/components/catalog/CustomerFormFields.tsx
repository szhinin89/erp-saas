import type { Control, FieldErrors, UseFormRegister } from 'react-hook-form';
import { Controller } from 'react-hook-form';
import { useI18n } from '../../i18n/i18n';
import { ZHField, ZHFormSection, ZHGrid, ZHToggle } from '../zh/ZHForm';
import { ZHColSpan } from '../zh/ZHLayout';
import { CUSTOMER_ID_TYPES } from '../../modules/catalog/customers/customerFormModel';
import type { CustomerFormValues } from '../../schemas/catalog/customerSchema';

export type CustomerFormFieldsSections = {
  identity?: boolean;
  contact?: boolean;
  status?: boolean;
};

export type CustomerFormFieldsProps = {
  register: UseFormRegister<CustomerFormValues>;
  control: Control<CustomerFormValues>;
  errors: FieldErrors<CustomerFormValues>;
  disabled: boolean;
  /** Por defecto se muestran identidad, contacto y estado (activo). Facturación puede ocultar secciones poco relevantes. */
  sections?: CustomerFormFieldsSections;
};

const defaultSections: Required<CustomerFormFieldsSections> = {
  identity: true,
  contact: true,
  status: true,
};

/**
 * Campos de cliente reutilizables (catálogo, y en el futuro facturación / pedidos).
 * La página contenedora mantiene `useForm` + `customerFormSchema` + llamadas API.
 */
export function CustomerFormFields({ register, control, errors, disabled, sections }: CustomerFormFieldsProps) {
  const { t } = useI18n();
  const s = { ...defaultSections, ...sections };

  return (
    <>
      {s.identity ? (
        <ZHFormSection title={t('customers.section.identity')}>
          <ZHGrid cols={2}>
            <ZHField label={t('customers.form.identificationType')} required fieldError={errors.identificationType?.message}>
              <select disabled={disabled} {...register('identificationType')}>
                {CUSTOMER_ID_TYPES.map((x) => (
                  <option key={x} value={x}>
                    {t(`customers.idType.${x}`)}
                  </option>
                ))}
              </select>
            </ZHField>
            <ZHField label={t('customers.form.identificationNumber')} required fieldError={errors.identificationNumber?.message}>
              <input disabled={disabled} autoComplete="off" {...register('identificationNumber')} />
            </ZHField>
            <ZHColSpan span={2}>
              <ZHField label={t('customers.form.legalName')} required fieldError={errors.legalName?.message}>
                <input disabled={disabled} autoComplete="organization" {...register('legalName')} />
              </ZHField>
            </ZHColSpan>
            <ZHColSpan span={2}>
              <ZHField label={t('customers.form.tradeName')} fieldError={errors.tradeName?.message}>
                <input disabled={disabled} {...register('tradeName')} />
              </ZHField>
            </ZHColSpan>
          </ZHGrid>
        </ZHFormSection>
      ) : null}

      {s.contact ? (
        <ZHFormSection title={t('customers.section.contact')}>
          <ZHGrid cols={2}>
            <ZHColSpan span={2}>
              <ZHField label={t('customers.form.addressLine')} fieldError={errors.addressLine?.message}>
                <input disabled={disabled} autoComplete="street-address" {...register('addressLine')} />
              </ZHField>
            </ZHColSpan>
            <ZHField label={t('customers.form.phone')} fieldError={errors.phone?.message}>
              <input disabled={disabled} autoComplete="tel" {...register('phone')} />
            </ZHField>
            <ZHField label={t('customers.form.email')} fieldError={errors.email?.message}>
              <input disabled={disabled} autoComplete="email" {...register('email')} />
            </ZHField>
            <ZHColSpan span={2}>
              <ZHField label={t('customers.form.notes')} fieldError={errors.notes?.message}>
                <textarea disabled={disabled} rows={3} {...register('notes')} />
              </ZHField>
            </ZHColSpan>
          </ZHGrid>
        </ZHFormSection>
      ) : null}

      {s.status ? (
        <ZHFormSection title={t('common.status')}>
          <ZHGrid cols={1}>
            <Controller
              name="isActive"
              control={control}
              render={({ field }) => (
                <ZHToggle
                  label={t('customers.form.enabled')}
                  description={t('customers.form.enabled')}
                  value={field.value}
                  onChange={field.onChange}
                  disabled={disabled}
                />
              )}
            />
          </ZHGrid>
        </ZHFormSection>
      ) : null}
    </>
  );
}
