using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Query ───────────────────────────────────────────────────────────────

/// <summary>
/// Descarga de lectura: Authorized usa exclusivamente la evidencia fiscal almacenada;
/// sin documento autorizado conserva la vista previa generada. Variant permite pedir
/// explícitamente Draft, Signed o Authorized sin sustituir una variante ausente.
/// </summary>
public sealed record GenerateRetentionXmlQuery(Guid RetentionId, ElectronicDocumentXmlVariant? Variant = null)
    : IRequest<Result<string>>,
        IRequiresCompanyContext;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class GenerateRetentionXmlValidator : AbstractValidator<GenerateRetentionXmlQuery>
{
    public GenerateRetentionXmlValidator()
    {
        RuleFor(x => x.RetentionId).NotEmpty();
        RuleFor(x => x.Variant).IsInEnum();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class GenerateRetentionXmlHandler
    : IRequestHandler<GenerateRetentionXmlQuery, Result<string>>
{
    private readonly IRetentionElectronicDocumentXmlService _xmlService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly IRetentionSourceAccess _sourceAccess;
    private readonly IElectronicDocumentInternalReader _reader;

    public GenerateRetentionXmlHandler(
        IRetentionElectronicDocumentXmlService xmlService,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        IRetentionSourceAccess sourceAccess,
        IElectronicDocumentInternalReader reader
    )
    {
        _sourceAccess = sourceAccess;
        _reader = reader;
        _xmlService = xmlService;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<string>> Handle(
        GenerateRetentionXmlQuery request,
        CancellationToken cancellationToken
    )
    {
        // ADR-036 (D-9) — autorización por origen, server-side (Compra → purchases.view; Gasto →
        // expenses.documents.view). Sin acceso = inexistente (fail-closed).
        if (await _sourceAccess.FindViewableAsync(request.RetentionId, cancellationToken) is null)
            return Result<string>.NotFound("La retención no existe.");

        var document = await _reader.GetAsync(_currentTenant.TenantId, _currentCompany.CompanyId,
            RetentionElectronicDocumentSource.SourceModule, request.RetentionId, cancellationToken);
        if (!document.IsSuccess)
            return Result<string>.Failure(document.Error!, document.Code);

        var variant = request.Variant
            ?? (document.Value?.CurrentState == nameof(ElectronicDocumentState.Authorized)
                ? ElectronicDocumentXmlVariant.Authorized : (ElectronicDocumentXmlVariant?)null);
        if (variant.HasValue)
            return await _reader.GetXmlAsync(_currentTenant.TenantId, _currentCompany.CompanyId,
                RetentionElectronicDocumentSource.SourceModule, request.RetentionId, variant.Value, cancellationToken);

        var generated = await _xmlService.GenerateXmlAsync(
            new ElectronicDocumentSourceReference(
                _currentTenant.TenantId,
                _currentCompany.CompanyId,
                request.RetentionId
            ),
            cancellationToken
        );
        return generated.IsSuccess
            ? Result<string>.Success(generated.Value!.Xml)
            : Result<string>.Failure(generated.Error!, generated.Code);
    }
}
