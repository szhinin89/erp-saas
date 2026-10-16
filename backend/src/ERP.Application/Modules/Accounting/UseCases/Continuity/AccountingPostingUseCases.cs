using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.Continuity;

// ACCOUNTING-CONTINUITY-01/02 — consulta de obligaciones contables y reproceso manual individual
// y en lote. Sin reintento automático (docs/architecture/accounting.md §6).

public sealed record GetAccountingPostingsQuery(
    AccountingPostingStatus? Status = null,
    string? SourceModule = null,
    Guid? SourceEventId = null,
    int Take = 200
) : IRequest<Result<IReadOnlyList<AccountingPostingDto>>>, ICompanyScopedRequest;

public sealed record GetAccountingPostingByIdQuery(Guid Id)
    : IRequest<Result<AccountingPostingDetailDto>>,
        ICompanyScopedRequest;

public sealed record ReprocessAccountingPostingCommand(Guid Id, string? Reason)
    : IRequest<Result<AccountingPostingDto>>,
        ICompanyScopedRequest;

/// <summary>Cuerpo HTTP del reproceso individual (el Id viaja en la ruta).</summary>
public sealed record ReprocessAccountingPostingRequest(string? Reason);

public sealed class GetAccountingPostingsQueryValidator : AbstractValidator<GetAccountingPostingsQuery>
{
    public GetAccountingPostingsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.SourceModule).MaximumLength(60);
    }
}

public sealed class GetAccountingPostingByIdQueryValidator
    : AbstractValidator<GetAccountingPostingByIdQuery>
{
    public GetAccountingPostingByIdQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ReprocessAccountingPostingCommandValidator
    : AbstractValidator<ReprocessAccountingPostingCommand>
{
    public ReprocessAccountingPostingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).ValidReprocessReason();
    }
}

internal static class AccountingPostingReprocessRules
{
    public static IRuleBuilderOptions<T, string?> ValidReprocessReason<T>(
        this IRuleBuilder<T, string?> rule
    ) =>
        rule.Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("El motivo del reproceso es obligatorio.")
            .MaximumLength(AccountingPostingAttempt.ReasonMaxLength)
            .WithMessage($"El motivo no puede superar {AccountingPostingAttempt.ReasonMaxLength} caracteres.");
}

public sealed class GetAccountingPostingsHandler
    : IRequestHandler<GetAccountingPostingsQuery, Result<IReadOnlyList<AccountingPostingDto>>>
{
    private readonly IAccountingPostingRepository _postings;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetAccountingPostingsHandler(
        IAccountingPostingRepository postings,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _postings = postings;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<IReadOnlyList<AccountingPostingDto>>> Handle(
        GetAccountingPostingsQuery query,
        CancellationToken ct
    )
    {
        var items = await _postings.ListAsync(
            _tenant.TenantId,
            _company.CompanyId,
            query.Status,
            string.IsNullOrWhiteSpace(query.SourceModule) ? null : query.SourceModule.Trim(),
            query.SourceEventId,
            query.Take,
            ct
        );
        return Result<IReadOnlyList<AccountingPostingDto>>.Success(
            items.Select(AccountingPostingDto.From).ToList()
        );
    }
}

public sealed class GetAccountingPostingByIdHandler
    : IRequestHandler<GetAccountingPostingByIdQuery, Result<AccountingPostingDetailDto>>
{
    private readonly IAccountingPostingRepository _postings;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;

    public GetAccountingPostingByIdHandler(
        IAccountingPostingRepository postings,
        ICurrentTenant tenant,
        ICurrentCompany company
    )
    {
        _postings = postings;
        _tenant = tenant;
        _company = company;
    }

    public async Task<Result<AccountingPostingDetailDto>> Handle(
        GetAccountingPostingByIdQuery query,
        CancellationToken ct
    )
    {
        var posting = await _postings.GetByIdAsync(_tenant.TenantId, _company.CompanyId, query.Id, ct);
        if (posting is null)
            return Result<AccountingPostingDetailDto>.NotFound("Contabilización no encontrada.");

        var attempts = await _postings.ListAttemptsAsync(
            _tenant.TenantId,
            _company.CompanyId,
            posting.Id,
            ct
        );
        return Result<AccountingPostingDetailDto>.Success(
            new AccountingPostingDetailDto(
                AccountingPostingDto.From(posting),
                attempts.Select(AccountingPostingAttemptDto.From).ToList()
            )
        );
    }
}

/// <summary>
/// Reproceso manual individual: un intento explícito del usuario autorizado, con la configuración
/// contable vigente y los datos económicos persistidos. Mismo ejecutor que el intento inicial.
/// </summary>
public sealed class ReprocessAccountingPostingHandler
    : IRequestHandler<ReprocessAccountingPostingCommand, Result<AccountingPostingDto>>
{
    private readonly AccountingPostingExecutor _executor;

    public ReprocessAccountingPostingHandler(AccountingPostingExecutor executor) =>
        _executor = executor;

    public Task<Result<AccountingPostingDto>> Handle(
        ReprocessAccountingPostingCommand command,
        CancellationToken ct
    ) => _executor.ExecuteAsync(command.Id, AccountingPostingAttemptTrigger.Manual, command.Reason, ct);
}
