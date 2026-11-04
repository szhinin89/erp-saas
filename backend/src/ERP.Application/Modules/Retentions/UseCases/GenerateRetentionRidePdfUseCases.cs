using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Ride.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Query ───────────────────────────────────────────────────────────────

/// <summary>
/// RIDE de lectura: usa el XML autorizado almacenado y la fecha oficial de ElectronicDocuments.
/// Sin autorización conserva la vista previa. No firma, no envía al SRI ni persiste nada.
/// </summary>
public sealed record GenerateRetentionRidePdfQuery(Guid RetentionId)
    : IRequest<Result<byte[]>>,
        IRequiresCompanyContext;

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
    private readonly IElectronicDocumentInternalReader _reader;

    public GenerateRetentionRidePdfHandler(
        IRetentionElectronicDocumentXmlService xmlService,
        IRetentionRidePdfService pdfService,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        IRetentionSourceAccess sourceAccess,
        IElectronicDocumentInternalReader reader
    )
    {
        _sourceAccess = sourceAccess;
        _reader = reader;
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

        var document = await _reader.GetAsync(_currentTenant.TenantId, _currentCompany.CompanyId,
            RetentionElectronicDocumentSource.SourceModule, request.RetentionId, cancellationToken);
        if (!document.IsSuccess)
            return Result<byte[]>.Failure(document.Error!, document.Code);
        if (document.Value?.CurrentState == nameof(ElectronicDocumentState.Authorized))
        {
            var authorized = await _reader.GetXmlAsync(_currentTenant.TenantId, _currentCompany.CompanyId,
                RetentionElectronicDocumentSource.SourceModule, request.RetentionId, ElectronicDocumentXmlVariant.Authorized, cancellationToken);
            if (!authorized.IsSuccess)
                return Result<byte[]>.Failure(authorized.Error!, authorized.Code);
            return await _pdfService.GeneratePdfAsync(authorized.Value!, _currentTenant.TenantId,
                _currentCompany.CompanyId, ct: cancellationToken, authorizationDateUtc: document.Value.AuthorizationDate);
        }

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
