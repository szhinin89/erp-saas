using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.ApproveExpense;

public sealed class ApproveExpenseCommandHandler
    : IRequestHandler<ApproveExpenseCommand, Result<ExpenseInvoiceDto>>
{
    private readonly IExpenseInvoiceRepository   _repo;
    private readonly IAccountingService      _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _subscriber;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<ApproveExpenseCommandHandler> _logger;

    public ApproveExpenseCommandHandler(
        IExpenseInvoiceRepository repo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentSubscriber subscriber,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<ApproveExpenseCommandHandler> logger)
    {
        _repo       = repo;
        _accounting = accounting;
        _activity   = activity;
        _subscriber = subscriber;
        _user       = user;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<Result<ExpenseInvoiceDto>> Handle(ApproveExpenseCommand command, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var userId   = _user.UserId;

        var gasto = await _repo.GetByIdAsync(subscriberId, command.ExpenseInvoiceId, ct);
        if (gasto is null)
            return Result<ExpenseInvoiceDto>.Failure("Gasto no encontrado.");

        if (gasto.Status != ExpenseStatus.Validated)
            return Result<ExpenseInvoiceDto>.Failure(
                $"Solo se puede aprobar un gasto Validado (estado actual: {gasto.Status}).");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var referencia = gasto.InvoiceNumber ?? gasto.AccessKey ?? gasto.Id.ToString("N")[..8];
            var asientoResult = await _accounting.CrearAsientoGastoAsync(
                gasto.Id,
                gasto.Category,
                reference:  referencia,
                date:       gasto.IssueDate,
                subtotal: gasto.Subtotal,
                vatTotal: gasto.TaxTotal,
                total:       gasto.Total,
                description: $"Gasto {gasto.Concept} — {gasto.Category}",
                ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<ExpenseInvoiceDto>.Failure(
                    asientoResult.Error ?? "No se pudo registrar el asiento contable del gasto.");
            }

            gasto.Approve(userId, asientoResult.Value);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.aprobar",
                entityType: "ExpenseInvoice", entityId: gasto.Id,
                description: $"{gasto.Concept} — asiento: {asientoResult.Value}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<ExpenseInvoiceDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al aprobar gasto {GastoId}", command.ExpenseInvoiceId);
            return Result<ExpenseInvoiceDto>.Failure($"No se pudo aprobar el gasto: {ex.Message}");
        }
    }

    private static ExpenseInvoiceDto ToDto(ExpenseInvoice g) => new(
        g.Id,
        g.AccessKey,
        g.IssueDate,
        g.BusinessPartnerId,
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
