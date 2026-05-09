using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Gastos.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Gastos.Entities;
using ERP.Domain.Gastos.Enums;
using ERP.Domain.Gastos.Interfaces;

namespace ERP.Application.Modules.Gastos.UseCases.RechazarGasto;

public sealed class RechazarGastoCommandHandler
    : IRequestHandler<RechazarGastoCommand, Result<GastoFacturaDto>>
{
    private readonly IGastoFacturaRepository   _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;

    public RechazarGastoCommandHandler(
        IGastoFacturaRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _repo     = repo;
        _activity = activity;
        _tenant   = tenant;
        _user     = user;
    }

    public async Task<Result<GastoFacturaDto>> Handle(RechazarGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var gasto = await _repo.GetByIdAsync(tenantId, command.GastoFacturaId, ct);
        if (gasto is null)
            return Result<GastoFacturaDto>.Failure("Gasto no encontrado.");

        if (gasto.Estado == EstadoGasto.Aprobado)
            return Result<GastoFacturaDto>.Failure("No se puede rechazar un gasto ya aprobado.");

        try
        {
            gasto.Rechazar(userId, command.Motivo);
        }
        catch (Exception ex)
        {
            return Result<GastoFacturaDto>.Failure(ex.Message);
        }

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "gastos", action: "gasto.rechazar",
            entityType: "GastoFactura", entityId: gasto.Id,
            description: command.Motivo), ct);

        await _repo.SaveChangesAsync(ct);

        return Result<GastoFacturaDto>.Success(ToDto(gasto));
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
