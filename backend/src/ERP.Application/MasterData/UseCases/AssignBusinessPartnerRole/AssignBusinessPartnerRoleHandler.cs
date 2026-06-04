using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AssignBusinessPartnerRole;

/// <summary>
/// Flujo UPSERT (ADR-BP-12):
///   1. Verifica que el BP exista y esté activo (regla cross-aggregate — Application layer)
///   2. Busca rol existente (activo o revocado) por tipo
///   3. Si activo → error "ya asignado"
///   4. Si revocado → Reactivate()
///   5. Si null → Create() + AddAsync
///   6. SaveChanges → unique violation capturada si race condition
/// </summary>
public sealed class AssignBusinessPartnerRoleHandler
    : IRequestHandler<AssignBusinessPartnerRoleCommand, Result<BusinessPartnerRoleDto>>
{
    private readonly IBusinessPartnerRepository     _bpRepo;
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext            _ctx;
    private readonly IDatabaseExceptionTranslator   _dbEx;

    public AssignBusinessPartnerRoleHandler(
        IBusinessPartnerRepository     bpRepo,
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext            ctx,
        IDatabaseExceptionTranslator   dbEx)
        => (_bpRepo, _roleRepo, _ctx, _dbEx) = (bpRepo, roleRepo, ctx, dbEx);

    public async Task<Result<BusinessPartnerRoleDto>> Handle(
        AssignBusinessPartnerRoleCommand cmd, CancellationToken ct)
    {
        // Verificar que el BP exista y esté activo (invariante cross-AR — Application layer)
        var bp = await _bpRepo.GetByIdAsync(cmd.BusinessPartnerId, ct);
        if (bp is null)
            return Result<BusinessPartnerRoleDto>.NotFound("BusinessPartner no encontrado.");
        if (!bp.IsActive)
            return Result<BusinessPartnerRoleDto>.ValidationFailure(
                "No se puede asignar un rol a un BusinessPartner inactivo.");

        // UPSERT: buscar rol existente (activo o revocado)
        var existing = await _roleRepo.GetByTypeAsync(cmd.BusinessPartnerId, cmd.RoleType, ct);

        BusinessPartnerRole role;
        bool isNew = false;

        if (existing is not null && existing.IsActive)
            return Result<BusinessPartnerRoleDto>.ValidationFailure(
                $"El rol {cmd.RoleType} ya está activo para este BusinessPartner.");

        if (existing is not null)
        {
            // Rol existe pero revocado → reactivar
            try { existing.Reactivate(_ctx.UserId); }
            catch (ArgumentException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
            role = existing;
        }
        else
        {
            // Nuevo rol
            try
            {
                role = BusinessPartnerRole.Create(
                    _ctx.SubscriberId,
                    cmd.BusinessPartnerId,
                    cmd.RoleType,
                    _ctx.UserId,
                    cmd.SupplierConfig,
                    cmd.CarrierConfig,
                    cmd.CustomerConfig);
            }
            catch (ArgumentException ex) { return Result<BusinessPartnerRoleDto>.ValidationFailure(ex.Message); }
            await _roleRepo.AddAsync(role, ct);
            isNew = true;
        }

        _ = isNew; // usado implícitamente para AddAsync arriba

        try
        {
            await _roleRepo.SaveChangesAsync(ct);
            return Result<BusinessPartnerRoleDto>.Success(BusinessPartnerRoleDto.From(role));
        }
        catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
        {
            return Result<BusinessPartnerRoleDto>.Conflict(
                $"El rol {cmd.RoleType} ya existe para este BusinessPartner (race condition).");
        }
    }
}
