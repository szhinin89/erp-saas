using ERP.Domain.Modules.DocTypes.Constants;
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

    /// <summary>
    /// IL-8A — FactType del único ASI de apertura de la empresa (<c>SourceEventId</c> = id del
    /// <c>OpeningJournalEntryPosting</c>). Reutiliza el código del catálogo
    /// <see cref="DocTypeCodes.ManualJournalEntry"/> como identificador del hecho; su
    /// <c>PostingRule</c> es habilitadora (0 líneas fijas): todas las líneas viajan como
    /// <c>PostingAllocation</c>.
    /// </summary>
    public const string OpeningJournalEntry = DocTypeCodes.ManualJournalEntry;

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
