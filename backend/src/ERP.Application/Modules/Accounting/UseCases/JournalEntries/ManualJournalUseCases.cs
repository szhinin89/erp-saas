using System.Text.Json;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.AccountingPeriods;
using ERP.Application.Modules.Companies;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.UseCases.JournalEntries;

public sealed record ManualJournalResultDto(Guid Id, string Status, int Version, int? EntryNumber);
public sealed record ManualJournalInfoDto(string? Reference, int Version, Guid CreatedBy,
    Guid? UpdatedBy, DateTime? DiscardedAtUtc, Guid? DiscardedBy, string? DiscardReason,
    IReadOnlyList<ManualJournalAttemptDto> Attempts);
public sealed record ManualJournalAttemptDto(Guid Id, DateTime AttemptedAtUtc, Guid AttemptedBy,
    Guid IdempotencyKey, bool Success, string? ErrorCode, string? ErrorMessage,
    int ExpectedVersion, int ResultVersion, string ResultStatus, int? EntryNumber)
{
    public static ManualJournalAttemptDto From(ManualJournalAttempt a) => new(a.Id, a.AttemptedAtUtc,
        a.AttemptedBy, a.IdempotencyKey, a.Success, a.ErrorCode, a.ErrorMessage, a.ExpectedVersion,
        a.ResultVersion, a.ResultStatus, a.EntryNumber);
}
public sealed record CreateManualJournalCommand(DateOnly EntryDate, string Concept, string? Reference,
    IReadOnlyList<ManualJournalLine> Lines, Guid IdempotencyKey)
    : IRequest<Result<ManualJournalResultDto>>, ICompanyScopedRequest;
public sealed record EditManualJournalCommand(Guid Id, int Version, DateOnly EntryDate, string Concept,
    string? Reference, IReadOnlyList<ManualJournalLine> Lines)
    : IRequest<Result<ManualJournalResultDto>>, ICompanyScopedRequest;
public sealed record PostManualJournalCommand(Guid Id, int Version, Guid IdempotencyKey)
    : IRequest<Result<ManualJournalResultDto>>, ICompanyScopedRequest;
public sealed record DiscardManualJournalCommand(Guid Id, int Version, string Reason)
    : IRequest<Result<ManualJournalResultDto>>, ICompanyScopedRequest;
public sealed record ReverseManualJournalCommand(Guid Id, DateOnly EffectiveDate, string Reason)
    : IRequest<Result<ManualJournalResultDto>>, ICompanyScopedRequest;

public sealed class CreateManualJournalValidator : AbstractValidator<CreateManualJournalCommand>
{
    public CreateManualJournalValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.EntryDate).NotEmpty();
        RuleFor(x => x.Concept).NotEmpty().MaximumLength(JournalEntry.DescriptionMaxLength);
        RuleFor(x => x.Reference).MaximumLength(JournalEntry.DescriptionMaxLength);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).NotNull();
    }
}
public sealed class EditManualJournalValidator : AbstractValidator<EditManualJournalCommand>
{
    public EditManualJournalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.EntryDate).NotEmpty();
        RuleFor(x => x.Concept).NotEmpty().MaximumLength(JournalEntry.DescriptionMaxLength);
        RuleFor(x => x.Reference).MaximumLength(JournalEntry.DescriptionMaxLength);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).NotNull();
    }
}
public sealed class PostManualJournalValidator : AbstractValidator<PostManualJournalCommand>
{
    public PostManualJournalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}
public sealed class DiscardManualJournalValidator : AbstractValidator<DiscardManualJournalCommand>
{
    public DiscardManualJournalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(JournalEntry.ReverseReasonMaxLength);
    }
}
public sealed class ReverseManualJournalValidator : AbstractValidator<ReverseManualJournalCommand>
{
    public ReverseManualJournalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.EffectiveDate).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(JournalEntry.ReverseReasonMaxLength);
    }
}

/// <summary>Manual commands own their transaction. They share the company gate, identity locks,
/// sequence, account/period guards and Domain invariants with the existing book.</summary>
public sealed class ManualJournalHandler(
    IUnitOfWork uow, IJournalEntryRepository journal, IManualJournalRepository manual,
    IAccountingPeriodRepository periods, IAccountingOperationalPolicyRepository policies,
    IJournalEntrySequenceRepository sequences, IAccountRepository accounts,
    ICurrentTenant tenant, ICurrentCompany company, ICurrentUser user, ICompanyClock clock,
    ICompanyPrecisionPolicyProvider precision, IAuditService audit, IAuditContext auditContext,
    ILogger<PostingEngine> postingLogger, ILogger<ManualJournalHandler> logger,
    FiscalYearClosingService closing)
    : IRequestHandler<CreateManualJournalCommand, Result<ManualJournalResultDto>>,
      IRequestHandler<EditManualJournalCommand, Result<ManualJournalResultDto>>,
      IRequestHandler<PostManualJournalCommand, Result<ManualJournalResultDto>>,
      IRequestHandler<DiscardManualJournalCommand, Result<ManualJournalResultDto>>,
      IRequestHandler<ReverseManualJournalCommand, Result<ManualJournalResultDto>>
{
    public Task<Result<ManualJournalResultDto>> Handle(CreateManualJournalCommand cmd, CancellationToken ct) =>
        TransactionAsync(cmd.IdempotencyKey, async () =>
        {
            var existing = await journal.FindByKeyAsync(tenant.TenantId, company.CompanyId,
                JournalEntry.ManualModule, JournalEntry.ManualFact, cmd.IdempotencyKey, ct);
            if (existing is not null)
                return Result<ManualJournalResultDto>.Success(new(existing.Id, "Draft", 1, null));
            var period = await DraftPeriodAsync(cmd.EntryDate, ct);
            if (!period.IsSuccess) return Failure(period);
            var accountResult = await ValidateDraftAccountsAsync(cmd.Lines, ct);
            if (!accountResult.IsSuccess) return accountResult;
            var policy = await precision.GetEffectiveAsync(ct);
            var entry = JournalEntry.CreateManual(tenant.TenantId, company.CompanyId, cmd.IdempotencyKey,
                cmd.EntryDate, period.Value?.Id, period.Value?.FiscalYear ?? cmd.EntryDate.Year,
                cmd.Concept, cmd.Reference, cmd.Lines, policy.AccountingDecimals, user.UserId);
            await journal.AddAsync(entry, ct);
            await AuditAsync(entry, "Created", ct);
            return Ok(entry);
        }, ct);

    public Task<Result<ManualJournalResultDto>> Handle(EditManualJournalCommand cmd, CancellationToken ct) =>
        TransactionAsync(cmd.Id, async () =>
        {
            var entry = await FindAsync(cmd.Id, ct);
            if (entry is { IsFiscalYearClosing: true })
                return Result<ManualJournalResultDto>.ValidationFailure(
                    "El asiento de cierre anual se calcula desde Períodos y no se edita: descártelo y vuelva a generarlo.",
                    FiscalYearClosingCodes.NotEditable);
            var check = CheckDraft(entry, cmd.Version);
            if (check is not null) return check;
            // Both old and new dates must remain writable; editing cannot move a closed entry out.
            var oldPeriod = await DraftPeriodAsync(entry!.EntryDate, ct);
            if (!oldPeriod.IsSuccess) return Failure(oldPeriod);
            var period = await DraftPeriodAsync(cmd.EntryDate, ct);
            if (!period.IsSuccess) return Failure(period);
            var accountResult = await ValidateDraftAccountsAsync(cmd.Lines, ct);
            if (!accountResult.IsSuccess) return accountResult;
            var policy = await precision.GetEffectiveAsync(ct);
            entry.ReplaceManual(cmd.EntryDate, period.Value?.Id, period.Value?.FiscalYear ?? cmd.EntryDate.Year,
                cmd.Concept, cmd.Reference, cmd.Lines, policy.AccountingDecimals, user.UserId);
            await AuditAsync(entry, "Edited", ct);
            return Ok(entry);
        }, ct);

    public async Task<Result<ManualJournalResultDto>> Handle(PostManualJournalCommand cmd, CancellationToken ct)
    {
        try
        {
            return await TransactionAsync(cmd.Id, async () =>
            {
                var entry = await FindAsync(cmd.Id, ct);
                if (entry is null) return Result<ManualJournalResultDto>.NotFound("Asiento manual no encontrado.");
                var prior = await manual.FindAttemptAsync(tenant.TenantId, company.CompanyId, cmd.Id, cmd.IdempotencyKey, ct);
                if (prior is not null) return Replay(prior, cmd.Version);
                var result = CheckDraft(entry, cmd.Version) ?? await PublishAsync(entry, ct);
                await manual.AddAttemptAsync(ManualJournalAttempt.Create(entry, user.UserId, cmd.IdempotencyKey,
                    cmd.Version, result.IsSuccess, result.Code, result.Error), ct);
                return result;
            }, ct, captureBusinessErrors: false);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // A failed flush/commit must not preserve a number, lines or a contaminated tracker.
            // A fresh transaction records the durable failure (or recovers a committed result).
            logger.LogError("Manual publication failed ({ExceptionType}) for {JournalEntryId}", ex.GetType().Name, cmd.Id);
            return await TransactionAsync(cmd.Id, async () =>
            {
                var entry = await FindAsync(cmd.Id, ct);
                if (entry is null) return Result<ManualJournalResultDto>.NotFound("Asiento manual no encontrado.");
                var prior = await manual.FindAttemptAsync(tenant.TenantId, company.CompanyId, cmd.Id, cmd.IdempotencyKey, ct);
                if (prior is not null) return Replay(prior, cmd.Version);
                const string message = "No se pudo completar la publicación. Consulte el intento antes de volver a publicar.";
                await manual.AddAttemptAsync(ManualJournalAttempt.Create(entry, user.UserId, cmd.IdempotencyKey,
                    cmd.Version, false, "MANUAL_POST_INTERNAL", message), ct);
                return Result<ManualJournalResultDto>.ValidationFailure(message, "MANUAL_POST_INTERNAL");
            }, ct, captureBusinessErrors: false);
        }
    }

    public Task<Result<ManualJournalResultDto>> Handle(DiscardManualJournalCommand cmd, CancellationToken ct) =>
        TransactionAsync(cmd.Id, async () =>
        {
            var entry = await FindAsync(cmd.Id, ct);
            var check = CheckDraft(entry, cmd.Version);
            if (check is not null) return check;
            var period = await DraftPeriodAsync(entry!.EntryDate, ct);
            if (!period.IsSuccess) return Failure(period);
            entry.Discard(user.UserId, cmd.Reason);
            await AuditAsync(entry, "Discarded", ct, cmd.Reason);
            return Ok(entry);
        }, ct);

    public Task<Result<ManualJournalResultDto>> Handle(ReverseManualJournalCommand cmd, CancellationToken ct) =>
        TransactionAsync(cmd.Id, async () =>
        {
            var entry = await FindAsync(cmd.Id, ct);
            if (entry is null) return Result<ManualJournalResultDto>.NotFound("Asiento manual no encontrado.");
            if (entry.Status != JournalEntryStatus.Posted)
                return Result<ManualJournalResultDto>.ValidationFailure("Solo un manual Posted puede reversarse.");
            var existing = await journal.FindByKeyAsync(tenant.TenantId, company.CompanyId, "Accounting", "Reversal", entry.Id, ct);
            if (entry.IsFiscalYearClosing && existing is null)
            {
                // ACCOUNTING-FINAL-FIX: el reverso del cierre anual vive en el mismo ejercicio (31-dic, último
                // período Open), serializado con la generación/publicación; nunca traslada el resultado al
                // ejercicio siguiente. El gate Open/Closed/Locked lo aplica el reverso común.
                await closing.AcquireLockAsync(company.CompanyId, entry.FiscalYear, ct);
                if (cmd.EffectiveDate != entry.EntryDate)
                    return Result<ManualJournalResultDto>.ValidationFailure(
                        $"El asiento de cierre anual solo se reversa con su fecha original ({entry.EntryDate:yyyy-MM-dd}), "
                            + "mientras el último período del ejercicio esté abierto.",
                        FiscalYearClosingCodes.ReversalDateInvalid);
            }
            var result = await new JournalEntryReversal(journal, periods, sequences, policies)
                .ReverseAsync(entry, user.UserId, cmd.Reason, cmd.EffectiveDate, ct);
            if (!result.IsSuccess) return Failure(result);
            if (existing is null)
                await AuditAsync(result.Value!, "Reversed", ct, cmd.Reason);
            return Ok(result.Value!);
        }, ct);

    private async Task<Result<ManualJournalResultDto>> PublishAsync(JournalEntry entry, CancellationToken ct)
    {
        if (entry.EntryDate > await clock.TodayAsync(company.CompanyId, tenant.TenantId, ct))
            return Result<ManualJournalResultDto>.ValidationFailure("La fecha contable no puede ser futura.", "MANUAL_FUTURE_DATE");
        if (await new AccountingOperationalDecision(policies).ResolveAsync(tenant.TenantId, company.CompanyId, entry.EntryDate, ct)
            != AccountingApplicability.Applicable)
            return Result<ManualJournalResultDto>.ValidationFailure("Accounting debe estar Enabled en la fecha contable.", "ACCOUNTING_NOT_ENABLED");
        var period = await periods.FindContainingDateAsync(tenant.TenantId, company.CompanyId, entry.EntryDate, ct);
        if (period is null) return Result<ManualJournalResultDto>.ValidationFailure("No existe período para la fecha contable.", "PERIOD_NOT_OPEN");
        var gate = new PostingPeriodGuard().Ensure(period);
        if (!gate.IsSuccess) return Failure(gate);
        if (entry.IsFiscalYearClosing)
        {
            // ACCOUNTING-12D: revalida todas las precondiciones del cierre bajo el lock del ejercicio y
            // exige que las líneas sigan siendo exactamente el cálculo vigente (sin doble cierre ni saldos viejos).
            await closing.AcquireLockAsync(company.CompanyId, entry.FiscalYear, ct);
            var plan = await closing.PrepareAsync(tenant.TenantId, company.CompanyId, entry.FiscalYear, entry, ct);
            if (!plan.IsSuccess) return Failure(plan);
            if (plan.Value!.EntryDate != entry.EntryDate || !FiscalYearClosingRules.SameLines(entry.Lines, plan.Value.Lines))
                return Result<ManualJournalResultDto>.ValidationFailure(
                    "Los saldos del ejercicio cambiaron desde que se generó el asiento de cierre: descártelo y genere uno nuevo.",
                    FiscalYearClosingCodes.Outdated);
        }
        try { entry.EnsurePostable(); }
        catch (DomainRuleViolationException ex) { return Result<ManualJournalResultDto>.FromDomainRule(ex); }
        var accountGate = await new PostingAccountGuard(accounts, postingLogger)
            .EnsureAccountsPostableAsync(tenant.TenantId, company.CompanyId, entry.Lines.Select(l => l.AccountId), ct);
        if (!accountGate.IsSuccess) return Failure(accountGate);
        entry.SetManualPostingPeriod(period.Id, period.FiscalYear);
        var number = await sequences.ReserveNextNumberAsync(tenant.TenantId, company.CompanyId, period.FiscalYear, ct);
        entry.Post(user.UserId, number);
        await AuditAsync(entry, "Posted", ct);
        return Ok(entry);
    }

    private async Task<Result<AccountingPeriod?>> DraftPeriodAsync(DateOnly date, CancellationToken ct)
    {
        var period = await periods.FindContainingDateAsync(tenant.TenantId, company.CompanyId, date, ct);
        if (period is not null)
        {
            var gate = new PostingPeriodGuard().Ensure(period);
            if (!gate.IsSuccess) return Result<AccountingPeriod?>.ValidationFailure(gate.Error!, gate.Code);
        }
        return Result<AccountingPeriod?>.Success(period);
    }

    private async Task<Result<ManualJournalResultDto>> ValidateDraftAccountsAsync(IReadOnlyList<ManualJournalLine> lines, CancellationToken ct)
    {
        foreach (var id in lines.Select(l => l.AccountId).Distinct())
            if (await accounts.GetByIdAsync(tenant.TenantId, company.CompanyId, id, ct) is null)
                return Result<ManualJournalResultDto>.ValidationFailure("La cuenta no existe en la empresa activa.");
        return Result<ManualJournalResultDto>.Success(new(Guid.Empty, "Draft", 0, null));
    }

    private async Task<JournalEntry?> FindAsync(Guid id, CancellationToken ct)
    {
        var entry = await journal.GetByIdAsync(tenant.TenantId, company.CompanyId, id, ct);
        return entry?.IsUserPublished == true ? entry : null;
    }

    private static Result<ManualJournalResultDto>? CheckDraft(JournalEntry? entry, int version) =>
        entry is null ? Result<ManualJournalResultDto>.NotFound("Asiento manual no encontrado.")
        : entry.Version != version ? Result<ManualJournalResultDto>.Conflict("El asiento cambió. Recargue antes de continuar.")
        : entry.Status != JournalEntryStatus.Draft ? Result<ManualJournalResultDto>.ValidationFailure("Solo un manual Draft puede modificarse/publicarse.")
        : null;

    private async Task<Result<ManualJournalResultDto>> TransactionAsync(Guid identity,
        Func<Task<Result<ManualJournalResultDto>>> operation, CancellationToken ct, bool captureBusinessErrors = true)
    {
        if (tenant.TenantId == Guid.Empty || company.CompanyId == Guid.Empty || user.UserId == Guid.Empty)
            return Result<ManualJournalResultDto>.Forbidden("Se requiere contexto autenticado de empresa.");
        if (uow.HasActiveTransaction) throw new InvalidOperationException("Los comandos manuales requieren una transacción propia.");
        await uow.BeginTransactionAsync(ct);
        try
        {
            await policies.AcquireSharedLockAsync(company.CompanyId, ct);
            await journal.AcquireIdempotencyLockAsync(company.CompanyId, "Accounting.Manual", identity, "Mutation", ct);
            var result = await operation();
            await uow.SaveChangesAsync(ct);
            await uow.CommitAsync(ct);
            return result;
        }
        catch (Exception ex)
        {
            await uow.RollbackAsync(CancellationToken.None);
            uow.ClearChangeTracker();
            if (captureBusinessErrors)
            {
                if (ex is ArgumentException argument)
                    return Result<ManualJournalResultDto>.ValidationFailure(argument.Message);
                if (ex is DomainRuleViolationException domain)
                    return Result<ManualJournalResultDto>.FromDomainRule(domain);
            }
            throw;
        }
    }

    private Task AuditAsync(JournalEntry entry, string action, CancellationToken ct, string? reason = null) =>
        audit.RecordAsync(JournalEntryAudit.Create(auditContext.Actor, entry, action,
            JsonSerializer.Serialize(new { entry.Id, entry.EntryDate, entry.Description, entry.Reference,
                entry.Version, Status = entry.Status.ToString(), entry.EntryNumber, entry.OriginalJournalEntryId,
                entry.Lines }), reason), ct);
    private static Result<ManualJournalResultDto> Ok(JournalEntry entry) =>
        Result<ManualJournalResultDto>.Success(new(entry.Id, entry.Status.ToString(), entry.Version, entry.EntryNumber));
    private static Result<ManualJournalResultDto> Failure<T>(Result<T> result) =>
        Result<ManualJournalResultDto>.ValidationFailure(result.Error!, result.Code);
    private static Result<ManualJournalResultDto> Replay(ManualJournalAttempt attempt, int version) =>
        attempt.ExpectedVersion != version ? Result<ManualJournalResultDto>.Conflict("La clave de idempotencia pertenece a otra versión.")
        : attempt.Success ? Result<ManualJournalResultDto>.Success(new(attempt.JournalEntryId, attempt.ResultStatus, attempt.ResultVersion, attempt.EntryNumber))
        : Result<ManualJournalResultDto>.Failure(attempt.ErrorMessage!, attempt.ErrorCode);
}
