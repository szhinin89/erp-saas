using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;

/// <summary>
/// Consulta el XML autorizado en el SRI para un <c>PurchaseReceptionDocument</c> ya persistido
/// (Fase 2) y lo almacena. Solo opera sobre documentos en estado Imported — si la consulta falla,
/// el documento permanece en su estado anterior (no se pierde progreso ni se corrompe el registro).
/// </summary>
public sealed record DownloadPurchaseReceptionXmlCommand(Guid PurchaseReceptionDocumentId)
    : IRequest<Result<DownloadPurchaseReceptionXmlResultDto>>, IBranchScopedRequest;
