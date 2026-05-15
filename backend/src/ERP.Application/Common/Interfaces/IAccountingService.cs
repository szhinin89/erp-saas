using ERP.Application.Common;

namespace ERP.Application.Common.Interfaces;

public interface IAccountingService
{
    Task<Result<Guid>> CrearAsientoCompraAsync(
        Guid     purchBillId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoVentaAsync(
        Guid     salesBillId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoGastoAsync(
        Guid     expenseId,
        string   category,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoNotaCreditoVentaAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoNotaDebitoVentaAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoRetencionEmitidaAsync(
        Guid     retentionId,
        string   reference,
        DateTime date,
        decimal  totalRetained,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoRetencionRecibidaAsync(
        Guid     retentionId,
        string   reference,
        DateTime date,
        decimal  totalRetained,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoNotaCreditoCompraProveedorAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoNotaDebitoCompraProveedorAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoNotaCreditoGastoProveedorAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  total,
        string   category,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CrearAsientoNotaDebitoGastoProveedorAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  total,
        string   category,
        string   description,
        CancellationToken ct);
}
