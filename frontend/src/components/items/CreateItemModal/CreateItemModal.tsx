import { useEffect } from 'react';
import { useForm, FormProvider } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ZHModal } from '../../zh/ZHModal';
import { CreateItemForm } from './CreateItemForm';
import { createItemModalSchema, type CreateItemModalFormValues } from './createItemSchema';
import type { CreateItemInitialData, CreateItemModalProps } from './types';

function buildDefaults(initialData?: CreateItemInitialData): CreateItemModalFormValues {
  const name = initialData?.name ?? '';
  return {
    sku: (initialData?.supplierCode ?? initialData?.barcode ?? '').trim().toUpperCase(),
    shortName: name.slice(0, 50),
    description: name.slice(0, 254),
    itemTypeId: '',
    categoryNodeId: '',
    brandId: '',
    defaultUomCode: initialData?.defaultUomCode ?? '',
    barcode: initialData?.barcode ?? '',
    barcodeType: '',
    salePrice: null,
    updatePrice: false,
  };
}

/**
 * Creación de Item reutilizable — no sabe de dónde viene la invocación (Compras, Inventario,
 * Ventas, Importaciones...). Depende únicamente del contrato de creación de Items
 * (`itemService.create`, `POST /api/v1/items`); no importa nada de `modules/purchases`.
 */
export function CreateItemModal({ open, initialData, onClose, onCreated }: CreateItemModalProps) {
  const form = useForm<CreateItemModalFormValues>({
    resolver: zodResolver(createItemModalSchema),
    defaultValues: buildDefaults(initialData),
  });

  useEffect(() => {
    if (open) form.reset(buildDefaults(initialData));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, initialData]);

  return (
    <ZHModal open={open} onClose={onClose} size="lg"
      title="Crear producto"
      subtitle="El producto se crea en el catálogo de Items."
    >
      <FormProvider {...form}>
        <CreateItemForm initialData={initialData} onClose={onClose} onCreated={onCreated} />
      </FormProvider>
    </ZHModal>
  );
}
