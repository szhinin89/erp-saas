using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Gastos.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Gastos.Entities;
using ERP.Domain.Gastos.Enums;
using ERP.Domain.Gastos.Interfaces;

namespace ERP.Application.Modules.Gastos.UseCases.AprobarGasto;

public sealed class AprobarGastoCommandHandler
    : IRequestHandler<AprobarGastoCommand, Result<GastoFacturaDto>>
{
    private readonly IGastoFacturaRepository   _repo;
    private readonly IAccountingService      _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<AprobarGastoCommandHandler> _logger;

    public AprobarGastoCommandHandler(
        IGastoFacturaRepository repo,
        IAccountingService accounting,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork unitOfWork,
        ILogger<AprobarGastoCommandHandler> logger)
    {
        _repo       = repo;
        _accounting = accounting;
        _activity   = activity;
        _tenant     = tenant;
        _user       = user;
        _unitOfWork = unitOfWork;
        _logger     = logger;
    }

    public async Task<Result<GastoFacturaDto>> Handle(AprobarGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        var gasto = await _repo.GetByIdAsync(tenantId, command.GastoFacturaId, ct);
        if (gasto is null)
            return Result<GastoFacturaDto>.Failure("Gasto no encontrado.");

        if (gasto.Estado != EstadoGasto.Validado)
            return Result<GastoFacturaDto>.Failure(
                $"Solo se puede aprobar un gasto Validado (estado actual: {gasto.Estado}).");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var referencia = gasto.NumeroFactura ?? gasto.ClaveAcceso ?? gasto.Id.ToString("N")[..8];
            var asientoResult = await _accounting.CrearAsientoGastoAsync(
                gasto.Id,
                gasto.CategoriaGasto,
                referencia:  referencia,
                fecha:       gasto.FechaEmision,
                subtotal:    gasto.Subtotal,
                impuesto:    gasto.Impuesto,
                total:       gasto.Total,
                descripcion: $"Gasto {gasto.Concepto} — {gasto.CategoriaGasto}",
                ct);

            if (!asientoResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<GastoFacturaDto>.Failure(
                    asientoResult.Error ?? "No se pudo registrar el asiento contable del gasto.");
            }

            gasto.Aprobar(userId, asientoResult.Value);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _user.Email, _user.FullName,
                module: "gastos", action: "gasto.aprobar",
                entityType: "GastoFactura", entityId: gasto.Id,
                description: $"{gasto.Concepto} — asiento: {asientoResult.Value}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<GastoFacturaDto>.Success(ToDto(gasto));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al aprobar gasto {GastoId}", command.GastoFacturaId);
            return Result<GastoFacturaDto>.Failure($"No se pudo aprobar el gasto: {ex.Message}");
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
