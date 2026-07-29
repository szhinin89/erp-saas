import { ItemEditorModal } from "../../../components/items/ItemEditorModal/ItemEditorModal";
import type {
  CreateItemInitialData,
  ItemCreatedResult,
} from "../../../components/items/ItemEditorModal/types";
import { message } from "../../../lib/messages";
import { logApiDevError } from "../../lib/apiError";
import {
  purchaseReceptionService,
  type PurchaseReceptionLineMatch,
} from "../api/purchaseReceptionService";

type Props = {
  open: boolean;
  line: PurchaseReceptionLineMatch | null;
  supplierName: string;
  onClose: () => void;
  /** La línea actualizada tras crear y vincular el ítem — el llamador la aplica sin recargar la lista. */
  onCreated: (
    line: PurchaseReceptionLineMatch,
    item: ItemCreatedResult,
  ) => void;
  /** El Item se creó pero el auto-vínculo falló — el llamador puede precargarlo como sugerencia para "Vincular Item". */
  onCreatedButNotLinked?: (lineId: string, item: ItemCreatedResult) => void;
  /**
   * Habilita, opcionalmente, la sección "Información de Compra" + simulador de precio/margen del
   * modal genérico (ver `CreateItemModal/types.ts`). `PurchaseReceptionLineMatch` no trae
   * descuento — quien invoca desde una línea con datos más completos (p. ej. `/purchases`) puede
   * pasarlo acá; si se omite, el modal se comporta exactamente igual que antes de esta mejora.
   */
  purchaseContext?: CreateItemInitialData["purchaseContext"];
};

/**
 * Wrapper específico de Purchase Reception sobre el `CreateItemModal` genérico: le pasa los datos
 * de la línea como precarga y, una vez creado el Item, lo vincula a la línea reutilizando el mismo
 * endpoint de vinculación manual (`matchItem`) ya existente — no reimplementa la relación
 * proveedor↔ítem ni la actualización de la línea.
 */
export function CreateItemFromReceptionLineModal({
  open,
  line,
  supplierName,
  onClose,
  onCreated,
  onCreatedButNotLinked,
  purchaseContext,
}: Props) {
  const handleCreated = async (item: ItemCreatedResult) => {
    if (!line) return;
    onClose();
    try {
      const updatedLine = await purchaseReceptionService.matchItem(
        line.lineId,
        item.id,
      );
      message.success("Item creado correctamente.");
      onCreated(updatedLine, item);
    } catch (err) {
      logApiDevError(err);
      onCreatedButNotLinked?.(line.lineId, item);
      message.warning(
        "El Item fue creado correctamente, pero no pudo asociarse automáticamente a esta línea. " +
          'Puede intentar nuevamente desde "Vincular Item".',
      );
    }
  };

  return (
    <ItemEditorModal
      open={open}
      initialData={
        line
          ? {
              name: line.description,
              barcode: line.supplierAuxCode ?? line.supplierCode ?? undefined,
              supplierCode: line.supplierCode ?? undefined,
              supplierName,
              supplierId: line.supplierId ?? undefined,
              source: "PurchaseReception",
              purchaseContext,
            }
          : undefined
      }
      onClose={onClose}
      onSaved={(item) => void handleCreated(item)}
    />
  );
}
