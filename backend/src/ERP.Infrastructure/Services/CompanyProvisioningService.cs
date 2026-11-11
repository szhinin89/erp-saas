using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Único punto de creación de <see cref="Company"/> en producción. Ambos métodos públicos dejan
/// la empresa completamente bootstrapeada (sucursal, bodega, establecimiento, punto de emisión,
/// numeraciones, formas de pago, tipos de ítem, consumidor final, lista de precios, condición de
/// pago, perfiles operativos iniciales en altas de empresa) antes de retornar — un único punto de invocación de
/// <see cref="ICompanyBootstrapService"/>, nunca hay que llamarlo por separado desde el caller.
/// <see cref="CreateManagedCompanyAsync"/> es atómico (H-16): empresa, membresía del creador, los
/// 13 steps del bootstrap y su <see cref="CompanyUserBranch"/> en la sucursal principal (H-25) se
/// confirman en una sola transacción PostgreSQL o se revierten completos. <see cref="EnsureDefaultCompanyAsync"/>
/// también es atómico y se serializa por tenant (COMPANY-DEFAULT-PROVISIONING-ATOMICITY-01).
/// </summary>
public sealed class CompanyProvisioningService : ICompanyProvisioningService
{
    private readonly ICompanyRepository _companies;
    private readonly IAccessRepository _access;
    private readonly ICompanyBootstrapService _bootstrap;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBranchRepository _branches;
    private readonly ICompanyUserBranchRepository _companyUserBranches;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentUser _currentUser;

    public CompanyProvisioningService(
        ICompanyRepository companies,
        IAccessRepository access,
        ICompanyBootstrapService bootstrap,
        IUnitOfWork unitOfWork,
        IBranchRepository branches,
        ICompanyUserBranchRepository companyUserBranches,
        IUserActivityRepository activity,
        ICurrentUser currentUser
    )
    {
        _companies = companies;
        _access = access;
        _bootstrap = bootstrap;
        _unitOfWork = unitOfWork;
        _branches = branches;
        _companyUserBranches = companyUserBranches;
        _activity = activity;
        _currentUser = currentUser;
    }

    /// <summary>
    /// COMPANY-DEFAULT-PROVISIONING-ATOMICITY-01: la comprobación de empresa activa, la creación y
    /// los 13 steps del bootstrap son una unidad todo-o-nada serializada por tenant. Con transacción
    /// ambiente (First Run) se participa en ella sin abrir otra ni limpiar el tracker: el rollback lo
    /// decide el caller. Sin ella, se abre una propia; si falla, se revierte completa y se limpia el
    /// tracker para que un reintento en el mismo scope no reinserte entidades huérfanas.
    /// </summary>
    public Task<Company> EnsureDefaultCompanyAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default
    ) => EnsureCompanyAsync(tenant, seedInitialProfiles: false, cancellationToken);

    /// <summary>
    /// First Run (IAM-V1-F3): empresa inicial de una instalación nueva, con los perfiles
    /// operativos iniciales. La auto-sanación del login (<see cref="EnsureDefaultCompanyAsync"/>)
    /// no los crea.
    /// </summary>
    public Task<Company> EnsureInitialCompanyAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default
    ) => EnsureCompanyAsync(tenant, seedInitialProfiles: true, cancellationToken);

    private async Task<Company> EnsureCompanyAsync(
        Tenant tenant,
        bool seedInitialProfiles,
        CancellationToken cancellationToken
    )
    {
        if (_unitOfWork.HasActiveTransaction)
            return await EnsureLockedAsync(cancellationToken);

        // Camino rápido (Login): con empresa activa no se abre transacción ni se toma el lock.
        var active = await _companies.GetActiveByTenantIdAsync(tenant.Id, cancellationToken);
        if (active.Count > 0)
            return active[0];

        Company? ensured = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(
                async ct => ensured = await EnsureLockedAsync(ct),
                cancellationToken
            );
        }
        catch
        {
            _unitOfWork.ClearChangeTracker();
            throw;
        }
        return ensured!;

        async Task<Company> EnsureLockedAsync(CancellationToken ct)
        {
            // Lock antes de volver a consultar: un proceso concurrente que ya creó la empresa
            // por defecto la confirma antes de que esta transacción la busque.
            await _companies.AcquireDefaultProvisioningLockAsync(tenant.Id, ct);
            var current = await _companies.GetActiveByTenantIdAsync(tenant.Id, ct);
            if (current.Count > 0)
                return current[0];

            var company = await CreateDefaultCompanyForTenantAsync(tenant, cancellationToken: ct);
            await _companies.AddAsync(company, ct);

            // Actor de sistema: esta rama solo se activa cuando el tenant se quedó sin ninguna
            // empresa activa (auto-sanación fuera de un flujo de creación explícito con usuario real).
            var systemActorId = Guid.NewGuid();

            // El bootstrap (PriceList, PricingRule, etc.) dispara domain events que los *AuditHandler
            // resuelven vía IAuditContext → ICurrentCompany, la cual lee de HttpContext/header y no
            // tiene ningún valor todavía aquí (la empresa recién se está creando, nunca fue "la
            // empresa actual" de ningún request). JobExecutionContext es el mecanismo ya existente
            // para este caso (mismo usado por ElectronicDocumentRetryJob) — sin él, cualquier
            // *AuditHandler que dependa de CompanyId lanza ArgumentException("companyId requerido").
            using (JobExecutionContext.Begin(tenant.Id, company.Id))
            {
                await _bootstrap.BootstrapCompanyAsync(
                    tenant.Id,
                    company.Id,
                    systemActorId,
                    ct,
                    seedInitialProfiles
                );
            }

            return company;
        }
    }

    public async Task<Company> CreateDefaultCompanyForTenantAsync(
        Tenant tenant,
        string? countryCode = "ECU",
        string? timezone = "America/Guayaquil",
        CancellationToken cancellationToken = default
    )
    {
        var (taxId, isProvisional, status) = await ResolveTaxIdAsync(null, cancellationToken);

        return Company.CreateManaged(
            tenant.Id,
            taxId,
            legalName: tenant.Name,
            tradeName: null,
            corporateEmail: null,
            countryCode: string.IsNullOrWhiteSpace(countryCode) ? "ECU" : countryCode,
            timezone: string.IsNullOrWhiteSpace(timezone) ? "America/Guayaquil" : timezone,
            currencyCode: "USD",
            isTemporaryTaxIdentification: isProvisional,
            taxIdentificationStatus: status
        );
    }

    /// <summary>
    /// <paramref name="mainAddress"/> y <paramref name="phone"/> se aceptan por compatibilidad con
    /// <c>IntegrationCompanyCreateRequest</c>/<c>CompanyProvisionRequest</c> (Platform), pero ya no
    /// se persisten en <see cref="Company"/> (pendiente de moverse a Branch).
    /// </summary>
    public async Task<Company> CreateManagedCompanyAsync(
        Guid tenantId,
        string ruc,
        string legalName,
        string mainAddress,
        Guid createdByUserId,
        string creatorRole,
        string? tradeName = null,
        string? email = null,
        string? phone = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD",
        string? website = null,
        CancellationToken cancellationToken = default,
        InitialCompanyAdministrator? initialAdministrator = null
    )
    {
        var normalized = NormalizeProvidedTaxId(ruc);

        // H-16: empresa, membresía, los 13 pasos del bootstrap y el acceso a la sucursal principal
        // son una única unidad todo-o-nada. Los SaveChanges intermedios (propios y de los steps) se
        // mantienen, pero ninguno se confirma hasta el commit final. Si ya hay una transacción
        // ambiente, se participa en ella (mismo criterio que UpsertCompanyUserMembershipHandler).
        if (_unitOfWork.HasActiveTransaction)
            return await CreateAsync(cancellationToken);

        Company? created = null;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(
                async ct => created = await CreateAsync(ct),
                cancellationToken
            );
        }
        catch
        {
            // El rollback ya revirtió la BD; se descartan las entidades huérfanas del tracker para
            // que un reintento en el mismo scope no vuelva a insertarlas.
            _unitOfWork.ClearChangeTracker();
            throw;
        }
        return created!;

        async Task<Company> CreateAsync(CancellationToken ct)
        {
            // Dos altas simultáneas del MISMO tenant sembraban a la vez los datos de nivel tenant
            // (catálogo de bancos, etc.) y se interbloqueaban (40P01) o chocaban con los índices
            // únicos. Mismo lock por tenant que EnsureDefaultCompanyAsync (pg_advisory_xact_lock,
            // se libera al confirmar o revertir): las altas del tenant se serializan y las
            // siguientes ya encuentran lo sembrado. Los demás tenants no se bloquean entre sí.
            await _companies.AcquireDefaultProvisioningLockAsync(tenantId, ct);

            await EnsureTaxIdAvailableGloballyAsync(normalized, ct);

            var company = Company.CreateManaged(
                tenantId,
                normalized,
                legalName,
                tradeName,
                corporateEmail: email,
                countryCode,
                timezone,
                currencyCode,
                website,
                isTemporaryTaxIdentification: ProvisionalTaxIdGenerator.IsProvisional(normalized),
                taxIdentificationStatus: ProvisionalTaxIdGenerator.IsProvisional(normalized)
                    ? TaxIdentificationStatus.Pending
                    : TaxIdentificationStatus.Verified,
                createdBy: createdByUserId
            );

            await _companies.AddAsync(company, ct);
            await _companies.SaveChangesAsync(ct);

            var membership = CompanyUserMembership.Create(
                company.Id,
                createdByUserId,
                creatorRole,
                profileId: null,
                createdByUserId
            );
            await _access.AddCompanyUserMembershipAsync(membership, ct);
            await _access.SaveChangesAsync(ct);

            // Ver comentario equivalente en EnsureDefaultCompanyAsync: el bootstrap dispara domain
            // events auditados que necesitan CompanyId, y esta empresa nueva nunca fue "la empresa
            // actual" de ningún HttpContext todavía.
            using (JobExecutionContext.Begin(tenantId, company.Id))
            {
                await _bootstrap.BootstrapCompanyAsync(
                    tenantId,
                    company.Id,
                    createdByUserId,
                    ct,
                    seedInitialProfiles: true
                );
            }

            // H-25: sin CompanyUserBranch el creador queda bloqueado por BranchAccessGuard en toda
            // operación branch-scoped. Mismo patrón que CreateInitialAdminHandler (First Run), pero
            // aquí la sucursal principal es obligatoria: sin ella la creación se revierte completa.
            var mainBranch =
                (
                    await _branches.GetByCompanyAsync(
                        tenantId,
                        company.Id,
                        activeFilter: true,
                        cancellationToken: ct
                    )
                ).FirstOrDefault(b => b.IsMainBranch)
                ?? throw new InvalidOperationException(
                    "El bootstrap de la empresa no dejó una sucursal principal activa."
                );
            await _companyUserBranches.AddAsync(
                CompanyUserBranch.Create(
                    tenantId,
                    company.Id,
                    membership.Id,
                    mainBranch.Id,
                    createdByUserId
                ),
                ct
            );
            await _companyUserBranches.SaveChangesAsync(ct);

            if (initialAdministrator is not null)
                await CreateInitialAdministratorAsync(
                    initialAdministrator,
                    tenantId,
                    company.Id,
                    mainBranch.Id,
                    createdByUserId,
                    ct
                );

            return company;
        }
    }

    /// <summary>
    /// IAM-V1-F3: cuenta independiente de Administrador de empresa, en la MISMA transacción que la
    /// empresa. Membresía <c>Admin</c> sin perfil, acceso a la sucursal principal y cambio
    /// obligatorio de contraseña en el primer ingreso (<c>RequirePasswordReset</c>). Usuario y
    /// correo son únicos en todo el sistema: se comprueban de nuevo aquí y la violación del índice
    /// único (concurrencia) se traduce a la misma excepción genérica, lo que revierte todo.
    /// Solo se audita el alta del administrador: nunca la contraseña ni su hash.
    /// </summary>
    private async Task CreateInitialAdministratorAsync(
        InitialCompanyAdministrator admin,
        Guid tenantId,
        Guid companyId,
        Guid mainBranchId,
        Guid createdByUserId,
        CancellationToken ct
    )
    {
        var username = admin.Username.Trim().ToLowerInvariant();
        var email = admin.Email.Trim().ToLowerInvariant();
        if (
            await _access.AnyUserWithUsernameAsync(username, ct)
            || await _access.AnyUserWithEmailAsync(email, ct)
        )
            throw new InitialAdministratorIdentityUnavailableException();

        var user = IdentityUser.Create(
            username,
            admin.FirstName,
            admin.LastName,
            email,
            admin.PasswordHash,
            createdByUserId
        );
        user.MarkRequirePasswordReset(createdByUserId);
        var membership = CompanyUserMembership.Create(
            companyId,
            user.Id,
            SecurityRoles.Admin,
            profileId: null,
            createdByUserId
        );

        await _access.AddUserAsync(user, ct);
        await _access.AddCompanyUserMembershipAsync(membership, ct);
        await _companyUserBranches.AddAsync(
            CompanyUserBranch.Create(tenantId, companyId, membership.Id, mainBranchId, createdByUserId),
            ct
        );
        await _activity.AddAsync(
            UserActivity.Create(
                tenantId,
                createdByUserId,
                _currentUser.Email,
                _currentUser.FullName,
                module: "companies",
                action: "company.initial-admin.create",
                entityType: "Company",
                entityId: companyId,
                description: $"Administrador inicial: {username}"
            ),
            ct
        );

        try
        {
            await _access.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InitialAdministratorIdentityUnavailableException();
        }
    }

    private async Task<(
        string TaxId,
        bool IsProvisional,
        TaxIdentificationStatus Status
    )> ResolveTaxIdAsync(string? providedRuc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providedRuc))
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var provisional = ProvisionalTaxIdGenerator.Create();
                if (
                    await _companies.GetByTaxIdentificationNumberAsync(
                        provisional,
                        cancellationToken
                    )
                    is null
                )
                    return (provisional, true, TaxIdentificationStatus.Pending);
            }
            throw new InvalidOperationException("No se pudo generar un RUC provisional único.");
        }

        var normalized = NormalizeProvidedTaxId(providedRuc);
        await EnsureTaxIdAvailableGloballyAsync(normalized, cancellationToken);
        return (normalized, false, TaxIdentificationStatus.Verified);
    }

    private static string NormalizeProvidedTaxId(string ruc) =>
        ProvisionalTaxIdGenerator.IsProvisional(ruc)
            ? ruc.Trim()
            : ProvisionalTaxIdGenerator.NormalizeOfficial(ruc);

    private async Task EnsureTaxIdAvailableGloballyAsync(
        string taxId,
        CancellationToken cancellationToken
    )
    {
        var existing = await _companies.GetByTaxIdentificationNumberAsync(taxId, cancellationToken);
        if (existing is not null)
            throw new CompanyRucAlreadyExistsException(taxId);
    }
}
