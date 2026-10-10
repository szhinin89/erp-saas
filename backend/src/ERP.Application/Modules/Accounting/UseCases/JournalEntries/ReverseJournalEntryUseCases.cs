using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.InitialLoad.Constants;
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
    public const string OpeningReversalNotAllowedCode = "OPENING_REVERSAL_REQUIRES_CORRECTION_FLOW";

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

        // IL-7B — el asiento de apertura de la Carga Inicial no se reversa por sí solo: el saldo
        // operativo que respalda (Kardex InitialBalance, CxC/CxP InitialBalance) no tiene flujo de
        // anulación/corrección, así que reversarlo dejaría inventario/CxC/CxP sin contrapartida
        // contable. Requiere un flujo específico de corrección de apertura (fuera de IL-7B).
        if (original.SourceModule == OpeningBalancePostingFacts.SourceModule)
            return Result<JournalEntryDto>.ValidationFailure(
                "El asiento de apertura de la Carga Inicial no puede reversarse: el saldo operativo que lo "
                    + "respalda no tiene flujo de corrección y quedaría sin contrapartida contable.",
                OpeningReversalNotAllowedCode
            );

        // IL-8B — núcleo compartido (período, número, Reverse, alta); el bloqueo de InitialLoad de
        // arriba queda solo en este reverso genérico.
        var reversed = await new JournalEntryReversal(
            _journalEntryRepository,
            _accountingPeriodRepository,
            _journalEntrySequenceRepository
        ).ReverseAsync(original, _u.UserId, cmd.Reason, ct);
        if (!reversed.IsSuccess)
            return Result<JournalEntryDto>.ValidationFailure(reversed.Error!, reversed.Code);
        var reversal = reversed.Value!;

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
