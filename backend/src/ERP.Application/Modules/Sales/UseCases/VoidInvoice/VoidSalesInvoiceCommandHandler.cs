using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Fiscal.Interfaces;

namespace ERP.Application.Sales.UseCases.VoidInvoice;

public sealed class VoidInvoiceCommandHandler : IRequestHandler<VoidInvoiceCommand, Result<Guid>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<VoidInvoiceCommandHandler> _logger;

    public VoidInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<VoidInvoiceCommandHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _activity = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(VoidInvoiceCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        var invoice = await _invoiceRepository.GetByPublicIdAsync(command.SaleId, ct);
        if (invoice is null)
            return Result<Guid>.Failure("salesBill de venta no encontrada.");

        if (invoice.Status == Invoice.Statuses.Authorized)
            return Result<Guid>.Failure(
                "No se puede anular una salesBill ya autorizada por el SRI. Debe emitir una note de crédito.");

        if (invoice.Status == Invoice.Statuses.Voided)
            return Result<Guid>.Failure("La salesBill ya está anulada.");

        _logger.LogInformation(
            "Anulando salesBill {FacturaId} (estado previo={Estado}, tenant={SubscriberId})",
            invoice.PublicId, invoice.Status, subscriberId);

        invoice.Void(userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId, userId, _currentUser.Email, _currentUser.FullName,
            module: "ventas", action: "venta.anular",
            entityType: "Invoice", entityId: invoice.PublicId,
            description: invoice.InvoiceNumber), ct);

        await _invoiceRepository.SaveChangesAsync(ct);

        return Result<Guid>.Success(invoice.PublicId);
    }
}
