using System.Data.Common;
using ERP.Application;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Exceptions;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// H-16 / H-25 — <c>CompanyProvisioningService.CreateManagedCompanyAsync</c> con DI/EF/bootstrap
/// reales sobre un PostgreSQL aislado (Testcontainer + migraciones + bootstrap global). Empresa,
/// membresía, los 13 steps y el <see cref="CompanyUserBranch"/> del creador se confirman juntos o
/// se revierten juntos; el reintento no duplica y el acceso queda aislado por empresa.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CompanyProvisioningAtomicityPostgreSqlTests
    : IClassFixture<CompanyProvisioningAtomicityPostgreSqlTests.Fixture>
{
    private const string RucA = "1790016919001";
    private const string RucB = "1791352688001";
    private readonly Fixture _fx;

    public CompanyProvisioningAtomicityPostgreSqlTests(Fixture fx) => _fx = fx;

    [Fact]
    public async Task Failure_mid_bootstrap_rolls_back_everything_and_retry_creates_once()
    {
        var (tenantId, userId) = await _fx.SeedTenantAndUserAsync();
        var before = await _fx.CountsAsync();

        // 1) Fallo intermedio: step simulado entre Sales (40) y Caja (45), después de que
        //    Organization/Inventory/Sales ya hicieron sus SaveChanges intermedios.
        Guid failedCompanyId = Guid.Empty;
        var failing = new Mock<ICompanyBootstrapStep>();
        failing.SetupGet(s => s.Order).Returns(41);
        failing
            .Setup(s => s.ExecuteAsync(It.IsAny<CompanyBootstrapContext>(), It.IsAny<CancellationToken>()))
            .Callback<CompanyBootstrapContext, CancellationToken>((c, _) => failedCompanyId = c.CompanyId)
            .ThrowsAsync(new InvalidOperationException("fallo simulado del bootstrap"));

        await using (var failingProvider = _fx.Provider(failing.Object))
        await using (var scope = failingProvider.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Create(provisioning, tenantId, userId, RucA));
            Assert.False(scope.ServiceProvider.GetRequiredService<IUnitOfWork>().HasActiveTransaction);
        }

        Assert.NotEqual(Guid.Empty, failedCompanyId);
        Assert.Equal(before, await _fx.CountsAsync());
        Assert.Equal(0, await _fx.RowsForCompanyAsync(failedCompanyId));

        // 2) Reintento con el mismo RUC: el rollback liberó el RUC y se crea exactamente una vez.
        Guid companyId;
        await using (var scope = _fx.Root.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            companyId = (await Create(provisioning, tenantId, userId, RucA)).Id;
        }
        await AssertFullyProvisionedAsync(tenantId, companyId, userId);

        // 3) Un segundo intento con el mismo RUC se rechaza sin dejar rastros.
        var afterSuccess = await _fx.CountsAsync();
        await using (var scope = _fx.Root.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            await Assert.ThrowsAsync<CompanyRucAlreadyExistsException>(() => Create(provisioning, tenantId, userId, RucA));
        }
        Assert.Equal(afterSuccess, await _fx.CountsAsync());

        // 4) Aislamiento: segunda empresa del mismo tenant y creador.
        Guid companyBId;
        await using (var scope = _fx.Root.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            companyBId = (await Create(provisioning, tenantId, userId, RucB)).Id;
        }
        await AssertFullyProvisionedAsync(tenantId, companyBId, userId);

        await using var readScope = _fx.Root.CreateAsyncScope();
        var db = readScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var grants =await db.CompanyUserBranches.IgnoreQueryFilters()
            .Join(db.CompanyUserMemberships.IgnoreQueryFilters(), b => b.CompanyUserMembershipId, m => m.Id, (b, m) => new { b, m })
            .Join(db.Branches.IgnoreQueryFilters(), x => x.b.BranchId, br => br.Id, (x, br) => new
            {
                GrantCompany = x.b.CompanyId,
                MembershipCompany = x.m.CompanyId,
                BranchCompany = br.CompanyId,
                x.m.IdentityUserId,
            })
            .Where(x => x.IdentityUserId == userId)
            .ToListAsync();
        Assert.Equal(2, grants.Count);
        Assert.All(grants, g =>
        {
            Assert.Equal(g.MembershipCompany, g.GrantCompany);
            Assert.Equal(g.MembershipCompany, g.BranchCompany);
        });
        Assert.Equal(new[] { companyId, companyBId }.OrderBy(x => x), grants.Select(g => g.GrantCompany).OrderBy(x => x));
    }

    // ── COMPANY-DEFAULT-PROVISIONING-ATOMICITY-01: EnsureDefaultCompanyAsync ──────────────────

    [Fact]
    public async Task EnsureDefault_failure_mid_bootstrap_rolls_back_and_retry_in_same_scope_creates_once()
    {
        var (tenantId, _) = await _fx.SeedTenantAndUserAsync();
        var before = await _fx.CountsAsync();
        var step = new ScriptedStep(failFirstCall: true);

        Guid companyId;
        await using (var provider = _fx.Provider(step))
        await using (var scope = provider.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tenant = await Fixture.LoadTenantAsync(db, tenantId);

            await Assert.ThrowsAsync<SimulatedBootstrapFailure>(() => provisioning.EnsureDefaultCompanyAsync(tenant));
            Assert.False(scope.ServiceProvider.GetRequiredService<IUnitOfWork>().HasActiveTransaction);
            Assert.Empty(db.ChangeTracker.Entries());
            Assert.Equal(before, await _fx.CountsAsync());
            Assert.Equal(0, await _fx.RowsForCompanyAsync(step.CompanyIds[0]));

            // Reintento en el mismo scope: el tracker limpio no reinserta huérfanos.
            companyId = (await provisioning.EnsureDefaultCompanyAsync(tenant)).Id;
        }

        Assert.Equal(2, step.Calls);
        Assert.NotEqual(step.CompanyIds[0], companyId);
        await AssertDefaultBootstrappedAsync(tenantId, companyId);
        Assert.Equal(1, await _fx.CompaniesForTenantAsync(tenantId));

        // Idempotencia: una segunda llamada devuelve la misma empresa sin escribir nada.
        var afterSuccess = await _fx.CountsAsync();
        await using (var scope = _fx.Root.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            var tenant = await Fixture.LoadTenantAsync(scope.ServiceProvider.GetRequiredService<ErpDbContext>(), tenantId);
            Assert.Equal(companyId, (await provisioning.EnsureDefaultCompanyAsync(tenant)).Id);
        }
        Assert.Equal(afterSuccess, await _fx.CountsAsync());
    }

    [Fact]
    public async Task EnsureDefault_concurrent_scopes_serialize_on_tenant_lock_and_create_a_single_company()
    {
        var (tenantId, _) = await _fx.SeedTenantAndUserAsync();
        var step = new ScriptedStep(failFirstCall: false, pauseFirstCall: true);

        await using var provider = _fx.Provider(step);
        await using var scope1 = provider.CreateAsyncScope();
        await using var scope2 = provider.CreateAsyncScope();
        var tenant1 = await Fixture.LoadTenantAsync(scope1.ServiceProvider.GetRequiredService<ErpDbContext>(), tenantId);
        var tenant2 = await Fixture.LoadTenantAsync(scope2.ServiceProvider.GetRequiredService<ErpDbContext>(), tenantId);

        // 1) El primero toma el lock, crea la empresa y queda detenido a mitad del bootstrap.
        var first = scope1.ServiceProvider.GetRequiredService<ICompanyProvisioningService>()
            .EnsureDefaultCompanyAsync(tenant1);
        await step.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // 2) El segundo no ve empresa activa (no confirmada) y queda esperando el mismo lock.
        var second = scope2.ServiceProvider.GetRequiredService<ICompanyProvisioningService>()
            .EnsureDefaultCompanyAsync(tenant2);
        await _fx.WaitForBlockedAdvisoryLockAsync(TimeSpan.FromSeconds(30));
        Assert.False(second.IsCompleted);

        // 3) Al confirmar el primero, el segundo vuelve a consultar bajo el lock y reutiliza la empresa.
        step.Release.SetResult();
        var created = await first;
        var reused = await second;

        Assert.Equal(created.Id, reused.Id);
        Assert.Equal(1, step.Calls);
        Assert.Equal(1, await _fx.CompaniesForTenantAsync(tenantId));
        await AssertDefaultBootstrappedAsync(tenantId, created.Id);
    }

    [Fact]
    public async Task EnsureDefault_returns_existing_managed_company_without_creating_another()
    {
        var (tenantId, userId) = await _fx.SeedTenantAndUserAsync();
        Guid managedId;
        await using (var scope = _fx.Root.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            managedId = (await Create(provisioning, tenantId, userId, ProvisionalTaxIdGenerator.Create())).Id;
        }
        var before = await _fx.CountsAsync();

        await using (var scope = _fx.Root.CreateAsyncScope())
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            var tenant = await Fixture.LoadTenantAsync(scope.ServiceProvider.GetRequiredService<ErpDbContext>(), tenantId);
            Assert.Equal(managedId, (await provisioning.EnsureDefaultCompanyAsync(tenant)).Id);
        }

        Assert.Equal(before, await _fx.CountsAsync());
        Assert.Equal(1, await _fx.CompaniesForTenantAsync(tenantId));
    }

    [Fact]
    public async Task EnsureDefault_participates_in_ambient_transaction_without_nesting_or_clearing_tracker()
    {
        var (tenantId, _) = await _fx.SeedTenantAndUserAsync();
        var before = await _fx.CountsAsync();
        var step = new ScriptedStep(failFirstCall: true);

        await using var provider = _fx.Provider(step);
        await using (var scope = provider.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            var tenant = await Fixture.LoadTenantAsync(db, tenantId);

            // Fallo: el rollback lo hace el caller (como First Run); el servicio no limpia su tracker.
            await Assert.ThrowsAsync<SimulatedBootstrapFailure>(() => uow.ExecuteInTransactionAsync(async ct =>
            {
                var ambient = db.Database.CurrentTransaction;
                Assert.NotNull(ambient);
                try { await provisioning.EnsureDefaultCompanyAsync(tenant, ct); }
                finally { Assert.Same(ambient, db.Database.CurrentTransaction); }
            }));
            Assert.NotEmpty(db.ChangeTracker.Entries());
        }
        Assert.Equal(before, await _fx.CountsAsync());
        Assert.Equal(0, await _fx.RowsForCompanyAsync(step.CompanyIds[0]));

        // Éxito: la empresa se confirma con el commit del caller.
        Guid companyId = Guid.Empty;
        await using (var scope = provider.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var provisioning = scope.ServiceProvider.GetRequiredService<ICompanyProvisioningService>();
            var tenant = await Fixture.LoadTenantAsync(scope.ServiceProvider.GetRequiredService<ErpDbContext>(), tenantId);
            await uow.ExecuteInTransactionAsync(async ct =>
                companyId = (await provisioning.EnsureDefaultCompanyAsync(tenant, ct)).Id);
        }
        await AssertDefaultBootstrappedAsync(tenantId, companyId);
        Assert.Equal(1, await _fx.CompaniesForTenantAsync(tenantId));
    }

    private async Task AssertDefaultBootstrappedAsync(Guid tenantId, Guid companyId)
    {
        await using var readScope = _fx.Root.CreateAsyncScope();
        var db = readScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var company = await db.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == companyId);
        Assert.Equal(tenantId, company.TenantId);
        Assert.True(company.IsActive);
        Assert.True(await db.Branches.IgnoreQueryFilters()
            .AnyAsync(b => b.CompanyId == companyId && b.IsMainBranch && b.IsActive));
        Assert.True(await db.Warehouses.IgnoreQueryFilters().AnyAsync(w => w.CompanyId == companyId));
        Assert.True(await db.Accounts.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == companyId));
    }

    private sealed class SimulatedBootstrapFailure() : Exception("fallo simulado del bootstrap");

    /// <summary>
    /// Step sintético entre Sales (40) y Caja (45): registra cada empresa, puede fallar en la primera
    /// llamada o detenerse en ella hasta que la prueba la libere.
    /// </summary>
    private sealed class ScriptedStep(bool failFirstCall, bool pauseFirstCall = false) : ICompanyBootstrapStep
    {
        private int _calls;
        public List<Guid> CompanyIds { get; } = [];
        public int Calls => _calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Order => 41;

        public async Task ExecuteAsync(CompanyBootstrapContext context, CancellationToken cancellationToken = default)
        {
            lock (CompanyIds) CompanyIds.Add(context.CompanyId);
            var call = Interlocked.Increment(ref _calls);
            if (call == 1 && pauseFirstCall)
            {
                Entered.SetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            }
            if (call == 1 && failFirstCall)
                throw new SimulatedBootstrapFailure();
        }
    }

    private async Task AssertFullyProvisionedAsync(Guid tenantId, Guid companyId, Guid userId)
    {
        await using var readScope = _fx.Root.CreateAsyncScope();
        var db = readScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var company =await db.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == companyId);
        Assert.Equal(tenantId, company.TenantId);

        var membership = await db.CompanyUserMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.CompanyId == companyId && m.IdentityUserId == userId);
        Assert.Equal(SecurityRoles.Admin, membership.Role);
        Assert.True(membership.IsActive);

        var mainBranch = await db.Branches.IgnoreQueryFilters()
            .SingleAsync(b => b.CompanyId == companyId && b.IsMainBranch && b.IsActive);
        var grant = await db.CompanyUserBranches.IgnoreQueryFilters()
            .SingleAsync(x => x.CompanyUserMembershipId == membership.Id);
        Assert.Equal(mainBranch.Id, grant.BranchId);
        Assert.Equal(tenantId, grant.TenantId);
        Assert.Equal(companyId, grant.CompanyId);
        Assert.True(grant.IsActive);

        // Evidencia del bootstrap completo dentro de la misma confirmación.
        Assert.True(await db.Warehouses.IgnoreQueryFilters().AnyAsync(w => w.CompanyId == companyId));
        Assert.True(await db.Accounts.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == companyId));
    }

    private static Task<ERP.Domain.Modules.Company.Entities.Company> Create(
        ICompanyProvisioningService provisioning, Guid tenantId, Guid userId, string ruc) =>
        provisioning.CreateManagedCompanyAsync(
            tenantId, ruc, "Synthetic Provisioning " + ruc, mainAddress: "Quito", userId,
            SecurityRoles.Admin, tradeName: "Synthetic " + ruc);

    public sealed class Fixture : IAsyncLifetime
    {
        private const string Database = "company_provisioning_isolated";
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine").WithDatabase(Database)
            .WithUsername("synthetic_test_owner").WithPassword("SyntheticContainer1!").Build();

        public ServiceProvider Root { get; private set; } = null!;

        private string ConnectionString => new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
            { Pooling = false }.ConnectionString;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
            Root = Provider();
            await using var scope = Root.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ErpDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<IGlobalBootstrapOrchestrator>().RunAsync();
        }

        public async Task DisposeAsync()
        {
            await Root.DisposeAsync();
            await _postgres.DisposeAsync();
        }

        public ServiceProvider Provider(ICompanyBootstrapStep? extraStep = null)
        {
            // Sin appsettings, variables de entorno, user-secrets ni endpoints del host.
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["InstallData:Enabled"] = "false",
                ["Snowflake:WorkerId"] = "18",
            }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging(logging => logging.ClearProviders());
            services.AddDistributedMemoryCache();
            services.AddSingleton<IHostApplicationLifetime, TestLifetime>();
            services.AddInfrastructure(config);
            services.AddApplication();
            if (extraStep is not null)
                services.AddScoped(_ => extraStep);
            services.ConfigureDbContext<ErpDbContext>((_, options) =>
                options.AddInterceptors(new ConnectionTargetGuard(ConnectionString)));
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public async Task<(Guid TenantId, Guid UserId)> SeedTenantAndUserAsync()
        {
            await using var scope = Root.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var user = IdentityUser.Create("synthetic.provisioner." + suffix, "Synthetic", "Provisioner",
                null, "not-a-real-hash", Guid.Empty);
            var tenant = Tenant.Create("Synthetic " + suffix, "synthetic-" + suffix, user.Id);
            db.IdentityUsers.Add(user);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            return (tenant.Id, user.Id);
        }

        /// <summary>Conteo global de las tablas que toca el alta de empresa.</summary>
        public async Task<long[]> CountsAsync()
        {
            await using var scope = Root.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            return
            [
                await db.Companies.IgnoreQueryFilters().LongCountAsync(),
                await db.CompanyUserMemberships.IgnoreQueryFilters().LongCountAsync(),
                await db.CompanyUserBranches.IgnoreQueryFilters().LongCountAsync(),
                await db.Branches.IgnoreQueryFilters().LongCountAsync(),
                await db.Warehouses.IgnoreQueryFilters().LongCountAsync(),
                await db.Accounts.IgnoreQueryFilters().LongCountAsync(),
            ];
        }

        public static Task<Tenant> LoadTenantAsync(ErpDbContext db, Guid tenantId) =>
            db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.Id == tenantId);

        public async Task<long> CompaniesForTenantAsync(Guid tenantId)
        {
            await using var scope = Root.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            return await db.Companies.IgnoreQueryFilters().LongCountAsync(c => c.TenantId == tenantId);
        }

        /// <summary>Espera a que una sesión quede bloqueada en un advisory lock (no concedido).</summary>
        public async Task WaitForBlockedAdvisoryLockAsync(TimeSpan timeout)
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                await using var cmd = new NpgsqlCommand(
                    "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted", conn);
                if ((long)(await cmd.ExecuteScalarAsync())! > 0)
                    return;
                await Task.Delay(50);
            }
            Assert.Fail("Ninguna sesión quedó esperando el lock de aprovisionamiento por tenant.");
        }

        public async Task<long> RowsForCompanyAsync(Guid companyId)
        {
            await using var scope = Root.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            return await db.Companies.IgnoreQueryFilters().LongCountAsync(c => c.Id == companyId)
                + await db.CompanyUserMemberships.IgnoreQueryFilters().LongCountAsync(m => m.CompanyId == companyId)
                + await db.CompanyUserBranches.IgnoreQueryFilters().LongCountAsync(b => b.CompanyId == companyId)
                + await db.Branches.IgnoreQueryFilters().LongCountAsync(b => b.CompanyId == companyId)
                + await db.Warehouses.IgnoreQueryFilters().LongCountAsync(w => w.CompanyId == companyId)
                + await db.Accounts.IgnoreQueryFilters().LongCountAsync(a => a.CompanyId == companyId);
        }

        private sealed class ConnectionTargetGuard(string expected) : DbConnectionInterceptor
        {
            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
                ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            {
                var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
                var isolated = new NpgsqlConnectionStringBuilder(expected);
                Assert.True(actual.Host == isolated.Host && actual.Port == isolated.Port && actual.Database == Database);
                return ValueTask.FromResult(result);
            }
        }

        private sealed class TestLifetime : IHostApplicationLifetime
        {
            private readonly CancellationTokenSource _stopping = new();
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => _stopping.Token;
            public CancellationToken ApplicationStopped => _stopping.Token;
            public void StopApplication() => _stopping.Cancel();
        }
    }
}
