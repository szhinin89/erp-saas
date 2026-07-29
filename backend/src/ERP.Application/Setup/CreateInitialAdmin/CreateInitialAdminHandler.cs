using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
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
    private readonly IPasswordHasher _hasher;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInitialAdminHandler(
        ISystemSetupRepository setupRepo,
        ITenantRepository tenants,
        IAccessRepository access,
        IPasswordHasher hasher,
        ICompanyProvisioningService companyProvisioning,
        IUnitOfWork unitOfWork
    )
    {
        _setupRepo = setupRepo;
        _tenants = tenants;
        _access = access;
        _hasher = hasher;
        _companyProvisioning = companyProvisioning;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<string>> Handle(
        CreateInitialAdminCommand cmd,
        CancellationToken cancellationToken
    )
    {
        // ── Precondiciones — se resuelven todas ANTES de tocar el UnitOfWork; ninguna entidad
        // se crea ni se marca como Added hasta que este bloque completo pase. ──────────────────
        var state = await _setupRepo.GetAsync(cancellationToken);
        if (state is null)
            return Result<string>.Failure(
                "No hay un token de instalación activo. Reinicie el backend para generar uno."
            );

        if (state.IsInitialized)
            return Result<string>.Failure("El sistema ya ha sido inicializado.");

        if (string.IsNullOrWhiteSpace(state.SetupTokenHash) || state.SetupTokenExpiryUtc is null)
            return Result<string>.Failure(
                "No hay un token de instalación activo. Reinicie el backend para generar uno."
            );

        if (state.SetupTokenExpiryUtc.Value <= DateTime.UtcNow)
            return Result<string>.Failure(
                "Token de inicialización expirado. Reinicie el backend para generar uno nuevo."
            );

        if (!SetupTokenCrypto.Matches(cmd.SetupToken.Trim(), state.SetupTokenHash))
            return Result<string>.Failure("Token de inicialización inválido.");

        var username = cmd.Username.Trim().ToLowerInvariant();
        if (await _access.AnyUserWithUsernameAsync(username, cancellationToken))
            return Result<string>.Failure("Ya existe un usuario con ese username.");

        var email = string.IsNullOrWhiteSpace(cmd.Email)
            ? null
            : cmd.Email.Trim().ToLowerInvariant();
        if (email is not null && await _access.AnyUserWithEmailAsync(email, cancellationToken))
            return Result<string>.Failure("Ya existe un usuario con ese email.");

        // ── Única unidad atómica: tenant + empresa (+ bootstrap completo) + usuario + membership +
        // flag de inicialización quedan todos persistidos o ninguno. ExecuteInTransactionAsync abre
        // la transacción (envuelta en el ExecutionStrategy de EF Core), corre este delegate y hace
        // un único SaveChangesAsync al final — commit si todo salió bien, rollback total si algo
        // lanza, sin dejar nunca un tenant/empresa huérfanos. Las entidades se construyen DENTRO del
        // delegate (no antes) para que, si el ExecutionStrategy alguna vez reintenta por una falla
        // transitoria de conexión, cada intento parta de instancias frescas.
        //
        // No se agrega ningún SaveChangesAsync adicional aquí: _companyProvisioning.EnsureDefaultCompanyAsync
        // (y el CompanyBootstrapOrchestrator que dispara) hace sus propios SaveChangesAsync internos
        // por cada paso de bootstrap — eso es una necesidad técnica preexistente de ese subsistema, no
        // algo que este handler deba reimplementar. Como todo corre bajo la MISMA transacción abierta
        // aquí, esos flush intermedios nunca comprometen los datos por sí solos: si cualquier paso
        // posterior falla, el rollback deshace también lo que esos SaveChanges internos ya escribieron.
        await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
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

                await _tenants.AddAsync(tenant, ct);

                var company = await _companyProvisioning.EnsureDefaultCompanyAsync(tenant, ct);
                var membership = CompanyUserMembership.Create(
                    company.Id,
                    user.Id,
                    role: SecurityRoles.Admin,
                    profileId: null,
                    createdBy: bootstrapId
                );

                await _access.AddUserAsync(user, ct);
                await _access.AddCompanyUserMembershipAsync(membership, ct);

                // Single-use: invalidate el setup token permanentemente. `state` ya está trackeado por
                // el DbContext (vino de una query sin AsNoTracking) — este cambio se persiste en el
                // mismo SaveChangesAsync final del wrapper, no requiere su propio SaveChanges.
                state.MarkInitialized(username);
            },
            cancellationToken
        );

        return Result<string>.Success("Admin inicial creado. Inicie sesión con sus credenciales.");
    }
}
