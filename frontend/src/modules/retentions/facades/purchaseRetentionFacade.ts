/**
 * purchaseRetentionFacade — contrato público del módulo Retentions para la retención de una compra
 * (consumidor: purchases, RetentionSection de PurchasesPage).
 *
 * ZH-PURCHASE-RETENTION-CONFIRM-01: la retención de una compra se EMITE solo dentro de su
 * confirmación (`RetentionIntentRequest` en POST /purchases/{id}/confirm); no existe una emisión
 * posterior. Operaciones sobre la retención ya emitida (casos de uso backend de Retentions):
 *  - lectura:   getForPurchase (GET /purchases/{id}/retention), getElectronicXmlBlob y
 *               getRidePdfBlob (on-demand, sin persistir).
 *  - anular:    cancelForPurchase (POST /purchases/{id}/retention/{retentionId}/cancel — reversa
 *               CxP + asiento).
 *  - registrar: registerElectronic (POST /retentions/{id}/electronic/register — firma + SRI, manual).
 * Los módulos externos deben importar desde aquí, nunca directamente de
 * retentions/api/retentionsService.
 */
import { retentionsService } from "../api/retentionsService";
import type {
  IssueRetentionLineRequest,
  RetentionDocumentDto,
  RetentionIntentRequest,
} from "../api/retentionsService";

export type { IssueRetentionLineRequest, RetentionDocumentDto, RetentionIntentRequest };

export const purchaseRetentionFacade = {
  getForPurchase: retentionsService.getForPurchase,
  cancelForPurchase: retentionsService.cancelForPurchase,
  getElectronicXmlBlob: retentionsService.getElectronicXmlBlob,
  getRidePdfBlob: retentionsService.getRidePdfBlob,
  registerElectronic: retentionsService.registerElectronic,
};
