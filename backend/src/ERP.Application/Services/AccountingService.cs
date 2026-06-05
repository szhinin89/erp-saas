using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Services;

/// <summary>
/// Asientos automáticos de compra, venta y gasto. Si existe configuración contable por tenant,
/// se usan las cuentas definidas; si no, se mantiene la heurística por tipo de cuenta.
/// </summary>
public sealed class AccountingService : IAccountingService
{
    private readonly IMediator              _mediator;
    private readonly IAccountingRepository  _accountingRepo;
    private readonly ILedgerAccountService _cuentaContable;
    private readonly ICurrentSubscriber         _subscriber;

    public AccountingService(
        IMediator mediator,
        IAccountingRepository accountingRepo,
        ILedgerAccountService cuentaContable,
        ICurrentSubscriber subscriber)
    {
        _mediator       = mediator;
        _accountingRepo = accountingRepo;
        _cuentaContable = cuentaContable;
        _subscriber = subscriber;
    }

    public async Task<Result<Guid>> CreatePurchaseJournalEntryAsync(
        Guid     purchBillId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;

        var mapping = await _cuentaContable.GetAccountsForPurchaseAsync(subscriberId, subtotal, vatTotal, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de compra.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.MainDebitAccountId, subtotal, 0m, "USD"),
            };
            if (vatTotal > 0.01m && m.VatDebitAccountId.HasValue)
                list.Add(new JournalEntryLineCommand(m.VatDebitAccountId.Value, vatTotal, 0m, "USD"));
            list.Add(new JournalEntryLineCommand(m.MainCreditAccountId, 0m, total, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);
            var cuentaPagar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit);

            if (cuentaGasto is null || cuentaPagar is null)
                return Result<Guid>.Failure(
                    "No se encontraron cuentas contables configuradas para registrar compras. " +
                    "Configure cuentas en Contabilidad → Configuración de cuentas por empresa, o bien una cuenta de tipo Gasto (Débito) " +
                    "y una de tipo Pasivo (Crédito) con movimientos permitidos en el Plan de Cuentas.");

            lines =
            [
                new JournalEntryLineCommand(cuentaGasto.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaPagar.Id, 0m, total, "USD"),
            ];
        }

        var command = new CreateJournalEntryCommand(
            Reference:   reference,
            Date:        date,
            Description: description,
            Lines:       lines);

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return Result<Guid>.Failure($"Error al crear asiento contable: {result.Error}");

        return Result<Guid>.Success(result.Value!.Id);
    }

    public async Task<Result<Guid>> CreateSalesJournalEntryAsync(
        Guid     salesBillId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;

        var mapping = await _cuentaContable.GetAccountsForSaleAsync(subscriberId, subtotal, vatTotal, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de venta.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.MainDebitAccountId, total, 0m, "USD"),
                new(m.MainCreditAccountId, 0m, subtotal, "USD"),
            };
            if (vatTotal > 0.01m && m.VatCreditAccountId.HasValue)
                list.Add(new JournalEntryLineCommand(m.VatCreditAccountId.Value, 0m, vatTotal, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);
            var cuentaCobrar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);
            var cuentaVentas = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Revenue && c.Nature == AccountNature.Credit);

            if (cuentaCobrar is null || cuentaVentas is null)
                return Result<Guid>.Failure(
                    "No se encontraron cuentas contables configuradas para registrar ventas. " +
                    "Configure cuentas en Contabilidad → Configuración de cuentas por empresa, o bien una cuenta de tipo IsActive (Débito) " +
                    "y una de tipo Ingreso (Crédito) con movimientos permitidos en el Plan de Cuentas.");

            lines =
            [
                new JournalEntryLineCommand(cuentaCobrar.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaVentas.Id, 0m, total, "USD"),
            ];
        }

        var command = new CreateJournalEntryCommand(
            Reference:   reference,
            Date:        date,
            Description: description,
            Lines:       lines);

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return Result<Guid>.Failure($"Error al crear asiento contable de venta: {result.Error}");

        return Result<Guid>.Success(result.Value!.Id);
    }

    public async Task<Result<Guid>> CreateExpenseJournalEntryAsync(
        Guid     expenseId,
        string   category,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var cuentas  = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);

        var debitoGasto = await _cuentaContable.GetAccountForExpenseAsync(subscriberId, category, ct);
        if (!debitoGasto.IsSuccess)
            return Result<Guid>.Failure(debitoGasto.Error ?? "Error al resolver cuenta de gasto.");

        Guid cuentaGastoId;
        if (debitoGasto.Value.HasValue)
            cuentaGastoId = debitoGasto.Value.Value;
        else
        {
            var cat = (category ?? string.Empty).Trim();
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive
                && c.AllowsMovements
                && c.Type == AccountType.Expense
                && c.Nature == AccountNature.Debit
                && !string.IsNullOrEmpty(cat)
                && c.Name.Contains(cat, StringComparison.OrdinalIgnoreCase));

            cuentaGasto ??= cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);

            if (cuentaGasto is null)
                return Result<Guid>.Failure(
                    "No se encontró cuenta de gasto. Configure un mapeo por categoría en Contabilidad → Gastos " +
                    "o cree una cuenta de tipo Gasto (Deudor) con movimientos permitidos.");

            cuentaGastoId = cuentaGasto.Id;
        }

        var creditoCaja = await _cuentaContable.GetCashAccountForExpenseAsync(subscriberId, ct);
        if (!creditoCaja.IsSuccess)
            return Result<Guid>.Failure(creditoCaja.Error ?? "Error al resolver cuenta de caja/banco.");

        Guid cuentaCajaId;
        if (creditoCaja.Value.HasValue)
            cuentaCajaId = creditoCaja.Value.Value;
        else
        {
            var cuentaCaja = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);
            if (cuentaCaja is null)
                return Result<Guid>.Failure(
                    "No se encontró cuenta de caja/banco para el crédito. Configure Cuenta de efectivo o Banco " +
                    "en la configuración contable del tenant, o una cuenta de tipo IsActive (Deudor) con movimientos permitidos.");
            cuentaCajaId = cuentaCaja.Id;
        }

        var command = new CreateJournalEntryCommand(
            Reference:   reference,
            Date:        date,
            Description: description,
            Lines:
            [
                new JournalEntryLineCommand(cuentaGastoId, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaCajaId, 0m, total, "USD"),
            ]);

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return Result<Guid>.Failure($"Error al crear asiento contable: {result.Error}");

        return Result<Guid>.Success(result.Value!.Id);
    }

    public async Task<Result<Guid>> CreateSalesCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var mapping    = await _cuentaContable.GetAccountsForSaleAsync(subscriberId, subtotal, vatTotal, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.MainCreditAccountId, subtotal, 0m, "USD"),
                new(m.MainDebitAccountId, 0m, total, "USD"),
            };
            if (vatTotal > 0.01m && m.VatCreditAccountId.HasValue)
                list.Insert(1, new JournalEntryLineCommand(m.VatCreditAccountId.Value, vatTotal, 0m, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);
            var cuentaCobrar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);
            var cuentaVentas = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Revenue && c.Nature == AccountNature.Credit);
            if (cuentaCobrar is null || cuentaVentas is null)
                return Result<Guid>.Failure("No se encontraron cuentas para el asiento de note de crédito.");

            lines =
            [
                new JournalEntryLineCommand(cuentaVentas.Id, subtotal, 0m, "USD"),
                new JournalEntryLineCommand(cuentaCobrar.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de note de crédito.");
    }

    public async Task<Result<Guid>> CreateSalesDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var mapping    = await _cuentaContable.GetAccountsForSaleAsync(subscriberId, subtotal, vatTotal, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.MainDebitAccountId, total, 0m, "USD"),
                new(m.MainCreditAccountId, 0m, subtotal, "USD"),
            };
            if (vatTotal > 0.01m && m.VatCreditAccountId.HasValue)
                list.Add(new JournalEntryLineCommand(m.VatCreditAccountId.Value, 0m, vatTotal, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);
            var cuentaCobrar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);
            var cuentaVentas = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Revenue && c.Nature == AccountNature.Credit);
            if (cuentaCobrar is null || cuentaVentas is null)
                return Result<Guid>.Failure("No se encontraron cuentas para el asiento de note de débito.");

            lines =
            [
                new JournalEntryLineCommand(cuentaCobrar.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaVentas.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de note de débito.");
    }

    public async Task<Result<Guid>> CreateIssuedWithholdingJournalEntryAsync(
        Guid     retentionId,
        string   reference,
        DateTime date,
        decimal  totalRetained,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var cuentas  = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);
        var pasivos = cuentas.Where(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit)
            .ToList();

        var cuentaSupplier = pasivos.FirstOrDefault(c =>
            c.Name.Contains("PROVEED", StringComparison.OrdinalIgnoreCase))
            ?? pasivos.FirstOrDefault();

        var cuentaRetencion = pasivos.FirstOrDefault(c =>
            c.Id != cuentaSupplier?.Id &&
            (c.Name.Contains("RETENC", StringComparison.OrdinalIgnoreCase)
             || c.Name.Contains("FUENTE", StringComparison.OrdinalIgnoreCase)))
            ?? pasivos.FirstOrDefault(c => c.Id != cuentaSupplier?.Id);

        if (cuentaSupplier is null || cuentaRetencion is null)
            return Result<Guid>.Failure(
                "Se requieren al menos dos cuentas de pasivo (proveedores y retenciones) para el asiento de retención emitida.");

        var lines = new[]
        {
            new JournalEntryLineCommand(cuentaSupplier.Id, totalRetained, 0m, "USD"),
            new JournalEntryLineCommand(cuentaRetencion.Id, 0m, totalRetained, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de retención emitida.");
    }

    public async Task<Result<Guid>> CreateReceivedWithholdingJournalEntryAsync(
        Guid     retentionId,
        string   reference,
        DateTime date,
        decimal  totalRetained,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var cuentas  = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);

        var ivaPasivo = cuentas.FirstOrDefault(c =>
            c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit
            && (c.Name.Contains("vatTotal", StringComparison.OrdinalIgnoreCase)
                || c.Name.Contains("IMPUEST", StringComparison.OrdinalIgnoreCase)));

        var clientes = cuentas.FirstOrDefault(c =>
            c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit
            && (c.Name.Contains("CLIENT", StringComparison.OrdinalIgnoreCase)
                || c.Name.Contains("COBRAR", StringComparison.OrdinalIgnoreCase)));

        clientes ??= cuentas.FirstOrDefault(c =>
            c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);

        if (ivaPasivo is null || clientes is null)
            return Result<Guid>.Failure(
                "Se requiere una cuenta de pasivo (vatTotal/impuestos) y una de activo (clientes) para la retención recibida.");

        var lines = new[]
        {
            new JournalEntryLineCommand(ivaPasivo.Id, totalRetained, 0m, "USD"),
            new JournalEntryLineCommand(clientes.Id, 0m, totalRetained, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de retención recibida.");
    }

    public async Task<Result<Guid>> CreatePurchaseSupplierCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var mapping  = await _cuentaContable.GetAccountsForPurchaseAsync(subscriberId, subtotal, vatTotal, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de compra.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.MainCreditAccountId, total, 0m, "USD"),
                new(m.MainDebitAccountId, 0m, subtotal, "USD"),
            };
            if (vatTotal > 0.01m && m.VatDebitAccountId.HasValue)
                list.Add(new JournalEntryLineCommand(m.VatDebitAccountId.Value, 0m, vatTotal, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);
            var cuentaPagar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit);
            if (cuentaGasto is null || cuentaPagar is null)
                return Result<Guid>.Failure(
                    "No se encontraron cuentas contables para la note de crédito de compra (gasto y pasivo).");

            lines =
            [
                new JournalEntryLineCommand(cuentaPagar.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaGasto.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de note de crédito de compra.");
    }

    public async Task<Result<Guid>> CreatePurchaseSupplierDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var mapping  = await _cuentaContable.GetAccountsForPurchaseAsync(subscriberId, subtotal, vatTotal, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de compra.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.MainDebitAccountId, subtotal, 0m, "USD"),
            };
            if (vatTotal > 0.01m && m.VatDebitAccountId.HasValue)
                list.Add(new JournalEntryLineCommand(m.VatDebitAccountId.Value, vatTotal, 0m, "USD"));
            list.Add(new JournalEntryLineCommand(m.MainCreditAccountId, 0m, total, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);
            var cuentaPagar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit);
            if (cuentaGasto is null || cuentaPagar is null)
                return Result<Guid>.Failure(
                    "No se encontraron cuentas contables para la note de débito de compra.");

            lines =
            [
                new JournalEntryLineCommand(cuentaGasto.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaPagar.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de note de débito de compra.");
    }

    public async Task<Result<Guid>> CreateExpenseSupplierCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  total,
        string   category,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var cuentas  = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);

        var pasivos = cuentas.Where(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit)
            .ToList();
        var cuentaSupplier = pasivos.FirstOrDefault(c =>
            c.Name.Contains("PROVEED", StringComparison.OrdinalIgnoreCase))
            ?? pasivos.FirstOrDefault();
        if (cuentaSupplier is null)
            return Result<Guid>.Failure("No se encontró cuenta de proveedores (pasivo) para la note de crédito de gasto.");

        var debitoGasto = await _cuentaContable.GetAccountForExpenseAsync(subscriberId, category, ct);
        if (!debitoGasto.IsSuccess)
            return Result<Guid>.Failure(debitoGasto.Error ?? "Error al resolver cuenta de gasto.");

        Guid cuentaGastoId;
        if (debitoGasto.Value.HasValue)
            cuentaGastoId = debitoGasto.Value.Value;
        else
        {
            var cat = (category ?? string.Empty).Trim();
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive
                && c.AllowsMovements
                && c.Type == AccountType.Expense
                && c.Nature == AccountNature.Debit
                && !string.IsNullOrEmpty(cat)
                && c.Name.Contains(cat, StringComparison.OrdinalIgnoreCase));
            cuentaGasto ??= cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);
            if (cuentaGasto is null)
                return Result<Guid>.Failure("No se encontró cuenta de gasto para la note de crédito.");
            cuentaGastoId = cuentaGasto.Id;
        }

        var lines = new[]
        {
            new JournalEntryLineCommand(cuentaSupplier.Id, total, 0m, "USD"),
            new JournalEntryLineCommand(cuentaGastoId, 0m, total, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de note de crédito de gasto.");
    }

    public async Task<Result<Guid>> CreateExpenseSupplierDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  total,
        string   category,
        string   description,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var cuentas  = await _accountingRepo.GetAllBySubscriberAsync(subscriberId, ct);

        var pasivos = cuentas.Where(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit)
            .ToList();
        var cuentaSupplier = pasivos.FirstOrDefault(c =>
            c.Name.Contains("PROVEED", StringComparison.OrdinalIgnoreCase))
            ?? pasivos.FirstOrDefault();
        if (cuentaSupplier is null)
            return Result<Guid>.Failure("No se encontró cuenta de proveedores (pasivo) para la note de débito de gasto.");

        var debitoGasto = await _cuentaContable.GetAccountForExpenseAsync(subscriberId, category, ct);
        if (!debitoGasto.IsSuccess)
            return Result<Guid>.Failure(debitoGasto.Error ?? "Error al resolver cuenta de gasto.");

        Guid cuentaGastoId;
        if (debitoGasto.Value.HasValue)
            cuentaGastoId = debitoGasto.Value.Value;
        else
        {
            var cat = (category ?? string.Empty).Trim();
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive
                && c.AllowsMovements
                && c.Type == AccountType.Expense
                && c.Nature == AccountNature.Debit
                && !string.IsNullOrEmpty(cat)
                && c.Name.Contains(cat, StringComparison.OrdinalIgnoreCase));
            cuentaGasto ??= cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);
            if (cuentaGasto is null)
                return Result<Guid>.Failure("No se encontró cuenta de gasto para la note de débito.");
            cuentaGastoId = cuentaGasto.Id;
        }

        var lines = new[]
        {
            new JournalEntryLineCommand(cuentaGastoId, total, 0m, "USD"),
            new JournalEntryLineCommand(cuentaSupplier.Id, 0m, total, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(reference, date, description, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de note de débito de gasto.");
    }
}

