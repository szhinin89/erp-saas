using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Contabilidad.UseCases.CreateJournalEntry;
using ERP.Domain.Modules.Contabilidad.Enums;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Asientos automáticos de compra, venta y gasto. Si existe configuración contable por tenant,
/// se usan las cuentas definidas; si no, se mantiene la heurística por tipo de cuenta.
/// </summary>
public sealed class AccountingService : IAccountingService
{
    private readonly IMediator              _mediator;
    private readonly IAccountingRepository  _accountingRepo;
    private readonly ICuentaContableService _cuentaContable;
    private readonly ICurrentTenant         _tenant;

    public AccountingService(
        IMediator mediator,
        IAccountingRepository accountingRepo,
        ICuentaContableService cuentaContable,
        ICurrentTenant tenant)
    {
        _mediator       = mediator;
        _accountingRepo = accountingRepo;
        _cuentaContable = cuentaContable;
        _tenant         = tenant;
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

        var mapping = await _cuentaContable.ObtenerCuentasParaCompraAsync(tenantId, subtotal, iva, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de compra.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.CuentaDebitoPrincipal, subtotal, 0m, "USD"),
            };
            if (iva > 0.01m && m.CuentaIvaDebito.HasValue)
                list.Add(new JournalEntryLineCommand(m.CuentaIvaDebito.Value, iva, 0m, "USD"));
            list.Add(new JournalEntryLineCommand(m.CuentaCreditoPrincipal, 0m, total, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
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
            Reference:   referencia,
            Date:        fecha,
            Description: descripcion,
            Lines:       lines);

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return Result<Guid>.Failure($"Error al crear asiento contable: {result.Error}");

        return Result<Guid>.Success(result.Value!.Id);
    }

    public async Task<Result<Guid>> CrearAsientoVentaAsync(
        Guid     ventaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;

        var mapping = await _cuentaContable.ObtenerCuentasParaVentaAsync(tenantId, subtotal, iva, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de venta.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.CuentaDebitoPrincipal, total, 0m, "USD"),
                new(m.CuentaCreditoPrincipal, 0m, subtotal, "USD"),
            };
            if (iva > 0.01m && m.CuentaIvaCredito.HasValue)
                list.Add(new JournalEntryLineCommand(m.CuentaIvaCredito.Value, 0m, iva, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
            var cuentaCobrar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);
            var cuentaVentas = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Revenue && c.Nature == AccountNature.Credit);

            if (cuentaCobrar is null || cuentaVentas is null)
                return Result<Guid>.Failure(
                    "No se encontraron cuentas contables configuradas para registrar ventas. " +
                    "Configure cuentas en Contabilidad → Configuración de cuentas por empresa, o bien una cuenta de tipo Activo (Débito) " +
                    "y una de tipo Ingreso (Crédito) con movimientos permitidos en el Plan de Cuentas.");

            lines =
            [
                new JournalEntryLineCommand(cuentaCobrar.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaVentas.Id, 0m, total, "USD"),
            ];
        }

        var command = new CreateJournalEntryCommand(
            Reference:   referencia,
            Date:        fecha,
            Description: descripcion,
            Lines:       lines);

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return Result<Guid>.Failure($"Error al crear asiento contable de venta: {result.Error}");

        return Result<Guid>.Success(result.Value!.Id);
    }

    public async Task<Result<Guid>> CrearAsientoGastoAsync(
        Guid     gastoId,
        string   categoriaGasto,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  impuesto,
        decimal  total,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var cuentas  = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);

        var debitoGasto = await _cuentaContable.ObtenerCuentaParaGastoAsync(tenantId, categoriaGasto, ct);
        if (!debitoGasto.IsSuccess)
            return Result<Guid>.Failure(debitoGasto.Error ?? "Error al resolver cuenta de gasto.");

        Guid cuentaGastoId;
        if (debitoGasto.Value.HasValue)
            cuentaGastoId = debitoGasto.Value.Value;
        else
        {
            var cat = (categoriaGasto ?? string.Empty).Trim();
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

        var creditoCaja = await _cuentaContable.ObtenerCuentaCajaParaGastoAsync(tenantId, ct);
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
                    "en la configuración contable del tenant, o una cuenta de tipo Activo (Deudor) con movimientos permitidos.");
            cuentaCajaId = cuentaCaja.Id;
        }

        var command = new CreateJournalEntryCommand(
            Reference:   referencia,
            Date:        fecha,
            Description: descripcion,
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

    public async Task<Result<Guid>> CrearAsientoNotaCreditoVentaAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var mapping    = await _cuentaContable.ObtenerCuentasParaVentaAsync(tenantId, subtotal, iva, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.CuentaCreditoPrincipal, subtotal, 0m, "USD"),
                new(m.CuentaDebitoPrincipal, 0m, total, "USD"),
            };
            if (iva > 0.01m && m.CuentaIvaCredito.HasValue)
                list.Insert(1, new JournalEntryLineCommand(m.CuentaIvaCredito.Value, iva, 0m, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
            var cuentaCobrar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);
            var cuentaVentas = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Revenue && c.Nature == AccountNature.Credit);
            if (cuentaCobrar is null || cuentaVentas is null)
                return Result<Guid>.Failure("No se encontraron cuentas para el asiento de nota de crédito.");

            lines =
            [
                new JournalEntryLineCommand(cuentaVentas.Id, subtotal, 0m, "USD"),
                new JournalEntryLineCommand(cuentaCobrar.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de nota de crédito.");
    }

    public async Task<Result<Guid>> CrearAsientoNotaDebitoVentaAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  iva,
        decimal  total,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var mapping    = await _cuentaContable.ObtenerCuentasParaVentaAsync(tenantId, subtotal, iva, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.CuentaDebitoPrincipal, total, 0m, "USD"),
                new(m.CuentaCreditoPrincipal, 0m, subtotal, "USD"),
            };
            if (iva > 0.01m && m.CuentaIvaCredito.HasValue)
                list.Add(new JournalEntryLineCommand(m.CuentaIvaCredito.Value, 0m, iva, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
            var cuentaCobrar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);
            var cuentaVentas = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Revenue && c.Nature == AccountNature.Credit);
            if (cuentaCobrar is null || cuentaVentas is null)
                return Result<Guid>.Failure("No se encontraron cuentas para el asiento de nota de débito.");

            lines =
            [
                new JournalEntryLineCommand(cuentaCobrar.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaVentas.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de nota de débito.");
    }

    public async Task<Result<Guid>> CrearAsientoRetencionEmitidaAsync(
        Guid     retencionId,
        string   referencia,
        DateTime fecha,
        decimal  totalRetenido,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var cuentas  = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
        var pasivos = cuentas.Where(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit)
            .ToList();

        var cuentaProveedor = pasivos.FirstOrDefault(c =>
            c.Name.Contains("PROVEED", StringComparison.OrdinalIgnoreCase))
            ?? pasivos.FirstOrDefault();

        var cuentaRetencion = pasivos.FirstOrDefault(c =>
            c.Id != cuentaProveedor?.Id &&
            (c.Name.Contains("RETENC", StringComparison.OrdinalIgnoreCase)
             || c.Name.Contains("FUENTE", StringComparison.OrdinalIgnoreCase)))
            ?? pasivos.FirstOrDefault(c => c.Id != cuentaProveedor?.Id);

        if (cuentaProveedor is null || cuentaRetencion is null)
            return Result<Guid>.Failure(
                "Se requieren al menos dos cuentas de pasivo (proveedores y retenciones) para el asiento de retención emitida.");

        var lines = new[]
        {
            new JournalEntryLineCommand(cuentaProveedor.Id, totalRetenido, 0m, "USD"),
            new JournalEntryLineCommand(cuentaRetencion.Id, 0m, totalRetenido, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de retención emitida.");
    }

    public async Task<Result<Guid>> CrearAsientoRetencionRecibidaAsync(
        Guid     retencionId,
        string   referencia,
        DateTime fecha,
        decimal  totalRetenido,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var cuentas  = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);

        var ivaPasivo = cuentas.FirstOrDefault(c =>
            c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit
            && (c.Name.Contains("IVA", StringComparison.OrdinalIgnoreCase)
                || c.Name.Contains("IMPUEST", StringComparison.OrdinalIgnoreCase)));

        var clientes = cuentas.FirstOrDefault(c =>
            c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit
            && (c.Name.Contains("CLIENT", StringComparison.OrdinalIgnoreCase)
                || c.Name.Contains("COBRAR", StringComparison.OrdinalIgnoreCase)));

        clientes ??= cuentas.FirstOrDefault(c =>
            c.IsActive && c.AllowsMovements && c.Type == AccountType.Asset && c.Nature == AccountNature.Debit);

        if (ivaPasivo is null || clientes is null)
            return Result<Guid>.Failure(
                "Se requiere una cuenta de pasivo (IVA/impuestos) y una de activo (clientes) para la retención recibida.");

        var lines = new[]
        {
            new JournalEntryLineCommand(ivaPasivo.Id, totalRetenido, 0m, "USD"),
            new JournalEntryLineCommand(clientes.Id, 0m, totalRetenido, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de retención recibida.");
    }

    public async Task<Result<Guid>> CrearAsientoNotaCreditoCompraProveedorAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  impuesto,
        decimal  total,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var mapping  = await _cuentaContable.ObtenerCuentasParaCompraAsync(tenantId, subtotal, impuesto, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de compra.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.CuentaCreditoPrincipal, total, 0m, "USD"),
                new(m.CuentaDebitoPrincipal, 0m, subtotal, "USD"),
            };
            if (impuesto > 0.01m && m.CuentaIvaDebito.HasValue)
                list.Add(new JournalEntryLineCommand(m.CuentaIvaDebito.Value, 0m, impuesto, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);
            var cuentaPagar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit);
            if (cuentaGasto is null || cuentaPagar is null)
                return Result<Guid>.Failure(
                    "No se encontraron cuentas contables para la nota de crédito de compra (gasto y pasivo).");

            lines =
            [
                new JournalEntryLineCommand(cuentaPagar.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaGasto.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de nota de crédito de compra.");
    }

    public async Task<Result<Guid>> CrearAsientoNotaDebitoCompraProveedorAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  subtotal,
        decimal  impuesto,
        decimal  total,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var mapping  = await _cuentaContable.ObtenerCuentasParaCompraAsync(tenantId, subtotal, impuesto, ct);
        if (!mapping.IsSuccess)
            return Result<Guid>.Failure(mapping.Error ?? "Error al resolver cuentas de compra.");

        JournalEntryLineCommand[] lines;
        if (mapping.Value is not null)
        {
            var m = mapping.Value;
            var list = new List<JournalEntryLineCommand>
            {
                new(m.CuentaDebitoPrincipal, subtotal, 0m, "USD"),
            };
            if (impuesto > 0.01m && m.CuentaIvaDebito.HasValue)
                list.Add(new JournalEntryLineCommand(m.CuentaIvaDebito.Value, impuesto, 0m, "USD"));
            list.Add(new JournalEntryLineCommand(m.CuentaCreditoPrincipal, 0m, total, "USD"));
            lines = list.ToArray();
        }
        else
        {
            var cuentas = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);
            var cuentaGasto = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Expense && c.Nature == AccountNature.Debit);
            var cuentaPagar = cuentas.FirstOrDefault(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit);
            if (cuentaGasto is null || cuentaPagar is null)
                return Result<Guid>.Failure(
                    "No se encontraron cuentas contables para la nota de débito de compra.");

            lines =
            [
                new JournalEntryLineCommand(cuentaGasto.Id, total, 0m, "USD"),
                new JournalEntryLineCommand(cuentaPagar.Id, 0m, total, "USD"),
            ];
        }

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de nota de débito de compra.");
    }

    public async Task<Result<Guid>> CrearAsientoNotaCreditoGastoProveedorAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  total,
        string   categoriaGasto,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var cuentas  = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);

        var pasivos = cuentas.Where(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit)
            .ToList();
        var cuentaProveedor = pasivos.FirstOrDefault(c =>
            c.Name.Contains("PROVEED", StringComparison.OrdinalIgnoreCase))
            ?? pasivos.FirstOrDefault();
        if (cuentaProveedor is null)
            return Result<Guid>.Failure("No se encontró cuenta de proveedores (pasivo) para la nota de crédito de gasto.");

        var debitoGasto = await _cuentaContable.ObtenerCuentaParaGastoAsync(tenantId, categoriaGasto, ct);
        if (!debitoGasto.IsSuccess)
            return Result<Guid>.Failure(debitoGasto.Error ?? "Error al resolver cuenta de gasto.");

        Guid cuentaGastoId;
        if (debitoGasto.Value.HasValue)
            cuentaGastoId = debitoGasto.Value.Value;
        else
        {
            var cat = (categoriaGasto ?? string.Empty).Trim();
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
                return Result<Guid>.Failure("No se encontró cuenta de gasto para la nota de crédito.");
            cuentaGastoId = cuentaGasto.Id;
        }

        var lines = new[]
        {
            new JournalEntryLineCommand(cuentaProveedor.Id, total, 0m, "USD"),
            new JournalEntryLineCommand(cuentaGastoId, 0m, total, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de nota de crédito de gasto.");
    }

    public async Task<Result<Guid>> CrearAsientoNotaDebitoGastoProveedorAsync(
        Guid     notaId,
        string   referencia,
        DateTime fecha,
        decimal  total,
        string   categoriaGasto,
        string   descripcion,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var cuentas  = await _accountingRepo.GetAllByTenantAsync(tenantId, ct);

        var pasivos = cuentas.Where(c =>
                c.IsActive && c.AllowsMovements && c.Type == AccountType.Liability && c.Nature == AccountNature.Credit)
            .ToList();
        var cuentaProveedor = pasivos.FirstOrDefault(c =>
            c.Name.Contains("PROVEED", StringComparison.OrdinalIgnoreCase))
            ?? pasivos.FirstOrDefault();
        if (cuentaProveedor is null)
            return Result<Guid>.Failure("No se encontró cuenta de proveedores (pasivo) para la nota de débito de gasto.");

        var debitoGasto = await _cuentaContable.ObtenerCuentaParaGastoAsync(tenantId, categoriaGasto, ct);
        if (!debitoGasto.IsSuccess)
            return Result<Guid>.Failure(debitoGasto.Error ?? "Error al resolver cuenta de gasto.");

        Guid cuentaGastoId;
        if (debitoGasto.Value.HasValue)
            cuentaGastoId = debitoGasto.Value.Value;
        else
        {
            var cat = (categoriaGasto ?? string.Empty).Trim();
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
                return Result<Guid>.Failure("No se encontró cuenta de gasto para la nota de débito.");
            cuentaGastoId = cuentaGasto.Id;
        }

        var lines = new[]
        {
            new JournalEntryLineCommand(cuentaGastoId, total, 0m, "USD"),
            new JournalEntryLineCommand(cuentaProveedor.Id, 0m, total, "USD"),
        };

        var result = await _mediator.Send(
            new CreateJournalEntryCommand(referencia, fecha, descripcion, lines), ct);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.Id)
            : Result<Guid>.Failure(result.Error ?? "Error al crear asiento de nota de débito de gasto.");
    }
}
