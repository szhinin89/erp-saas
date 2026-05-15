using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.AnularFactura;

public sealed class AnularFacturaCommandHandler : IRequestHandler<AnularFacturaCommand, Result<Guid>>
{
    private readonly IVentasRepository    _ventasRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _currentTenant;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<AnularFacturaCommandHandler> _logger;

    public AnularFacturaCommandHandler(
        IVentasRepository ventasRepository,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<AnularFacturaCommandHandler> logger)
    {
        _ventasRepository = ventasRepository;
        _activity         = activity;
        _currentTenant    = currentTenant;
        _currentUser      = currentUser;
        _logger           = logger;
    }

    public async Task<Result<Guid>> Handle(AnularFacturaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var factura = await _ventasRepository.GetFacturaByIdAsync(tenantId, command.VentaId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (factura.Estado == "Autorizado")
            return Result<Guid>.Failure(
                "No se puede anular una factura ya autorizada por el SRI. Debe emitir una nota de crédito.");

        if (factura.Estado == "Anulado")
            return Result<Guid>.Failure("La factura ya está anulada.");

        _logger.LogInformation(
            "Anulando factura {FacturaId} (estado previo={Estado}, tenant={TenantId})",
            factura.Id, factura.Estado, tenantId);
        factura.Anular(userId);

        var numeroFactura = $"{factura.Establecimiento}-{factura.PuntoEmision}-{factura.Secuencial}";
        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _currentUser.Email, _currentUser.FullName,
            module: "ventas", action: "venta.anular",
            entityType: "VentasFactura", entityId: factura.Id,
            description: numeroFactura), ct);

        await _ventasRepository.SaveChangesAsync(ct);

        return Result<Guid>.Success(factura.Id);
    }
}
