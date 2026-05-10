using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Ventas.UseCases.EmitirFacturaElectronica;
using ERP.Domain.Ventas.Interfaces;

namespace ERP.Application.Ventas.UseCases.ReintentarEnvio;

public sealed class ReintentarEnvioCommandHandler : IRequestHandler<ReintentarEnvioCommand, Result<Guid>>
{
    private readonly IVentasRepository _ventasRepository;
    private readonly IMediator         _mediator;
    private readonly ICurrentTenant    _currentTenant;
    private readonly ICurrentUser      _currentUser;
    private readonly ILogger<ReintentarEnvioCommandHandler> _logger;

    public ReintentarEnvioCommandHandler(
        IVentasRepository ventasRepository,
        IMediator mediator,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<ReintentarEnvioCommandHandler> logger)
    {
        _ventasRepository = ventasRepository;
        _mediator         = mediator;
        _currentTenant    = currentTenant;
        _currentUser      = currentUser;
        _logger           = logger;
    }

    public async Task<Result<Guid>> Handle(ReintentarEnvioCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        _logger.LogInformation(
            "Reintentando envío SRI de factura {FacturaId} (tenant={TenantId})",
            command.VentaId, tenantId);

        var factura = await _ventasRepository.GetFacturaByIdAsync(tenantId, command.VentaId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (factura.Estado != "ErrorEnvio" && factura.Estado != "Rechazado")
            return Result<Guid>.Failure(
                $"Solo se puede reintentar una factura en ErrorEnvio o Rechazado (estado actual: {factura.Estado}).");

        // Resetear a Validado para reutilizar el flujo completo de emisión
        factura.PrepararReintento(userId);
        await _ventasRepository.SaveChangesAsync(ct);

        return await _mediator.Send(new EmitirFacturaElectronicaCommand(command.VentaId), ct);
    }
}
