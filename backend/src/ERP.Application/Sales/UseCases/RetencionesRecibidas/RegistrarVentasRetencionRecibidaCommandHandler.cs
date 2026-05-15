using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Sales.Services;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.RetencionesRecibidas;

public sealed class RegistrarSalesRetentionCommandHandler
    : IRequestHandler<RegistrarSalesRetentionCommand, Result<Guid>>
{
    private readonly ISalesRepository     _ventasRepository;
    private readonly IFileStorage        _fileStorage;
    private readonly IAccountingService  _accounting;
    private readonly IUserActivityRepository _activity;
    private readonly IUnitOfWork         _unitOfWork;
    private readonly ICurrentTenant      _currentTenant;
    private readonly ICurrentUser      _currentUser;
    private readonly ILogger<RegistrarSalesRetentionCommandHandler> _logger;

    public RegistrarSalesRetentionCommandHandler(
        ISalesRepository ventasRepository,
        IFileStorage fileStorage,
        IAccountingService accounting,
        IUserActivityRepository activity,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ILogger<RegistrarSalesRetentionCommandHandler> logger)
    {
        _ventasRepository = ventasRepository;
        _fileStorage      = fileStorage;
        _accounting       = accounting;
        _activity         = activity;
        _unitOfWork       = unitOfWork;
        _currentTenant    = currentTenant;
        _currentUser      = currentUser;
        _logger           = logger;
    }

    public async Task<Result<Guid>> Handle(RegistrarSalesRetentionCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        if (string.IsNullOrWhiteSpace(command.XmlContent))
            return Result<Guid>.Failure("El XML es obligatorio.");

        if (!RetencionRecibidaXmlParser.TryParse(command.XmlContent, out var clave, out var fecha, out var total))
            return Result<Guid>.Failure("No se pudo interpretar el XML de retención (clave / totales).");

        var existe = await _ventasRepository.ExistsRetentionAccessKeyAsync(tenantId, clave, ct);
        if (existe)
            return Result<Guid>.Failure("Ya existe una retención recibida con la misma clave de acceso.");

        var factura = await _ventasRepository.GetBillByIdAsync(tenantId, command.SalesBillId, ct);
        if (factura is null)
            return Result<Guid>.Failure("Factura de venta no encontrada.");
        if (factura.Status != "Autorizado")
            return Result<Guid>.Failure("La factura debe estar autorizada.");

        var fechaEmision = fecha ?? DateTime.UtcNow;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var xmlPath = $"ventas/retenciones-recibidas/{tenantId}/{clave}.xml";
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
                tenantId,
                factura.CustomerId,
                clave,
                fechaEmision,
                total,
                factura.Id,
                xmlPath,
                userId);

            var det = SalesRetentionLine.Create(
                tenantId, "TOTAL", "0", total, 0, total, userId);
            det.AssignRetentionId(ret.Id);
            ret.AddLine(det);

            var asiento = await _accounting.CrearAsientoRetencionRecibidaAsync(
                ret.Id, clave, fechaEmision, total,
                $"Retención recibida {clave}", ct);
            if (!asiento.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<Guid>.Failure(asiento.Error ?? "Asiento contable");
            }

            ret.LinkJournalEntry(asiento.Value, userId);

            await _ventasRepository.AddRetentionAsync(ret, ct);
            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "ventas", action: "ventas.retencion-recibida.registrar",
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
