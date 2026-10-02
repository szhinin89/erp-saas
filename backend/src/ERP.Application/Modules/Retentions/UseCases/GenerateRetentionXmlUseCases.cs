using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Query ───────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-ELECTRONIC-ENDPOINTS-03F — genera el XML <c>comprobanteRetencion</c> on-demand para
/// QA/diagnóstico/vista previa, delegando íntegramente en
/// <see cref="IRetentionElectronicDocumentXmlService"/> (RETENTIONS-ELECTRONIC-WIRING-03E). Sin
/// lógica propia: no firma, no envía al SRI, no persiste el XML como autorizado — cada llamada
/// vuelve a generar el XML en memoria a partir del estado actual de la retención.
/// </summary>
public sealed record GenerateRetentionXmlQuery(Guid RetentionId)
    : IRequest<Result<ElectronicDocumentXml>>;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class GenerateRetentionXmlValidator : AbstractValidator<GenerateRetentionXmlQuery>
{
    public GenerateRetentionXmlValidator()
    {
        RuleFor(x => x.RetentionId).NotEmpty();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class GenerateRetentionXmlHandler
    : IRequestHandler<GenerateRetentionXmlQuery, Result<ElectronicDocumentXml>>
{
    private readonly IRetentionElectronicDocumentXmlService _xmlService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly IRetentionSourceAccess _sourceAccess;

    public GenerateRetentionXmlHandler(
        IRetentionElectronicDocumentXmlService xmlService,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        IRetentionSourceAccess sourceAccess
    )
    {
        _sourceAccess = sourceAccess;
        _xmlService = xmlService;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<ElectronicDocumentXml>> Handle(
        GenerateRetentionXmlQuery request,
        CancellationToken cancellationToken
    )
    {
        // ADR-036 (D-9) — autorización por origen, server-side (Compra → purchases.view; Gasto →
        // expenses.documents.view). Sin acceso = inexistente (fail-closed).
        if (await _sourceAccess.FindViewableAsync(request.RetentionId, cancellationToken) is null)
            return Result<ElectronicDocumentXml>.NotFound("La retención no existe.");

        return await _xmlService.GenerateXmlAsync(
            new ElectronicDocumentSourceReference(
                _currentTenant.TenantId,
                _currentCompany.CompanyId,
                request.RetentionId
            ),
            cancellationToken
        );
    }
}
