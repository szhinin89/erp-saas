using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;

/// <summary>
/// Vista de solo lectura del XML ya guardado en <c>PurchaseReceptionDocument.XmlContent</c>
/// (FLOW-READY-02E.1) — nunca descarga, reprocesa, ni cambia el estado del documento.
/// </summary>
public sealed record GetPurchaseReceptionXmlViewQuery(Guid PurchaseReceptionDocumentId)
    : IRequest<Result<PurchaseReceptionXmlViewDto>>,
        IBranchScopedRequest;
