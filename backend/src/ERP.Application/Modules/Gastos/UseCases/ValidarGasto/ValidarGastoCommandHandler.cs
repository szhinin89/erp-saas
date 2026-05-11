using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Gastos.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Gastos.Entities;
using ERP.Domain.Modules.Gastos.Enums;
using ERP.Domain.Modules.Gastos.Interfaces;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Gastos.UseCases.ValidarGasto;

public sealed class ValidarGastoCommandHandler
    : IRequestHandler<ValidarGastoCommand, Result<GastoFacturaDto>>
{
    private readonly IGastoFacturaRepository   _repo;
    private readonly IProveedorRepository      _proveedorRepo;
    private readonly IUserActivityRepository   _activity;
    private readonly ICurrentTenant            _tenant;
    private readonly ICurrentUser              _user;
    private readonly IUnitOfWork               _unitOfWork;

    public ValidarGastoCommandHandler(
        IGastoFacturaRepository repo,
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

    public async Task<Result<GastoFacturaDto>> Handle(ValidarGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var gasto = await _repo.GetByIdAsync(tenantId, command.GastoFacturaId, ct);
        if (gasto is null)
            return Result<GastoFacturaDto>.Failure("Gasto no encontrado.");

        if (gasto.Estado != EstadoGasto.Borrador)
            return Result<GastoFacturaDto>.Failure(
                $"Solo se puede validar un gasto en Borrador (estado actual: {gasto.Estado}).");

        if (gasto.ProveedorId.HasValue)
        {
            var proveedor = await _proveedorRepo.GetByIdAsync(tenantId, gasto.ProveedorId.Value, ct);
            if (proveedor is null || !proveedor.IsActive)
                return Result<GastoFacturaDto>.Failure("El proveedor del gasto no existe o está deshabilitado.");
        }

        var totalCalc = gasto.Subtotal + gasto.Impuesto;
        if (Math.Abs(totalCalc - gasto.Total) > GastoFactura.ToleranciaTotal)
            return Result<GastoFacturaDto>.Failure(
                $"Los totales no cuadran: Subtotal({gasto.Subtotal:F2}) + Impuesto({gasto.Impuesto:F2}) = " +
                $"{totalCalc:F2}, pero Total es {gasto.Total:F2}.");

        if (!string.IsNullOrEmpty(gasto.ClaveAcceso))
        {
            var clave = gasto.ClaveAcceso;
            if (clave.Length != GastoFactura.ClaveAccesoLen || !clave.All(char.IsDigit))
                return Result<GastoFacturaDto>.Failure(
                    "La clave de acceso debe tener exactamente 49 dígitos numéricos.");
        }

        try
        {
            gasto.Validar(userId);
        }
        catch (Exception ex)
        {
            return Result<GastoFacturaDto>.Failure(ex.Message);
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.validar",
                entityType: "GastoFactura", entityId: gasto.Id,
                description: gasto.Concepto), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<GastoFacturaDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<GastoFacturaDto>.Failure($"No se pudo validar el gasto: {ex.Message}");
        }
    }

    private static GastoFacturaDto ToDto(GastoFactura g) => new(
        g.Id,
        g.ClaveAcceso,
        g.FechaEmision,
        g.ProveedorId,
        g.NumeroFactura,
        g.Concepto,
        g.CategoriaGasto,
        g.Subtotal,
        g.Impuesto,
        g.Total,
        g.Estado,
        g.XmlPath,
        g.Observaciones,
        g.AsientoContableId,
        g.CreatedAt);
}
