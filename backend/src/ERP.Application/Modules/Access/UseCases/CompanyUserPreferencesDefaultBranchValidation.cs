using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;

namespace ERP.Application.Access.UseCases;

/// <summary>
/// Regla compartida por CreateCompanyUserPreferences y UpdateCompanyUserPreferences: valida
/// DefaultBranchId antes de tocar el agregado. Fuente única de autorización de sucursal:
/// CompanyUserBranch — esta clase nunca concede permisos, nunca asume Branch.IsMainBranch y
/// nunca inserta la autorización faltante; solo verifica que ya exista.
/// </summary>
internal static class CompanyUserPreferencesDefaultBranchValidation
{
    public static async Task<Result<CompanyUserPreferencesDto>?> ValidateAsync(
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        Guid tenantId,
        Guid companyUserMembershipId,
        Guid? defaultBranchId,
        CompanyUserLoginMode loginMode,
        CancellationToken cancellationToken
    )
    {
        if (loginMode == CompanyUserLoginMode.DirectToDefault && defaultBranchId is null)
            return Result<CompanyUserPreferencesDto>.ValidationFailure(
                "El modo DirectToDefault requiere una sucursal por defecto."
            );

        if (defaultBranchId is null)
            return null;

        var branch = await branchRepository.GetByIdAsync(
            tenantId,
            defaultBranchId.Value,
            cancellationToken
        );
        if (branch is null)
            return Result<CompanyUserPreferencesDto>.NotFound("La sucursal no existe.");

        // Fase H — hallazgo de auditoría: GetByIdAsync no filtra por IsActive (a diferencia de
        // ICompanyUserBranchRepository.ExistsAsync, que sí exige IsActive=true en la propia
        // autorización). Sin este chequeo, una sucursal desactivada (soft-delete, nunca DELETE
        // físico — regla del proyecto) seguía aceptándose como DefaultBranchId válido. Mismo
        // código de fallo que "no autorizada" — ambos son la misma familia de regla de negocio
        // (sucursal no utilizable), nunca un dato inexistente.
        if (!branch.IsActive)
            return Result<CompanyUserPreferencesDto>.ValidationFailure(
                "La sucursal por defecto está inactiva y no puede usarse como sucursal por defecto."
            );

        var authorized = await companyUserBranchRepository.ExistsAsync(
            companyUserMembershipId,
            defaultBranchId.Value,
            cancellationToken
        );
        if (!authorized)
            return Result<CompanyUserPreferencesDto>.ValidationFailure(
                "La sucursal por defecto debe estar previamente autorizada para este usuario "
                    + "(CompanyUserBranch). CompanyUserPreferences nunca concede permisos de sucursal."
            );

        return null;
    }
}
