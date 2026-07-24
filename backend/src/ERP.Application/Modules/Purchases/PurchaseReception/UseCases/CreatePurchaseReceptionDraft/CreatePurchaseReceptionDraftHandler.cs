using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;
using DraftModel = ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraft;
using DraftMapper = ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraftMapper;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;

public sealed class CreatePurchaseReceptionDraftHandler
    : IRequestHandler<CreatePurchaseReceptionDraftCommand, Result<PurchaseDraftDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly IPurchaseXmlDraftParser _parser;
    private readonly ICurrentTenant _tenant;

    public CreatePurchaseReceptionDraftHandler(
        IPurchaseReceptionDocumentRepository documentRepo, IPurchaseXmlDraftParser parser, ICurrentTenant tenant)
    {
        _documentRepo = documentRepo;
        _parser = parser;
        _tenant = tenant;
    }

    public async Task<Result<PurchaseDraftDto>> Handle(
        CreatePurchaseReceptionDraftCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepo.GetByIdAsync(_tenant.TenantId, request.PurchaseReceptionDocumentId, cancellationToken);
        if (document is null)
            return Result<PurchaseDraftDto>.NotFound("El documento de recepción no existe.");

        if (document.Status != PurchaseReceptionDocumentStatus.Verified || string.IsNullOrWhiteSpace(document.XmlContent))
        {
            return Result<PurchaseDraftDto>.ValidationFailure(
                "Solo se puede generar un borrador de compra desde documentos con XML autorizado (estado Verificado).");
        }

        var parseResult = _parser.Parse(document.XmlContent);
        if (!parseResult.IsSuccess)
            return Result<PurchaseDraftDto>.ValidationFailure(parseResult.Error!);

        var draft = DraftModel.FromParsedXml(
            parseResult.Value!, document.SupplierId,
            document.AccessKey, document.AuthorizationNumber, document.AuthorizationDate);

        return Result<PurchaseDraftDto>.Success(DraftMapper.ToDto(draft));
    }
}
