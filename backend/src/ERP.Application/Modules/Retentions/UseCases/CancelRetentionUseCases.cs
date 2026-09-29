using ERP.Application.Common;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Retentions.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// RETENTIONS-APPLICATION-01C — anula una retención ya emitida. Las guardas de negocio (no
/// cancelar <c>Draft</c>, no cancelar dos veces, motivo obligatorio) viven en
/// <see cref="RetentionDocument.Cancel"/> — el handler no las reimplementa, delega en
/// <see cref="IRetentionCanceller"/> (RETENTIONS-EXPENSES-INTEGRATION-01D-3), mismo patrón que
/// <c>CancelExpenseDocumentUseCases.cs</c> usa para anular el gasto. Desde 01D-3, la anulación SÍ
/// reversa el impacto en CxP si el <c>ExpenseDocument</c> origen tiene una <c>AccountsPayable</c>
/// con la retención aplicada (<see cref="IRetentionCanceller"/> lo detecta y revierte por su
/// cuenta) — antes de esta fase no reversaba nada porque la integración con CxP no existía aún.
/// ZH-PURCHASES-RETENTION-OWNERSHIP-01: el documento origen (<paramref name="SourceDocumentType"/>
/// + <paramref name="SourceDocumentId"/>, siempre de la RUTA) es obligatorio — el handler solo anula
/// la retención ACTIVA de ese origen. Antes esa regla vivía como check-then-act en el controller.
/// </summary>
public sealed record CancelRetentionCommand(
    RetentionSourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    Guid RetentionDocumentId,
    string Reason
) : IRequest<Result<RetentionDocumentDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelRetentionValidator : AbstractValidator<CancelRetentionCommand>
{
    public CancelRetentionValidator()
    {
        RuleFor(x => x.SourceDocumentType).IsInEnum();
        RuleFor(x => x.SourceDocumentId).NotEmpty();
        RuleFor(x => x.RetentionDocumentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelRetentionHandler : IRequestHandler<CancelRetentionCommand, Result<RetentionDocumentDto>>
{
    private readonly IRetentionDocumentRepository _repo;
    private readonly IRetentionCanceller _canceller;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public CancelRetentionHandler(
        IRetentionDocumentRepository repo,
        IRetentionCanceller canceller,
        IUnitOfWork uow,
        ICurrentTenant tenant,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _repo = repo;
        _canceller = canceller;
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
        //
        // Ownership (ZH-PURCHASES-RETENTION-OWNERSHIP-01): solo la retención ACTIVA del documento
        // origen de la ruta — mismo criterio que GetRetentionBySourceQuery, que el controller usaba
        // antes como check-then-act. Inexistente, de otro origen (otra compra, un gasto), de otra
        // sucursal o ya anulada responden el MISMO NotFound: no revela si la retención existe.
        var document = await _repo.GetByIdAsync(_tenant.TenantId, cmd.RetentionDocumentId, ct);
        if (
            document is null
            || document.BranchId != _branch.BranchId
            || document.SourceDocumentType != cmd.SourceDocumentType
            || document.SourceDocumentId != cmd.SourceDocumentId
            || document.Status == RetentionStatus.Cancelled
        )
            return Result<RetentionDocumentDto>.NotFound(NotFoundMessage(cmd.SourceDocumentType));

        // RETENTIONS-EXPENSES-INTEGRATION-01D-3: delega en la operación interna común (staged, sin
        // SaveChanges) — cancelledBy sale siempre de ICurrentUser, nunca del body. Si la CxP del
        // gasto origen ya tiene pagos aplicados, IRetentionCanceller bloquea con ValidationFailure
        // en vez de reversar de forma insegura.
        var result = await _canceller.CancelAsync(document, cmd.Reason, _user.UserId, ct);
        if (!result.IsSuccess)
            return Result<RetentionDocumentDto>.ValidationFailure(result.Error!, result.Code);

        await _uow.SaveChangesAsync(ct);

        return Result<RetentionDocumentDto>.Success(RetentionDocumentMapper.ToDto(document));
    }

    /// <summary>Mensaje público vigente (el que devolvía el controller de Compras).</summary>
    private static string NotFoundMessage(RetentionSourceDocumentType sourceType) =>
        sourceType switch
        {
            RetentionSourceDocumentType.PurchaseInvoice => "La retención no existe o no pertenece a esta compra.",
            RetentionSourceDocumentType.ExpenseDocument => "La retención no existe o no pertenece a este gasto.",
            _ => "La retención no existe o no pertenece a este documento.",
        };
}
