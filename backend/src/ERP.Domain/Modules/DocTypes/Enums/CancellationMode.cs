namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cómo puede anularse un documento de este tipo.</summary>
public enum CancellationMode
{
    NotAllowed = 0,
    AllowedBeforeConfirmation = 1,
    AllowedAfterConfirmationWithReversal = 2,
}
