using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocument;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentXml;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Infrastructure.Ride.ElectronicDocumentsAdapter;

/// <summary>
/// Único punto de contacto de Ride con ElectronicDocuments (ADR-025 §8). Consume exclusivamente
/// los dos requests públicos de lectura de ese módulo vía <see cref="ISender"/>
/// (<c>GetElectronicDocumentQuery</c>/<c>GetElectronicDocumentXmlQuery</c>) — nunca su entidad,
/// su repositorio ni su <c>ErpDbContext</c>. Traduce <c>ElectronicDocumentType</c> (string en el
/// DTO) a <see cref="RideDocumentType"/> — la única clase de todo el módulo que conoce ambos
/// vocabularios (H5, ADR-025 §8).
/// </summary>
public sealed class ElectronicDocumentRideSourceXmlProvider : IRideSourceXmlProvider
{
    private readonly ISender _sender;

    public ElectronicDocumentRideSourceXmlProvider(ISender sender) => _sender = sender;

    public async Task<Result<RideSourceXmlLookup>> GetAuthorizedXmlAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId, CancellationToken ct = default)
    {
        var documentResult = await _sender.Send(new GetElectronicDocumentQuery(sourceModule, sourceEntityId), ct);
        if (!documentResult.IsSuccess)
            return Result<RideSourceXmlLookup>.Failure(
                documentResult.Error ?? "No se pudo consultar el documento electrónico de origen.");

        var document = documentResult.Value;
        if (document is null)
        {
            // No existe ElectronicDocument para este origen — nunca habrá RIDE (Caso 3/4 del
            // enunciado se generalizan aquí: sin documento, no hay nada que reintentar).
            return Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                RideSourceXmlStatus.NotApplicable, AuthorizedXml: null, ElectronicDocumentId: Guid.Empty, DocumentType: default));
        }

        var documentType = MapDocumentType(document.DocumentType);

        // Rejected/Cancelled: terminal, nunca tendrá RIDE (Casos 3 y 4). Cualquier otro estado
        // que no sea Authorized todavía puede llegar a estarlo (incluye DeadLetter, reversible
        // vía Reactivate en ElectronicDocuments) — se trata como PendingSource, reintentable.
        if (document.CurrentState is "Rejected" or "Cancelled")
        {
            return Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                RideSourceXmlStatus.NotApplicable, AuthorizedXml: null, document.Id, documentType));
        }

        if (document.CurrentState != "Authorized")
        {
            return Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                RideSourceXmlStatus.PendingSource, AuthorizedXml: null, document.Id, documentType));
        }

        var xmlResult = await _sender.Send(
            new GetElectronicDocumentXmlQuery(sourceModule, sourceEntityId, ElectronicDocumentXmlVariant.Authorized), ct);

        if (xmlResult.IsSuccess)
        {
            return Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
                RideSourceXmlStatus.Available, xmlResult.Value, document.Id, documentType));
        }

        // Authorized en ElectronicDocuments pero AuthorizedXmlPath aún no persistido (H1,
        // ADR-025 §3) — nunca un error, reintentable.
        return Result<RideSourceXmlLookup>.Success(new RideSourceXmlLookup(
            RideSourceXmlStatus.PendingSource, AuthorizedXml: null, document.Id, documentType));
    }

    private static RideDocumentType MapDocumentType(string electronicDocumentType) => electronicDocumentType switch
    {
        "Invoice" => RideDocumentType.Invoice,
        "CreditNote" => RideDocumentType.CreditNote,
        "DebitNote" => RideDocumentType.DebitNote,
        "Retention" => RideDocumentType.Retention,
        "ShippingGuide" => RideDocumentType.ShippingGuide,
        "PurchaseSettlement" => RideDocumentType.PurchaseSettlement,
        _ => throw new InvalidOperationException(
            $"Tipo de documento electrónico desconocido para Ride: '{electronicDocumentType}'."),
    };
}
