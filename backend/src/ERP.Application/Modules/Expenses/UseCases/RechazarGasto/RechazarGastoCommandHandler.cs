using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.RechazarGasto;

public sealed class RechazarGastoCommandHandler
    : IRequestHandler<RechazarGastoCommand, Result<ExpenseInvoiceDto>>
{
    private readonly IExpenseInvoiceRepository   _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;

    public RechazarGastoCommandHandler(
        IExpenseInvoiceRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork)
    {
        _repo       = repo;
        _activity   = activity;
        _tenant     = tenant;
        _user       = user;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ExpenseInvoiceDto>> Handle(RechazarGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var gasto = await _repo.GetByIdAsync(tenantId, command.ExpenseInvoiceId, ct);
        if (gasto is null)
            return Result<ExpenseInvoiceDto>.Failure("Gasto no encontrado.");

        if (gasto.Status == ExpenseStatus.Approved)
            return Result<ExpenseInvoiceDto>.Failure("No se puede rechazar un gasto ya aprobado.");

        try
        {
            gasto.Reject(userId, command.Reason);
        }
        catch (Exception ex)
        {
            return Result<ExpenseInvoiceDto>.Failure(ex.Message);
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.rechazar",
                entityType: "ExpenseInvoice", entityId: gasto.Id,
                description: command.Reason), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<ExpenseInvoiceDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<ExpenseInvoiceDto>.Failure(
                $"No se pudo rechazar el gasto: {ex.Message}");
        }
    }

    private static ExpenseInvoiceDto ToDto(ExpenseInvoice g) => new(
        g.Id,
        g.AccessKey,
        g.IssueDate,
        g.SupplierId,
        g.InvoiceNumber,
        g.Concept,
        g.Category,
        g.Subtotal,
        g.TaxTotal,
        g.Total,
        g.Status,
        g.XmlPath,
        g.Notes,
        g.JournalEntryId,
        g.CreatedAt);
}
