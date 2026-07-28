import { useEffect } from 'react';
import { useForm, FormProvider, useFormContext } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ZHModal } from '../../../components/zh/ZHModal';
import { ZHField, ZHFormActions, ZHFormAlert, ZHGrid } from '../../../components/zh/ZHForm';
import { useAsync } from '../../../hooks/useAsync';
import { apiGet } from '../../lib/apiEnvelope';
import { applyServerErrors } from '../../lib/validationErrors';
import { formatApiRequestError } from '../../lib/apiError';
import { formatMoneyWithSymbol } from '../../../lib/sanitizers';
import { useItemTypeOptions } from '../../items/hooks/useItemTypeOptions';
import { purchaseReceptionService, type PurchaseReceptionLineMatch } from '../api/purchaseReceptionService';
import { createItemFromLineSchema, type CreateItemFromLineFormValues } from '../schemas/createItemFromLineSchema';

type Props = {
  open: boolean;
  line: PurchaseReceptionLineMatch | null;
  supplierName: string;
  onClose: () => void;
  onCreated: () => void;
};

// CONTRACT: mismos catálogos ya consumidos por ItemFormTabs.tsx (formulario completo de Items) —
// no se crea un segundo contrato, solo se reutilizan los GET ya existentes.
interface BrandOption { id: string; name: string; }
interface CategoryNodeApi { id: string; name: string; path: string; parentId: string | null; isActive: boolean; }
interface UomOption { code: string; name: string; abbrev: string | null; }
interface BarcodeTypeOption { code: string; name: string; }

function buildDefaults(line: PurchaseReceptionLineMatch | null): CreateItemFromLineFormValues {
  const code = (line?.supplierAuxCode ?? line?.supplierCode ?? '').trim().toUpperCase();
  const description = line?.description ?? '';
  return {
    sku: code,
    shortName: description.slice(0, 50),
    description: description.slice(0, 254),
    itemTypeId: '',
    categoryNodeId: '',
    brandId: '',
    defaultUomCode: '',
    barcodeType: '',
  };
}

export function CreateItemFromLineModal({ open, line, supplierName, onClose, onCreated }: Props) {
  const form = useForm<CreateItemFromLineFormValues>({
    resolver: zodResolver(createItemFromLineSchema),
    defaultValues: buildDefaults(null),
  });

  useEffect(() => {
    if (open) form.reset(buildDefaults(line));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, line]);

  return (
    <ZHModal open={open} onClose={onClose} size="md"
      title="Crear producto desde recepción"
      subtitle="El producto se crea en el catálogo de Items y queda vinculado automáticamente a esta línea."
    >
      {line && (
        <FormProvider {...form}>
          <CreateItemFromLineForm line={line} supplierName={supplierName} onClose={onClose} onCreated={onCreated} />
        </FormProvider>
      )}
    </ZHModal>
  );
}

function CreateItemFromLineForm({ line, supplierName, onClose, onCreated }: {
  line: PurchaseReceptionLineMatch; supplierName: string; onClose: () => void; onCreated: () => void;
}) {
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useFormContext<CreateItemFromLineFormValues>();

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
      await purchaseReceptionService.createItemFromLine(line.lineId, values);
      onCreated();
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

      <ZHGrid cols={2}>
        <ZHField label="Proveedor" readOnly>
          <input value={supplierName} disabled />
        </ZHField>
        <ZHField label="Costo de referencia" readOnly hint="Informativo — no se guarda en el ítem. El costo real se calculará al registrar la compra.">
          <input value={formatMoneyWithSymbol(line.unitPrice)} disabled />
        </ZHField>
      </ZHGrid>

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

      <ZHGrid cols={2}>
        <ZHField label="Unidad de medida" required fieldError={errors.defaultUomCode?.message}>
          <select {...register('defaultUomCode')}>
            <option value="">Seleccione...</option>
            {uomOptions.map(u => <option key={u.code} value={u.code}>{u.name}</option>)}
          </select>
        </ZHField>
        <ZHField label="Tipo de código de barras" required fieldError={errors.barcodeType?.message}
          hint={`Código: ${line.supplierAuxCode ?? line.supplierCode ?? '—'}`}>
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
