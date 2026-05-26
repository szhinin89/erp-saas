using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Inventory.UseCases.CreateTransfer;

public sealed class CreateTransferCommandHandler
    : IRequestHandler<CreateTransferCommand, Result<TransferDto>>
{
    private readonly IStockTransferRepository    _transferenciaRepo;
    private readonly IWarehouseRepository           _bodegaRepo;
    private readonly IProductRepository          _productRepo;
    private readonly IStockRepository  _stockRepo;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentSubscriber              _currentSubscriber;
    private readonly ICurrentCompany               _currentCompany;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CreateTransferCommandHandler> _logger;

    public CreateTransferCommandHandler(
        IStockTransferRepository transferenciaRepo,
        IWarehouseRepository bodegaRepo,
        IProductRepository productRepo,
        IStockRepository stockRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateTransferCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _bodegaRepo        = bodegaRepo;
        _productRepo       = productRepo;
        _stockRepo         = stockRepo;
        _activity          = activity;
        _currentSubscriber     = currentSubscriber;
        _currentCompany    = currentCompany;
        _currentUser       = currentUser;
        _unitOfWork        = unitOfWork;
        _logger            = logger;
    }

    public async Task<Result<TransferDto>> Handle(
        CreateTransferCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        _logger.LogInformation(
            "Creando transfer: origen={Origen}, destino={Destino}, ítems={Count}",
            command.SourceWarehouseId, command.TargetWarehouseId, command.Items.Count);

        // 1. Validar bodegas
        var origen = await _bodegaRepo.GetByIdAsync(subscriberId, command.SourceWarehouseId, ct);
        if (origen is null || !origen.IsActive)
            return Result<TransferDto>.Failure("La Warehouse origen no existe o no está activa.");

        var destino = await _bodegaRepo.GetByIdAsync(subscriberId, command.TargetWarehouseId, ct);
        if (destino is null || !destino.IsActive)
            return Result<TransferDto>.Failure("La Warehouse destino no existe o no está activa.");

        if (_currentCompany.HasCompanyContext)
        {
            var companyId = _currentCompany.CompanyId;
            if (origen.CompanyId != companyId || destino.CompanyId != companyId)
                return Result<TransferDto>.Failure("Las bodegas deben pertenecer a la empresa operativa activa.");
        }

        // 2. Validar productos + stock (de-duplication por ProductoId)
        var productos = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productos.ContainsKey(item.ProductId)) continue;
            var producto = await _productRepo.GetByIdAsync(item.ProductId, subscriberId, ct);
            if (producto is null || !producto.IsActive)
                return Result<TransferDto>.Failure(
                    $"El producto {item.ProductId} no existe o no está activo.");
            productos[item.ProductId] = producto;
        }

        foreach (var item in command.Items)
        {
            var producto = productos[item.ProductId];
            if (producto.IsService || !producto.TracksStock) continue;

            var stock = await _stockRepo.GetStockAsync(
                subscriberId, command.SourceWarehouseId, item.ProductId, ct);

            if (stock is null || stock.AvailableQuantity < item.Quantity)
            {
                _logger.LogWarning(
                    "Stock insuficiente: producto={ProductoId}, disponible={Disp}, solicitado={Sol}",
                    item.ProductId, stock?.AvailableQuantity ?? 0, item.Quantity);
                return Result<TransferDto>.Failure(
                    $"Stock insuficiente para '{producto.ShortName}' en la Warehouse origen. " +
                    $"Disponible: {stock?.AvailableQuantity ?? 0}, Solicitado: {item.Quantity}");
            }
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 3. Generar número de transfer (dentro de la transacción)
            var secuencial = await _transferenciaRepo.GetNextSequentialAsync(subscriberId, ct);

            // 4. Crear la transfer en Borrador + sus detalles
            // El Id es client-generated (Guid.NewGuid()), no requiere flush previo.
            var companyIdOp = _currentCompany.HasCompanyContext ? _currentCompany.CompanyId : (Guid?)null;
            var t = StockTransfer.Create(
                subscriberId, secuencial,
                command.SourceWarehouseId, command.TargetWarehouseId,
                command.Reason, command.Notes, userId,
                companyId: companyIdOp);

            foreach (var item in command.Items)
            {
                var producto = productos[item.ProductId];
                var detalle  = StockTransferLine.Create(
                    subscriberId, t.Id, item.ProductId,
                    item.Quantity, producto.Description, userId);
                t.AddLine(detalle);
            }

            await _transferenciaRepo.AddAsync(t, ct);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "transfer.crear",
                entityType: "StockTransfer", entityId: t.Id,
                description: t.TransferNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("StockTransfer creada: {Numero} ({Id})",
                t.TransferNumber, t.Id);

            return Result<TransferDto>.Success(ToDto(t, origen.Name, destino.Name));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear transfer (tenant {SubscriberId})", subscriberId);
            return Result<TransferDto>.Failure($"No se pudo crear la transfer: {ex.Message}");
        }
    }

    private static TransferDto ToDto(StockTransfer t, string nombreOrigen, string nombreDestino) => new(
        t.Id, t.TransferNumber,
        t.SourceWarehouseId, nombreOrigen,
        t.TargetWarehouseId, nombreDestino,
        t.TransferDate, t.Status,
        t.Reason, t.Notes,
        t.ConfirmedAt, t.ConfirmedBy,
        t.CreatedAt);
}
