namespace ERP.Domain.Modules.Purchases.Enums;

public enum SriDocumentStatus
{
    None = 0,
    PendingSignature = 1,
    Signed = 2,
    PendingSubmission = 3,
    Submitted = 4,
    Received = 5,
    Authorized = 6,
    Rejected = 7,
    Voided = 8,
}
