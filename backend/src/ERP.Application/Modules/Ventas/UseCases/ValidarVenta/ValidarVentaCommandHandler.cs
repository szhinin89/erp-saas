using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Ventas.Interfaces;

namespace ERP.Application.Ventas.UseCases.ValidarVenta;

public sealed class ValidarVentaCommandHandler : IRequestHandler<ValidarVentaCommand, Result<Guid>>
{
    private readonly IVentasRepository    _ventasRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly IUnitOfWork             _unitOfWork;
    private readonly ILogger<ValidarVentaCommandHandler> _logger;

    public ValidarVentaCommandHandler(
        IVentasRepository ventasRepository,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<ValidarVentaCommandHandler> logger)
    {
        _ventasRepository = ventasRepository;
        _activity         = activity;
        _currentTenant    = currentTenant;
        _currentUser      = currentUser;
        _unitOfWork       = unitOfWork;
        _logger           = logger;
    }

    public async Task<Result<Guid>> Handle(ValidarVentaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        _logger.LogInformation("Validando factura {FacturaId} (tenant={TenantId})", command.VentaId, tenantId);

        var factura = await _ventasRepository.GetFacturaByIdAsync(tenantId, command.VentaId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (factura.Estado != "Borrador")
            return Result<Guid>.Failure(
                $"Solo se puede validar una factura en Borrador (estado actual: {factura.Estado}).");

        if (factura.Detalles.Count == 0)
            return Result<Guid>.Failure("La factura debe tener al menos un detalle.");

        // Verificar consistencia de totales (tolerancia 0.01)
        var totalCalculado = factura.Subtotal + factura.Impuesto;
        if (Math.Abs(totalCalculado - factura.Total) > 0.01m)
            return Result<Guid>.Failure(
                $"Los totales no cuadran: Subtotal({factura.Subtotal:F2}) + IVA({factura.Impuesto:F2}) = " +
                $"{totalCalculado:F2}, pero Total es {factura.Total:F2}.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            factura.Validar(userId);

            _logger.LogInformation(
                "Factura {FacturaId} validada: secuencial={Secuencial}, total={Total}",
                factura.Id, factura.Secuencial, factura.Total);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "venta.validar",
                entityType: "VentasFactura", entityId: factura.Id,
                description: $"{factura.Establecimiento}-{factura.PuntoEmision}-{factura.Secuencial}"), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<Guid>.Success(factura.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al validar factura {FacturaId}", command.VentaId);
            return Result<Guid>.Failure($"No se pudo validar la factura: {ex.Message}");
        }
    }
}
