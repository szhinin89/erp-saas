using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Fiscal.Integration;
using ERP.Application.Sales.Services;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.ReceivedRetentions;

public sealed class RegisterSalesRetentionCommandHandler
    : IRequestHandler<RegisterSalesRetentionCommand, Result<Guid>>
{
    private readonly ISalesRepository     _ventasRepository;
    private readonly ISalesOriginalBillResolver _originalBillResolver;
    private readonly IFileStorage        _fileStorage;
    private readonly IAccountingService  _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly IUnitOfWork         _unitOfWork;
    private readonly ICurrentSubscriber      _currentSubscriber;
    private readonly ICurrentUser      _currentUser;
    private readonly ILogger<RegisterSalesRetentionCommandHandler> _logger;

    public RegisterSalesRetentionCommandHandler(
        ISalesRepository ventasRepository,
        ISalesOriginalBillResolver originalBillResolver,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<RegisterSalesRetentionCommandHandler> logger)
    {
        _ventasRepository = ventasRepository;
        _originalBillResolver = originalBillResolver;
        _fileStorage      = fileStorage;
        _accounting       = accounting;
        _activity         = activity;
        _unitOfWork       = unitOfWork;
        _currentSubscriber    = currentSubscriber;
        _currentUser      = currentUser;
        _logger           = logger;
    }

    public async Task<Result<Guid>> Handle(RegisterSalesRetentionCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        if (string.IsNullOrWhiteSpace(command.XmlContent))
            return Result<Guid>.Failure("El XML es obligatorio.");

        if (!RetentionRecibidaXmlParser.TryParse(command.XmlContent, out var clave, out var fecha, out var total))
            return Result<Guid>.Failure("No se pudo interpretar el XML de retención (clave / totales).");

        var existe = await _ventasRepository.ExistsRetentionAccessKeyAsync(subscriberId, clave, ct);
        if (existe)
            return Result<Guid>.Failure("Ya existe una retención recibida con la misma clave de acceso.");

        var salesBill = await _originalBillResolver.ResolveAsync(subscriberId, command.SalesBillId, ct);
        if (salesBill is null)
            return Result<Guid>.Failure("salesBill de venta no encontrada.");
        if (salesBill.Status != "Autorizado")
            return Result<Guid>.Failure("La salesBill debe estar autorizada.");

        var issueDate = fecha ?? DateTime.UtcNow;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var xmlPath = $"ventas/retenciones-recibidas/{subscriberId}/{clave}.xml";
            try
            {
                await _fileStorage.SaveAsync(xmlPath, new MemoryStream(Encoding.UTF8.GetBytes(command.XmlContent)), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se guardó copia del XML");
                xmlPath = null!;
            }

            var ret = SalesRetention.Create(
                subscriberId,
                salesBill.BusinessPartnerId,
                clave,
                issueDate,
                total,
                salesBill.Id,
                xmlPath,
                userId);

            var det = SalesRetentionLine.Create(
                subscriberId, "TOTAL", "0", total, 0, total, userId);
            det.AssignRetentionId(ret.Id);
            ret.AddLine(det);

            var asiento = await _accounting.CreateReceivedWithholdingJournalEntryAsync(
                ret.Id, clave, issueDate, total,
                $"Retención recibida {clave}", ct);
            if (!asiento.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<Guid>.Failure(asiento.Error ?? "Asiento contable");
            }

            ret.LinkJournalEntry(asiento.Value, userId);

            await _ventasRepository.AddRetentionAsync(ret, ct);
            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "ventas.retention-recibida.registrar",
                entityType: "SalesRetention", entityId: ret.Id,
                description: clave), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<Guid>.Success(ret.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<Guid>.Failure(ex.Message);
        }
    }
}
