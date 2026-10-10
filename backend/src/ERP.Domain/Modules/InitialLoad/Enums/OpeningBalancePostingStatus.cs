namespace ERP.Domain.Modules.InitialLoad.Enums;

/// <summary>
/// IL-7A — estado contable de la apertura de un lote. Independiente de <see cref="ImportStatus"/>:
/// un fallo contable nunca revierte la carga operativa ya confirmada.
/// </summary>
public enum OpeningBalancePostingStatus
{
    Pending = 1,
    Posted = 2,
    Failed = 3,
}
