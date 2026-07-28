import { useFormContext } from 'react-hook-form';
import { ZHField, ZHFormActions, ZHFormAlert, ZHGrid } from '../../zh/ZHForm';
import { useAsync } from '../../../hooks/useAsync';
import { apiGet } from '../../../modules/lib/apiEnvelope';
import { applyServerErrors } from '../../../modules/lib/validationErrors';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import { useItemTypeOptions } from '../../../modules/items/hooks/useItemTypeOptions';
import { itemService } from '../../../modules/items/api/itemService';
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
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useFormContext<CreateItemModalFormValues>();

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
        baseSalePrice: null,
        supplierCodes: initialData?.supplierId && initialData?.supplierCode
          ? [{ supplierId: initialData.supplierId, code: initialData.supplierCode, isPrimary: true }]
          : undefined,
      });
      onCreated({ id: item.id, sku: item.sku, shortName: item.shortName });
    } catch (err) {
      applyServerErrors(err, setError, () => {
        setError('root', { type: 'server', message: formatApiRequestError(err, { generic: 'No se pudo crear el ítem.' }) });
      });
    }
  });

  const rootError = (errors.root as { message?: string } | undefined)?.message;

  return (
    <form onSubmit={onSubmit}>
      {rootError && <ZHFormAlert type="error" message={rootError} />}

      {(initialData?.supplierName || initialData?.supplierCode) && (
        <ZHGrid cols={2}>
          {initialData.supplierName && (
            <ZHField label="Proveedor" readOnly>
              <input value={initialData.supplierName} disabled />
            </ZHField>
          )}
          {initialData.supplierCode && (
            <ZHField label="Código proveedor" readOnly>
              <input value={initialData.supplierCode} disabled />
            </ZHField>
          )}
        </ZHGrid>
      )}

      <ZHField label="SKU" required fieldError={errors.sku?.message}>
        <input {...register('sku')} />
      </ZHField>

      <ZHGrid cols={2}>
        <ZHField label="Nombre corto" required fieldError={errors.shortName?.message}>
          <input {...register('shortName')} />
        </ZHField>
        <ZHField label="Tipo de ítem" required fieldError={errors.itemTypeId?.message}>
          <select {...register('itemTypeId')}>
            <option value="">Seleccione...</option>
            {itemTypeOptions.map(it => <option key={it.id} value={it.id}>{it.name}</option>)}
          </select>
        </ZHField>
      </ZHGrid>

      <ZHField label="Descripción" required fieldError={errors.description?.message}>
        <textarea {...register('description')} rows={2} />
      </ZHField>

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

      <ZHField label="Unidad de medida" required fieldError={errors.defaultUomCode?.message}>
        <select {...register('defaultUomCode')}>
          <option value="">Seleccione...</option>
          {uomOptions.map(u => <option key={u.code} value={u.code}>{u.name}</option>)}
        </select>
      </ZHField>

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

      <ZHFormActions onCancel={onClose} hideDraft saveButtonType="submit" disableSave={isSubmitting}
        labels={{ save: isSubmitting ? 'Creando...' : 'Crear Producto' }} />
    </form>
  );
}
