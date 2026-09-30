/**
 * retentionDocumentFacade — contrato público de tipos del documento de retención (comprobante 07)
 * para módulos que lo muestran sin gestionar su ciclo de vida (expenses: retención emitida al
 * confirmar un gasto). Declaración canónica en retentions/api/retentionsService (espejo de
 * ERP.Application/Modules/Retentions/DTOs/RetentionDocumentDto.cs); nunca se redeclara en otro
 * módulo. El ciclo de vida de la retención de una compra vive en purchaseRetentionFacade.
 */
export type {
  RetentionDocumentDto,
  RetentionDocumentLineDto,
  RetentionStatus,
  RetentionTaxType,
} from "../api/retentionsService";
