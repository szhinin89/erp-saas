using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Sales.UseCases.EmitirFacturaElectronica;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.ReintentarEnvio;

public sealed class ReintentarEnvioCommandHandler : IRequestHandler<ReintentarEnvioCommand, Result<Guid>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly IMediator         _mediator;
    private readonly ICurrentTenant    _currentTenant;
    private readonly ICurrentUser      _currentUser;
    private readonly ILogger<ReintentarEnvioCommandHandler> _logger;

    public ReintentarEnvioCommandHandler(
        ISalesRepository ventasRepository,
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

        var factura = await _ventasRepository.GetBillByIdAsync(tenantId, command.VentaId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (factura.Status != "ErrorEnvio" && factura.Status != "Rechazado")
            return Result<Guid>.Failure(
                $"Solo se puede reintentar una factura en ErrorEnvio o Rechazado (estado actual: {factura.Status}).");

        // Resetear a Validado para reutilizar el flujo completo de emisión
        factura.PrepareRetry(userId);
        await _ventasRepository.SaveChangesAsync(ct);

        return await _mediator.Send(new EmitirFacturaElectronicaCommand(command.VentaId), ct);
    }
}
