using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Audit;

/// <summary>
/// Suite de integración (PostgreSQL real vía Testcontainers) para la Fase 2 del rediseño
/// de auditoría: PriceListItem reutiliza el 100% de la infraestructura común (mismo
/// EfAuditWriter/EfAuditReader genéricos) — solo agrega su propia entidad de auditoría y su
/// propio event handler, sin tocar nada de lo compartido con PricingRule.
/// </summary>
public sealed class PriceListItemAuditIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_price_list_item_audit_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private ServiceProvider _serviceProvider = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _priceListId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(() => _tenantId, () => _companyId, _userId));
        services.AddDbContext<ErpDbContext>((sp, options) => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(() => _tenantId));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(() => _companyId));

        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;

        var priceList = PriceList.Create(_tenantId, _companyId, "GEN", "Lista general", "USD", isDefault: true, createdBy: _userId);
        db.PriceLists.Add(priceList);
        await db.SaveChangesAsync();
        _priceListId = priceList.Id;
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Create_persists_Assigned_audit_row()
    {
        var itemId = Guid.NewGuid();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var assignment = PriceListItem.Create(_tenantId, _companyId, _priceListId, itemId, _userId);
        db.PriceListItems.Add(assignment);
        await db.SaveChangesAsync();

        var audit = await db.PriceListItemAudits.SingleAsync(a => a.EntityId == assignment.Id);
        audit.Action.Should().Be("Assigned");
        audit.UserId.Should().Be(_userId);
        audit.UserName.Should().Be("Test User");
        audit.PriceListId.Should().Be(_priceListId);
        audit.ItemId.Should().Be(itemId);
        audit.TenantId.Should().Be(_tenantId);
        audit.CompanyId.Should().Be(_companyId);
    }

    [Fact]
    public async Task Disable_then_enable_each_persist_their_own_audit_row()
    {
        var itemId = Guid.NewGuid();
        Guid assignmentId;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = PriceListItem.Create(_tenantId, _companyId, _priceListId, itemId, _userId);
            db.PriceListItems.Add(assignment);
            await db.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = await db.PriceListItems.FirstAsync(a => a.Id == assignmentId);
            assignment.Disable(_userId);
            await db.SaveChangesAsync();
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = await db.PriceListItems.FirstAsync(a => a.Id == assignmentId);
            assignment.Enable(_userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var rows = await verifyDb.PriceListItemAudits
            .Where(a => a.EntityId == assignmentId)
            .OrderBy(a => a.OccurredAtUtc)
            .ToListAsync();

        rows.Should().HaveCount(3);
        rows[0].Action.Should().Be("Assigned");
        rows[1].Action.Should().Be("Disabled");
        rows[2].Action.Should().Be("Enabled");
    }
}
