using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.Mapping;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
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
    private readonly IPurchaseXmlDraftParser _parser;
    private readonly IItemRepository _itemRepo;
    private readonly IItemMatchFinder _matchFinder;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;
    private readonly ILogger<DownloadPurchaseReceptionXmlHandler> _logger;

    public DownloadPurchaseReceptionXmlHandler(
        IPurchaseReceptionDocumentRepository documentRepo, ISriReceptionXmlProvider xmlProvider,
        IPurchaseXmlDraftParser parser, IItemRepository itemRepo, IItemMatchFinder matchFinder,
        ICurrentTenant tenant, ICurrentCompany company, ICurrentUser user,
        ILogger<DownloadPurchaseReceptionXmlHandler> logger)
    {
        _documentRepo = documentRepo;
        _xmlProvider = xmlProvider;
        _parser = parser;
        _itemRepo = itemRepo;
        _matchFinder = matchFinder;
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

        // 5. Parsear el detalle del comprobante (Item Matching) — si el XML no trae detalles
        //    interpretables, el documento igual se verifica (el XML crudo queda persistido); las
        //    líneas simplemente quedan vacías, sin bloquear la verificación del comprobante.
        var lines = new List<PurchaseReceptionLine>();
        var parseResult = _parser.Parse(queryResult.XmlContent);
        if (parseResult.IsSuccess)
        {
            foreach (var parsedLine in parseResult.Value!.Lines)
            {
                Guid? itemId = null;
                var matchStatus = ItemMatchStatus.Pending;

                // Resolución automática — solo por código de proveedor exacto (sin intervención del usuario).
                if (document.SupplierId is { } supplierId && !string.IsNullOrWhiteSpace(parsedLine.SupplierCode))
                {
                    itemId = await _itemRepo.FindItemIdBySupplierCodeAsync(
                        supplierId, parsedLine.SupplierCode, _tenant.TenantId, cancellationToken);
                    if (itemId is not null)
                        matchStatus = ItemMatchStatus.AutoMatched;
                }

                try
                {
                    var line = PurchaseReceptionLine.Create(
                        document.Id, _tenant.TenantId, parsedLine.Description, parsedLine.Quantity, parsedLine.UnitPrice,
                        parsedLine.SupplierCode, parsedLine.SupplierAuxCode, itemId, matchStatus);

                    // Si no hubo código exacto pero el motor de matching ya encuentra candidatos por
                    // descripción/similitud, la línea queda marcada como "requiere revisión" en vez de
                    // "pendiente" — distingue en la UI qué líneas tienen sugerencias de las que no.
                    if (matchStatus == ItemMatchStatus.Pending)
                    {
                        var suggestions = await _matchFinder.FindCandidatesAsync(
                            _tenant.TenantId, document.SupplierId, parsedLine.SupplierCode, parsedLine.SupplierAuxCode,
                            parsedLine.Description, maxResults: 1, cancellationToken);
                        if (suggestions.Count > 0)
                            line.MarkNeedsReview();
                    }

                    lines.Add(line);
                }
                catch (ArgumentException ex)
                {
                    // Una línea individual inválida (p. ej. cantidad cero por un detalle informativo del
                    // comprobante) no debe bloquear la verificación de todo el documento — se omite y se
                    // registra; el usuario la verá ausente en el panel de conciliación.
                    _logger.LogWarning(ex,
                        "Línea de detalle omitida al verificar el documento de recepción {DocumentId}: {Reason}",
                        document.Id, ex.Message);
                }
            }
        }
        else
        {
            _logger.LogWarning(
                "El XML del documento de recepción {DocumentId} se autorizó pero no se pudo parsear su detalle: {Reason}",
                document.Id, parseResult.Error);
        }

        // 6-7. Guardar XML + líneas + actualizar estado (Imported -> Verified), atómico en el dominio.
        document.AttachSriAuthorization(
            queryResult.AuthorizationNumber, queryResult.AuthorizationDate.Value,
            queryResult.XmlContent, DateTime.UtcNow, lines, _user.UserId);
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
