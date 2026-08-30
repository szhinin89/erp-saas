namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cuándo se genera el asiento contable de un documento de este tipo.</summary>
public enum AccountingPostingMode
{
    None = 0,
    OnConfirmation = 1,
    OnAuthorization = 2,
}
