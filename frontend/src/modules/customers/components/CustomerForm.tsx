import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import { ZHField, ZHFormSection, ZHGrid } from '../../../components/zh/ZHForm';
import { useI18n } from '../../../i18n/i18n';
import type { CustomerFormValues } from '../schemas/customerSchema';

type CustomerFormProps = {
  register: UseFormRegister<CustomerFormValues>;
  errors: FieldErrors<CustomerFormValues>;
  disabled?: boolean;
};

export function CustomerForm({ register, errors, disabled = false }: CustomerFormProps) {
  const { t } = useI18n();

  return (
    <ZHFormSection title={t('customers.title')}>
      <ZHGrid cols={2}>
        <ZHField
          label={t('customers.form.identification')}
          required
          fieldError={errors.identification?.message ? t(String(errors.identification.message)) : null}
          hint={t('customers.form.identificationHelper')}
          hintType="muted"
        >
          <input
            {...register('identification')}
            disabled={disabled}
            placeholder={t('customers.form.identificationPlaceholder')}
            autoComplete="off"
          />
        </ZHField>

        <ZHField
          label={t('customers.form.fullName')}
          required
          fieldError={errors.fullName?.message ? t(String(errors.fullName.message)) : null}
          hint={t('customers.form.fullNameHelper')}
          hintType="muted"
        >
          <input
            {...register('fullName')}
            disabled={disabled}
            placeholder={t('customers.form.fullNamePlaceholder')}
            autoComplete="name"
          />
        </ZHField>

        <ZHField
          label={t('customers.form.email')}
          fieldError={errors.email?.message ? t(String(errors.email.message)) : null}
          hint={t('customers.form.emailHelper')}
          hintType="muted"
        >
          <input
            {...register('email')}
            disabled={disabled}
            placeholder={t('customers.form.emailPlaceholder')}
            autoComplete="email"
          />
        </ZHField>

        <ZHField
          label={t('customers.form.phone')}
          fieldError={errors.phone?.message ? t(String(errors.phone.message)) : null}
          hint={t('customers.form.phoneHelper')}
          hintType="muted"
        >
          <input
            {...register('phone')}
            disabled={disabled}
            placeholder={t('customers.form.phonePlaceholder')}
            autoComplete="tel"
          />
        </ZHField>

        <ZHField
          label={t('customers.form.address')}
          fieldError={errors.address?.message ? t(String(errors.address.message)) : null}
          hint={t('customers.form.addressHelper')}
          hintType="muted"
        >
          <input
            {...register('address')}
            disabled={disabled}
            placeholder={t('customers.form.addressPlaceholder')}
            autoComplete="street-address"
          />
        </ZHField>
      </ZHGrid>
    </ZHFormSection>
  );
}
