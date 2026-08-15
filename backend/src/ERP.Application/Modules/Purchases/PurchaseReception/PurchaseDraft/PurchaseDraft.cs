using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;

/// <summary>
/// Compra lista para editar en el formulario de Nueva Compra — modelo temporal, nunca persistido.
/// Se arma exclusivamente desde el <see cref="PurchaseReceptionDocument"/> ya verificado: cabecera
/// SRI persistida + líneas ya fusionadas (<see cref="PurchaseDraftMergedLine"/> — ver
/// <see cref="PurchaseDraftLineMerger"/>: datos fiscales/documentales frescos del XML re-parseado
/// cuando aplica, Item Matching siempre tomado de lo persistido). Este modelo nunca persiste nada.
/// </summary>
public sealed record PurchaseDraft(
    Guid? SupplierId,
    string SupplierRuc,
    string SupplierName,
    string? DocTypeCode,
    string InvoiceNumber,
    DateOnly IssueDate,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    string? SriPaymentMethodCode,
    IReadOnlyList<PurchaseDraftMergedLine> Lines,
    PurchaseReceptionProcessingStatus ProcessingStatus,
    string? ProcessingNotes
)
{
    /// <summary>Camino sin fusión: líneas persistidas tal cual, sin re-parsear el XML.</summary>
    public static PurchaseDraft FromReceptionDocument(PurchaseReceptionDocument document) =>
        FromMergedLines(
            document,
            document.Lines.Select(PurchaseDraftMergedLine.FromPersisted).ToList()
        );

    /// <summary>Camino con fusión: <paramref name="lines"/> ya salió de <see cref="PurchaseDraftLineMerger.Merge"/> (o del fallback sin fusión).</summary>
    public static PurchaseDraft FromMergedLines(
        PurchaseReceptionDocument document,
        IReadOnlyList<PurchaseDraftMergedLine> lines
    ) =>
        new(
            SupplierId: document.SupplierId,
            SupplierRuc: document.SupplierRuc,
            SupplierName: document.SupplierName,
            DocTypeCode: document.DocTypeCode,
            InvoiceNumber: document.InvoiceNumber,
            IssueDate: document.IssueDate,
            AccessKey: document.AccessKey,
            AuthorizationNumber: document.AuthorizationNumber,
            AuthorizationDate: document.AuthorizationDate,
            SriPaymentMethodCode: document.SriPaymentMethodCode,
            Lines: lines,
            ProcessingStatus: document.ProcessingStatus,
            ProcessingNotes: document.ProcessingNotes
        );
}
