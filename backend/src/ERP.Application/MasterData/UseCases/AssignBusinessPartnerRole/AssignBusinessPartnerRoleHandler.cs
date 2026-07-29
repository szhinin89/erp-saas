using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;

/// <summary>
/// Flujo UPSERT (ADR-BP-12):
///   1. Verifica que el BP exista y esté activo (regla cross-aggregate — Application layer)
///   2. Valida tipo de identificación contra matriz de uso SRI
///   3. Busca rol existente (activo o revocado) por tipo
///   4. Si activo → error "ya asignado"
///   5. Si revocado → Reactivate()
///   6. Si null → Create() + AddAsync
///   7. SaveChanges → unique violation capturada si race condition
/// </summary>
public sealed class AssignBusinessPartnerRoleHandler
    : IRequestHandler<AssignBusinessPartnerRoleCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IIdentificationUsageValidator _usageValidator;
    private readonly IOperationalContext _ctx;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public AssignBusinessPartnerRoleHandler(
        IBusinessPartnerRepository bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IIdentificationUsageValidator usageValidator,
        IOperationalContext ctx,
        IDatabaseExceptionTranslator dbEx)
        => (_bpRepo, _roleRepo, _usageValidator, _ctx, _dbEx) = (bpRepo, roleRepo, usageValidator, ctx, dbEx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        AssignBusinessPartnerRoleCommand cmd, CancellationToken cancellationToken)
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.BusinessPartnerId, cancellationToken);
        if (bp is null)
            return Result<BusinessPartnerRoleDto>.NotFound("BusinessPartner no encontrado.");
        if (!bp.IsActive)
            return Result<BusinessPartnerRoleDto>.ValidationFailure(
                "No se puede asignar un rol a un BusinessPartner inactivo.");

        var usageType = MapRoleToUsage(cmd.RoleType);
        if (usageType.HasValue)
        {
            var allowed = await _usageValidator.IsAllowedAsync(bp.Identification.Type, usageType.Value, cancellationToken);
            if (!allowed)
                return Result<BusinessPartnerRoleDto>.ValidationFailure(
                    $"El tipo de identificación '{bp.Identification.Type}' no está permitido para el rol {cmd.RoleType}.");
        }

        var existing = await _roleRepo.GetByTypeAsync(cmd.BusinessPartnerId, cmd.RoleType, cancellationToken);

        BusinessPartnerRole role;
        bool isNew = false;

        if (existing is not null && existing.IsActive)
            return Result<BusinessPartnerRoleDto>.ValidationFailure(
                $"El rol {cmd.RoleType} ya está activo para este BusinessPartner.");

        if (existing is not null)
        {
            try { existing.Reactivate(_ctx.UserId); }
            catch (ArgumentException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
            role = existing;
        }
        else
        {
            try
            {
                role = BusinessPartnerRole.Create(
                    _ctx.TenantId,
                    cmd.BusinessPartnerId,
                    cmd.RoleType,
                    _ctx.UserId,
                    cmd.SupplierConfig,
                    cmd.CarrierConfig,
                    cmd.CustomerConfig,
                    cmd.ClassificationConfig);
            }
            catch (ArgumentException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
            await _roleRepo.AddAsync(role, cancellationToken);
            isNew = true;
        }

        _ = isNew;

        try
        {
            await _roleRepo.SaveChangesAsync(cancellationToken);
            return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<BusinessPartnerRoleDto>.Conflict(
                $"El rol {cmd.RoleType} ya existe para este BusinessPartner (race condition).");
        }
    }

    private static IdentificationUsageType? MapRoleToUsage(RoleType role) => role switch
    {
        RoleType.Customer => IdentificationUsageType.Customer,
        RoleType.Supplier => IdentificationUsageType.Supplier,
        RoleType.Employee => IdentificationUsageType.Employee,
        RoleType.Carrier => IdentificationUsageType.Carrier,
        _ => null,
    };
}
