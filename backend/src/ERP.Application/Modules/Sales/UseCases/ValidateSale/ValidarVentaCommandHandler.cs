using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Fiscal.Interfaces;

namespace ERP.Application.Sales.UseCases.ValidateSale;

public sealed class ValidateSaleCommandHandler : IRequestHandler<ValidateSaleCommand, Result<Guid>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ValidateSaleCommandHandler> _logger;

    public ValidateSaleCommandHandler(
        IInvoiceRepository invoiceRepository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<ValidateSaleCommandHandler> logger)
    {
        _invoiceRepository = invoiceRepository;
        _activity = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(ValidateSaleCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        _logger.LogInformation("Validando factura {FacturaId} (tenant={SubscriberId})", command.VentaId, subscriberId);

        var invoice = await _invoiceRepository.GetByPublicIdAsync(command.VentaId, ct);
        if (invoice is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");

        if (invoice.Status != Invoice.Statuses.Draft)
            return Result<Guid>.Failure(
                $"Solo se puede validar una factura en Borrador (estado actual: {invoice.Status}).");

        if (invoice.Lines.Count == 0)
            return Result<Guid>.Failure("La factura debe tener al menos un detalle.");

        var totalCalculado = invoice.Subtotal + invoice.TaxTotal;
        if (Math.Abs(totalCalculado - invoice.Total) > 0.01m)
            return Result<Guid>.Failure(
                $"Los totales no cuadran: Subtotal({invoice.Subtotal:F2}) + IVA({invoice.TaxTotal:F2}) = " +
                $"{totalCalculado:F2}, pero Total es {invoice.Total:F2}.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            invoice.Validate(userId);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "venta.validar",
                entityType: "Invoice", entityId: invoice.PublicId,
                description: invoice.InvoiceNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<Guid>.Success(invoice.PublicId);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al validar factura {FacturaId}", command.VentaId);
            return Result<Guid>.Failure($"No se pudo validar la factura: {ex.Message}");
        }
    }
}
