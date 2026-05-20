using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ValidarCompra;

public sealed class ValidatePurchaseCommandHandler
    : IRequestHandler<ValidatePurchaseCommand, Result<PurchBillDto>>
{
    private readonly IPurchBillRepository       _repo;
    private readonly ISupplierRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;

    public ValidatePurchaseCommandHandler(
        IPurchBillRepository repo,
        ISupplierRepository proveedorRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber tenant,
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

    public async Task<Result<PurchBillDto>> Handle(
        ValidatePurchaseCommand command, CancellationToken ct)
    {
        var subscriberId = _tenant.SubscriberId;
        var userId   = _user.UserId;

        var compra = await _repo.GetByIdAsync(subscriberId, command.PurchBillId, ct);
        if (compra is null)
            return Result<PurchBillDto>.Failure("Compra no encontrada.");

        if (compra.Status != PurchaseStatus.Draft)
            return Result<PurchBillDto>.Failure(
                $"Solo se puede validar una compra en Borrador (estado actual: {compra.Status}).");

        // 1. Verificar Supplier activo
        var Supplier = await _proveedorRepo.GetByIdAsync(subscriberId, compra.SupplierId, ct);
        if (Supplier is null || !Supplier.IsActive)
            return Result<PurchBillDto>.Failure("El Supplier de la compra no existe o está deshabilitado.");

        // 2. Validar que tenga detalles
        if (compra.Lines.Count == 0)
            return Result<PurchBillDto>.Failure("La compra debe tener al menos un detalle.");

        // 3. Validar consistencia de totales (subtotal + iva ≈ total, tolerancia 0.01)
        var totalCalculado = compra.Subtotal + compra.VatTotal;
        if (Math.Abs(totalCalculado - compra.Total) > PurchBill.TotalTolerance)
            return Result<PurchBillDto>.Failure(
                $"Los totales no cuadran: Subtotal({compra.Subtotal:F2}) + IVA({compra.VatTotal:F2}) = " +
                $"{totalCalculado:F2}, pero Total es {compra.Total:F2} (tolerancia: {PurchBill.TotalTolerance}).");

        // 4. Validar clave de acceso (si existe)
        if (!string.IsNullOrEmpty(compra.AccessKey))
        {
            var clave = compra.AccessKey;
            if (clave.Length != 49 || !clave.All(char.IsDigit))
                return Result<PurchBillDto>.Failure(
                    "La clave de acceso debe tener exactamente 49 dígitos numéricos.");
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            compra.Validate(userId);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.validar",
                entityType: "PurchBill", entityId: compra.Id,
                description: compra.InvoiceNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<PurchBillDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<PurchBillDto>.Failure($"No se pudo validar la compra: {ex.Message}");
        }
    }

    private static PurchBillDto ToDto(ERP.Domain.Modules.Purchasing.Entities.PurchBill c) => new(
        c.Id, c.SupplierId, c.InvoiceNumber, c.AccessKey, c.XmlPath,
        c.InvoiceDate, c.DueDate, c.Status, c.PaymentTerms,
        c.Subtotal, c.VatTotal, c.Total, c.Notes, c.JournalEntryId, c.CreatedAt);
}
