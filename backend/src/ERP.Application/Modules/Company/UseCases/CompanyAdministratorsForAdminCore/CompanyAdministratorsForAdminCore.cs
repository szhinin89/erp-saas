using ERP.Application.Auth;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.CompanyAdministratorsForAdminCore;

/// <summary>Administrador empresarial (no global) visible desde Admin Core.</summary>
public sealed record CompanyAdministratorDto(
    Guid IdentityUserId,
    string Username,
    string FullName,
    string? Email,
    bool PasswordResetPending
);

/// <summary>
/// Credencial temporal regenerada. La contraseña viaja solo en esta respuesta: no se persiste, no se
/// audita y no puede volver a consultarse.
/// </summary>
public sealed record RegeneratedAdministratorPasswordDto(
    Guid IdentityUserId,
    string Username,
    string TemporaryPassword
);

public sealed record ListCompanyAdministratorsForAdminCoreQuery(Guid CompanyId)
    : IRequest<Result<IReadOnlyList<CompanyAdministratorDto>>>;

public sealed record RegenerateCompanyAdministratorPasswordCommand(
    Guid CompanyId,
    Guid IdentityUserId
) : IRequest<Result<RegeneratedAdministratorPasswordDto>>;

/// <summary>
/// Alcance común (IAM-V1-F3): solo administrador global (token sin tenant, rol Admin) y solo
/// cuentas con membresía <c>Admin</c> activa en la empresa indicada que NO sean globales. Cualquier
/// otro caso — empresa inexistente, usuario de otra empresa/tenant, cuenta global, rol no Admin o
/// membresía inactiva — responde exactamente igual (404 uniforme) para no filtrar existencia.
/// </summary>
internal static class CompanyAdministratorScope
{
    public const string NotFoundMessage = "Administrador no encontrado.";
    public const string CompanyNotFoundMessage = "Empresa no encontrada.";

    public static bool IsGlobalAdmin(ICurrentUser user, ICurrentTenant tenant) =>
        user.IsAuthenticated && tenant.TenantId == Guid.Empty && user.Role == SecurityRoles.Admin;

    public static async Task<IReadOnlyList<(CompanyUserMembership Membership, IdentityUser User)>> GetAsync(
        IAccessRepository access,
        Guid companyId,
        CancellationToken cancellationToken
    )
    {
        var memberships = (
            await access.GetCompanyUserMembershipsByCompanyAsync(
                companyId,
                onlyActive: true,
                cancellationToken
            )
        )
            .Where(m => m.Role == SecurityRoles.Admin)
            .ToList();
        if (memberships.Count == 0)
            return [];

        var ids = memberships.Select(m => m.IdentityUserId).ToList();
        var global = (await access.GetActiveGlobalUserIdsAsync(ids, cancellationToken)).ToHashSet();
        var users = (await access.GetUsersByIdsAsync(ids, cancellationToken)).ToDictionary(u => u.Id);

        return memberships
            .Where(m => !global.Contains(m.IdentityUserId) && users.ContainsKey(m.IdentityUserId))
            .Select(m => (m, users[m.IdentityUserId]))
            .ToList();
    }
}

public sealed class ListCompanyAdministratorsForAdminCoreHandler(
    IAccessRepository access,
    ICompanyRepository companies,
    ICurrentUser user,
    ICurrentTenant tenant
) : IRequestHandler<ListCompanyAdministratorsForAdminCoreQuery, Result<IReadOnlyList<CompanyAdministratorDto>>>
{
    public async Task<Result<IReadOnlyList<CompanyAdministratorDto>>> Handle(
        ListCompanyAdministratorsForAdminCoreQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!CompanyAdministratorScope.IsGlobalAdmin(user, tenant))
            return Result<IReadOnlyList<CompanyAdministratorDto>>.Forbidden(
                "Solo Admin Global puede consultar administradores de empresa."
            );

        if (await companies.GetTrackedByIdForAdminCoreAsync(request.CompanyId, cancellationToken) is null)
            return Result<IReadOnlyList<CompanyAdministratorDto>>.NotFound(
                CompanyAdministratorScope.CompanyNotFoundMessage
            );

        var admins = await CompanyAdministratorScope.GetAsync(access, request.CompanyId, cancellationToken);
        IReadOnlyList<CompanyAdministratorDto> dtos = admins
            .Select(a => new CompanyAdministratorDto(
                a.User.Id,
                a.User.Username,
                $"{a.User.FirstName} {a.User.LastName}".Trim(),
                a.User.Email?.Value,
                a.User.RequirePasswordReset
            ))
            .OrderBy(a => a.Username)
            .ToList();
        return Result<IReadOnlyList<CompanyAdministratorDto>>.Success(dtos);
    }
}

public sealed class RegenerateCompanyAdministratorPasswordHandler(
    IAccessRepository access,
    ICompanyRepository companies,
    IPasswordHasher hasher,
    IUserAccessRevocationService revocation,
    IUserActivityRepository activity,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    ICurrentTenant tenant
) : IRequestHandler<RegenerateCompanyAdministratorPasswordCommand, Result<RegeneratedAdministratorPasswordDto>>
{
    public async Task<Result<RegeneratedAdministratorPasswordDto>> Handle(
        RegenerateCompanyAdministratorPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!CompanyAdministratorScope.IsGlobalAdmin(user, tenant))
            return Result<RegeneratedAdministratorPasswordDto>.Forbidden(
                "Solo Admin Global puede regenerar contraseñas de administradores de empresa."
            );

        var company = await companies.GetTrackedByIdForAdminCoreAsync(
            command.CompanyId,
            cancellationToken
        );
        if (company is null)
            return Result<RegeneratedAdministratorPasswordDto>.NotFound(
                CompanyAdministratorScope.CompanyNotFoundMessage
            );

        var target = (
            await CompanyAdministratorScope.GetAsync(access, company.Id, cancellationToken)
        )
            .Where(a => a.User.Id == command.IdentityUserId)
            .Select(a => a.User)
            .FirstOrDefault();
        if (target is null)
            return Result<RegeneratedAdministratorPasswordDto>.NotFound(
                CompanyAdministratorScope.NotFoundMessage
            );

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var hash = hasher.HashPassword(temporaryPassword);

        // Contraseña nueva, cambio obligatorio, revocación de todo acceso vivo y auditoría: una sola
        // unidad. Si algo falla, la contraseña anterior sigue vigente y no se muestra nada.
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            target.SetPasswordHash(hash, updatedBy: user.UserId);
            target.MarkRequirePasswordReset(updatedBy: user.UserId);
            await access.SaveChangesAsync(cancellationToken);

            await revocation.RevokeAllAccessAsync(
                target.Id,
                company.TenantId,
                user.UserId,
                "Contraseña temporal regenerada (admin global)", // refresh_token.revoked_reason: varchar(50)
                cancellationToken
            );

            // Solo el hecho: nunca la contraseña ni su hash.
            await activity.AddAsync(
                UserActivity.Create(
                    company.TenantId,
                    user.UserId,
                    user.Email,
                    user.FullName,
                    module: "companies",
                    action: "company.admin.password-regenerate",
                    entityType: "IdentityUser",
                    entityId: target.Id,
                    description: $"Contraseña temporal regenerada para {target.Username} (empresa {company.Id})"
                ),
                cancellationToken
            );
            await access.SaveChangesAsync(cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RegeneratedAdministratorPasswordDto>.Success(
            new RegeneratedAdministratorPasswordDto(target.Id, target.Username, temporaryPassword)
        );
    }
}
