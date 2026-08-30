import { apiGet, apiPut } from "../../../lib/apiEnvelope";

export type CreationMode = "DraftRequired" | "DirectCreation";
export type ConfirmationMode =
  | "ManualConfirmation"
  | "AutoConfirmOnCreate"
  | "RequiresAuthorization";
export type AuthorizationMode = "None" | "SingleStep" | "MultiStep";
export type PendingDocumentMode =
  | "None"
  | "GenerateOnCreate"
  | "GenerateBeforeConfirmation";
export type CancellationMode =
  | "NotAllowed"
  | "AllowedBeforeConfirmation"
  | "AllowedAfterConfirmationWithReversal";
export type PayableGenerationMode = "None" | "OnConfirmation" | "OnAuthorization";
export type AccountingPostingMode = "None" | "OnConfirmation" | "OnAuthorization";
export type InventoryImpactMode = "None" | "OnConfirmation" | "OnAuthorization";
export type NotificationMode =
  | "None"
  | "OnPendingAuthorization"
  | "OnConfirmation"
  | "OnCancellation";

export type DocumentFlowPolicyDto = {
  id: string;
  documentTypeCode: string;
  documentTypeName: string;
  isActive: boolean;
  creationMode: CreationMode;
  confirmationMode: ConfirmationMode;
  authorizationMode: AuthorizationMode;
  pendingDocumentMode: PendingDocumentMode;
  cancellationMode: CancellationMode;
  requiresCancellationReason: boolean;
  requiresAttachment: boolean;
  requiresSupplier: boolean;
  requiresDueDate: boolean;
  payableGenerationMode: PayableGenerationMode;
  accountingPostingMode: AccountingPostingMode;
  inventoryImpactMode: InventoryImpactMode;
  notificationMode: NotificationMode;
};

export type UpdateDocumentFlowPolicyPayload = Omit<
  DocumentFlowPolicyDto,
  "documentTypeCode" | "documentTypeName"
>;

const BASE = "/api/v1/settings/document-flows";

export const documentFlowPolicyService = {
  list: () => apiGet<DocumentFlowPolicyDto[]>(BASE),

  getById: (id: string) => apiGet<DocumentFlowPolicyDto>(`${BASE}/${id}`),

  update: (id: string, body: UpdateDocumentFlowPolicyPayload) =>
    apiPut<DocumentFlowPolicyDto>(`${BASE}/${id}`, body),
};
