namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>
/// Modo de soporte de borrador de un <see cref="ERP.Domain.Modules.DocTypes.Entities.DocType"/>
/// para una company, definido por <see cref="ERP.Domain.Modules.DocTypes.Entities.DocWorkflowPolicy"/>.
/// </summary>
public enum DraftMode
{
    /// <summary>El documento no admite borrador — siempre se crea confirmado.</summary>
    Disabled = 0,

    /// <summary>El documento puede crearse como borrador o confirmado, a elección del usuario.</summary>
    Optional = 1,

    /// <summary>El documento siempre debe pasar por borrador antes de confirmarse.</summary>
    Required = 2,
}
