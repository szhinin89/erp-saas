using ERP.Domain.Common;
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

namespace ERP.Application.Modules.Purchasing.UseCases.SupplierNotes;

public sealed class ApprovePurchaseSupplierNoteCommandHandler
    : IRequestHandler<ApprovePurchaseSupplierNoteCommand, Result<SupplierPurchaseNoteDto>>
{
    private readonly IPurchBillRepository       _compraRepo;
    private readonly IExpenseInvoiceRepository _gastoRepo;
    private readonly IAccountingService     _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork           _unitOfWork;
    private readonly ILogger<ApprovePurchaseSupplierNoteCommandHandler> _logger;

    public ApprovePurchaseSupplierNoteCommandHandler(
        IPurchBillRepository compraRepo,
        IExpenseInvoiceRepository gastoRepo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<ApprovePurchaseSupplierNoteCommandHandler> logger)
    {
        _compraRepo   = compraRepo;
        _gastoRepo    = gastoRepo;
        _accounting   = accounting;
        _activity     = activity;
        _subscriber = subscriber;
        _user         = user;
        _unitOfWork   = unitOfWork;
        _logger       = logger;
    }

    public async Task<Result<SupplierPurchaseNoteDto>> Handle(
        ApprovePurchaseSupplierNoteCommand command,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        var note = await _compraRepo.GetPurchNoteByIdWithLinesAsync(subscriberId, command.NoteId, ct);
        if (note is null)
            return Result<SupplierPurchaseNoteDto>.Failure("note de Supplier no encontrada.");

        if (note.Status != "Draft")
            return Result<SupplierPurchaseNoteDto>.Failure(
                $"Solo se puede aprobar una note en Draft (estado: {note.Status}).");

        if (!note.PurchBillId.HasValue && !note.ExpenseInvoiceId.HasValue)
            return Result<SupplierPurchaseNoteDto>.Failure(
                "Vincule la note a una salesBill de compra o de gasto antes de aprobar.");

        var numeroNota = $"{note.EstabCode}-{note.EmPointCode}-{note.Sequential}";
        var descripcionBase = $"note {note.NoteType} Supplier {numeroNota} (clave {note.AccessKey})";

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            Result<Guid> journalEntryResult;
            if (note.PurchBillId.HasValue)
            {
                var compra = await _compraRepo.GetByIdAsync(subscriberId, note.PurchBillId.Value, ct);
                if (compra is null || compra.Status != PurchaseStatus.Approved)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<SupplierPurchaseNoteDto>.Failure(
                        "La salesBill de compra vinculada no existe o no estÃ¡ aprobada.");
                }

                compra.RegisterAppliedNote(note.NoteType, note.Total, userId);

                journalEntryResult = note.NoteType == NoteType.Credit
                    ? await _accounting.CreatePurchaseSupplierCreditNoteJournalEntryAsync(
                        note.Id,
                        reference:  numeroNota,
                        date:       note.IssueDate,
                        subtotal:    note.Subtotal,
                        vatTotal:    note.VatTotal,
                        total:       note.Total,
                        description: $"{descripcionBase} â€” compra {compra.InvoiceNumber}",
                        ct)
                    : await _accounting.CreatePurchaseSupplierDebitNoteJournalEntryAsync(
                        note.Id,
                        reference:  numeroNota,
                        date:       note.IssueDate,
                        subtotal:    note.Subtotal,
                        vatTotal:    note.VatTotal,
                        total:       note.Total,
                        description: $"{descripcionBase} â€” compra {compra.InvoiceNumber}",
                        ct);
            }
            else
            {
                var gasto = await _gastoRepo.GetByIdAsync(subscriberId, note.ExpenseInvoiceId!.Value, ct);
                if (gasto is null || gasto.Status != ExpenseStatus.Approved)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<SupplierPurchaseNoteDto>.Failure(
                        "La salesBill de gasto vinculada no existe o no estÃ¡ aprobada.");
                }

                gasto.RegisterAppliedSupplierNote(note.NoteType, note.Total, userId);

                journalEntryResult = note.NoteType == NoteType.Credit
                    ? await _accounting.CreateExpenseSupplierCreditNoteJournalEntryAsync(
                        note.Id,
                        reference:     numeroNota,
                        date:          note.IssueDate,
                        total:          note.Total,
                        category: gasto.Category,
                        description:    $"{descripcionBase} â€” gasto {gasto.Concept}",
                        ct)
                    : await _accounting.CreateExpenseSupplierDebitNoteJournalEntryAsync(
                        note.Id,
                        reference:     numeroNota,
                        date:          note.IssueDate,
                        total:          note.Total,
                        category: gasto.Category,
                        description:    $"{descripcionBase} â€” gasto {gasto.Concept}",
                        ct);
            }

            if (!journalEntryResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<SupplierPurchaseNoteDto>.Failure(
                    journalEntryResult.Error ?? "No se pudo registrar el asiento contable de la note.");
            }

            var asientoId = journalEntryResult.Value;
            IReadOnlyList<PurchNoteStockLine>? stockLines = null;
            if (note.PurchBillId.HasValue)
            {
                var compraFull = await _compraRepo.GetByIdAsync(subscriberId, note.PurchBillId.Value, ct);
                var asigs =
                    await _compraRepo.GetWarehouseAllocsByBillIdAsync(
                        subscriberId, note.PurchBillId.Value, ct);
                if (compraFull is not null)
                    stockLines = BuildStockLines(note, compraFull, asigs);
            }

            note.Approve(
                userId,
                asientoId,
                command.AuthNumber,
                command.AuthDate,
                stockLines);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "compras", action: "notas-Supplier.aprobar",
                entityType: "PurchNote", entityId: note.Id,
                description: $"{numeroNota} â€” asiento {asientoId}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("note Supplier aprobada: {NoteId}, asiento {JournalEntryId}", note.Id, asientoId);
            return Result<SupplierPurchaseNoteDto>.Success(ToDto(note));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al aprobar note Supplier {NoteId}", command.NoteId);
            return Result<SupplierPurchaseNoteDto>.Failure($"No se pudo aprobar la note: {ex.Message}");
        }
    }

    private static IReadOnlyList<PurchNoteStockLine> BuildStockLines(
        PurchNote note,
        PurchBill compra,
        IReadOnlyList<PurchWarehouseAlloc> asignaciones)
    {
        var lines = new List<PurchNoteStockLine>();
        foreach (var nd in note.Lines)
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
            var sign    = note.NoteType == NoteType.Credit ? 1m : -1m;
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
        n.BusinessPartnerId,
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

