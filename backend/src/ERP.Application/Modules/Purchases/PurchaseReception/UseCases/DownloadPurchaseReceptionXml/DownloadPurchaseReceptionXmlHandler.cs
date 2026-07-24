using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;

public sealed class DownloadPurchaseReceptionXmlHandler
    : IRequestHandler<DownloadPurchaseReceptionXmlCommand, Result<DownloadPurchaseReceptionXmlResultDto>>
{
    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly ISriReceptionXmlProvider _xmlProvider;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;
    private readonly ILogger<DownloadPurchaseReceptionXmlHandler> _logger;

    public DownloadPurchaseReceptionXmlHandler(
        IPurchaseReceptionDocumentRepository documentRepo, ISriReceptionXmlProvider xmlProvider,
        ICurrentTenant tenant, ICurrentCompany company, ICurrentUser user,
        ILogger<DownloadPurchaseReceptionXmlHandler> logger)
    {
        _documentRepo = documentRepo;
        _xmlProvider = xmlProvider;
        _tenant = tenant;
        _company = company;
        _user = user;
        _logger = logger;
    }

    public async Task<Result<DownloadPurchaseReceptionXmlResultDto>> Handle(
        DownloadPurchaseReceptionXmlCommand request, CancellationToken cancellationToken)
    {
        // 1. Obtener documento — GetByIdAsync ya está scoped por Tenant+Company (Branch Ownership),
        //    así que un documento de otra empresa/tenant resuelve como NotFound, nunca se filtra info.
        var document = await _documentRepo.GetByIdAsync(_tenant.TenantId, request.PurchaseReceptionDocumentId, cancellationToken);
        if (document is null)
            return Result<DownloadPurchaseReceptionXmlResultDto>.NotFound("El documento de recepción no existe.");

        // 2. El contexto Company/Tenant/Branch ya lo valida el pipeline (IBranchScopedRequest) y el
        //    scope del repositorio — solo falta la regla de negocio: no reintentar si ya se procesó.
        if (document.Status != PurchaseReceptionDocumentStatus.Imported)
        {
            return Result<DownloadPurchaseReceptionXmlResultDto>.ValidationFailure(
                "Solo se puede consultar el XML de documentos en estado Importado.");
        }

        // 3. Validar que tenga AccessKey (garantizado por el dominio al crear, se valida igual por defensa).
        if (string.IsNullOrWhiteSpace(document.AccessKey))
            return Result<DownloadPurchaseReceptionXmlResultDto>.ValidationFailure("El documento no tiene clave de acceso.");

        // 4. Consultar XML — nunca lanza, siempre un resultado tipado.
        var queryResult = await _xmlProvider.GetAuthorizedXmlAsync(
            _tenant.TenantId, _company.CompanyId, document.AccessKey, cancellationToken);

        if (!queryResult.Authorized
            || string.IsNullOrWhiteSpace(queryResult.XmlContent)
            || string.IsNullOrWhiteSpace(queryResult.AuthorizationNumber)
            || queryResult.AuthorizationDate is null)
        {
            // Si falla: mantener estado anterior — no se toca el documento, solo se registra el error.
            _logger.LogWarning(
                "No se pudo descargar el XML autorizado del documento de recepción {DocumentId}: {Reason}",
                document.Id, queryResult.ErrorMessage ?? "El comprobante no está autorizado en el SRI.");

            return Result<DownloadPurchaseReceptionXmlResultDto>.Failure(
                "No se pudo obtener el XML autorizado del SRI para este comprobante.",
                ApiResponseCodes.Common.SriCommunicationError);
        }

        // 5-6. Guardar XML + actualizar estado (Imported -> Verified), atómico en el dominio.
        document.AttachSriAuthorization(
            queryResult.AuthorizationNumber, queryResult.AuthorizationDate.Value,
            queryResult.XmlContent, DateTime.UtcNow, _user.UserId);
        await _documentRepo.SaveChangesAsync(cancellationToken);

        var dto = new DownloadPurchaseReceptionXmlResultDto(
            document.Id,
            PurchaseReceptionMapper.ToDocumentStatusCode(document.Status),
            XmlDownloaded: true,
            document.AuthorizationNumber,
            document.AuthorizationDate);

        return Result<DownloadPurchaseReceptionXmlResultDto>.Success(dto);
    }
}
