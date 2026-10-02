using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Ride.Services;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Query ───────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-ELECTRONIC-ENDPOINTS-03F — genera el PDF RIDE del Comprobante de Retención
/// on-demand para QA/diagnóstico/vista previa: XML (<see cref="IRetentionElectronicDocumentXmlService"/>,
/// 03E) → PDF (<see cref="IRetentionRidePdfService"/>, 03E). Sin lógica propia, sin cache: cada
/// llamada genera XML y PDF de nuevo. No firma, no envía al SRI, no persiste nada.
/// </summary>
public sealed record GenerateRetentionRidePdfQuery(Guid RetentionId) : IRequest<Result<byte[]>>;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class GenerateRetentionRidePdfValidator
    : AbstractValidator<GenerateRetentionRidePdfQuery>
{
    public GenerateRetentionRidePdfValidator()
    {
        RuleFor(x => x.RetentionId).NotEmpty();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class GenerateRetentionRidePdfHandler
    : IRequestHandler<GenerateRetentionRidePdfQuery, Result<byte[]>>
{
    private readonly IRetentionElectronicDocumentXmlService _xmlService;
    private readonly IRetentionRidePdfService _pdfService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly IRetentionSourceAccess _sourceAccess;

    public GenerateRetentionRidePdfHandler(
        IRetentionElectronicDocumentXmlService xmlService,
        IRetentionRidePdfService pdfService,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        IRetentionSourceAccess sourceAccess
    )
    {
        _sourceAccess = sourceAccess;
        _xmlService = xmlService;
        _pdfService = pdfService;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<byte[]>> Handle(
        GenerateRetentionRidePdfQuery request,
        CancellationToken cancellationToken
    )
    {
        // ADR-036 (D-9) — autorización por origen, server-side (Compra → purchases.view; Gasto →
        // expenses.documents.view). Sin acceso = inexistente (fail-closed).
        if (await _sourceAccess.FindViewableAsync(request.RetentionId, cancellationToken) is null)
            return Result<byte[]>.NotFound("La retención no existe.");

        var xmlResult = await _xmlService.GenerateXmlAsync(
            new ElectronicDocumentSourceReference(
                _currentTenant.TenantId,
                _currentCompany.CompanyId,
                request.RetentionId
            ),
            cancellationToken
        );
        if (!xmlResult.IsSuccess)
            return Result<byte[]>.Failure(
                xmlResult.Error ?? "No se pudo generar el XML de la retención.",
                xmlResult.Code
            );

        return await _pdfService.GeneratePdfAsync(
            xmlResult.Value!.Xml,
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            ct: cancellationToken
        );
    }
}
