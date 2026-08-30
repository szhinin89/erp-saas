namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cómo se confirma un documento de este tipo, según <see cref="ERP.Domain.Modules.DocTypes.Entities.DocumentFlowPolicy"/>.</summary>
public enum ConfirmationMode
{
    /// <summary>Requiere una acción explícita del usuario para confirmar.</summary>
    ManualConfirmation = 0,

    /// <summary>Se confirma automáticamente al crearse.</summary>
    AutoConfirmOnCreate = 1,

    /// <summary>Requiere autorización (ver <see cref="AuthorizationMode"/>) antes de poder confirmarse.</summary>
    RequiresAuthorization = 2,
}
