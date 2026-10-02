using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.Retentions.Services;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Command ───────────────────────────────────────────────────────────────

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — acción de RECUPERACIÓN controlada (ya no es el camino
/// normal: la transmisión se inicia automáticamente al confirmar Compra/Gasto, con el job de
/// recuperación como respaldo). Entra por el mismo <see cref="IRetentionElectronicTransmission"/>
/// que el flujo automático — mismo gate de ciclo de vida, mismo reclamo Dispatching, misma
/// idempotencia (Draft/Failed se reanudan; un documento en estado posterior responde Conflict).
///
/// Autorización: <c>electronic-documents.retry</c> en el endpoint, más el permiso de acción del
/// módulo origen resuelto server-side (<see cref="IRetentionSourceAccess.CanOperateAsync"/>).
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
    private readonly IRetentionElectronicTransmission _transmission;
    private readonly IRetentionSourceAccess _sourceAccess;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public RegisterRetentionElectronicDocumentHandler(
        IRetentionElectronicTransmission transmission,
        IRetentionSourceAccess sourceAccess,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _transmission = transmission;
        _sourceAccess = sourceAccess;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Result<ElectronicDocumentDto>> Handle(
        RegisterRetentionElectronicDocumentCommand request,
        CancellationToken cancellationToken
    )
    {
        var retention = await _sourceAccess.FindViewableAsync(request.RetentionId, cancellationToken);
        if (retention is null)
            return Result<ElectronicDocumentDto>.NotFound("La retención no existe.");
        if (!await _sourceAccess.CanOperateAsync(retention.SourceDocumentType, cancellationToken))
            return Result<ElectronicDocumentDto>.Forbidden(
                "No tiene permiso para operar el documento origen de esta retención."
            );

        // El estado de la retención (solo Issued es procesable) lo decide el gate SSOT del emisor.
        return await _transmission.StartAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            retention.Id,
            _currentUser.UserId,
            cancellationToken
        );
    }
}
