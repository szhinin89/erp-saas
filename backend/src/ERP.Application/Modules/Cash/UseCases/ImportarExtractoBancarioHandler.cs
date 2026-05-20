using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ImportarBankStatementCommand(
    Guid BankAccountId,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<StatementParseRow> Rows) : IRequest<Result<BankStatementDto>>;

public sealed class ImportarBankStatementCommandHandler
    : IRequestHandler<ImportarBankStatementCommand, Result<BankStatementDto>>
{
    private readonly ICashRepository _caja;
    private readonly ICurrentSubscriber _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public ImportarBankStatementCommandHandler(
        ICashRepository caja,
        ICurrentSubscriber tenant,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<BankStatementDto>> Handle(ImportarBankStatementCommand cmd, CancellationToken ct)
    {
        var cuenta = await _caja.GetBankAccountByIdAsync(cmd.BankAccountId, ct);
        if (cuenta is null || !cuenta.IsActive)
            return Result<BankStatementDto>.Failure("Cuenta bancaria no encontrada o inactiva.");

        if (cmd.Rows.Count == 0)
            return Result<BankStatementDto>.Failure("No hay movimientos para importar.");

        decimal net = 0;
        foreach (var r in cmd.Rows)
        {
            if (r.TransactionType.Equals("Credito", StringComparison.OrdinalIgnoreCase))
                net += r.Amount;
            else if (r.TransactionType.Equals("Debito", StringComparison.OrdinalIgnoreCase))
                net -= r.Amount;
            else
                return Result<BankStatementDto>.Failure($"Tipo de movimiento no válido: {r.TransactionType}");
        }

        var esperado = cmd.OpeningBalance + net;
        if (Math.Abs(esperado - cmd.ClosingBalance) > 0.05m)
        {
            return Result<BankStatementDto>.Failure(
                $"El saldo final del extracto ({cmd.ClosingBalance:F2}) no coincide con saldo inicial + movimientos ({esperado:F2}).");
        }

        var extracto = BankStatement.Create(
            _tenant.SubscriberId,
            cmd.BankAccountId,
            cmd.PeriodFrom,
            cmd.PeriodTo,
            cmd.OpeningBalance,
            cmd.ClosingBalance,
            _user.UserId);

        foreach (var r in cmd.Rows.OrderBy(x => x.TransactionDate))
            extracto.AddTransaction(r.TransactionDate, r.Description, r.Amount, r.TransactionType, r.Reference, _user.UserId);

        await _caja.AddBankStatementAsync(extracto, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<BankStatementDto>.Success(
            new BankStatementDto(
                extracto.Id,
                extracto.BankAccountId,
                extracto.PeriodFrom,
                extracto.PeriodTo,
                extracto.OpeningBalance,
                extracto.ClosingBalance,
                extracto.LoadedAt,
                extracto.IsReconciled,
                extracto.Transactions.Count));
    }
}
