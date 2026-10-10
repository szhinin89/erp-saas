using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Constants;

/// <summary>
/// IL-7A — hechos contables de la apertura de la Carga Inicial: un asiento por
/// <c>ImportBatch</c> confirmado (<c>SourceEventId</c> = id del lote), resuelto por el Posting
/// Engine con la <c>PostingRule</c> (<see cref="SourceModule"/>, FactType) de la empresa. Solo los
/// lotes de saldos (inventario, CxC, CxP) tienen hecho contable; maestros y catálogos no.
/// </summary>
public static class OpeningBalancePostingFacts
{
    public const string SourceModule = "InitialLoad";
    public const string OpeningInventory = "OpeningInventory";
    public const string OpeningReceivables = "OpeningReceivables";
    public const string OpeningPayables = "OpeningPayables";

    /// <summary>FactType del lote, o <c>null</c> si ese tipo de carga no genera asiento de apertura.</summary>
    public static string? ForImportType(ImportType importType) =>
        importType switch
        {
            ImportType.InitialStock => OpeningInventory,
            ImportType.InitialReceivables => OpeningReceivables,
            ImportType.InitialPayables => OpeningPayables,
            _ => null,
        };
}
