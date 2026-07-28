import { CreateItemModal } from '../../../components/items/CreateItemModal/CreateItemModal';
import type { ItemCreatedResult } from '../../../components/items/CreateItemModal/types';
import { message } from '../../../lib/messages';
import { purchaseReceptionService, type PurchaseReceptionLineMatch } from '../api/purchaseReceptionService';

type Props = {
  open: boolean;
  line: PurchaseReceptionLineMatch | null;
  supplierName: string;
  onClose: () => void;
  /** La línea actualizada tras crear y vincular el ítem — el llamador la aplica sin recargar la lista. */
  onCreated: (line: PurchaseReceptionLineMatch) => void;
};

/**
 * Wrapper específico de Purchase Reception sobre el `CreateItemModal` genérico: le pasa los datos
 * de la línea como precarga y, una vez creado el Item, lo vincula a la línea reutilizando el mismo
 * endpoint de vinculación manual (`matchItem`) ya existente — no reimplementa la relación
 * proveedor↔ítem ni la actualización de la línea.
 */
export function CreateItemFromReceptionLineModal({ open, line, supplierName, onClose, onCreated }: Props) {
  const handleCreated = async (item: ItemCreatedResult) => {
    if (!line) return;
    onClose();
    try {
      const updatedLine = await purchaseReceptionService.matchItem(line.lineId, item.id);
      message.success('Item creado correctamente.');
      onCreated(updatedLine);
    } catch {
      message.warning('Item creado, pero no se pudo vincular automáticamente a la línea. Use "Vincular" para asociarlo.');
    }
  };

  return (
    <CreateItemModal
      open={open}
      initialData={line ? {
        name: line.description,
        barcode: line.supplierAuxCode ?? line.supplierCode ?? undefined,
        supplierCode: line.supplierCode ?? undefined,
        supplierName,
        supplierId: line.supplierId ?? undefined,
        source: 'PurchaseReception',
      } : undefined}
      onClose={onClose}
      onCreated={(item) => void handleCreated(item)}
    />
  );
}
