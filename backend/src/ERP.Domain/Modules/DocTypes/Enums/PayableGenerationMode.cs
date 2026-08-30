namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cuándo se genera la cuenta por pagar (CxP) originada por un documento de este tipo.</summary>
public enum PayableGenerationMode
{
    None = 0,
    OnConfirmation = 1,
    OnAuthorization = 2,
}
