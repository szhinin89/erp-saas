using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.Services;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

public sealed class UpdateSupplierRoleConfigHandler
    : IRequestHandler<UpdateSupplierRoleConfigCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext _ctx;

    public UpdateSupplierRoleConfigHandler(
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext ctx
    ) => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        UpdateSupplierRoleConfigCommand cmd,
        CancellationToken cancellationToken
    )
    {
        // Primero la config (antes que el rol): un invariante violado responde igual exista o no el rol.
        var config = RoleConfigFactory.Build(cmd.Config);
        if (!config.IsValid)
            return Result<BusinessPartnerRoleDto>.ValidationFailure(config.Error!, RoleConfigFactory.InvalidConfigCode);

        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, cancellationToken);
        if (role is null || role.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<BusinessPartnerRoleDto>.NotFound("Rol no encontrado.");

        try
        {
            role.UpdateSupplierConfig(config.Config!, _ctx.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message);
        }

        await _roleRepo.SaveChangesAsync(cancellationToken);
        return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
    }
}

public sealed class UpdateCarrierRoleConfigHandler
    : IRequestHandler<UpdateCarrierRoleConfigCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext _ctx;

    public UpdateCarrierRoleConfigHandler(
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext ctx
    ) => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        UpdateCarrierRoleConfigCommand cmd,
        CancellationToken cancellationToken
    )
    {
        // Primero la config (antes que el rol): un invariante violado responde igual exista o no el rol.
        var config = RoleConfigFactory.Build(cmd.Config);
        if (!config.IsValid)
            return Result<BusinessPartnerRoleDto>.ValidationFailure(config.Error!, RoleConfigFactory.InvalidConfigCode);

        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, cancellationToken);
        if (role is null || role.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<BusinessPartnerRoleDto>.NotFound("Rol no encontrado.");

        try
        {
            role.UpdateCarrierConfig(config.Config!, _ctx.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message);
        }

        await _roleRepo.SaveChangesAsync(cancellationToken);
        return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
    }
}

public sealed class UpdateCustomerRoleConfigHandler
    : IRequestHandler<UpdateCustomerRoleConfigCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext _ctx;

    public UpdateCustomerRoleConfigHandler(
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext ctx
    ) => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        UpdateCustomerRoleConfigCommand cmd,
        CancellationToken cancellationToken
    )
    {
        // Primero la config (antes que el rol): un invariante violado responde igual exista o no el rol.
        var config = RoleConfigFactory.Build(cmd.Config);
        if (!config.IsValid)
            return Result<BusinessPartnerRoleDto>.ValidationFailure(config.Error!, RoleConfigFactory.InvalidConfigCode);

        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, cancellationToken);
        if (role is null || role.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<BusinessPartnerRoleDto>.NotFound("Rol no encontrado.");

        try
        {
            role.UpdateCustomerConfig(config.Config!, _ctx.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message);
        }

        await _roleRepo.SaveChangesAsync(cancellationToken);
        return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
    }
}

public sealed class UpdateRoleNotesHandler : IRequestHandler<UpdateRoleNotesCommand, Result<bool>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext _ctx;

    public UpdateRoleNotesHandler(
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext ctx
    ) => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<bool>> Handle(
        UpdateRoleNotesCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, cancellationToken);
        if (role is null || role.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<bool>.NotFound("Rol no encontrado.");

        try
        {
            role.UpdateNotes(cmd.Notes, _ctx.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _roleRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
