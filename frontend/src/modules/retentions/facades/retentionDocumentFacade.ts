/**
 * retentionDocumentFacade — contrato público de tipos del documento de retención (comprobante 07)
 * para módulos que lo muestran sin gestionar su ciclo de vida (expenses: retención emitida al
 * confirmar un gasto). Declaración canónica en retentions/api/retentionsService (espejo de
 * ERP.Application/Modules/Retentions/DTOs/RetentionDocumentDto.cs y UseCases/RetentionIntent.cs);
 * nunca se redeclara en otro módulo. `RetentionIntentRequest` es el contrato único de la intención
 * de retener al confirmar (Gastos y Compras). El ciclo de vida de la retención de una compra vive en
 * purchaseRetentionFacade.
 */
export type {
  IssueRetentionLineRequest,
  RetentionDocumentDto,
  RetentionAnnulmentRequestDto,
  RetentionDocumentLineDto,
  RetentionElectronicStatus,
  RetentionIntentRequest,
  RetentionStatus,
  RetentionTaxType,
} from "../api/retentionsService";
export { RetentionElectronicStatusBadge } from "../components/RetentionElectronicStatusBadge";
export { SRI_ANNULMENT_REQUIRED_CODE } from "../api/retentionsService";
export { RetentionAnnulmentPanel } from "../components/RetentionAnnulmentPanel";
