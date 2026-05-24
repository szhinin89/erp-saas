using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;

public sealed class ApprovePurchaseCommandHandler
    : IRequestHandler<ApprovePurchaseCommand, Result<PurchBillDto>>
{
    private readonly IPurchBillRepository          _repo;
    private readonly IAccountingService         _accounting;
    private readonly IUserActivityRepository    _activity;
    private readonly ICurrentSubscriber             _tenant;
    private readonly ICurrentUser               _user;
    private readonly IUnitOfWork                _unitOfWork;
    private readonly ILogger<ApprovePurchaseCommandHandler> _logger;

    public ApprovePurchaseCommandHandler(
        IPurchBillRepository repo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentSubscriber tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<ApprovePurchaseCommandHandler> logger)
    {
        _repo       = repo;
        _accounting = accounting;
        _activity   = activity;
        _tenant     = tenant;
        _user       = user;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<Result<PurchBillDto>> Handle(
        ApprovePurchaseCommand command,
        CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var userId   = _user.UserId;

        var compra = await _repo.GetByIdAsync(subscriberId, command.PurchBillId, ct);
        if (compra is null)
            return Result<PurchBillDto>.Failure("Compra no encontrada.");

        if (compra.Status != PurchaseStatus.Validated)
            return Result<PurchBillDto>.Failure(
                $"Solo se puede aprobar una compra Validada (estado actual: {compra.Status}).");

        var asignaciones =
            await _repo.GetWarehouseAllocsByBillIdAsync(subscriberId, command.PurchBillId, ct);

        var stockLines = new List<PurchBillApprovedStockLine>();
        foreach (var asig in asignaciones)
        {
            var detalle = compra.Lines.FirstOrDefault(d => d.Id == asig.PurchBillLineId);
            if (detalle is null)
            {
                _logger.LogWarning(
                    "Compra {CompraId}: asignación huérfana (detalle {DetalleId} no encontrado en la factura).",
                    compra.Id, asig.PurchBillLineId);
                continue;
            }

            if (!asig.ProductId.HasValue)
            {
                _logger.LogWarning(
                    "Compra {CompraId}: línea sin producto enlazado; no se actualiza inventario físico (detalle {DetalleId}, Warehouse {BodegaId}, cantidad {Cantidad}).",
                    compra.Id, asig.PurchBillLineId, asig.WarehouseId, asig.Quantity);
                continue;
            }

            var unitCost = detalle.Quantity > 0
                ? detalle.UnitPrice * (1 - detalle.DiscountPct / 100m)
                : 0m;

            stockLines.Add(new PurchBillApprovedStockLine(
                asig.PurchBillLineId,
                asig.ProductId,
                asig.WarehouseId,
                asig.Quantity,
                unitCost));
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var asientoResult = await _accounting.CrearAsientoCompraAsync(
                compra.Id,
                reference:  compra.InvoiceNumber,
                date:       compra.InvoiceDate,
                subtotal:    compra.Subtotal,
                vatTotal:         compra.VatTotal,
                total:       compra.Total,
                description: $"Compra {compra.InvoiceNumber} — {compra.BusinessPartnerId}",
                ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<PurchBillDto>.Failure(
                    asientoResult.Error ?? "No se pudo registrar el asiento contable de la compra.");
            }

            var asientoId = asientoResult.Value;

            compra.Approve(userId, asientoId, stockLines);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.aprobar",
                entityType: "PurchBill", entityId: compra.Id,
                description: $"{compra.InvoiceNumber} — asiento: {asientoId}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Compra aprobada: id {CompraId}, tenant {SubscriberId}, usuario {UserId}, asiento {JournalEntryId}.",
                compra.Id, subscriberId, userId, asientoId);

            return Result<PurchBillDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al aprobar compra {CompraId}", command.PurchBillId);
            return Result<PurchBillDto>.Failure($"No se pudo aprobar la compra: {ex.Message}");
        }
    }

    private static PurchBillDto ToDto(ERP.Domain.Modules.Purchasing.Entities.PurchBill c) => new(
        c.Id, c.BusinessPartnerId, c.InvoiceNumber, c.AccessKey, c.XmlPath,
        c.InvoiceDate, c.DueDate, c.Status, c.PaymentTerms,
        c.Subtotal, c.VatTotal, c.Total, c.Notes, c.JournalEntryId, c.CreatedAt);
}
