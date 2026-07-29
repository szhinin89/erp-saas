using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserBranchesAdmin;

/// <summary>
/// Fase I-B. Reemplaza la autorización de sucursales de una CompanyUserMembership por la lista
/// recibida — todo o nada: si cualquier BranchId es inválido (inexistente, de otra empresa o
/// inactivo), no se persiste ningún cambio y se devuelve ValidationFailure. CompanyUserBranch
/// sigue siendo la única fuente de verdad: este handler nunca escribe BranchIds en Membership,
/// Preferences ni IdentityUser.
///
/// Decisión — lista vacía permitida: revoca todas las sucursales autorizadas sin desactivar la
/// membresía (acción administrativa legítima, p. ej. suspender acceso operativo sin revocar el
/// login). Es seguro porque CompanyUserPreferencesLoginResolver (Fase E) ya revalida en cada
/// login que el DefaultBranchId siga autorizado y falla con un ValidationFailure controlado si no
/// lo está — nunca fue asumido que CompanyUserBranch tuviera siempre al menos una fila activa.
///
/// IBranchRepository.GetByIdAsync solo filtra por TenantId (no por CompanyId, a diferencia de
/// entidades que usan ForOperationalScope) — por eso se compara branch.CompanyId manualmente
/// contra membership.CompanyId. Se usa el mismo mensaje para "no existe" y "pertenece a otra
/// empresa" (igual criterio que GetCompanyUserPreferencesAdminHandler para membresías) para no
/// revelar por diferencia de mensaje que un BranchId de otra empresa existe.
/// </summary>
public sealed class UpdateCompanyUserBranchesAdminHandler
    : IRequestHandler<UpdateCompanyUserBranchesAdminCommand, Result<CompanyUserBranchesAdminDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;

    public UpdateCompanyUserBranchesAdminHandler(
        IAccessRepository accessRepository,
        ICurrentCompany currentCompany,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository
    )
    {
        _accessRepository = accessRepository;
        _currentCompany = currentCompany;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
    }

    public async Task<Result<CompanyUserBranchesAdminDto>> Handle(
        UpdateCompanyUserBranchesAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        var membership = await _accessRepository.GetCompanyUserMembershipByIdAsync(
            command.CompanyUserMembershipId,
            cancellationToken
        );
        if (membership is null || membership.CompanyId != _currentCompany.CompanyId)
            return Result<CompanyUserBranchesAdminDto>.NotFound(
                "Usuario de empresa no encontrado."
            );

        // Fase E: una membership revocada no debe poder recibir nuevas autorizaciones de sucursal
        // por esta vía administrativa — evita dejar CompanyUserBranch "activas" sobre un acceso ya
        // revocado, sin depender de que el login sea el único punto que valide IsActive.
        if (!membership.IsActive)
            return Result<CompanyUserBranchesAdminDto>.Forbidden(
                "La membresía está revocada; no se pueden modificar sus sucursales autorizadas."
            );

        var requestedBranchIds = command.AuthorizedBranchIds.Distinct().ToList();

        // Validación todo-o-nada: ninguna escritura ocurre hasta que todas las sucursales
        // solicitadas pasen las tres reglas (existe, pertenece a la empresa, activa).
        foreach (var branchId in requestedBranchIds)
        {
            var branch = await _branchRepository.GetByIdAsync(
                _currentTenant.TenantId,
                branchId,
                cancellationToken
            );
            if (branch is null || branch.CompanyId != membership.CompanyId)
                return Result<CompanyUserBranchesAdminDto>.ValidationFailure(
                    $"La sucursal {branchId} no existe o no pertenece a la empresa."
                );

            if (!branch.IsActive)
                return Result<CompanyUserBranchesAdminDto>.ValidationFailure(
                    $"La sucursal {branchId} está inactiva y no puede autorizarse."
                );
        }

        var existingAuthorizations = await _companyUserBranchRepository.GetByMembershipAsync(
            membership.Id,
            cancellationToken
        );
        var existingByBranchId = existingAuthorizations.ToDictionary(x => x.BranchId);

        foreach (var branchId in requestedBranchIds)
        {
            if (existingByBranchId.TryGetValue(branchId, out var existing))
                existing.Activate(_currentUser.UserId);
            else
                await _companyUserBranchRepository.AddAsync(
                    CompanyUserBranch.Create(
                        _currentTenant.TenantId,
                        membership.CompanyId,
                        membership.Id,
                        branchId,
                        _currentUser.UserId
                    ),
                    cancellationToken
                );
        }

        foreach (var existing in existingAuthorizations)
        {
            if (!requestedBranchIds.Contains(existing.BranchId))
                existing.Deactivate(_currentUser.UserId);
        }

        await _companyUserBranchRepository.SaveChangesAsync(cancellationToken);

        return Result<CompanyUserBranchesAdminDto>.Success(
            await BuildDtoAsync(
                membership.Id,
                membership.CompanyId,
                requestedBranchIds,
                cancellationToken
            )
        );
    }

    private async Task<CompanyUserBranchesAdminDto> BuildDtoAsync(
        Guid membershipId,
        Guid companyId,
        IReadOnlyCollection<Guid> authorizedBranchIds,
        CancellationToken cancellationToken
    )
    {
        var companyBranches = (
            await _branchRepository.GetAsync(
                _currentTenant.TenantId,
                activeFilter: true,
                cancellationToken: cancellationToken
            )
        )
            .Where(b => b.CompanyId == companyId)
            .ToList();

        var options = companyBranches
            .Select(b => new CompanyUserBranchOptionDto(
                b.Id,
                b.Name,
                authorizedBranchIds.Contains(b.Id)
            ))
            .ToList();

        return new CompanyUserBranchesAdminDto(membershipId, options);
    }
}
