import type { UseFormReturn } from 'react-hook-form';
import { ZHField, ZHFormSection, ZHGrid } from '../../../../components/zh/ZHForm';
import type { CreateItemFormValues } from '../../schemas/createItemSchema';

type Props = {
  form: UseFormReturn<CreateItemFormValues>;
  t: (key: string, fallback?: string) => string;
  disabled: boolean;
};

export function StockSaleTab({ form, t, disabled }: Props) {
  const { register, formState: { errors } } = form;
  const fe = (msg?: string) => (msg ? t(msg, msg) : null);

  return (
    <>
      {/* Stock config */}
      <ZHFormSection title={t('items.stock.title', 'Configuración de inventario')}>
        <ZHGrid cols={3}>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('stockConfig.tracksStock')} disabled={disabled} />
              {t('items.stock.tracksStock', 'Maneja stock')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('stockConfig.tracksLot')} disabled={disabled} />
              {t('items.stock.tracksLot', 'Rastreo por lote')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('stockConfig.tracksSeries')} disabled={disabled} />
              {t('items.stock.tracksSeries', 'Rastreo por serie')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('stockConfig.allowDecimalQty')} disabled={disabled} />
              {t('items.stock.allowDecimalQty', 'Cantidades decimales')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('stockConfig.allowDecimalSale')} disabled={disabled} />
              {t('items.stock.allowDecimalSale', 'Venta en decimales')}
            </label>
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField label={t('items.stock.minStockQty', 'Stock mínimo')} fieldError={fe(errors.stockConfig?.minStockQty?.message)}>
            <input type="number" step="0.001" min="0" {...register('stockConfig.minStockQty', { valueAsNumber: true, setValueAs: v => v === '' ? null : Number(v) })} disabled={disabled} />
          </ZHField>
          <ZHField label={t('items.stock.maxStockQty', 'Stock máximo')} fieldError={fe(errors.stockConfig?.maxStockQty?.message)}>
            <input type="number" step="0.001" min="0" {...register('stockConfig.maxStockQty', { valueAsNumber: true, setValueAs: v => v === '' ? null : Number(v) })} disabled={disabled} />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      {/* Sale config */}
      <ZHFormSection title={t('items.sale.title', 'Configuración de venta')}>
        <ZHGrid cols={3}>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('saleConfig.isForSale')} disabled={disabled} />
              {t('items.sale.isForSale', 'Disponible para venta')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('saleConfig.isAvailableOnPOS')} disabled={disabled} />
              {t('items.sale.isAvailableOnPOS', 'Disponible en POS')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('saleConfig.isAvailableOnWeb')} disabled={disabled} />
              {t('items.sale.isAvailableOnWeb', 'Disponible en web')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('saleConfig.isAvailableOnMobile')} disabled={disabled} />
              {t('items.sale.isAvailableOnMobile', 'Disponible en móvil')}
            </label>
          </ZHField>
          <ZHField label="">
            <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <input type="checkbox" {...register('saleConfig.isEcommerceActive')} disabled={disabled} />
              {t('items.sale.isEcommerceActive', 'eCommerce activo')}
            </label>
          </ZHField>
        </ZHGrid>
        <ZHGrid cols={2}>
          <ZHField label={t('items.sale.maxDiscount', 'Descuento máximo (%)')} fieldError={fe(errors.saleConfig?.maxDiscountPercent?.message)}>
            <input type="number" step="0.01" min="0" max="100" {...register('saleConfig.maxDiscountPercent', { valueAsNumber: true, setValueAs: v => v === '' ? null : Number(v) })} disabled={disabled} />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>
    </>
  );
}
