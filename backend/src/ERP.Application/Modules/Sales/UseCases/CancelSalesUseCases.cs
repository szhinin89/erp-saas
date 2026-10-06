using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.Policies;
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
    private readonly ISalesReturnRepository _returnRepo;
    private readonly IStockRepository _stockRepo;
    private readonly ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository _edocRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly ICompanyClock _companyClock;
    private readonly IUnitOfWork _uow;

    public CancelSalesInvoiceHandler(
        ISalesInvoiceRepository repo,
        ISalesReceivableRepository rxRepo,
        ISalesReturnRepository returnRepo,
        IStockRepository stockRepo,
        ERP.Domain.Modules.ElectronicDocuments.Interfaces.IElectronicDocumentRepository edocRepo,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u,
        ICompanyClock companyClock,
        IUnitOfWork uow
    )
    {
        _repo = repo;
        _rxRepo = rxRepo;
        _returnRepo = returnRepo;
        _stockRepo = stockRepo;
        _edocRepo = edocRepo;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
        _companyClock = companyClock;
        _uow = uow;
    }

    /// <remarks>
    /// ZH-SALES-CANCEL-COLLECTION-CONCURRENCY-01 — la decisión de anular y todos sus efectos (CxC,
    /// Kardex, reverso contable, outbox) ocurren en UNA transacción, con la factura y su CxC
    /// bloqueadas (FOR UPDATE) ANTES de validar. Regla vigente preservada: solo se anula si la CxC
    /// no tiene cobros registrados. Un cobro concurrente espera el lock de la CxC y ve la CxC ya
    /// anulada (rechazo canónico); una anulación que llega después de un cobro lo ve registrado.
    /// Orden de locks: factura → CxC → secuencias (Kardex, asientos en SaveChanges).
    /// </remarks>
    public async Task<Result<SalesInvoiceDto>> Handle(
        CancelSalesInvoiceCommand cmd,
        CancellationToken ct
    )
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var result = await CancelAsync(cmd, ct);
            if (result.IsSuccess)
                await _uow.CommitAsync(ct);
            else
                await _uow.RollbackAsync(ct);
            return result;
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<Result<SalesInvoiceDto>> CancelAsync(
        CancelSalesInvoiceCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdForUpdateAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<SalesInvoiceDto>.NotFound("Factura no encontrada.");

        // ── Devoluciones (ZH-SALES-CANCEL-AUTHORIZED-RETURN-RULE-01) — bajo el lock de la
        // factura, antes de cualquier efecto: la autorización de una devolución toma el mismo lock,
        // así que esta consulta ve toda devolución ya autorizada y ninguna puede autorizarse después.
        SalesInvoiceCancellationPolicy.EnsureCanCancel(
            await _returnRepo.ExistsAuthorizedBySalesInvoiceIdAsync(_t.TenantId, inv.Id, ct)
        );

        // ── Cancelar CxC asociada si existe (bloqueada antes de validar sus cobros) ──
        var receivable = await _rxRepo.GetByInvoiceIdForUpdateAsync(_t.TenantId, inv.Id, ct);
        if (receivable is not null)
        {
            try
            {
                receivable.Cancel(_u.UserId);
            }
            catch (DomainRuleViolationException ex)
            {
                // Mismo mensaje público de siempre, con el contexto de la anulación; la traducción a
                // Result sigue siendo la única (DomainRuleBehavior → DOMAIN_RULE_VIOLATION).
                throw new DomainRuleViolationException($"No se puede anular: {ex.Message}", ex);
            }
        }

        inv.Cancel(cmd.Reason, _u.UserId);

        // ── Revertir inventario (Kardex) ────────────────────────────
        // WarehouseId solo está poblado en líneas que sí generaron egreso al autorizar.
        // SALES-PRESENTATIONS-02: debe revertir exactamente lo que se descontó al autorizar
        // (QuantityInBaseUom/BaseUomCode) — nunca Quantity/UomCode crudos, o el stock queda
        // desincronizado en cuanto la línea tenga ConversionFactor != 1.
        var effectiveDate = await _companyClock.TodayAsync(_c.CompanyId, _t.TenantId, ct);
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
                line.QuantityInBaseUom,
                line.BaseUomCode,
                effectiveDate,
                $"ANULACIÓN: {inv.InvoiceNumber}",
                inv.Id,
                "SalesInvoice",
                _u.UserId,
                cancellationToken: ct,
                sourceDocLineId: line.Id
            );
        }

        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        // Fase 10: ElectronicDocument es la única fuente de verdad del estado electrónico.
        var edoc = await _edocRepo.GetBySourceAsync(_t.TenantId, "Sales", inv.Id, ct);
        return Result<SalesInvoiceDto>.Success(SalesMapper.ToDto(inv, edoc));
    }
}
