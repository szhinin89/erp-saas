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

public sealed class ValidarCompraCommandHandler
    : IRequestHandler<ValidarCompraCommand, Result<CompraFacturaDto>>
{
    private readonly ICompraRepository       _repo;
    private readonly IProveedorRepository    _proveedorRepo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;

    public ValidarCompraCommandHandler(
        ICompraRepository repo,
        IProveedorRepository proveedorRepo,
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

    public async Task<Result<CompraFacturaDto>> Handle(
        ValidarCompraCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var compra = await _repo.GetByIdWithDetailsAsync(tenantId, command.CompraFacturaId, ct);
        if (compra is null)
            return Result<CompraFacturaDto>.Failure("Compra no encontrada.");

        if (compra.Estado != EstadoCompra.Borrador)
            return Result<CompraFacturaDto>.Failure(
                $"Solo se puede validar una compra en Borrador (estado actual: {compra.Estado}).");

        // 1. Verificar proveedor activo
        var proveedor = await _proveedorRepo.GetByIdAsync(tenantId, compra.ProveedorId, ct);
        if (proveedor is null || !proveedor.IsActive)
            return Result<CompraFacturaDto>.Failure("El proveedor de la compra no existe o está deshabilitado.");

        // 2. Validar que tenga detalles
        if (compra.Detalles.Count == 0)
            return Result<CompraFacturaDto>.Failure("La compra debe tener al menos un detalle.");

        // 3. Validar consistencia de totales (subtotal + iva ≈ total, tolerancia 0.01)
        var totalCalculado = compra.Subtotal + compra.IvaTotal;
        if (Math.Abs(totalCalculado - compra.Total) > CompraFactura.ToleranciaTotal)
            return Result<CompraFacturaDto>.Failure(
                $"Los totales no cuadran: Subtotal({compra.Subtotal:F2}) + IVA({compra.IvaTotal:F2}) = " +
                $"{totalCalculado:F2}, pero Total es {compra.Total:F2} (tolerancia: {CompraFactura.ToleranciaTotal}).");

        // 4. Validar clave de acceso (si existe)
        if (!string.IsNullOrEmpty(compra.ClaveAcceso))
        {
            var clave = compra.ClaveAcceso;
            if (clave.Length != 49 || !clave.All(char.IsDigit))
                return Result<CompraFacturaDto>.Failure(
                    "La clave de acceso debe tener exactamente 49 dígitos numéricos.");
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            compra.Validar(userId);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "compras", action: "compra.validar",
                entityType: "CompraFactura", entityId: compra.Id,
                description: compra.NumeroFactura), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<CompraFacturaDto>.Success(ToDto(compra));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<CompraFacturaDto>.Failure($"No se pudo validar la compra: {ex.Message}");
        }
    }

    private static CompraFacturaDto ToDto(ERP.Domain.Modules.Purchasing.Entities.CompraFactura c) => new(
        c.Id, c.ProveedorId, c.NumeroFactura, c.ClaveAcceso, c.XmlPath,
        c.FechaFactura, c.FechaVencimiento, c.Estado, c.CondicionPago,
        c.Subtotal, c.IvaTotal, c.Total, c.Observaciones, c.AsientoContableId, c.CreatedAt);
}
