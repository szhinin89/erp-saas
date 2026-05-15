using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.ValidarGasto;

public sealed class ValidarGastoCommandHandler
    : IRequestHandler<ValidarGastoCommand, Result<ExpenseInvoiceDto>>
{
    private readonly IExpenseInvoiceRepository   _repo;
    private readonly ISupplierRepository      _proveedorRepo;
    private readonly IUserActivityRepository   _activity;
    private readonly ICurrentTenant            _tenant;
    private readonly ICurrentUser              _user;
    private readonly IUnitOfWork               _unitOfWork;

    public ValidarGastoCommandHandler(
        IExpenseInvoiceRepository repo,
        ISupplierRepository proveedorRepo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _activity      = activity;
        _tenant        = tenant;
        _user          = user;
        _unitOfWork    = unitOfWork;
    }

    public async Task<Result<ExpenseInvoiceDto>> Handle(ValidarGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var gasto = await _repo.GetByIdAsync(tenantId, command.ExpenseInvoiceId, ct);
        if (gasto is null)
            return Result<ExpenseInvoiceDto>.Failure("Gasto no encontrado.");

        if (gasto.Status != ExpenseStatus.Draft)
            return Result<ExpenseInvoiceDto>.Failure(
                $"Solo se puede validar un gasto en Borrador (estado actual: {gasto.Status}).");

        if (gasto.SupplierId.HasValue)
        {
            var Supplier = await _proveedorRepo.GetByIdAsync(tenantId, gasto.SupplierId.Value, ct);
            if (Supplier is null || !Supplier.IsActive)
                return Result<ExpenseInvoiceDto>.Failure("El Supplier del gasto no existe o está deshabilitado.");
        }

        var totalCalc = gasto.Subtotal + gasto.TaxTotal;
        if (Math.Abs(totalCalc - gasto.Total) > ExpenseInvoice.TotalTolerance)
            return Result<ExpenseInvoiceDto>.Failure(
                $"Los totales no cuadran: Subtotal({gasto.Subtotal:F2}) + Impuesto({gasto.TaxTotal:F2}) = " +
                $"{totalCalc:F2}, pero Total es {gasto.Total:F2}.");

        if (!string.IsNullOrEmpty(gasto.AccessKey))
        {
            var clave = gasto.AccessKey;
            if (clave.Length != ExpenseInvoice.AccessKeyLen || !clave.All(char.IsDigit))
                return Result<ExpenseInvoiceDto>.Failure(
                    "La clave de acceso debe tener exactamente 49 dígitos numéricos.");
        }

        try
        {
            gasto.Validate(userId);
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
                module: "gastos", action: "gasto.validar",
                entityType: "ExpenseInvoice", entityId: gasto.Id,
                description: gasto.Concept), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<ExpenseInvoiceDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<ExpenseInvoiceDto>.Failure($"No se pudo validar el gasto: {ex.Message}");
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
