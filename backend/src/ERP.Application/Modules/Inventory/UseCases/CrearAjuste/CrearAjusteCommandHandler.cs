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

namespace ERP.Application.Inventory.UseCases.CrearAjuste;

public sealed class CrearAjusteCommandHandler
    : IRequestHandler<CrearAjusteCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _ajusteRepo;
    private readonly IWarehouseRepository           _bodegaRepo;
    private readonly IProductRepository          _productRepo;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CrearAjusteCommandHandler> _logger;

    public CrearAjusteCommandHandler(
        IStockAdjustmentRepository ajusteRepo,
        IWarehouseRepository bodegaRepo,
        IProductRepository productRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CrearAjusteCommandHandler> logger)
    {
        _ajusteRepo    = ajusteRepo;
        _bodegaRepo    = bodegaRepo;
        _productRepo   = productRepo;
        _activity      = activity;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _unitOfWork    = unitOfWork;
        _logger        = logger;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        CrearAjusteCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var Warehouse = await _bodegaRepo.GetByIdAsync(tenantId, command.WarehouseId, ct);
        if (Warehouse is null || !Warehouse.IsActive)
            return Result<StockAdjustmentDto>.Failure("La Warehouse no existe o no está activa.");

        var producto = await _productRepo.GetByIdAsync(command.ProductId, tenantId, ct);
        if (producto is null || !producto.IsActive)
            return Result<StockAdjustmentDto>.Failure("El producto no existe o no está activo.");

        if (producto.IsService || !producto.TracksStock)
            return Result<StockAdjustmentDto>.Failure(
                "El producto es un servicio o no maneja stock físico y no puede tener ajustes de inventario.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var secuencial = await _ajusteRepo.GetNextSequentialAsync(tenantId, ct);

            var ajuste = StockAdjustment.Create(
                tenantId, secuencial,
                command.WarehouseId,   Warehouse.Name,
                command.ProductId, producto.ShortName,
                command.AdjustmentQty, command.Reason, command.Notes,
                userId);

            await _ajusteRepo.AddAsync(ajuste, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
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
            _logger.LogError(ex, "Error al crear ajuste (tenant {TenantId})", tenantId);
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
