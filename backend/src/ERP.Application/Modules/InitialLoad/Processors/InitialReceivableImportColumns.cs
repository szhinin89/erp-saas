namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// Nombres de columna (encabezado, fila 1) de la plantilla Excel de CxC Inicial — SSOT compartida
/// entre <c>InitialReceivableImportProcessor</c> y <c>ClosedXmlInitialReceivableImportSheetReader</c>.
/// IL-5A: una fila = un documento pendiente de un cliente al corte. El cliente se identifica igual
/// que en la Carga Inicial de Clientes (tipo + número SRI); la sucursal es la activa del lote.
/// </summary>
public static class InitialReceivableImportColumns
{
    public const string IdentificationType = PartnerImportColumns.IdentificationType;
    public const string IdentificationNumber = PartnerImportColumns.IdentificationNumber;
    public const string DocumentNumber = "Número Documento";
    public const string IssueDate = "Fecha Emisión";
    public const string DueDate = "Fecha Vencimiento";
    public const string Balance = "Saldo Pendiente";
    public const string Currency = "Moneda";
    public const string CutoffDate = "Fecha de corte";

    public static readonly IReadOnlyList<string> All =
    [
        IdentificationType,
        IdentificationNumber,
        DocumentNumber,
        IssueDate,
        DueDate,
        Balance,
        Currency,
        CutoffDate,
    ];
}
