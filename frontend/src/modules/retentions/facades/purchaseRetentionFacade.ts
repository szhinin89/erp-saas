/**
 * purchaseRetentionFacade — contrato público del módulo Retentions para el ciclo de vida de la
 * retención de una compra (consumidor: purchases, RetentionSection de PurchasesPage).
 *
 * Todas las operaciones delegan en retentionsService; los casos de uso backend son de Retentions
 * (IssueRetentionCommand, CancelRetentionCommand, GetRetentionBySourceQuery,
 * GenerateRetentionXmlQuery, GenerateRetentionRidePdfQuery,
 * RegisterRetentionElectronicDocumentCommand). Mutaciones fiscales explícitas:
 *  - lectura:   getForPurchase (GET /purchases/{id}/retention), getElectronicXmlBlob y
 *               getRidePdfBlob (on-demand, sin persistir).
 *  - emitir:    issueForPurchase (POST /purchases/{id}/retention — número vía secuencia server-side).
 *  - anular:    cancelForPurchase (POST /purchases/{id}/retention/{retentionId}/cancel — reversa
 *               CxP + asiento).
 *  - registrar: registerElectronic (POST /retentions/{id}/electronic/register — firma + SRI, manual).
 * Los módulos externos deben importar desde aquí, nunca directamente de
 * retentions/api/retentionsService.
 */
import { retentionsService } from "../api/retentionsService";
import type { IssueRetentionLineRequest, RetentionDocumentDto } from "../api/retentionsService";

export type { IssueRetentionLineRequest, RetentionDocumentDto };

export const purchaseRetentionFacade = {
  getForPurchase: retentionsService.getForPurchase,
  issueForPurchase: retentionsService.issueForPurchase,
  cancelForPurchase: retentionsService.cancelForPurchase,
  getElectronicXmlBlob: retentionsService.getElectronicXmlBlob,
  getRidePdfBlob: retentionsService.getRidePdfBlob,
  registerElectronic: retentionsService.registerElectronic,
};
