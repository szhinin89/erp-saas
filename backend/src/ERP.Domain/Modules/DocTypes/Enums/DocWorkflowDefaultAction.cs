namespace ERP.Domain.Modules.DocTypes.Enums;

/// <summary>
/// Acción por defecto sugerida al usuario al crear un documento de un tipo dado, según
/// <see cref="ERP.Domain.Modules.DocTypes.Entities.DocWorkflowPolicy"/>.
/// </summary>
public enum DocWorkflowDefaultAction
{
    Confirm = 0,
    Draft = 1,
}
