using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Audit;

/// <summary>
/// Suite de integración (PostgreSQL real vía Testcontainers) para la Remediación técnica limitada
/// 01 de P0-02 Fase 4: verifica que Create/UpdateName/SetActive/ChangeAccountingAccount en
/// <see cref="CompanyFinancialDestination"/> terminan en filas reales de
/// <see cref="CompanyFinancialDestinationAudit"/>, con los 6 valores Old/New persistidos y
/// recuperados correctamente vía el pipeline real de domain events (ErpDbContext.SaveChangesAsync
/// → MediatR IPublisher → CompanyFinancialDestinationAuditHandler → IAuditService →
/// EfAuditWriter). No usa InMemory Database — mismo criterio que
/// <c>PricingRuleAuditIntegrationTests</c>.
/// </summary>
public sealed class CompanyFinancialDestinationAuditIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_company_financial_destination_audit_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private ServiceProvider _serviceProvider = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _accountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => _tenantId,
            () => _companyId,
            _userId
        ));
        services.AddDbContext<ErpDbContext>(
            (sp, options) => options.UseNpgsql(_postgres.GetConnectionString())
        );
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(() => _tenantId));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(() => _companyId));

        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;

        var account = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create("1.1.01.001"),
            "Bancos",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            _userId
        );
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        _accountId = account.Id;
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<Guid> CreateDestinationAsync()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var destination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            _accountId,
            "USD",
            _userId,
            bankInstitutionCode: "PICHINCHA",
            bankAccountIdentifierNormalized: "2200123456"
        );
        db.CompanyFinancialDestinations.Add(destination);
        await db.SaveChangesAsync();
        return destination.Id;
    }

    [Fact]
    public async Task Create_persists_a_CompanyFinancialDestinationAudit_row_with_only_New_values_populated()
    {
        var destinationId = await CreateDestinationAsync();

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var audit = await verifyDb.CompanyFinancialDestinationAudits.SingleAsync(a =>
            a.EntityId == destinationId
        );

        audit.Action.Should().Be("Created");
        audit.TenantId.Should().Be(_tenantId);
        audit.CompanyId.Should().Be(_companyId);
        audit.Code.Should().Be("BANCO-001");
        audit.NewName.Should().Be("Cuenta corriente Pichincha");
        audit.NewIsActive.Should().BeTrue();
        audit.NewAccountingAccountId.Should().Be(_accountId);
        audit.OldName.Should().BeNull();
        audit.OldIsActive.Should().BeNull();
        audit.OldAccountingAccountId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateName_persists_OldName_and_NewName_leaving_the_other_pairs_null()
    {
        var destinationId = await CreateDestinationAsync();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var destination = await db.CompanyFinancialDestinations.FirstAsync(d =>
                d.Id == destinationId
            );
            destination.UpdateName("Nueva razón social visible", _userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var audit = await verifyDb.CompanyFinancialDestinationAudits.SingleAsync(a =>
            a.EntityId == destinationId && a.Action == "Renamed"
        );

        audit.OldName.Should().Be("Cuenta corriente Pichincha");
        audit.NewName.Should().Be("Nueva razón social visible");
        audit.OldIsActive.Should().BeNull();
        audit.NewIsActive.Should().BeNull();
        audit.OldAccountingAccountId.Should().BeNull();
        audit.NewAccountingAccountId.Should().BeNull();
    }

    [Fact]
    public async Task SetActive_persists_OldIsActive_and_NewIsActive_leaving_the_other_pairs_null()
    {
        var destinationId = await CreateDestinationAsync();

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var destination = await db.CompanyFinancialDestinations.FirstAsync(d =>
                d.Id == destinationId
            );
            destination.SetActive(false, _userId);
            await db.SaveChangesAsync();
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var audit = await verifyDb.CompanyFinancialDestinationAudits.SingleAsync(a =>
            a.EntityId == destinationId && a.Action == "Deactivated"
        );

        audit.OldIsActive.Should().BeTrue();
        audit.NewIsActive.Should().BeFalse();
        audit.OldName.Should().BeNull();
        audit.NewName.Should().BeNull();
        audit.OldAccountingAccountId.Should().BeNull();
        audit.NewAccountingAccountId.Should().BeNull();
    }

    [Fact]
    public async Task ChangeAccountingAccount_persists_OldAccountingAccountId_and_NewAccountingAccountId_leaving_the_other_pairs_null()
    {
        var destinationId = await CreateDestinationAsync();
        Guid newAccountId;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var newAccount = Account.Create(
                _tenantId,
                _companyId,
                AccountCode.Create("1.1.01.002"),
                "Bancos — nueva cuenta",
                null,
                AccountType.Asset,
                AccountNature.Debit,
                allowsPosting: true,
                _userId
            );
            db.Accounts.Add(newAccount);
            var destination = await db.CompanyFinancialDestinations.FirstAsync(d =>
                d.Id == destinationId
            );
            destination.ChangeAccountingAccount(newAccount.Id, _userId);
            await db.SaveChangesAsync();
            newAccountId = newAccount.Id;
        }

        await using var verify = _serviceProvider.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var audit = await verifyDb.CompanyFinancialDestinationAudits.SingleAsync(a =>
            a.EntityId == destinationId && a.Action == "AccountChanged"
        );

        audit.OldAccountingAccountId.Should().Be(_accountId);
        audit.NewAccountingAccountId.Should().Be(newAccountId);
        audit.OldName.Should().BeNull();
        audit.NewName.Should().BeNull();
        audit.OldIsActive.Should().BeNull();
        audit.NewIsActive.Should().BeNull();
    }
}
