using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Sales.UseCases.EmitirFacturaElectronica;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Fiscal.Interfaces;

namespace ERP.Application.Sales.UseCases.ReintentarEnvio;

public sealed class RetrySubmissionCommandHandler : IRequestHandler<RetrySubmissionCommand, Result<Guid>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMediator _mediator;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RetrySubmissionCommandHandler> _logger;

    public RetrySubmissionCommandHandler(
        IInvoiceRepository invoiceRepository,
        IMediator mediator,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<RetrySubmissionCommandHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _mediator = mediator;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(RetrySubmissionCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        _logger.LogInformation(
            "Reintentando envío SRI de factura {FacturaId} (tenant={SubscriberId})",
            command.VentaId, subscriberId);

        var invoice = await _invoiceRepository.GetByPublicIdAsync(command.VentaId, ct);
        if (invoice is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (invoice.Status != Invoice.Statuses.SendError && invoice.Status != Invoice.Statuses.Rejected)
            return Result<Guid>.Failure(
                $"Solo se puede reintentar una factura en ErrorEnvio o Rechazado (estado actual: {invoice.Status}).");

        invoice.PrepareRetry(userId);
        await _invoiceRepository.SaveChangesAsync(ct);

        return await _mediator.Send(new IssueElectronicInvoiceCommand(command.VentaId), ct);
    }
}
