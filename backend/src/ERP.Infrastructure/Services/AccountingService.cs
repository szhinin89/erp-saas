using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Accounting.UseCases.CreateJournalEntry;
using ERP.Domain.Accounting.Enums;
using ERP.Domain.Accounting.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Integración contable para el módulo de Compras.
/// Busca automáticamente la primera cuenta de Gasto (débito) y la primera
/// cuenta de Pasivo (crédito — Cuentas por Pagar) activas del tenant.
/// Si no encuentra cuentas configuradas, retorna Failure sin bloquear la aprobación.
/// </summary>
public sealed class AccountingService : IAccountingService
{
    private readonly IMediator              _mediator;
    private readonly IAccountingRepository  _accountingRepo;
    private readonly ICurrentTenant         _tenant;
    private readonly ICurrentUser           _user;

    public AccountingService(
        IMediator mediator,
        IAccountingRepository accountingRepo,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _mediator       = mediator;
        _accountingRepo = accountingRepo;
        _tenant         = tenant;
        _user           = user;
    }

    public async Task<Result<Guid>> CrearAsientoCompraAsync(
        Guid     compraId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;

        // Buscar cuenta de Gasto (débito)
        var cuentas = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
        var cuentaGasto = cuentas.FirstOrDefault(c =>
            c.IsActive && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);

        // Buscar cuenta de Pasivo — Cuentas por Pagar (crédito)
        var cuentaPagar = cuentas.FirstOrDefault(c =>
            c.IsActive && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit);

        if (cuentaGasto is null || cuentaPagar is null)
            return Result<Guid>.Failure(
                "No se encontraron cuentas contables configuradas para registrar compras. " +
                "Configure una cuenta de tipo Gasto (Débito) y una de tipo Pasivo (Crédito) en el Plan de Cuentas.");

        var command = new CreateJournalEntryCommand(
            Reference:   referencia,
            Date:        fecha,
            Description: descripcion,
            Lines: new[]
            {
                new JournalEntryLineCommand(cuentaGasto.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaPagar.Id, 0m, total, "USD"),
            });

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return Result<Guid>.Failure($"Error al crear asiento contable: {result.Error}");

        return Result<Guid>.Success(result.Value!.Id);
    }
}
