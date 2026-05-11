using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Bodegas.Interfaces;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Inventario.UseCases.CrearTransferencia;

public sealed class CrearTransferenciaCommandHandler
    : IRequestHandler<CrearTransferenciaCommand, Result<TransferenciaDto>>
{
    private readonly ITransferenciaRepository    _transferenciaRepo;
    private readonly IBodegaRepository           _bodegaRepo;
    private readonly IProductRepository          _productRepo;
    private readonly IInventarioStockRepository  _stockRepo;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CrearTransferenciaCommandHandler> _logger;

    public CrearTransferenciaCommandHandler(
        ITransferenciaRepository transferenciaRepo,
        IBodegaRepository bodegaRepo,
        IProductRepository productRepo,
        IInventarioStockRepository stockRepo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CrearTransferenciaCommandHandler> logger)
    {
        _transferenciaRepo = transferenciaRepo;
        _bodegaRepo        = bodegaRepo;
        _productRepo       = productRepo;
        _stockRepo         = stockRepo;
        _activity          = activity;
        _currentTenant     = currentTenant;
        _currentUser       = currentUser;
        _unitOfWork        = unitOfWork;
        _logger            = logger;
    }

    public async Task<Result<TransferenciaDto>> Handle(
        CrearTransferenciaCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        _logger.LogInformation(
            "Creando transferencia: origen={Origen}, destino={Destino}, ítems={Count}",
            command.BodegaOrigenId, command.BodegaDestinoId, command.Items.Count);

        // 1. Validar bodegas
        var origen = await _bodegaRepo.GetByIdAsync(tenantId, command.BodegaOrigenId, ct);
        if (origen is null || !origen.IsActive)
            return Result<TransferenciaDto>.Failure("La bodega origen no existe o no está activa.");

        var destino = await _bodegaRepo.GetByIdAsync(tenantId, command.BodegaDestinoId, ct);
        if (destino is null || !destino.IsActive)
            return Result<TransferenciaDto>.Failure("La bodega destino no existe o no está activa.");

        // 2. Validar productos + stock (de-duplication por ProductoId)
        var productos = new Dictionary<Guid, ERP.Domain.Products.Entities.Product>();
        foreach (var item in command.Items)
        {
            if (productos.ContainsKey(item.ProductoId)) continue;
            var producto = await _productRepo.GetByIdAsync(item.ProductoId, tenantId, ct);
            if (producto is null || !producto.IsActive)
                return Result<TransferenciaDto>.Failure(
                    $"El producto {item.ProductoId} no existe o no está activo.");
            productos[item.ProductoId] = producto;
        }

        foreach (var item in command.Items)
        {
            var producto = productos[item.ProductoId];
            if (producto.IsService || !producto.TracksStock) continue;

            var stock = await _stockRepo.GetStockByTenantBodegaProductAsync(
                tenantId, command.BodegaOrigenId, item.ProductoId, ct);

            if (stock is null || stock.CantidadDisponible < item.Cantidad)
            {
                _logger.LogWarning(
                    "Stock insuficiente: producto={ProductoId}, disponible={Disp}, solicitado={Sol}",
                    item.ProductoId, stock?.CantidadDisponible ?? 0, item.Cantidad);
                return Result<TransferenciaDto>.Failure(
                    $"Stock insuficiente para '{producto.ShortName}' en la bodega origen. " +
                    $"Disponible: {stock?.CantidadDisponible ?? 0}, Solicitado: {item.Cantidad}");
            }
        }

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 3. Generar número de transferencia (dentro de la transacción)
            var secuencial = await _transferenciaRepo.GetNextSecuencialAsync(tenantId, ct);

            // 4. Crear la transferencia en Borrador + sus detalles
            // El Id es client-generated (Guid.NewGuid()), no requiere flush previo.
            var transferencia = Transferencia.Create(
                tenantId, secuencial,
                command.BodegaOrigenId, command.BodegaDestinoId,
                command.Motivo, command.Observaciones, userId);

            foreach (var item in command.Items)
            {
                var producto = productos[item.ProductoId];
                var detalle  = TransferenciaDetalle.Create(
                    tenantId, transferencia.Id, item.ProductoId,
                    item.Cantidad, producto.Description, userId);
                transferencia.AgregarDetalle(detalle);
            }

            await _transferenciaRepo.AddAsync(transferencia, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "transferencia.crear",
                entityType: "Transferencia", entityId: transferencia.Id,
                description: transferencia.NumeroTransferencia), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Transferencia creada: {Numero} ({Id})",
                transferencia.NumeroTransferencia, transferencia.Id);

            return Result<TransferenciaDto>.Success(ToDto(transferencia, origen.Nombre, destino.Nombre));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear transferencia (tenant {TenantId})", tenantId);
            return Result<TransferenciaDto>.Failure($"No se pudo crear la transferencia: {ex.Message}");
        }
    }

    private static TransferenciaDto ToDto(Transferencia t, string nombreOrigen, string nombreDestino) => new(
        t.Id, t.NumeroTransferencia,
        t.BodegaOrigenId, nombreOrigen,
        t.BodegaDestinoId, nombreDestino,
        t.FechaTransferencia, t.Estado,
        t.Motivo, t.Observaciones,
        t.FechaConfirmacion, t.ConfirmadoPor,
        t.CreatedAt);
}
