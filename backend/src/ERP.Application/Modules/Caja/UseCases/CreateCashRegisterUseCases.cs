using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Command ────────────────────────────────────────────────────────────

/// <summary>
/// Comando de administración: la Caja es un recurso de configuración (igual que <c>Warehouse</c>),
/// no un documento de proceso operativo — el admin elige explícitamente la Sucursal, a diferencia
/// de <c>OpenCashSessionCommand</c> (el proceso operativo real) que sigue resolviendo BranchId
/// exclusivamente desde <c>ICurrentBranch</c>. CompanyId/TenantId nunca vienen del cliente.
/// </summary>
public sealed record CreateCashRegisterCommand(
    Guid BranchId,
    string Code,
    string Name,
    Guid? EmissionPointId = null,
    string? Notes = null,
    Guid? DefaultWarehouseId = null,
    Guid? DefaultCustomerId = null)
    : IRequest<Result<CashRegisterDto>>, ICompanyScopedRequest;

// ── Validator ──────────────────────────────────────────────────────────

public sealed class CreateCashRegisterValidator : AbstractValidator<CreateCashRegisterCommand>
{
    public CreateCashRegisterValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es obligatoria.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CashRegister.CodeMaxLen)
            .WithMessage($"El código de la caja es obligatorio y no puede exceder {CashRegister.CodeMaxLen} caracteres.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CashRegister.NameMaxLen)
            .WithMessage($"El nombre de la caja es obligatorio y no puede exceder {CashRegister.NameMaxLen} caracteres.");
        RuleFor(x => x.EmissionPointId).NotNull()
            .WithMessage("El punto de emisión es obligatorio.");
        RuleFor(x => x.Notes).MaximumLength(CashRegister.NotesMaxLen)
            .WithMessage($"Las notas no pueden exceder {CashRegister.NotesMaxLen} caracteres.");
    }
}

// ── Handler ────────────────────────────────────────────────────────────

public sealed class CreateCashRegisterHandler : IRequestHandler<CreateCashRegisterCommand, Result<CashRegisterDto>>
{
    private readonly ICashRegisterRepository _repo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly IBranchRepository _branchRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IBusinessPartnerRepository _customerRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CreateCashRegisterHandler(
        ICashRegisterRepository repo,
        IEmissionPointRepository emissionPointRepo,
        IBranchRepository branchRepo,
        IWarehouseRepository warehouseRepo,
        IBusinessPartnerRepository customerRepo,
        ICurrentTenant t, ICurrentCompany c, ICurrentUser u)
    {
        _repo = repo;
        _emissionPointRepo = emissionPointRepo;
        _branchRepo = branchRepo;
        _warehouseRepo = warehouseRepo;
        _customerRepo = customerRepo;
        _t = t; _c = c; _u = u;
    }

    public async Task<Result<CashRegisterDto>> Handle(CreateCashRegisterCommand cmd, CancellationToken ct)
    {
        var tid = _t.TenantId;
        var code = cmd.Code.Trim();

        var branch = await _branchRepo.GetByIdAsync(tid, cmd.BranchId, ct);
        if (branch is null || !branch.IsActive)
            return Result<CashRegisterDto>.ValidationFailure("La sucursal no existe o no está activa.");

        if (cmd.EmissionPointId.HasValue)
        {
            var emissionPoint = await _emissionPointRepo.GetByIdAsync(cmd.EmissionPointId.Value, tid, ct);
            if (emissionPoint is null)
                return Result<CashRegisterDto>.ValidationFailure("El punto de emisión no existe.");
            if (emissionPoint.Establishment.BranchId != cmd.BranchId)
                return Result<CashRegisterDto>.ValidationFailure("El punto de emisión no pertenece a la sucursal seleccionada.");
        }

        if (cmd.DefaultWarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepo.GetByIdAsync(tid, cmd.DefaultWarehouseId.Value, ct);
            if (warehouse is null)
                return Result<CashRegisterDto>.ValidationFailure("La bodega por defecto no existe.");
            if (warehouse.BranchId != cmd.BranchId)
                return Result<CashRegisterDto>.ValidationFailure("La bodega por defecto no pertenece a la sucursal seleccionada.");
        }

        if (cmd.DefaultCustomerId.HasValue)
        {
            var customer = await _customerRepo.GetByIdAsync(cmd.DefaultCustomerId.Value, ct);
            if (customer is null)
                return Result<CashRegisterDto>.ValidationFailure("El cliente por defecto no existe.");
        }

        var exists = await _repo.ExistsByCodeAsync(tid, cmd.BranchId, code, ct: ct);
        if (exists)
            return Result<CashRegisterDto>.ValidationFailure($"Ya existe una caja con código '{code}' en esta sucursal.");

        var register = CashRegister.Create(
            tid, _c.CompanyId, cmd.BranchId, code, cmd.Name.Trim(), _u.UserId,
            cmd.EmissionPointId, cmd.Notes, cmd.DefaultWarehouseId, cmd.DefaultCustomerId);

        await _repo.AddAsync(register, ct);
        await _repo.SaveChangesAsync(ct);

        var created = await _repo.GetByIdAsync(tid, register.Id, ct);
        // Una Caja recién creada nunca tiene historial — no requiere consultar el guard.
        return Result<CashRegisterDto>.Success(CajaMapper.ToDto(created!, hasHistory: false));
    }
}
