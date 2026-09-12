using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;

/// <summary>
/// PURCHASE-RECEPTION-BULK-SRI-XML-DOWNLOAD-01 — descarga en lote el XML autorizado en el SRI
/// para varios <c>PurchaseReceptionDocument</c>. Reutiliza íntegramente
/// <see cref="DownloadPurchaseReceptionXml.DownloadPurchaseReceptionXmlCommand"/> por documento
/// (mismo <see cref="MediatR.IMediator"/>, cero lógica duplicada) — este comando solo orquesta el
/// lote: filtra documentos que ya tienen XML y acota la concurrencia de llamadas al SRI.
/// <see cref="OnlyMissingXml"/> siempre se comporta como <c>true</c> (regla no negociable: nunca
/// se reintenta un documento que ya tiene XML guardado); el campo se acepta explícito en el
/// contrato para que el cliente declare la intención, pero un valor <c>false</c> es rechazado por
/// el validador en vez de ignorarse silenciosamente.
/// </summary>
public sealed record BatchDownloadPurchaseReceptionXmlCommand(
    IReadOnlyList<Guid> DocumentIds,
    bool OnlyMissingXml = true
) : IRequest<Result<BatchDownloadPurchaseReceptionXmlResultDto>>, IBranchScopedRequest;
