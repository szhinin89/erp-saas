using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Command ───────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-SRI-MANUAL-REGISTER-04E — disparo manual y controlado del registro electrónico
/// (firma + envío + consulta de autorización SRI) de una retención ya <c>Issued</c>, vía el mismo
/// pipeline genérico que usan Factura/Nota de Crédito (<see cref="IElectronicDocumentIssuer.RegisterAsync"/>).
///
/// Deliberadamente manual en esta fase: no se dispara automáticamente al emitir la retención
/// (eso queda para una decisión de negocio posterior, ver 04B/04D). Idempotente por herencia —
/// <c>RegisterAsync</c> reanuda Draft/Failed y devuelve <c>Conflict</c> si ya existe un
/// <c>ElectronicDocument</c> en un estado posterior; este comando no agrega ninguna lógica de
/// idempotencia propia.
/// </summary>
public sealed record RegisterRetentionElectronicDocumentCommand(Guid RetentionId)
    : IRequest<Result<ElectronicDocumentDto>>;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class RegisterRetentionElectronicDocumentValidator
    : AbstractValidator<RegisterRetentionElectronicDocumentCommand>
{
    public RegisterRetentionElectronicDocumentValidator()
    {
        RuleFor(x => x.RetentionId).NotEmpty();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class RegisterRetentionElectronicDocumentHandler
    : IRequestHandler<RegisterRetentionElectronicDocumentCommand, Result<ElectronicDocumentDto>>
{
    private readonly IRetentionDocumentRepository _retentionRepository;
    private readonly IElectronicDocumentIssuer _issuer;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public RegisterRetentionElectronicDocumentHandler(
        IRetentionDocumentRepository retentionRepository,
        IElectronicDocumentIssuer issuer,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _retentionRepository = retentionRepository;
        _issuer = issuer;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<ElectronicDocumentDto>> Handle(
        RegisterRetentionElectronicDocumentCommand request,
        CancellationToken cancellationToken
    )
    {
        // GetByIdAsync solo filtra por tenant (ver IRetentionDocumentRepository) — el chequeo de
        // company se hace explícito aquí, mismo criterio fail-closed que GetRetentionBySourceHandler.
        var retention = await _retentionRepository.GetByIdAsync(
            _currentTenant.TenantId,
            request.RetentionId,
            cancellationToken
        );
        if (retention is null || retention.CompanyId != _currentCompany.CompanyId)
            return Result<ElectronicDocumentDto>.NotFound("La retención no existe.");

        if (retention.Status != RetentionStatus.Issued)
            return Result<ElectronicDocumentDto>.ValidationFailure(
                $"La retención debe estar emitida para registrar su documento electrónico (estado actual: {retention.Status})."
            );

        var registerRequest = new RegisterElectronicDocumentRequest(
            TenantId: _currentTenant.TenantId,
            CompanyId: _currentCompany.CompanyId,
            DocumentType: ElectronicDocumentType.Retention,
            SourceModule: "Retentions",
            SourceEntityId: retention.Id,
            UserId: _currentUser.UserId
        );

        return await _issuer.RegisterAsync(registerRequest, cancellationToken);
    }
}
