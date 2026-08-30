namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cuándo se genera un documento pendiente (a la espera de autorización) para este tipo.</summary>
public enum PendingDocumentMode
{
    None = 0,
    GenerateOnCreate = 1,
    GenerateBeforeConfirmation = 2,
}
