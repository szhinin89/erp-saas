using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

public sealed record CancelSalesInvoiceCommand(Guid InvoiceId, string Reason)
    : IRequest<Result<SalesInvoiceDto>>,
        IBranchScopedRequest;

public sealed class CancelSalesInvoiceHandler
    : IRequestHandler<CancelSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly ISalesInvoiceRepository _repo;
    private readonly ISalesReceivableRepository _rxRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository _edocRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CancelSalesInvoiceHandler(
        ISalesInvoiceRepository repo,
        ISalesReceivableRepository rxRepo,
        IStockRepository stockRepo,
        ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository edocRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _rxRepo = rxRepo;
        _stockRepo = stockRepo;
        _edocRepo = edocRepo;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(
        CancelSalesInvoiceCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        // ── Cancelar CxC asociada si existe ────────────────────────
        var receivable = await _rxRepo.GetByInvoiceIdAsync(_t.TenantId, inv.Id, ct);
        if (receivable is not null)
        {
            try
            {
                receivable.Cancel(_u.UserId);
            }
            catch (InvalidOperationException ex)
            {
                return Result<SalesInvoiceDto>.ValidationFailure(
                    $"No se puede anular: {ex.Message}"
                );
            }
        }

        try
        {
            inv.Cancel(cmd.Reason, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<SalesInvoiceDto>.ValidationFailure(ex.Message);
        }

        // ── Revertir inventario (Kardex) ────────────────────────────
        // WarehouseId solo está poblado en líneas que sí generaron egreso al autorizar.
        foreach (var line in inv.Lines)
        {
            if (line.ItemId is null || line.WarehouseId is null)
                continue;

            await _stockRepo.AppendMovementAsync(
                _t.TenantId,
                _c.CompanyId,
                line.ItemId.Value,
                line.WarehouseId.Value,
                StockMovementType.SaleReturn,
                line.Quantity,
                line.UomCode,
                DateOnly.FromDateTime(DateTime.UtcNow),
                $"ANULACIÓN: {inv.InvoiceNumber}",
                inv.Id,
                "SalesInvoice",
                _u.UserId,
                cancellationToken: ct
            );
        }

        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        // Fase 10: ElectronicDocument es la única fuente de verdad del estado electrónico.
        var edoc = await _edocRepo.GetBySourceAsync(_t.TenantId, "Sales", inv.Id, ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv, edoc));
    }
}
