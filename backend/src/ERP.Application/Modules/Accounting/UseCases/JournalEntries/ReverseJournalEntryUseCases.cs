using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.JournalEntries;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// Caso de uso específico del reverso contable (Fase 5.4, ADR-026 §9). Deliberadamente no
/// reutiliza <c>PostingPipeline</c>: un reverso no traduce un hecho de negocio externo (Sales,
/// Purchases, ...) vía <c>PostingRule</c> — parte directamente de un <see cref="JournalEntry"/>
/// ya existente y usa <see cref="JournalEntry.Reverse"/> para construir el asiento inverso.
/// </summary>
public sealed record ReverseJournalEntryCommand(Guid JournalEntryId, string Reason)
    : IRequest<Result<JournalEntryDto>>,
        ICompanyScopedRequest;

public sealed class ReverseJournalEntryCommandValidator
    : AbstractValidator<ReverseJournalEntryCommand>
{
    public ReverseJournalEntryCommandValidator()
    {
        RuleFor(x => x.JournalEntryId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo del reverso es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class ReverseJournalEntryCommandHandler
    : IRequestHandler<ReverseJournalEntryCommand, Result<JournalEntryDto>>
{
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IJournalEntrySequenceRepository _journalEntrySequenceRepository;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ReverseJournalEntryCommandHandler(
        IJournalEntryRepository journalEntryRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IJournalEntrySequenceRepository journalEntrySequenceRepository,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _journalEntryRepository = journalEntryRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _journalEntrySequenceRepository = journalEntrySequenceRepository;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<JournalEntryDto>> Handle(
        ReverseJournalEntryCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;

        var original = await _journalEntryRepository.GetByIdAsync(
            tenantId,
            companyId,
            cmd.JournalEntryId,
            ct
        );
        if (original is null)
            return Result<JournalEntryDto>.NotFound("Asiento contable no encontrado.");

        // Mismo criterio que PostingPeriodGuard usa para Post() (ADR-026 §6.1): un asiento no
        // puede reversarse si su período ya no admite contabilización (Closed o Locked). No se
        // duplica la regla — se reutiliza el mismo guard interno del Posting Engine.
        var period = await _accountingPeriodRepository.GetByIdAsync(
            tenantId,
            companyId,
            original.AccountingPeriodId,
            ct
        );
        if (period is null)
            return Result<JournalEntryDto>.ValidationFailure(
                "El período contable del asiento original no existe."
            );

        var periodGuardResult = new PostingPeriodGuard().Ensure(period);
        if (!periodGuardResult.IsSuccess)
            return Result<JournalEntryDto>.ValidationFailure(
                periodGuardResult.Error!,
                periodGuardResult.Code
            );

        var entryNumber = await _journalEntrySequenceRepository.ReserveNextNumberAsync(
            tenantId,
            companyId,
            original.FiscalYear,
            ct
        );

        JournalEntry reversal;
        try
        {
            reversal = original.Reverse(_u.UserId, entryNumber, cmd.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result<JournalEntryDto>.ValidationFailure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result<JournalEntryDto>.ValidationFailure(ex.Message);
        }

        await _journalEntryRepository.AddAsync(reversal, ct);
        await _journalEntryRepository.SaveChangesAsync(ct);

        return Result<JournalEntryDto>.Success(Map.ToDto(reversal));
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

file static class Map
{
    public static JournalEntryDto ToDto(JournalEntry e) =>
        new(
            e.Id,
            e.EntryDate,
            e.AccountingPeriodId,
            e.FiscalYear,
            e.SourceModule,
            e.SourceEventType,
            e.SourceEventId,
            e.Description,
            e.Status.ToString(),
            e.EntryNumber,
            e.PostedAtUtc,
            e.OriginalJournalEntryId,
            e.ReverseJournalEntryId,
            e.ReversedAtUtc,
            e.ReverseReason
        );
}
