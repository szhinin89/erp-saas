using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.AnularFactura;

public sealed class VoidInvoiceCommandHandler : IRequestHandler<VoidInvoiceCommand, Result<Guid>>
{
    private readonly ISalesRepository    _ventasRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentUser            _currentUser;
    private readonly ILogger<VoidInvoiceCommandHandler> _logger;

    public VoidInvoiceCommandHandler(
        ISalesRepository ventasRepository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<VoidInvoiceCommandHandler> logger)
    {
        _ventasRepository = ventasRepository;
        _activity         = activity;
        _currentSubscriber    = currentSubscriber;
        _currentUser      = currentUser;
        _logger           = logger;
    }

    public async Task<Result<Guid>> Handle(VoidInvoiceCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var factura = await _ventasRepository.GetBillByIdAsync(subscriberId, command.VentaId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (factura.Status == "Autorizado")
            return Result<Guid>.Failure(
                "No se puede anular una factura ya autorizada por el SRI. Debe emitir una nota de crédito.");

        if (factura.Status == "Anulado")
            return Result<Guid>.Failure("La factura ya está anulada.");

        _logger.LogInformation(
            "Anulando factura {FacturaId} (estado previo={Estado}, tenant={SubscriberId})",
            factura.Id, factura.Status, subscriberId);
        factura.Void(userId);

        var numeroFactura = $"{factura.EstabCode}-{factura.EmPointCode}-{factura.Sequential}";
        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _currentUser.Email, _currentUser.FullName,
            module: "ventas", action: "venta.anular",
            entityType: "SalesBill", entityId: factura.Id,
            description: numeroFactura), ct);

        await _ventasRepository.SaveChangesAsync(ct);

        return Result<Guid>.Success(factura.Id);
    }
}
