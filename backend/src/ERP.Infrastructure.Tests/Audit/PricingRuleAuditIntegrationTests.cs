using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Audit;

/// <summary>
/// Suite de integración (PostgreSQL real vía Testcontainers) para el piloto de la nueva
/// infraestructura de auditoría por dominio: verifica que UpdateRule/Enable/Disable en
/// PricingRule terminan en una fila de <see cref="PricingRuleAudit"/>, dentro de la misma
/// transacción que el cambio de negocio, vía el pipeline real de domain events
/// (ErpDbContext.SaveChangesAsync → MediatR IPublisher → PricingRuleAuditHandler →
/// IAuditService → EfAuditWriter). No usa mocks para nada de esto — mismo criterio de
/// NewChildEntityTrackingInterceptorTests.
/// </summary>
public sealed class PricingRuleAuditIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_pricing_audit_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private ServiceProvider _serviceProvider = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _priceListId;
    private readonly Guid _itemId = Guid.NewGuid();
    private string _currentUserName = "Test User";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(() => _tenantId, () => _companyId, _userId, () => _currentUserName));
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

    private async Task<Guid> CreateRuleAsync(Guid? itemId = null)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var rule = PricingRule.Create(
            _tenantId, _companyId, _priceListId, itemId ?? _itemId,
            PricingRuleType.FixedPrice, 10m, _userId);
        db.PricingRules.Add(rule);
        await db.SaveChangesAsync();
        return rule.Id;
    }

    [Fact]
    public async Task UpdateRule_persists_typed_old_and_new_values_in_pricing_rule_audit()
    {
        var ruleId = await CreateRuleAsync();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PricingRules.FirstAsync(r => r.Id == ruleId);
            rule.UpdateRule(PricingRuleType.PercentDiscount, 15m, _userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var audit = await verifyDb.PricingRuleAudits.SingleAsync(a => a.EntityId == ruleId);

        audit.Action.Should().Be("Updated");
        audit.OldRuleType.Should().Be(PricingRuleType.FixedPrice);
        audit.OldRuleValue.Should().Be(10m);
        audit.NewRuleType.Should().Be(PricingRuleType.PercentDiscount);
        audit.NewRuleValue.Should().Be(15m);
        audit.TenantId.Should().Be(_tenantId);
        audit.CompanyId.Should().Be(_companyId);
        audit.UserId.Should().Be(_userId);
        audit.UserName.Should().Be("Test User");
    }

    [Fact]
    public async Task Every_audit_row_persists_both_UserId_and_a_non_empty_UserName_snapshot()
    {
        var ruleId = await CreateRuleAsync();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PricingRules.FirstAsync(r => r.Id == ruleId);
            rule.UpdateRule(PricingRuleType.PercentDiscount, 15m, _userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var audit = await verifyDb.PricingRuleAudits.SingleAsync(a => a.EntityId == ruleId);

        // UserId mantiene la identidad; UserName mantiene el snapshot histórico del nombre
        // visible al momento del evento — ambos son obligatorios en toda auditoría.
        audit.UserId.Should().NotBeEmpty();
        audit.UserName.Should().NotBeNullOrWhiteSpace();
        audit.UserName.Should().Be("Test User");
    }

    [Fact]
    public async Task Snapshot_keeps_the_name_at_event_time_even_if_the_user_later_changes_it()
    {
        var ruleId = await CreateRuleAsync();

        _currentUserName = "Nombre Original";
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PricingRules.FirstAsync(r => r.Id == ruleId);
            rule.UpdateRule(PricingRuleType.PercentDiscount, 15m, _userId);
            await db.SaveChangesAsync();
        }

        // El usuario "cambia su nombre" después del primer evento — la fila ya persistida
        // no debe resincronizarse ni recalcularse retroactivamente.
        _currentUserName = "Nombre Cambiado Después";
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PricingRules.FirstAsync(r => r.Id == ruleId);
            rule.UpdateRule(PricingRuleType.PercentMarkup, 20m, _userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var rows = await verifyDb.PricingRuleAudits
            .Where(a => a.EntityId == ruleId)
            .OrderBy(a => a.OccurredAtUtc)
            .ToListAsync();

        rows.Should().HaveCount(2);
        rows[0].UserName.Should().Be("Nombre Original");
        rows[1].UserName.Should().Be("Nombre Cambiado Después");
        // La primera fila conserva su snapshot exacto — nunca se actualiza retroactivamente.
        rows[0].UserName.Should().NotBe(rows[1].UserName);
    }

    [Fact]
    public async Task Disable_then_enable_each_persist_their_own_audit_row()
    {
        var ruleId = await CreateRuleAsync();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PricingRules.FirstAsync(r => r.Id == ruleId);
            rule.Disable(_userId);
            await db.SaveChangesAsync();
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule = await db.PricingRules.FirstAsync(r => r.Id == ruleId);
            rule.Enable(_userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var rows = await verifyDb.PricingRuleAudits
            .Where(a => a.EntityId == ruleId)
            .OrderBy(a => a.OccurredAtUtc)
            .ToListAsync();

        rows.Should().HaveCount(2);
        rows[0].Action.Should().Be("Disabled");
        rows[1].Action.Should().Be("Enabled");
    }

    [Fact]
    public async Task GetLastByEntityIdsAsync_resolves_last_audit_for_multiple_rules_in_one_batch_query()
    {
        var ruleId1 = await CreateRuleAsync(Guid.NewGuid());
        var ruleId2 = await CreateRuleAsync(Guid.NewGuid());

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var rule1 = await db.PricingRules.FirstAsync(r => r.Id == ruleId1);
            var rule2 = await db.PricingRules.FirstAsync(r => r.Id == ruleId2);
            rule1.UpdateRule(PricingRuleType.PercentDiscount, 5m, _userId);
            rule2.UpdateRule(PricingRuleType.PercentMarkup, 8m, _userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var reader = verify.ServiceProvider.GetRequiredService<IAuditReader<PricingRuleAudit>>();
        var last = await reader.GetLastByEntityIdsAsync(_tenantId, new[] { ruleId1, ruleId2 });

        last.Should().HaveCount(2);
        last[ruleId1].NewRuleValue.Should().Be(5m);
        last[ruleId2].NewRuleValue.Should().Be(8m);
    }
}
