import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, type Resolver } from 'react-hook-form';
import { ZHBtn, ZHPageNotice } from '../../../../components/zh/ZHForm';
import { useI18n } from '../../../../i18n/i18n';
import { useAsync } from '../../../../hooks/useAsync';
import {
  createItemSchema,
  defaultCreateItemValues,
  type CreateItemFormValues,
} from '../../schemas/createItemSchema';
import { GeneralTab } from './GeneralTab';
import { TaxConfigTab } from './TaxConfigTab';
import { StockSaleTab } from './StockSaleTab';
import type { ItemDto } from '../../../../types/items';

type TabId = 'general' | 'tax' | 'stockSale' | 'variants';

const TABS: { id: TabId; labelKey: string; labelFb: string }[] = [
  { id: 'general',   labelKey: 'items.tabs.general',   labelFb: 'General' },
  { id: 'tax',       labelKey: 'items.tabs.tax',        labelFb: 'Impuestos' },
  { id: 'stockSale', labelKey: 'items.tabs.stockSale',  labelFb: 'Stock / Venta' },
  { id: 'variants',  labelKey: 'items.tabs.variants',   labelFb: 'Variantes' },
];

type Props = {
  submitting: boolean;
  editingItem?: ItemDto | null;
  onSubmit: (values: CreateItemFormValues) => Promise<boolean>;
  onCancel?: () => void;
};

export function ItemFormTabs({ submitting, editingItem, onSubmit, onCancel }: Props) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<TabId>('general');
  const [formError, setFormError] = useState<string | null>(null);
  const isEditMode = !!editingItem;

  // Catalog lookups (lightweight)
  const sriUomState = useAsync(() =>
    fetch('/api/catalog/sri-uom').then(r => r.json()).then(d => d?.responseObject ?? []).catch(() => [])
  );
  const vatRateState = useAsync(() =>
    fetch('/api/catalog/sri-vat-rates').then(r => r.json()).then(d => d?.responseObject ?? []).catch(() => [])
  );
  const brandsState = useAsync(() =>
    fetch('/api/catalog/brands').then(r => r.json()).then(d => d?.responseObject ?? []).catch(() => [])
  );

  const sriUomOptions  = sriUomState.data  ?? [];
  const vatRateOptions = vatRateState.data ?? [];
  const brandOptions   = brandsState.data  ?? [];

  const form = useForm<CreateItemFormValues>({
    resolver: zodResolver(createItemSchema) as Resolver<CreateItemFormValues>,
    defaultValues: editingItem
      ? {
          ...defaultCreateItemValues,
          shortName: editingItem.shortName,
          description: editingItem.description,
          purchaseCode: editingItem.purchaseCode,
          itemType: editingItem.itemType,
          defaultUomCode: editingItem.defaultUomCode,
          categoryNodeId: editingItem.categoryNodeId,
          brandId: editingItem.brandId,
          saleConfig: {
            isForSale: editingItem.isForSale,
            isEcommerceActive: editingItem.isEcommerceActive,
            isAvailableOnWeb: false,
            isAvailableOnPOS: false,
            isAvailableOnMobile: false,
            maxDiscountPercent: null,
          },
          stockConfig: {
            tracksStock: editingItem.tracksStock,
            tracksLot: editingItem.tracksLot,
            tracksSeries: editingItem.tracksSeries,
            allowDecimalQty: false,
            allowDecimalSale: false,
            minStockQty: null,
            maxStockQty: null,
          },
        }
      : defaultCreateItemValues,
  });

  const handleSubmit = async (values: CreateItemFormValues) => {
    setFormError(null);
    const ok = await onSubmit(values);
    if (!ok) setFormError(t('common.saveError', 'Error al guardar. Revisa los datos.'));
  };

  const handleSubmitError = () => {
    // Move to first tab that has errors
    const errorKeys = Object.keys(form.formState.errors);
    if (errorKeys.some(k => ['sku', 'shortName', 'description', 'itemType', 'defaultUomCode'].includes(k))) {
      setActiveTab('general');
    } else if (errorKeys.includes('taxConfig')) {
      setActiveTab('tax');
    } else if (errorKeys.some(k => ['saleConfig', 'stockConfig'].includes(k))) {
      setActiveTab('stockSale');
    }
  };

  const tabs = isEditMode ? TABS.filter(t => t.id !== 'general' || true) : TABS;

  return (
    <div>
      {formError && <ZHPageNotice variant="error" message={formError} style={{ marginBottom: 16 }} />}

      {/* Tab bar */}
      <div className="prd-tabs" role="tablist" style={{ marginBottom: 24 }}>
        {tabs.map(tab => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            className={`prd-tab-btn ${activeTab === tab.id ? 'prd-tab-btn--active' : ''}`}
            onClick={() => setActiveTab(tab.id)}
          >
            {t(tab.labelKey, tab.labelFb)}
          </button>
        ))}
      </div>

      <form onSubmit={form.handleSubmit(handleSubmit, handleSubmitError)}>
        {activeTab === 'general' && (
          <GeneralTab
            form={form}
            t={t}
            disabled={submitting}
            isEditMode={isEditMode}
            sriUomOptions={sriUomOptions}
            categoryOptions={[]}
            brandOptions={brandOptions}
          />
        )}
        {activeTab === 'tax' && (
          <TaxConfigTab
            form={form}
            t={t}
            disabled={submitting}
            vatRateOptions={vatRateOptions}
            iceRateOptions={[]}
          />
        )}
        {activeTab === 'stockSale' && (
          <StockSaleTab form={form} t={t} disabled={submitting} />
        )}
        {activeTab === 'variants' && (
          <div style={{ padding: 24, color: 'var(--color-text-secondary)', textAlign: 'center' }}>
            {isEditMode
              ? t('items.variants.saveFirstHint', 'Las variantes se gestionan desde el detalle del ítem.')
              : t('items.variants.availableAfterCreate', 'Guarda el ítem primero para agregar variantes.')}
          </div>
        )}

        {/* Actions */}
        <div className="zh-form-actions-row zh-form-actions-row--end" style={{ marginTop: 32 }}>
          {onCancel && (
            <ZHBtn type="button" variant="ghost" size="md" onClick={onCancel} disabled={submitting}>
              {t('common.cancel', 'Cancelar')}
            </ZHBtn>
          )}
          <ZHBtn type="submit" variant="primary" size="md" disabled={submitting}>
            {submitting
              ? t('common.saving', 'Guardando...')
              : isEditMode
                ? t('items.form.update', 'Actualizar ítem')
                : t('items.form.create', 'Crear ítem')}
          </ZHBtn>
        </div>
      </form>
    </div>
  );
}
