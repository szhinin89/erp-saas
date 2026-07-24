using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Command ────────────────────────────────────────────────────────────

/// <summary>
/// Actualiza Nombre/Notas y opcionalmente reasigna el Punto de Emisión. Código y Sucursal son
/// inmutables post-creación (Código igual que <c>EmissionPoint.Code</c>; Sucursal por Branch
/// Ownership Rule). DefaultWarehouseId/DefaultCustomerId son conveniencias operativas — siempre
/// editables, incluso con historial (a diferencia de EmissionPointId).
/// </summary>
public sealed record UpdateCashRegisterCommand(
    Guid Id,
    string Name,
    string? Notes,
    Guid? EmissionPointId,
    Guid? DefaultWarehouseId = null,
    Guid? DefaultCustomerId = null)
    : IRequest<Result<CashRegisterDto>>, ICompanyScopedRequest;

// ── Validator ──────────────────────────────────────────────────────────

public sealed class UpdateCashRegisterValidator : AbstractValidator<UpdateCashRegisterCommand>
{
    public UpdateCashRegisterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CashRegister.NameMaxLen)
            .WithMessage($"El nombre de la caja es obligatorio y no puede exceder {CashRegister.NameMaxLen} caracteres.");
        RuleFor(x => x.Notes).MaximumLength(CashRegister.NotesMaxLen)
            .WithMessage($"Las notas no pueden exceder {CashRegister.NotesMaxLen} caracteres.");
    }
}

// ── Handler ────────────────────────────────────────────────────────────

public sealed class UpdateCashRegisterHandler : IRequestHandler<UpdateCashRegisterCommand, Result<CashRegisterDto>>
{
    private readonly ICashRegisterRepository _repo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IBusinessPartnerRepository _customerRepo;
    private readonly ICashRegisterUsageGuard _usageGuard;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdateCashRegisterHandler(
        ICashRegisterRepository repo, IEmissionPointRepository emissionPointRepo,
        IWarehouseRepository warehouseRepo, IBusinessPartnerRepository customerRepo,
        ICashRegisterUsageGuard usageGuard, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _emissionPointRepo = emissionPointRepo;
        _warehouseRepo = warehouseRepo;
        _customerRepo = customerRepo;
        _usageGuard = usageGuard;
        _t = t; _u = u;
    }

    public async Task<Result<CashRegisterDto>> Handle(UpdateCashRegisterCommand cmd, CancellationToken ct)
    {
        var tid = _t.TenantId;
        var entity = await _repo.GetByIdAsync(tid, cmd.Id, ct);
        if (entity is null)
            return Result<CashRegisterDto>.NotFound("Caja no encontrada.");

        var hasHistory = await _usageGuard.HasHistoryAsync(tid, entity.Id, ct);
        var changesEmissionPoint = cmd.EmissionPointId != entity.EmissionPointId;

        // Regla de integridad histórica (INMUTABLE): una Caja con historial operativo no puede
        // reasignar su Punto de Emisión — su identidad operacional queda congelada. El
        // procedimiento correcto es desactivarla y crear una Caja nueva.
        if (hasHistory && changesEmissionPoint)
        {
            return Result<CashRegisterDto>.ValidationFailure(
                "Esta caja ya tiene historial operativo (aperturas, movimientos o cierres) y su punto de emisión no puede reasignarse. " +
                "Desactive esta caja y cree una nueva para operar con otro punto de emisión.");
        }

        if (changesEmissionPoint && cmd.EmissionPointId.HasValue)
        {
            var emissionPoint = await _emissionPointRepo.GetByIdAsync(cmd.EmissionPointId.Value, tid, ct);
            if (emissionPoint is null)
                return Result<CashRegisterDto>.ValidationFailure("El punto de emisión no existe.");
            if (emissionPoint.Establishment.BranchId != entity.BranchId)
                return Result<CashRegisterDto>.ValidationFailure("El punto de emisión no pertenece a la sucursal de la caja.");
        }

        if (cmd.DefaultWarehouseId != entity.DefaultWarehouseId && cmd.DefaultWarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepo.GetByIdAsync(tid, cmd.DefaultWarehouseId.Value, ct);
            if (warehouse is null)
                return Result<CashRegisterDto>.ValidationFailure("La bodega por defecto no existe.");
            if (warehouse.BranchId != entity.BranchId)
                return Result<CashRegisterDto>.ValidationFailure("La bodega por defecto no pertenece a la sucursal de la caja.");
        }

        if (cmd.DefaultCustomerId != entity.DefaultCustomerId && cmd.DefaultCustomerId.HasValue)
        {
            var customer = await _customerRepo.GetByIdAsync(cmd.DefaultCustomerId.Value, ct);
            if (customer is null)
                return Result<CashRegisterDto>.ValidationFailure("El cliente por defecto no existe.");
        }

        entity.Update(cmd.Name.Trim(), cmd.Notes, _u.UserId);
        if (changesEmissionPoint)
            entity.ChangeEmissionPoint(cmd.EmissionPointId, _u.UserId);
        if (cmd.DefaultWarehouseId != entity.DefaultWarehouseId)
            entity.SetDefaultWarehouse(cmd.DefaultWarehouseId, _u.UserId);
        if (cmd.DefaultCustomerId != entity.DefaultCustomerId)
            entity.SetDefaultCustomer(cmd.DefaultCustomerId, _u.UserId);

        await _repo.SaveChangesAsync(ct);

        var updated = await _repo.GetByIdAsync(tid, entity.Id, ct);
        return Result<CashRegisterDto>.Success(CajaMapper.ToDto(updated!, hasHistory));
    }
}
