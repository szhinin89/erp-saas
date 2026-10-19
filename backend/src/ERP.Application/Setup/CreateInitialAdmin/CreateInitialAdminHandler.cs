using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using MediatR;

namespace ERP.Application.Setup.CreateInitialAdmin;

public sealed class CreateInitialAdminHandler
    : IRequestHandler<CreateInitialAdminCommand, Result<string>>
{
    private readonly ISystemSetupRepository _setupRepo;
    private readonly ITenantRepository _tenants;
    private readonly IAccessRepository _access;
    private readonly IBranchRepository _branches;
    private readonly ICompanyUserBranchRepository _companyUserBranches;
    private readonly IPasswordHasher _hasher;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFirstRunSession _session;

    public CreateInitialAdminHandler(
        ISystemSetupRepository setupRepo,
        ITenantRepository tenants,
        IAccessRepository access,
        IBranchRepository branches,
        ICompanyUserBranchRepository companyUserBranches,
        IPasswordHasher hasher,
        ICompanyProvisioningService companyProvisioning,
        IUnitOfWork unitOfWork,
        IFirstRunSession session
    )
    {
        _setupRepo = setupRepo;
        _tenants = tenants;
        _access = access;
        _branches = branches;
        _companyUserBranches = companyUserBranches;
        _hasher = hasher;
        _companyProvisioning = companyProvisioning;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    public Task<Result<string>> Handle(
        CreateInitialAdminCommand cmd,
        CancellationToken cancellationToken
    ) => _session.RunInstallationAsync(ct => HandleOwnedAsync(cmd, ct), cancellationToken);

    private async Task<Result<string>> HandleOwnedAsync(CreateInitialAdminCommand cmd, CancellationToken cancellationToken)
    {
        Result<string>? result = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            result = await ValidateAndCreateAsync(cmd, ct);
        }, cancellationToken);
        return result!;
    }

    private async Task<Result<string>> ValidateAndCreateAsync(CreateInitialAdminCommand cmd, CancellationToken cancellationToken)
    {
        // Re-read state and validate inside the existing transaction on the owner session.
        var state = await _setupRepo.GetAsync(cancellationToken);
        if (state is null)
            return Result<string>.Failure(
                "No hay un token de instalación activo. Reinicie el backend para generar uno."
            );

        if (state.IsInitialized)
            return Result<string>.Conflict("El sistema ya ha sido inicializado.");

        if (string.IsNullOrWhiteSpace(state.SetupTokenHash) || state.SetupTokenExpiryUtc is null)
            return Result<string>.Failure(
                "No hay un token de instalación activo. Reinicie el backend para generar uno."
            );

        if (state.SetupTokenExpiryUtc.Value <= DateTime.UtcNow)
            return Result<string>.Failure(
                "Token de inicialización expirado. Reinicie el backend para generar uno nuevo."
            );

        if (!SetupTokenCrypto.Matches(cmd.SetupToken.Trim(), state.SetupTokenHash))
            return Result<string>.Failure(
                "Token de inicialización inválido.",
                ApiResponseCodes.Common.Unauthorized
            );

        var username = cmd.Username.Trim().ToLowerInvariant();
        if (await _access.AnyUserWithUsernameAsync(username, cancellationToken))
            return Result<string>.Failure("Ya existe un usuario con ese username.");

        var email = string.IsNullOrWhiteSpace(cmd.Email)
            ? null
            : cmd.Email.Trim().ToLowerInvariant();
        if (email is not null && await _access.AnyUserWithEmailAsync(email, cancellationToken))
            return Result<string>.Failure("Ya existe un usuario con ese email.");

        // Preserve the existing atomic provisioning flow and its intermediate flushes.
        var bootstrapId = Guid.NewGuid();
        // "Principal"/"principal" es el único tenant que este flujo puede crear — First Run solo
        // corre una vez (bloqueado por state.IsInitialized) y, gracias a esta transacción, un
        // intento fallido nunca deja el slug ocupado para el siguiente — no hace falta generarlo
        // dinámicamente ni reutilizar uno existente (ver Regla 5: no introducir idempotencia aquí,
        // la atomicidad ya impide el estado parcial que la idempotencia intentaría reparar).
        var tenant = Tenant.Create("Principal", "principal", bootstrapId);
        var passwordHash = _hasher.HashPassword(cmd.Password);
        var user = IdentityUser.Create(
            username,
            cmd.FirstName.Trim(),
            cmd.LastName.Trim(),
            email,
            passwordHash,
            bootstrapId
        );

        await _tenants.AddAsync(tenant, cancellationToken);

        var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, cancellationToken);
        var membership = CompanyUserMembership.Create(
            company.Id,
            user.Id,
            role: SecurityRoles.Admin,
            profileId: null,
            createdBy: bootstrapId
        );

        await _access.AddUserAsync(user, cancellationToken);
        await _access.AddCompanyUserMembershipAsync(membership, cancellationToken);
        var globalRole = GlobalUserRole.Create(user.Id, SecurityRoles.Admin, bootstrapId);

        await _access.AddGlobalUserRoleAsync(globalRole, cancellationToken);
        // ERP-CORE-CLOSEOUT-06: sin esto, el admin inicial queda con CompanyUserMembership
        // pero CERO CompanyUserBranch — BranchAccessGuard lo bloquea en toda operación
        // branch-scoped (venta/compra/caja) y el modal de selección de sucursal del
        // frontend no se puede cerrar sin una sucursal autorizada, dejándolo sin forma de
        // recuperarse desde la propia app. El bootstrap (EnsureDefaultCompanyAsync, arriba)
        // ya creó y persistió la sucursal principal — la buscamos y autorizamos aquí, dentro
        // de la misma transacción, igual patrón que E2ESeedService.SeedAdminAccessAsync.
        var mainBranch = (
            await _branches.GetByCompanyAsync(
                tenant.Id,
                company.Id,
                activeFilter: true,
                cancellationToken: cancellationToken
            )
        ).FirstOrDefault(b => b.IsMainBranch);
        if (mainBranch is not null)
        {
            await _companyUserBranches.AddAsync(
                CompanyUserBranch.Create(
                    tenant.Id,
                    company.Id,
                    membership.Id,
                    mainBranch.Id,
                    bootstrapId
                ),
                cancellationToken
            );
        }

        // Single-use: invalidate el setup token permanentemente. `state` ya está trackeado por
        // el DbContext (vino de una query sin AsNoTracking) — este cambio se persiste en el
        // mismo SaveChangesAsync final del wrapper, no requiere su propio SaveChanges.
        state.MarkInitialized(username);
        return Result<string>.Success("Admin inicial creado. Inicie sesión con sus credenciales.");
    }
}
