namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>Cómo puede nacer un documento de este tipo, según <see cref="ERP.Domain.Modules.DocTypes.Entities.DocumentFlowPolicy"/>.</summary>
public enum CreationMode
{
    /// <summary>El documento siempre debe pasar por borrador antes de confirmarse.</summary>
    DraftRequired = 0,

    /// <summary>El documento se crea directamente confirmado, sin borrador previo.</summary>
    DirectCreation = 1,
}
