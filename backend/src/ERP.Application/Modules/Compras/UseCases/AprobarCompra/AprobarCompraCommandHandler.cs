using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Compras.Enums;
using ERP.Domain.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.AprobarCompra;

public sealed class AprobarCompraCommandHandler
    : IRequestHandler<AprobarCompraCommand, Result<CompraFacturaDto>>
{
    private readonly ICompraRepository       _repo;
    private readonly IAccountingService      _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public AprobarCompraCommandHandler(
        ICompraRepository repo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo       = repo;
        _accounting = accounting;
        _activity   = activity;
        _tenant     = tenant;
        _user       = user;
    }

    public async Task<Result<CompraFacturaDto>> Handle(
        AprobarCompraCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var compra = await _repo.GetByIdWithDetailsAsync(tenantId, command.CompraFacturaId, ct);
        if (compra is null)
            return Result<CompraFacturaDto>.Failure("Compra no encontrada.");

        if (compra.Estado != EstadoCompra.Validado)
            return Result<CompraFacturaDto>.Failure(
                $"Solo se puede aprobar una compra Validada (estado actual: {compra.Estado}).");

        // Integración contable — intento no bloqueante
        Guid? asientoId = null;
        var asientoResult = await _accounting.CrearAsientoCompraAsync(
            compra.Id,
            referencia:  compra.NumeroFactura,
            fecha:       compra.FechaFactura,
            subtotal:    compra.Subtotal,
            iva:         compra.IvaTotal,
            total:       compra.Total,
            descripcion: $"Compra {compra.NumeroFactura} — {compra.ProveedorId}",
            ct);

        if (asientoResult.IsSuccess)
            asientoId = asientoResult.Value;
        // Si falla la contabilidad, la aprobación continúa (asientoId queda null)

        compra.Aprobar(userId, asientoId);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "compras", action: "compra.aprobar",
            entityType: "CompraFactura", entityId: compra.Id,
            description: $"{compra.NumeroFactura} — asiento: {asientoId?.ToString() ?? "no creado"}"), ct);

        await _repo.SaveChangesAsync(ct);

        return Result<CompraFacturaDto>.Success(ToDto(compra));
    }

    private static CompraFacturaDto ToDto(Domain.Compras.Entities.CompraFactura c) => new(
        c.Id, c.ProveedorId, c.NumeroFactura, c.ClaveAcceso, c.XmlPath,
        c.FechaFactura, c.FechaVencimiento, c.Estado, c.CondicionPago,
        c.Subtotal, c.IvaTotal, c.Total, c.Observaciones, c.AsientoContableId, c.CreatedAt);
}
