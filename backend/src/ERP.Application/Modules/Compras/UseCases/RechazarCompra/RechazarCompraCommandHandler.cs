using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Compras.Enums;
using ERP.Domain.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.RechazarCompra;

public sealed class RechazarCompraCommandHandler
    : IRequestHandler<RechazarCompraCommand, Result<CompraFacturaDto>>
{
    private readonly ICompraRepository       _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public RechazarCompraCommandHandler(
        ICompraRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _user     = user;
    }

    public async Task<Result<CompraFacturaDto>> Handle(
        RechazarCompraCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        if (string.IsNullOrWhiteSpace(command.Motivo))
            return Result<CompraFacturaDto>.Failure("El motivo de rechazo es obligatorio.");

        var compra = await _repo.GetByIdAsync(tenantId, command.CompraFacturaId, ct);
        if (compra is null)
            return Result<CompraFacturaDto>.Failure("Compra no encontrada.");

        if (compra.Estado == EstadoCompra.Aprobado)
            return Result<CompraFacturaDto>.Failure("No se puede rechazar una compra ya aprobada.");

        try
        {
            compra.Rechazar(userId, command.Motivo);
        }
        catch (InvalidOperationException ex)
        {
            return Result<CompraFacturaDto>.Failure(ex.Message);
        }

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "compras", action: "compra.rechazar",
            entityType: "CompraFactura", entityId: compra.Id,
            description: $"{compra.NumeroFactura} — motivo: {command.Motivo}"), ct);

        await _repo.SaveChangesAsync(ct);

        return Result<CompraFacturaDto>.Success(ToDto(compra));
    }

    private static CompraFacturaDto ToDto(Domain.Compras.Entities.CompraFactura c) => new(
        c.Id, c.ProveedorId, c.NumeroFactura, c.ClaveAcceso, c.XmlPath,
        c.FechaFactura, c.FechaVencimiento, c.Estado, c.CondicionPago,
        c.Subtotal, c.IvaTotal, c.Total, c.Observaciones, c.AsientoContableId, c.CreatedAt);
}
