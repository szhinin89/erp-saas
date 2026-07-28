import { useFormContext } from 'react-hook-form';
import { ZHField, ZHFormActions, ZHFormAlert, ZHFormSection, ZHGrid } from '../../zh/ZHForm';
import { ZhDecimalInput } from '../../zh/inputs/ZhDecimalInput';
import { useAsync } from '../../../hooks/useAsync';
import { apiGet } from '../../../modules/lib/apiEnvelope';
import { applyServerErrors } from '../../../modules/lib/validationErrors';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { useItemTypeOptions } from '../../../modules/items/hooks/useItemTypeOptions';
import { itemService } from '../../../modules/items/api/itemService';
import { getDecimalConfig } from '../../../lib/config/decimal.config';
import { formatMoney, formatMoneyWithSymbol } from '../../../lib/sanitizers';
import { calcMarginAmount, calcMarginPercent } from '../../../lib/margin';
import type { CreateItemModalFormValues } from './createItemSchema';
import type { CreateItemInitialData, ItemCreatedResult } from './types';

// CONTRACT: mismos catálogos ya consumidos por ItemFormTabs.tsx (formulario completo de Items) —
// no se crea un segundo contrato, solo se reutilizan los GET ya existentes.
interface BrandOption { id: string; name: string; }
interface CategoryNodeApi { id: string; name: string; path: string; parentId: string | null; isActive: boolean; }
interface UomOption { code: string; name: string; abbrev: string | null; }
interface BarcodeTypeOption { code: string; name: string; }

type Props = {
  initialData?: CreateItemInitialData;
  onClose: () => void;
  onCreated: (item: ItemCreatedResult) => void;
};

export function CreateItemForm({ initialData, onClose, onCreated }: Props) {
  const { register, handleSubmit, setError, watch, formState: { errors, isSubmitting } } = useFormContext<CreateItemModalFormValues>();

  const itemTypesState = useItemTypeOptions();
  const itemTypeOptions = itemTypesState.data ?? [];

  const brandsState = useAsync(() =>
    apiGet<BrandOption[]>('/api/v1/catalog/brands').catch(() => [] as BrandOption[]));
  const brandOptions = brandsState.data ?? [];

  const categoriesState = useAsync(() =>
    apiGet<{ nodes: CategoryNodeApi[] }>('/api/v1/catalog/category-nodes').catch(() => ({ nodes: [] })));
  const allNodes = categoriesState.data?.nodes ?? [];
  const nodesById = new Map(allNodes.map(n => [n.id, n]));
  const parentIds = new Set(allNodes.filter(n => n.isActive).map(n => n.parentId).filter(Boolean));
  const breadcrumb = (node: CategoryNodeApi) =>
    node.path.split('/').filter(Boolean).map(id => nodesById.get(id)?.name).filter(Boolean).join(' > ') || node.name;
  const categoryOptions = allNodes.filter(n => n.isActive && !parentIds.has(n.id)).map(n => ({ id: n.id, name: breadcrumb(n) }));

  const uomState = useAsync(() => apiGet<UomOption[]>('/api/v1/catalog/sri-uom').catch(() => [] as UomOption[]));
  const uomOptions = uomState.data ?? [];

  const barcodeTypeState = useAsync(() => apiGet<BarcodeTypeOption[]>('/api/v1/catalog/barcode-types').catch(() => [] as BarcodeTypeOption[]));
  const barcodeTypeOptions = barcodeTypeState.data ?? [];

  const onSubmit = handleSubmit(async (values) => {
    try {
      const item = await itemService.create({
        sku: values.sku,
        shortName: values.shortName,
        description: values.description,
        itemTypeId: values.itemTypeId,
        categoryNodeId: values.categoryNodeId,
        brandId: values.brandId,
        defaultUomCode: values.defaultUomCode,
        barcodes: [{ code: values.barcode, barcodeType: values.barcodeType, isPrimary: true }],
        saleVatCode: null,
        purchaseVatCode: null,
        baseSalePrice: values.updatePrice ? values.salePrice ?? null : null,
        supplierCodes: initialData?.supplierId && initialData?.supplierCode
          ? [{ supplierId: initialData.supplierId, code: initialData.supplierCode, isPrimary: true }]
          : undefined,
      });
      onCreated({ id: item.id, sku: item.sku, shortName: item.shortName, baseSalePrice: item.baseSalePrice });
    } catch (err) {
      // applyServerErrors solo mapea 422 con mapa de campos. Cualquier otro caso (400/409 con
      // data.errors plano, p.ej. SKU_DUPLICATE/BARCODE_DUPLICATE, o error inesperado) no dispara
      // su fallback interno — hay que revisar el resultado y formatear el mensaje aquí siempre.
      const handledAsFieldErrors = applyServerErrors(err, setError);
      if (!handledAsFieldErrors) {
        setError('root', { type: 'server', message: formatApiRequestError(err, { generic: 'No se pudo crear el ítem.' }) });
      }
    }
  });

  const rootError = (errors.root as { message?: string } | undefined)?.message;
  const purchaseContext = initialData?.purchaseContext;
  const salePrice = watch('salePrice');
  const updatePrice = watch('updatePrice');

  return (
    <form onSubmit={onSubmit}>
      {rootError && <ZHFormAlert type="error" message={rootError} />}

      <ZHFormSection title="Información del Item" description="Datos del producto a crear en el catálogo.">
        {(initialData?.supplierName || initialData?.supplierCode) && (
          <ZHGrid cols={2}>
            {initialData.supplierName && (
              <ZHField label="Proveedor" readOnly density="compact">
                <input value={initialData.supplierName} disabled />
              </ZHField>
            )}
            {initialData.supplierCode && (
              <ZHField label="Código proveedor" readOnly density="compact">
                <input value={initialData.supplierCode} disabled />
              </ZHField>
            )}
          </ZHGrid>
        )}

        <ZHGrid cols={2}>
          <ZHField label="SKU" required fieldError={errors.sku?.message}>
            <input {...register('sku')} />
          </ZHField>
          <ZHField label="Nombre corto" required fieldError={errors.shortName?.message}>
            <input {...register('shortName')} />
          </ZHField>
        </ZHGrid>

        <ZHField label="Descripción" required fieldError={errors.description?.message}>
          <textarea {...register('description')} rows={2} />
        </ZHField>

        <ZHGrid cols={2}>
          <ZHField label="Tipo de ítem" required fieldError={errors.itemTypeId?.message}>
            <select {...register('itemTypeId')}>
              <option value="">Seleccione...</option>
              {itemTypeOptions.map(it => <option key={it.id} value={it.id}>{it.name}</option>)}
            </select>
          </ZHField>
          <ZHField label="Unidad de medida" required fieldError={errors.defaultUomCode?.message}>
            <select {...register('defaultUomCode')}>
              <option value="">Seleccione...</option>
              {uomOptions.map(u => <option key={u.code} value={u.code}>{u.name}</option>)}
            </select>
          </ZHField>
        </ZHGrid>

        <ZHGrid cols={2}>
          <ZHField label="Categoría" required fieldError={errors.categoryNodeId?.message}>
            <select {...register('categoryNodeId')}>
              <option value="">Seleccione...</option>
              {categoryOptions.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </ZHField>
          <ZHField label="Marca" required fieldError={errors.brandId?.message}>
            <select {...register('brandId')}>
              <option value="">Seleccione...</option>
              {brandOptions.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          </ZHField>
        </ZHGrid>

        <ZHGrid cols={2}>
          <ZHField label="Código de barras" required fieldError={errors.barcode?.message}>
            <input {...register('barcode')} />
          </ZHField>
          <ZHField label="Tipo de código de barras" required fieldError={errors.barcodeType?.message}>
            <select {...register('barcodeType')}>
              <option value="">Seleccione...</option>
              {barcodeTypeOptions.map(t => <option key={t.code} value={t.code}>{t.name}</option>)}
            </select>
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      {purchaseContext && (
        <PurchasePriceSimulation
          purchaseContext={purchaseContext}
          salePrice={salePrice ?? null}
          updatePrice={!!updatePrice}
          register={register}
          fieldError={errors.salePrice?.message}
        />
      )}

      <ZHFormActions onCancel={onClose} hideDraft saveButtonType="submit" disableSave={isSubmitting}
        labels={{ save: isSubmitting ? 'Creando...' : 'Crear Producto' }} />
    </form>
  );
}

/**
 * Sección opcional "Información de Compra" + simulador de precio/margen — solo se monta cuando
 * quien invoca el modal (hoy, Compras) provee `initialData.purchaseContext`. Componente propio y
 * desacoplado del resto del formulario para poder ampliarlo a futuro (múltiples listas de precio,
 * mayorista/distribuidor, margen objetivo/mínimo, etc.) sin tocar `CreateItemForm`. La fórmula de
 * margen es la misma que usa el resto del ERP — ver `lib/margin.ts`, nunca se reimplementa acá.
 */
function PurchasePriceSimulation({ purchaseContext, salePrice, updatePrice, register, fieldError }: {
  purchaseContext: NonNullable<CreateItemInitialData['purchaseContext']>;
  salePrice: number | null;
  updatePrice: boolean;
  register: ReturnType<typeof useFormContext<CreateItemModalFormValues>>['register'];
  fieldError?: string;
}) {
  const { unitCost, discountPct } = purchaseContext;
  const costFinal = unitCost * (1 - (discountPct ?? 0) / 100);

  return (
    <>
      <PurchaseInfoReadOnly purchaseContext={purchaseContext} costFinal={costFinal} />
      <SalePriceSimulator costFinal={costFinal} salePrice={salePrice} updatePrice={updatePrice}
        register={register} fieldError={fieldError} />
    </>
  );
}

/** Bloque 1 — "¿Qué llegó del proveedor?": solo lectura, nunca editable. */
function PurchaseInfoReadOnly({ purchaseContext, costFinal }: {
  purchaseContext: NonNullable<CreateItemInitialData['purchaseContext']>;
  costFinal: number;
}) {
  const dc = getDecimalConfig();
  const { unitCost, quantity, discountPct } = purchaseContext;
  const hasDiscount = discountPct != null;

  return (
    <ZHFormSection title="Información de Compra" description="Datos de la factura de origen — solo lectura.">
      <ZHGrid cols={2}>
        <ZHField label="Costo unitario" readOnly density="compact">
          <input value={formatMoneyWithSymbol(unitCost, dc.purchaseUnitPrice)} disabled />
        </ZHField>
        <ZHField label="Cantidad" readOnly density="compact">
          <input value={formatMoney(quantity, dc.quantity)} disabled />
        </ZHField>
        <ZHField label="Descuento aplicado" readOnly density="compact">
          <input value={hasDiscount ? `${formatMoney(discountPct ?? 0, dc.percentage)}%` : '—'} disabled />
        </ZHField>
        <ZHField label="Costo final utilizado para la simulación" readOnly density="compact">
          <input value={formatMoneyWithSymbol(costFinal, dc.purchaseUnitPrice)} disabled />
        </ZHField>
      </ZHGrid>
    </ZHFormSection>
  );
}

/**
 * Bloque 2 — "Precio de Venta": único campo editable de esta mejora, con simulación de margen en
 * tiempo real. Modular a propósito (props mínimas: costo + precio) para poder reutilizarse el día
 * que existan múltiples listas/mayorista/distribuidor sin reescribir este componente.
 */
function SalePriceSimulator({ costFinal, salePrice, updatePrice, register, fieldError }: {
  costFinal: number;
  salePrice: number | null;
  updatePrice: boolean;
  register: ReturnType<typeof useFormContext<CreateItemModalFormValues>>['register'];
  fieldError?: string;
}) {
  const dc = getDecimalConfig();
  const hasPrice = salePrice != null && salePrice > 0;
  const salePriceValue = salePrice ?? 0;
  const marginAmount = hasPrice ? calcMarginAmount(costFinal, salePriceValue) : 0;
  const marginPct = hasPrice ? calcMarginPercent(costFinal, salePriceValue) : 0;

  return (
    <ZHFormSection title="Precio de Venta y Rentabilidad" description="Opcional — la simulación es solo una ayuda visual, no requiere guardarse.">
      <ZHGrid cols={2}>
        <ZHField label="Precio sugerido" fieldError={fieldError}>
          <ZhDecimalInput decimals={dc.salesUnitPrice} positiveOnly placeholder="0.00"
            {...register('salePrice', { valueAsNumber: true, setValueAs: v => v === '' ? null : Number(v) })} />
        </ZHField>
        <ZHField label="Actualizar precio del Item">
          <label className="zh-checkbox-label">
            <input type="checkbox" {...register('updatePrice')} />
            <span>Usar este precio como precio inicial del Item</span>
          </label>
        </ZHField>
      </ZHGrid>

      <div className="citm-margin-sim">
        <ZHGrid cols={4}>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Costo</span>
            <span className="citm-margin-sim__value">{formatMoneyWithSymbol(costFinal, dc.purchaseUnitPrice)}</span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Precio</span>
            <span className="citm-margin-sim__value">{hasPrice ? formatMoneyWithSymbol(salePriceValue, dc.salesUnitPrice) : '—'}</span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Ganancia</span>
            <span className={`citm-margin-sim__value ${hasPrice && marginAmount < 0 ? 'citm-margin-sim__value--neg' : ''}`}>
              {hasPrice ? formatMoneyWithSymbol(marginAmount, dc.salesUnitPrice) : '—'}
            </span>
          </div>
          <div className="citm-margin-sim__item">
            <span className="citm-margin-sim__label">Margen</span>
            <span className={`citm-margin-sim__value citm-margin-sim__value--pct ${hasPrice && marginPct < 0 ? 'citm-margin-sim__value--neg' : ''}`}>
              {hasPrice ? `${formatMoney(marginPct, dc.percentage)}%` : '—'}
            </span>
          </div>
        </ZHGrid>
      </div>
      {!updatePrice && hasPrice && (
        <ZHFormAlert type="info" message="El precio no se guardará: active &quot;Actualizar precio del Item&quot; para usarlo como precio inicial." />
      )}
    </ZHFormSection>
  );
}
