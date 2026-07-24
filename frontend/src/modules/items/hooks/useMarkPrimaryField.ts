import type { UseFormSetValue } from 'react-hook-form';
import type { CreateItemFormValues } from '../schemas/createItemSchema';

/**
 * "Marcar como principal" es la misma operación en barcodes y supplierCodes:
 * desmarca todas las filas del array y marca solo la elegida. Antes duplicada
 * en BarcodeListEditor.tsx y SupplierCodesSection.tsx.
 */
export function useMarkPrimaryField(
  setValue: UseFormSetValue<CreateItemFormValues>,
  arrayName: 'barcodes' | 'supplierCodes',
  fieldCount: number,
) {
  return (index: number) => {
    for (let i = 0; i < fieldCount; i++) {
      setValue(`${arrayName}.${i}.isPrimary`, i === index, { shouldDirty: true });
    }
  };
}
