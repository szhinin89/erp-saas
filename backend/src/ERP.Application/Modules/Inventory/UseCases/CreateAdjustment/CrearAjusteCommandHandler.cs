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

namespace ERP.Application.Inventory.UseCases.CreateAdjustment;

public sealed class CreateStockAdjustmentCommandHandler
    : IRequestHandler<CreateStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _ajusteRepo;
    private readonly IWarehouseRepository           _bodegaRepo;
    private readonly IProductRepository          _productRepo;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentSubscriber              _currentSubscriber;
    private readonly ICurrentCompany               _currentCompany;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CreateStockAdjustmentCommandHandler> _logger;

    public CreateStockAdjustmentCommandHandler(
        IStockAdjustmentRepository ajusteRepo,
        IWarehouseRepository bodegaRepo,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CreateStockAdjustmentCommandHandler> logger)
    {
        _ajusteRepo    = ajusteRepo;
        _bodegaRepo    = bodegaRepo;
        _productRepo   = productRepo;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _currentUser   = currentUser;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        CreateStockAdjustmentCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var Warehouse = await _bodegaRepo.GetByIdAsync(subscriberId, command.WarehouseId, ct);
        if (Warehouse is null || !Warehouse.IsActive)
            return Result<StockAdjustmentDto>.Failure("La Warehouse no existe o no está activa.");

        var producto = await _productRepo.GetByIdAsync(command.ProductId, subscriberId, ct);
        if (producto is null || !producto.IsActive)
            return Result<StockAdjustmentDto>.Failure("El producto no existe o no está activo.");

        if (producto.IsService || !producto.TracksStock)
            return Result<StockAdjustmentDto>.Failure(
                "El producto es un servicio o no maneja stock físico y no puede tener ajustes de inventario.");

        if (_currentCompany.HasCompanyContext &&
            (Warehouse.CompanyId != _currentCompany.CompanyId || producto.CompanyId != _currentCompany.CompanyId))
            return Result<StockAdjustmentDto>.Failure("La bodega y el producto deben pertenecer a la empresa operativa activa.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var secuencial = await _ajusteRepo.GetNextSequentialAsync(subscriberId, ct);

            var companyIdOp = _currentCompany.HasCompanyContext ? _currentCompany.CompanyId : (Guid?)null;
            var ajuste = StockAdjustment.Create(
                subscriberId, secuencial,
                command.WarehouseId,   Warehouse.Name,
                command.ProductId, producto.ShortName,
                command.AdjustmentQty, command.Reason, command.Notes,
                userId,
                companyId: companyIdOp);

            await _ajusteRepo.AddAsync(ajuste, ct);

            await _activity.AddAsync(UserActivity.Create(
                subscriberId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "ajuste.crear",
                entityType: "StockAdjustment", entityId: ajuste.Id,
                description: ajuste.AdjustmentNumber), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Ajuste creado: {Numero} ({Id})", ajuste.AdjustmentNumber, ajuste.Id);

            return Result<StockAdjustmentDto>.Success(ToDto(ajuste));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear ajuste (tenant {SubscriberId})", subscriberId);
            return Result<StockAdjustmentDto>.Failure($"No se pudo crear el ajuste: {ex.Message}");
        }
    }

    private static StockAdjustmentDto ToDto(StockAdjustment a) => new(
        a.Id, a.AdjustmentNumber,
        a.WarehouseId,   a.WarehouseName,
        a.ProductId, a.ProductName,
        a.AdjustmentQty, a.AdjustmentType,
        a.Reason, a.Notes,
        a.AdjustmentDate, a.Status,
        a.ExecutedAt, a.ExecutedBy,
        a.CreatedAt);
}
