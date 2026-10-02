/**
 * purchaseRetentionFacade — contrato público del módulo Retentions para la retención de una compra
 * (consumidor: purchases, RetentionSection de PurchasesPage).
 *
 * ZH-PURCHASE-RETENTION-CONFIRM-01: la retención de una compra se EMITE solo dentro de su
 * confirmación (`RetentionIntentRequest` en POST /purchases/{id}/confirm); no existe una emisión
 * posterior. Operaciones sobre la retención ya emitida (casos de uso backend de Retentions):
 *  - lectura:   getForPurchase (GET /purchases/{id}/retention), getElectronicXmlBlob y
 *               getRidePdfBlob (on-demand, sin persistir).
 * ZH-RETENTION-CANCELLATION-LIFECYCLE-01: no hay anulación aislada — la retención se anula solo
 * anulando la compra (cascada en el backend) y una retención anulada es terminal.
 * ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A: la transmisión electrónica (firma + SRI) es automática al
 * confirmar la compra — no hay acción manual; el estado se muestra con RetentionElectronicStatusBadge.
 * Los módulos externos deben importar desde aquí, nunca directamente de
 * retentions/api/retentionsService.
 */
import { retentionsService } from "../api/retentionsService";
import type {
  IssueRetentionLineRequest,
  RetentionDocumentDto,
  RetentionElectronicStatus,
  RetentionIntentRequest,
} from "../api/retentionsService";

export type {
  IssueRetentionLineRequest,
  RetentionDocumentDto,
  RetentionElectronicStatus,
  RetentionIntentRequest,
};
export { RetentionElectronicStatusBadge } from "../components/RetentionElectronicStatusBadge";

export const purchaseRetentionFacade = {
  getForPurchase: retentionsService.getForPurchase,
  getElectronicXmlBlob: retentionsService.getElectronicXmlBlob,
  getRidePdfBlob: retentionsService.getRidePdfBlob,
};
