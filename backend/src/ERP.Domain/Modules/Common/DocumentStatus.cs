namespace ERP.Domain.Common;

/// <summary>
/// Estados posibles de un documento transaccional.
/// </summary>
public enum DocumentStatus
{
    /// <summary>
    /// Borrador. Creado pero no confirmado. Único estado editable.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Confirmado / Contabilizado. Ya no es editable, solo puede anularse.
    /// </summary>
    Posted = 1,

    /// <summary>
    /// Anulado. Estado final, no reversible.
    /// </summary>
    Voided = 2
}
