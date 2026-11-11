using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Perfiles operativos iniciales «Facturador» y «Contador» (IAM-V1-F3, SEC-05: pertenecen a
/// <c>TenantId + CompanyId</c>). Solo actúa en el aprovisionamiento de una empresa NUEVA
/// (<see cref="CompanyBootstrapContext.SeedInitialProfiles"/>); en cualquier otro caso (p. ej.
/// auto-sanación del login) no crea nada. Si ya existe un perfil con el mismo nombre en la
/// empresa se conserva íntegro: nunca se le reponen permisos retirados ni se le quitan los que el
/// administrador agregó. Participa en la misma transacción que la creación de la empresa.
/// </summary>
public sealed partial class AccessBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Access;

    private readonly ErpDbContext _db;
    private readonly ILogger<AccessBootstrapStep> _logger;

    public AccessBootstrapStep(ErpDbContext db, ILogger<AccessBootstrapStep> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (!context.SeedInitialProfiles)
            return;

        foreach (var bundle in InitialProfileBundles.All)
        {
            var exists = await _db
                .AccessProfiles.IgnoreQueryFilters()
                .AnyAsync(
                    p =>
                        p.TenantId == context.TenantId
                        && p.CompanyId == context.CompanyId
                        && p.Name == bundle.Name,
                    cancellationToken
                );
            if (exists)
            {
                LogProfileKept(bundle.Name, context.CompanyId);
                continue;
            }

            var profile = AccessProfile.Create(
                tenantId: context.TenantId,
                companyId: context.CompanyId,
                name: bundle.Name,
                description: bundle.Description,
                createdBy: context.ActorId
            );
            _db.AccessProfiles.Add(profile);
            await _db.SaveChangesAsync(cancellationToken);

            _db.AccessProfilePermissions.AddRange(
                bundle.Permissions.Select(key =>
                    AccessProfilePermission.Create(
                        tenantId: context.TenantId,
                        profileId: profile.Id,
                        permissionKey: key,
                        isAllowed: true,
                        createdBy: context.ActorId
                    )
                )
            );
            await _db.SaveChangesAsync(cancellationToken);

            LogProfileCreated(bundle.Name, context.CompanyId, bundle.Permissions.Count);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Initial profile '{Name}' created for company {CompanyId} ({Count} permissions)."
    )]
    private partial void LogProfileCreated(string name, Guid companyId, int count);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Initial profile '{Name}' already exists in company {CompanyId}; kept untouched."
    )]
    private partial void LogProfileKept(string name, Guid companyId);
}
