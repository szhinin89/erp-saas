import type { UseFormReturn } from 'react-hook-form';
import { ZHField, ZHFormSection, ZHGrid } from '../../../../components/zh/ZHForm';
import type { CreateItemFormValues } from '../../schemas/createItemSchema';

type Props = {
  form: UseFormReturn<CreateItemFormValues>;
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
  vatRateOptions: { code: string; name: string; rate: number }[];
  iceRateOptions: { code: string; name: string }[];
};

export function TaxConfigTab({ form, t, disabled, vatRateOptions, iceRateOptions }: Props) {
  const { register, watch, formState: { errors } } = form;
  const tax = watch('taxConfig');
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);

  return (
    <>
      {/* VAT on Sale */}
      <ZHFormSection title={t('items.tax.saleVat', 'IVA en Venta')}>
        <ZHGrid cols={1}>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('taxConfig.appliesVatOnSale')} disabled={disabled} />
              {t('items.tax.appliesVatOnSale', 'Aplica IVA en venta')}
            </label>
          </ZHField>
        </ZHGrid>
        {tax?.appliesVatOnSale && (
          <ZHGrid cols={2}>
            <ZHField label={t('items.tax.saleVatCode', 'Tarifa IVA venta *')} required fieldError={fe(errors.taxConfig?.saleVatCode?.message)}>
              <select {...register('taxConfig.saleVatCode')} disabled={disabled}>
                <option value="">{t('common.selectOption', '— Seleccionar —')}</option>
                {vatRateOptions.map(v => (
                  <option key={v.code} value={v.code}>{v.code} — {v.name} ({v.rate}%)</option>
                ))}
              </select>
            </ZHField>
            <ZHField label={t('items.tax.vatAccount', 'Cuenta contable IVA venta')}>
              <input {...register('taxConfig.vatAccountId')} placeholder="UUID cuenta" disabled={disabled} />
            </ZHField>
          </ZHGrid>
        )}
      </ZHFormSection>

      {/* VAT on Purchase */}
      <ZHFormSection title={t('items.tax.purchaseVat', 'IVA en Compra')}>
        <ZHGrid cols={1}>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('taxConfig.appliesVatOnPurchase')} disabled={disabled} />
              {t('items.tax.appliesVatOnPurchase', 'Aplica IVA en compra')}
            </label>
          </ZHField>
        </ZHGrid>
        {tax?.appliesVatOnPurchase && (
          <ZHGrid cols={2}>
            <ZHField label={t('items.tax.purchaseVatCode', 'Tarifa IVA compra *')} required fieldError={fe(errors.taxConfig?.purchaseVatCode?.message)}>
              <select {...register('taxConfig.purchaseVatCode')} disabled={disabled}>
                <option value="">{t('common.selectOption', '— Seleccionar —')}</option>
                {vatRateOptions.map(v => (
                  <option key={v.code} value={v.code}>{v.code} — {v.name} ({v.rate}%)</option>
                ))}
              </select>
            </ZHField>
          </ZHGrid>
        )}
      </ZHFormSection>

      {/* ICE / Excise */}
      <ZHFormSection title={t('items.tax.excise', 'Impuesto ICE')}>
        <ZHGrid cols={1}>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('taxConfig.appliesExciseTax')} disabled={disabled} />
              {t('items.tax.appliesExcise', 'Aplica impuesto ICE')}
            </label>
          </ZHField>
        </ZHGrid>
        {tax?.appliesExciseTax && (
          <ZHGrid cols={2}>
            <ZHField label={t('items.tax.exciseTaxCode', 'Código ICE *')} required fieldError={fe(errors.taxConfig?.exciseTaxCode?.message)}>
              <select {...register('taxConfig.exciseTaxCode')} disabled={disabled}>
                <option value="">{t('common.selectOption', '— Seleccionar —')}</option>
                {iceRateOptions.map(v => (
                  <option key={v.code} value={v.code}>{v.code} — {v.name}</option>
                ))}
              </select>
            </ZHField>
          </ZHGrid>
        )}
      </ZHFormSection>

      {/* SRI Service code */}
      <ZHFormSection title={t('items.tax.sri', 'SRI')}>
        <ZHGrid cols={2}>
          <ZHField label={t('items.tax.sriServiceCode', 'Código tipo servicio SRI')}
            hint={t('items.tax.sriHint', 'Solo para servicios: 01=Servicios, 02=Arrendamiento, etc.')}>
            <input {...register('taxConfig.sriServiceCode')} placeholder="01" maxLength={5} disabled={disabled} />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>
    </>
  );
}
