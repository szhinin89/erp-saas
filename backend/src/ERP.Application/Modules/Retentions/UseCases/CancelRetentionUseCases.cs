using ERP.Application.Common;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-APPLICATION-01C — anula una retención ya emitida. Las guardas de negocio (no
/// cancelar <c>Draft</c>, no cancelar dos veces, motivo obligatorio) viven en
/// <see cref="RetentionDocument.Cancel"/> — el handler no las reimplementa, solo traduce la
/// excepción de dominio a <see cref="Result{T}"/>, mismo patrón que
/// <c>CancelExpenseDocumentUseCases.cs</c>. No reversa CxP ni contabilidad — esta fase no crea
/// esos efectos (la emisión es aislada), por lo que tampoco hay nada que reversar aquí.
/// </summary>
public sealed record CancelRetentionCommand(Guid RetentionDocumentId, string Reason)
    : IRequest<Result<RetentionDocumentDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelRetentionValidator : AbstractValidator<CancelRetentionCommand>
{
    public CancelRetentionValidator()
    {
        RuleFor(x => x.RetentionDocumentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelRetentionHandler : IRequestHandler<CancelRetentionCommand, Result<RetentionDocumentDto>>
{
    private readonly IRetentionDocumentRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public CancelRetentionHandler(
        IRetentionDocumentRepository repo,
        IUnitOfWork uow,
        ICurrentTenant tenant,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _repo = repo;
        _uow = uow;
        _tenant = tenant;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<RetentionDocumentDto>> Handle(CancelRetentionCommand cmd, CancellationToken ct)
    {
        // GetByIdAsync ya filtra por tenant+company (ForOperationalScope); el branch se valida
        // explícitamente porque el repositorio no lo filtra — mismo patrón fail-closed usado en
        // IssueRetentionHandler/GetRetentionEligibilityHandler, nunca IgnoreQueryFilters.
        var document = await _repo.GetByIdAsync(_tenant.TenantId, cmd.RetentionDocumentId, ct);
        if (document is null || document.BranchId != _branch.BranchId)
            return Result<RetentionDocumentDto>.NotFound("Retención no encontrada.");

        try
        {
            // cancelledBy sale siempre de ICurrentUser — nunca del body.
            document.Cancel(cmd.Reason, _user.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<RetentionDocumentDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RetentionDocumentDto>.ValidationFailure(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);

        return Result<RetentionDocumentDto>.Success(RetentionDocumentMapper.ToDto(document));
    }
}
