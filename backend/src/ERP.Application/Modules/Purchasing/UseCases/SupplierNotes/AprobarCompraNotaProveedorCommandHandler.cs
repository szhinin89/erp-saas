using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Events;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed class ApprovePurchaseSupplierNoteCommandHandler
    : IRequestHandler<ApprovePurchaseSupplierNoteCommand, Result<SupplierPurchaseNoteDto>>
{
    private readonly IPurchBillRepository       _compraRepo;
    private readonly IExpenseInvoiceRepository _gastoRepo;
    private readonly IAccountingService     _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork           _unitOfWork;
    private readonly ILogger<ApprovePurchaseSupplierNoteCommandHandler> _logger;

    public ApprovePurchaseSupplierNoteCommandHandler(
        IPurchBillRepository compraRepo,
        IExpenseInvoiceRepository gastoRepo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentSubscriber tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<ApprovePurchaseSupplierNoteCommandHandler> logger)
    {
        _compraRepo   = compraRepo;
        _gastoRepo    = gastoRepo;
        _accounting   = accounting;
        _activity     = activity;
        _tenant       = tenant;
        _user         = user;
        _unitOfWork   = unitOfWork;
        _logger       = logger;
    }

    public async Task<Result<SupplierPurchaseNoteDto>> Handle(
        ApprovePurchaseSupplierNoteCommand command,
        CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var userId   = _user.UserId;

        var nota = await _compraRepo.GetPurchNoteByIdWithLinesAsync(subscriberId, command.NotaId, ct);
        if (nota is null)
            return Result<SupplierPurchaseNoteDto>.Failure("Nota de Supplier no encontrada.");

        if (nota.Status != "Borrador")
            return Result<SupplierPurchaseNoteDto>.Failure(
                $"Solo se puede aprobar una nota en Borrador (estado: {nota.Status}).");

        if (!nota.PurchBillId.HasValue && !nota.ExpenseInvoiceId.HasValue)
            return Result<SupplierPurchaseNoteDto>.Failure(
                "Vincule la nota a una factura de compra o de gasto antes de aprobar.");

        var numeroNota = $"{nota.EstabCode}-{nota.EmPointCode}-{nota.Sequential}";
        var descripcionBase = $"Nota {nota.NoteType} Supplier {numeroNota} (clave {nota.AccessKey})";

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            Result<Guid> asientoResult;
            if (nota.PurchBillId.HasValue)
            {
                var compra = await _compraRepo.GetByIdAsync(subscriberId, nota.PurchBillId.Value, ct);
                if (compra is null || compra.Status != PurchaseStatus.Approved)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<SupplierPurchaseNoteDto>.Failure(
                        "La factura de compra vinculada no existe o no está aprobada.");
                }

                compra.RegisterAppliedNote(nota.NoteType, nota.Total, userId);

                asientoResult = nota.NoteType == "CREDITO"
                    ? await _accounting.CrearAsientoNotaCreditoCompraProveedorAsync(
                        nota.Id,
                        reference:  numeroNota,
                        date:       nota.IssueDate,
                        subtotal:    nota.Subtotal,
                        vatTotal:    nota.VatTotal,
                        total:       nota.Total,
                        description: $"{descripcionBase} — compra {compra.InvoiceNumber}",
                        ct)
                    : await _accounting.CrearAsientoNotaDebitoCompraProveedorAsync(
                        nota.Id,
                        reference:  numeroNota,
                        date:       nota.IssueDate,
                        subtotal:    nota.Subtotal,
                        vatTotal:    nota.VatTotal,
                        total:       nota.Total,
                        description: $"{descripcionBase} — compra {compra.InvoiceNumber}",
                        ct);
            }
            else
            {
                var gasto = await _gastoRepo.GetByIdAsync(subscriberId, nota.ExpenseInvoiceId!.Value, ct);
                if (gasto is null || gasto.Status != ExpenseStatus.Approved)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<SupplierPurchaseNoteDto>.Failure(
                        "La factura de gasto vinculada no existe o no está aprobada.");
                }

                gasto.RegisterAppliedSupplierNote(nota.NoteType, nota.Total, userId);

                asientoResult = nota.NoteType == "CREDITO"
                    ? await _accounting.CrearAsientoNotaCreditoGastoProveedorAsync(
                        nota.Id,
                        reference:     numeroNota,
                        date:          nota.IssueDate,
                        total:          nota.Total,
                        category: gasto.Category,
                        description:    $"{descripcionBase} — gasto {gasto.Concept}",
                        ct)
                    : await _accounting.CrearAsientoNotaDebitoGastoProveedorAsync(
                        nota.Id,
                        reference:     numeroNota,
                        date:          nota.IssueDate,
                        total:          nota.Total,
                        category: gasto.Category,
                        description:    $"{descripcionBase} — gasto {gasto.Concept}",
                        ct);
            }

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<SupplierPurchaseNoteDto>.Failure(
                    asientoResult.Error ?? "No se pudo registrar el asiento contable de la nota.");
            }

            var asientoId = asientoResult.Value;
            IReadOnlyList<PurchNoteStockLine>? stockLines = null;
            if (nota.PurchBillId.HasValue)
            {
                var compraFull = await _compraRepo.GetByIdAsync(subscriberId, nota.PurchBillId.Value, ct);
                var asigs =
                    await _compraRepo.GetWarehouseAllocsByBillIdAsync(
                        subscriberId, nota.PurchBillId.Value, ct);
                if (compraFull is not null)
                    stockLines = BuildStockLines(nota, compraFull, asigs);
            }

            nota.Approve(
                userId,
                asientoId,
                command.AuthNumber,
                command.AuthDate,
                stockLines);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "compras", action: "notas-Supplier.aprobar",
                entityType: "PurchNote", entityId: nota.Id,
                description: $"{numeroNota} — asiento {asientoId}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Nota Supplier aprobada: {NotaId}, asiento {JournalEntryId}", nota.Id, asientoId);
            return Result<SupplierPurchaseNoteDto>.Success(ToDto(nota));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al aprobar nota Supplier {NotaId}", command.NotaId);
            return Result<SupplierPurchaseNoteDto>.Failure($"No se pudo aprobar la nota: {ex.Message}");
        }
    }

    private static IReadOnlyList<PurchNoteStockLine> BuildStockLines(
        PurchNote nota,
        PurchBill compra,
        IReadOnlyList<PurchWarehouseAlloc> asignaciones)
    {
        var lines = new List<PurchNoteStockLine>();
        foreach (var nd in nota.Lines)
        {
            if (!nd.ProductId.HasValue)
                continue;

            var compraDet = compra.Lines.FirstOrDefault(d =>
                (!string.IsNullOrEmpty(nd.SupplierProductCode) &&
                 string.Equals(d.SupplierProductCode, nd.SupplierProductCode, StringComparison.OrdinalIgnoreCase))
                || (d.ProductId == nd.ProductId));

            if (compraDet is null)
                continue;

            var asigs = asignaciones
                .Where(a => a.PurchBillLineId == compraDet.Id && a.ProductId == nd.ProductId)
                .ToList();
            if (asigs.Count == 0)
                continue;

            var detCant = compraDet.Quantity;
            var sign    = nota.NoteType == "CREDITO" ? 1m : -1m;
            var costo   = compraDet.Quantity > 0
                ? compraDet.UnitPrice * (1 - compraDet.DiscountPct / 100m)
                : 0m;

            foreach (var a in asigs)
            {
                var frac = detCant > 0 ? a.Quantity / detCant : 1m / asigs.Count;
                var qty  = nd.Quantity * frac * sign;
                if (qty == 0)
                    continue;
                lines.Add(new PurchNoteStockLine(
                    nd.ProductId.Value,
                    a.WarehouseId,
                    qty,
                    costo));
            }
        }

        return lines;
    }

    private static SupplierPurchaseNoteDto ToDto(PurchNote n) => new(
        n.Id,
        n.SupplierId,
        n.PurchBillId,
        n.ExpenseInvoiceId,
        n.NoteType,
        n.Reason,
        n.AccessKey,
        n.IssueDate,
        n.EstabCode,
        n.EmPointCode,
        n.Sequential,
        n.Subtotal,
        n.VatTotal,
        n.Total,
        n.Status,
        n.XmlPath,
        n.JournalEntryId,
        n.CreatedAt);
}
