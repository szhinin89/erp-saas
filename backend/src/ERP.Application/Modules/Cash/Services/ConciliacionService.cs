using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Domain.Modules.Cash.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Cash.Services;

public sealed class ReconciliationService : IReconciliationService
{
    private const decimal ToleranciaMonto = 0.02m;
    private static readonly TimeSpan dateToleranceDays = TimeSpan.FromDays(3);

    private readonly ICashRepository _caja;
    private readonly IAccountingRepository _accounting;
    private readonly ICurrentUser _user;

    public ReconciliationService(
        ICashRepository caja,
        IAccountingRepository accounting,
        ICurrentUser user)
    {
        _caja        = caja;
        _accounting = accounting;
        _user        = user;
    }

    public async Task<Result<IReadOnlyList<SugerenciaReconciliationDto>>> SugerirConciliacionAsync(
        Guid extractoId,
        CancellationToken ct)
    {
        var extracto = await _caja.GetBankStatementWithTransactionsAsync(extractoId, ct);
        if (extracto is null)
            return Result<IReadOnlyList<SugerenciaReconciliationDto>>.Failure("Extracto no encontrado.");

        var cuenta = await _caja.GetBankAccountByIdAsync(extracto.BankAccountId, ct);
        if (cuenta?.LedgerAccountId is not { } glId)
        {
            return Result<IReadOnlyList<SugerenciaReconciliationDto>>.Failure(
                "La cuenta bancaria no tiene cuenta contable asociada; no se pueden sugerir coincidencias.");
        }

        var desde = extracto.PeriodFrom.AddDays(-5);
        var hasta = extracto.PeriodTo.AddDays(5);

        var asientos = await _accounting.GetPostedJournalEntriesWithAccountAsync(
            extracto.TenantId,
            glId,
            desde,
            hasta,
            ct);

        var sugerencias = new List<SugerenciaReconciliationDto>();

        foreach (var mov in extracto.Transactions.Where(m => m.Status == "Pendiente"))
        {
            Guid? mejor = null;
            string? motivo = null;
            var mejorDiff = decimal.MaxValue;

            foreach (var je in asientos)
            {
                var linea = je.Lines.FirstOrDefault(l => l.AccountId == glId);
                if (linea is null)
                    continue;

                var montoLinea = linea.Debit.Amount > 0 ? linea.Debit.Amount : linea.Credit.Amount;
                var diff       = Math.Abs(montoLinea - mov.Amount);
                if (diff > ToleranciaMonto)
                    continue;

                var okTipo = CoincideTipoMovimiento(mov.TransactionType, linea.Debit.Amount, linea.Credit.Amount);
                if (!okTipo)
                    continue;

                if (Math.Abs((je.Date - mov.TransactionDate).Ticks) > dateToleranceDays.Ticks)
                    continue;

                if (diff < mejorDiff)
                {
                    mejorDiff = diff;
                    mejor     = je.Id;
                    motivo    = $"Monto y fecha cercanos al asiento {je.Reference}.";
                }
            }

            sugerencias.Add(new SugerenciaReconciliationDto(mov.Id, mejor, motivo));
        }

        return Result<IReadOnlyList<SugerenciaReconciliationDto>>.Success(sugerencias);
    }

    private static bool CoincideTipoMovimiento(string tipoMov, decimal debit, decimal credit)
    {
        var t = tipoMov.Trim();
        if (t.Equals("Debito", StringComparison.OrdinalIgnoreCase))
            return debit > 0;
        if (t.Equals("Credito", StringComparison.OrdinalIgnoreCase))
            return credit > 0;
        return false;
    }

    public async Task<Result<bool>> ConciliarMovimientoAsync(
        Guid movimientoId,
        Guid asientoContableId,
        CancellationToken ct)
    {
        var extracto = await _caja.GetBankStatementForTransactionAsync(movimientoId, ct);
        if (extracto is null)
            return Result<bool>.Failure("Movimiento o extracto no encontrado.");

        var m = extracto.Transactions.FirstOrDefault(x => x.Id == movimientoId);
        if (m is null)
            return Result<bool>.Failure("Movimiento no encontrado en el extracto.");

        var asiento = await _accounting.GetJournalEntryByIdAsync(asientoContableId, extracto.TenantId, ct);
        if (asiento is null || asiento.Status != DocumentStatus.Posted)
            return Result<bool>.Failure("Asiento contable no encontrado o no está contabilizado.");

        m.Reconcile(asientoContableId);
        extracto.MarkReconciledIfComplete(_user.UserId);
        await _accounting.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
