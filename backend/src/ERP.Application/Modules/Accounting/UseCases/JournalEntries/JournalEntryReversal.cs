using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.JournalEntries;

/// <summary>
/// IL-8B — núcleo único del reverso contable (Fase 5.4, ADR-026 §9), compartido por
/// <see cref="ReverseJournalEntryCommandHandler"/> (anulaciones de Ventas/Compras/Gastos/...) y el
/// reverso específico del ASI de apertura: período del original abierto (<see cref="PostingPeriodGuard"/>),
/// número reservado vía <c>IJournalEntrySequenceRepository.ReserveNextNumberAsync</c>,
/// <see cref="JournalEntry.Reverse"/> y alta del asiento inverso. No conoce módulos de origen (cada
/// llamador decide qué asientos puede reversar), no guarda ni abre transacción: el llamador hace
/// <c>SaveChanges</c> dentro de su transacción, que es la que retiene el advisory lock de la secuencia.
/// Clase sin registro DI: la construye cada handler con sus propios repositorios.
/// </summary>
internal sealed class JournalEntryReversal
{
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IJournalEntrySequenceRepository _journalEntrySequenceRepository;

    public JournalEntryReversal(
        IJournalEntryRepository journalEntryRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IJournalEntrySequenceRepository journalEntrySequenceRepository
    )
    {
        _journalEntryRepository = journalEntryRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _journalEntrySequenceRepository = journalEntrySequenceRepository;
    }

    /// <summary>
    /// Reversa <paramref name="original"/> (tracked) con la misma fecha y período del original y
    /// agrega el asiento inverso al contexto sin guardar. Devuelve el asiento inverso.
    /// </summary>
    public async Task<Result<JournalEntry>> ReverseAsync(
        JournalEntry original,
        Guid reversedBy,
        string reason,
        CancellationToken ct
    )
    {
        // Mismo criterio que PostingPeriodGuard usa para Post() (ADR-026 §6.1): un asiento no
        // puede reversarse si su período ya no admite contabilización (Closed o Locked). No se
        // duplica la regla — se reutiliza el mismo guard interno del Posting Engine.
        var period = await _accountingPeriodRepository.GetByIdAsync(
            original.TenantId,
            original.CompanyId,
            original.AccountingPeriodId,
            ct
        );
        if (period is null)
            return Result<JournalEntry>.ValidationFailure(
                "El período contable del asiento original no existe."
            );

        var periodGuardResult = new PostingPeriodGuard().Ensure(period);
        if (!periodGuardResult.IsSuccess)
            return Result<JournalEntry>.ValidationFailure(
                periodGuardResult.Error!,
                periodGuardResult.Code
            );

        var entryNumber = await _journalEntrySequenceRepository.ReserveNextNumberAsync(
            original.TenantId,
            original.CompanyId,
            original.FiscalYear,
            ct
        );

        JournalEntry reversal;
        try
        {
            reversal = original.Reverse(reversedBy, entryNumber, reason);
        }
        catch (ArgumentException ex)
        {
            return Result<JournalEntry>.ValidationFailure(ex.Message);
        }

        await _journalEntryRepository.AddAsync(reversal, ct);
        return Result<JournalEntry>.Success(reversal);
    }
}
