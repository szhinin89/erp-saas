using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

public sealed class UpdateSupplierRoleConfigHandler
    : IRequestHandler<UpdateSupplierRoleConfigCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext            _ctx;

    public UpdateSupplierRoleConfigHandler(IBusinessPartnerRoleRepository roleRepo, IOperationalContext ctx)
        => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        UpdateSupplierRoleConfigCommand cmd, CancellationToken ct)
    {
        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, ct);
        if (role is null) return Result<BusinessPartnerRoleDto>.NotFound("Rol no encontrado.");

        try { role.UpdateSupplierConfig(cmd.Config, _ctx.UserId); }
        catch (ArgumentException ex)        { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }

        await _roleRepo.SaveChangesAsync(ct);
        return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
    }
}

public sealed class UpdateSupplierClassificationConfigHandler
    : IRequestHandler<UpdateSupplierClassificationConfigCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext            _ctx;

    public UpdateSupplierClassificationConfigHandler(IBusinessPartnerRoleRepository roleRepo, IOperationalContext ctx)
        => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        UpdateSupplierClassificationConfigCommand cmd, CancellationToken ct)
    {
        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, ct);
        if (role is null) return Result<BusinessPartnerRoleDto>.NotFound("Rol no encontrado.");

        try { role.UpdateClassificationConfig(cmd.Config, _ctx.UserId); }
        catch (ArgumentException ex)        { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }

        await _roleRepo.SaveChangesAsync(ct);
        return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
    }
}

public sealed class UpdateCarrierRoleConfigHandler
    : IRequestHandler<UpdateCarrierRoleConfigCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext            _ctx;

    public UpdateCarrierRoleConfigHandler(IBusinessPartnerRoleRepository roleRepo, IOperationalContext ctx)
        => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        UpdateCarrierRoleConfigCommand cmd, CancellationToken ct)
    {
        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, ct);
        if (role is null) return Result<BusinessPartnerRoleDto>.NotFound("Rol no encontrado.");

        try { role.UpdateCarrierConfig(cmd.Config, _ctx.UserId); }
        catch (ArgumentException ex)        { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }

        await _roleRepo.SaveChangesAsync(ct);
        return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
    }
}

public sealed class UpdateCustomerRoleConfigHandler
    : IRequestHandler<UpdateCustomerRoleConfigCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext            _ctx;

    public UpdateCustomerRoleConfigHandler(IBusinessPartnerRoleRepository roleRepo, IOperationalContext ctx)
        => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        UpdateCustomerRoleConfigCommand cmd, CancellationToken ct)
    {
        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, ct);
        if (role is null) return Result<BusinessPartnerRoleDto>.NotFound("Rol no encontrado.");

        try { role.UpdateCustomerConfig(cmd.Config, _ctx.UserId); }
        catch (ArgumentException ex)        { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }

        await _roleRepo.SaveChangesAsync(ct);
        return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
    }
}

public sealed class UpdateRoleNotesHandler
    : IRequestHandler<UpdateRoleNotesCommand, Result<bool>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext            _ctx;

    public UpdateRoleNotesHandler(IBusinessPartnerRoleRepository roleRepo, IOperationalContext ctx)
        => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<bool>> Handle(UpdateRoleNotesCommand cmd, CancellationToken ct)
    {
        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, ct);
        if (role is null) return Result<bool>.NotFound("Rol no encontrado.");

        try { role.UpdateNotes(cmd.Notes, _ctx.UserId); }
        catch (ArgumentException ex)        { return Result<bool>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }

        await _roleRepo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
