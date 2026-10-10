namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de CxP Inicial — SSOT compartida
/// entre <c>InitialPayableImportProcessor</c> y <c>ClosedXmlInitialPayableImportSheetReader</c>.
/// IL-6A: una fila = un documento pendiente de un proveedor al corte = una cuota. El proveedor se
/// identifica igual que en la Carga Inicial de Proveedores (tipo + número SRI); la sucursal es la
/// activa del lote.
/// </summary>
public static class InitialPayableImportColumns
{
    public const string IdentificationType = OpeningBalanceImportColumns.IdentificationType;
    public const string IdentificationNumber = OpeningBalanceImportColumns.IdentificationNumber;
    public const string DocumentType = "Tipo Documento";
    public const string DocumentNumber = OpeningBalanceImportColumns.DocumentNumber;
    public const string IssueDate = OpeningBalanceImportColumns.IssueDate;
    public const string DueDate = OpeningBalanceImportColumns.DueDate;
    public const string Balance = OpeningBalanceImportColumns.Balance;
    public const string Currency = OpeningBalanceImportColumns.Currency;
    public const string CutoffDate = OpeningBalanceImportColumns.CutoffDate;

    public static readonly IReadOnlyList<string> All =
    [
        IdentificationType,
        IdentificationNumber,
        DocumentType,
        DocumentNumber,
        IssueDate,
        DueDate,
        Balance,
        Currency,
        CutoffDate,
    ];
}
