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

namespace ERP.Application.Inventario.UseCases.CrearAjuste;

public sealed class CrearAjusteCommandHandler
    : IRequestHandler<CrearAjusteCommand, Result<AjusteInventarioDto>>
{
    private readonly IAjusteInventarioRepository _ajusteRepo;
    private readonly IBodegaRepository           _bodegaRepo;
    private readonly IProductRepository          _productRepo;
    private readonly IUserActivityRepository     _activity;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ILogger<CrearAjusteCommandHandler> _logger;

    public CrearAjusteCommandHandler(
        IAjusteInventarioRepository ajusteRepo,
        IBodegaRepository bodegaRepo,
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

    public async Task<Result<AjusteInventarioDto>> Handle(
        CrearAjusteCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var bodega = await _bodegaRepo.GetByIdAsync(tenantId, command.BodegaId, ct);
        if (bodega is null || !bodega.IsActive)
            return Result<AjusteInventarioDto>.Failure("La bodega no existe o no está activa.");

        var producto = await _productRepo.GetByIdAsync(command.ProductoId, tenantId, ct);
        if (producto is null || !producto.IsActive)
            return Result<AjusteInventarioDto>.Failure("El producto no existe o no está activo.");

        if (producto.IsService || !producto.TracksStock)
            return Result<AjusteInventarioDto>.Failure(
                "El producto es un servicio o no maneja stock físico y no puede tener ajustes de inventario.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var secuencial = await _ajusteRepo.GetNextSecuencialAsync(tenantId, ct);

            var ajuste = AjusteInventario.Create(
                tenantId, secuencial,
                command.BodegaId,   bodega.Nombre,
                command.ProductoId, producto.ShortName,
                command.CantidadAjuste, command.Motivo, command.Observaciones,
                userId);

            await _ajusteRepo.AddAsync(ajuste, ct);

            await _activity.AddAsync(UserActivity.Create(
                tenantId, userId, _currentUser.Email, _currentUser.FullName,
                module: "inventario", action: "ajuste.crear",
                entityType: "AjusteInventario", entityId: ajuste.Id,
                description: ajuste.NumeroAjuste), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Ajuste creado: {Numero} ({Id})", ajuste.NumeroAjuste, ajuste.Id);

            return Result<AjusteInventarioDto>.Success(ToDto(ajuste));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            _logger.LogError(ex, "Error al crear ajuste (tenant {TenantId})", tenantId);
            return Result<AjusteInventarioDto>.Failure($"No se pudo crear el ajuste: {ex.Message}");
        }
    }

    private static AjusteInventarioDto ToDto(AjusteInventario a) => new(
        a.Id, a.NumeroAjuste,
        a.BodegaId,   a.BodegaNombre,
        a.ProductoId, a.ProductoNombre,
        a.CantidadAjuste, a.TipoAjuste,
        a.Motivo, a.Observaciones,
        a.FechaAjuste, a.Estado,
        a.FechaEjecucion, a.EjecutadoPor,
        a.CreatedAt);
}
