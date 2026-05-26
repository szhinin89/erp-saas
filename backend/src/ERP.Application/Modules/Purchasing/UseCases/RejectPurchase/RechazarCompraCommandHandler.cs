using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.RejectPurchase;

public sealed class RejectPurchaseCommandHandler
    : IRequestHandler<RejectPurchaseCommand, Result<PurchBillDto>>
{
    private readonly IPurchBillRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;

    public RejectPurchaseCommandHandler(
        IPurchBillRepository repo,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork unitOfWork)
    {
        _repo       = repo;
        _activity   = activity;
        _subscriber = subscriber;
        _user       = user;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PurchBillDto>> Handle(
        RejectPurchaseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        var compra = await _repo.GetByIdAsync(subscriberId, command.PurchBillId, ct);
        if (compra is null)
            return Result<PurchBillDto>.Failure("Compra no encontrada.");

        if (compra.Status == PurchaseStatus.Approved)
            return Result<PurchBillDto>.Failure("No se puede rechazar una compra ya aprobada.");

        try
        {
            compra.Reject(userId, command.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PurchBillDto>.Failure(ex.Message);
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.rechazar",
                entityType: "PurchBill", entityId: compra.Id,
                description: $"{compra.InvoiceNumber} — motivo: {command.Reason}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<PurchBillDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<PurchBillDto>.Failure($"No se pudo rechazar la compra: {ex.Message}");
        }
    }

    private static PurchBillDto ToDto(ERP.Domain.Modules.Purchasing.Entities.PurchBill c) => new(
        c.Id, c.BusinessPartnerId, c.InvoiceNumber, c.AccessKey, c.XmlPath,
        c.InvoiceDate, c.DueDate, c.Status, c.PaymentTerms,
        c.Subtotal, c.VatTotal, c.Total, c.Notes, c.JournalEntryId, c.CreatedAt);
}
