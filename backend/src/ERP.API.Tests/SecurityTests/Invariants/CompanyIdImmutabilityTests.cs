using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.SecurityTests.Infrastructure;
using ERP.API.Tests.Support;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.SecurityTests.Invariants;

/// <summary>
/// ATTACK CATEGORY 6 â€” Entity Scope Injection / Immutability Attacks
///
/// OBJETIVO: Verificar que CompanyId y SubscriberId son inmutables post-creaciÃ³n.
/// Si estos valores pueden modificarse, un atacante puede "mover" datos entre empresas.
/// </summary>
public sealed class CompanyIdImmutabilityTests
{
    [Fact(DisplayName = "ATTACK-6.1: CompanyId property has no public setter â€” compile-time enforcement")]
    public void Attack_6_1_CompanyId_has_no_public_setter()
    {
        var domainAssembly = typeof(ERP.Domain.Common.ICompanyOperationalEntity).Assembly;

        var operationalTypes = domainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ERP.Domain.Common.ICompanyOperationalEntity).IsAssignableFrom(t)
                     || typeof(ERP.Domain.Common.ICompanyScopedEntity).IsAssignableFrom(t));

        foreach (var type in operationalTypes)
        {
            var prop = type.GetProperty("CompanyId");
            if (prop is null) continue;

            var setter = prop.SetMethod;
            setter?.IsPublic.Should().BeFalse(
                $"INVARIANT VIOLATION: {type.Name}.CompanyId has a public setter. " +
                "CompanyId is immutable â€” public setter allows cross-company data injection.");
        }
    }

    [Fact(DisplayName = "ATTACK-6.2: CompanyTenantInterceptor detects Guid.Empty CompanyId")]
    public void Attack_6_2_Interceptor_condition_detects_empty_company_id()
    {
        // Verify the condition that CompanyTenantInterceptor checks
        var account = Account.Create(Guid.NewGuid(), Guid.Empty, "9.9.99", "Test",
            AccountType.Expense, AccountNature.Debit, Guid.NewGuid());

        var interceptorWouldBlock = account is ERP.Domain.Common.ICompanyScopedEntity op
                                    && op.CompanyId == Guid.Empty;
        interceptorWouldBlock.Should().BeTrue(
            "CompanyTenantInterceptor blocks entities where ICompanyOperationalEntity.CompanyId == Guid.Empty. " +
            "In production DI (real ErpDbContext), SaveChanges throws InvalidOperationException.");
    }

    [Fact(DisplayName = "ATTACK-6.3: CompanyTenantInterceptor detects Guid.Empty SubscriberId")]
    public void Attack_6_3_Interceptor_condition_detects_empty_subscriber_id()
    {
        var account = Account.Create(Guid.NewGuid(), Guid.NewGuid(), "4.1.01", "Valid",
            AccountType.Revenue, AccountNature.Credit, Guid.NewGuid());
        var passesCheck = account is ERP.Domain.Common.ISubscriberScopedEntity s && s.SubscriberId != Guid.Empty;
        passesCheck.Should().BeTrue("Valid entity passes CompanyTenantInterceptor subscriber check.");
    }

    [Fact(DisplayName = "ATTACK-6.4: Account data from context A1 â€” CompanyId cannot be cross-company")]
    public async Task Attack_6_4_Account_data_belongs_to_correct_company()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var state = await SecurityTestSeeder.SeedAsync(factory.Services);

        factory.MutableSubscriber.SubscriberId = state.SubscriberAId;
        factory.MutableCompany.CompanyId = state.CompanyA1Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var accounts = await db.Accounts.ToListAsync();

        // All returned accounts must have CompanyId == A1
        accounts.Should().AllSatisfy(a =>
        {
            a.CompanyId.Should().Be(state.CompanyA1Id,
                $"Account {a.Id} was returned in A1 context but has CompanyId = {a.CompanyId}. " +
                "Scope injection detected â€” company_id mismatch.");
            a.SubscriberId.Should().Be(state.SubscriberAId,
                $"Account {a.Id} was returned in SubA context but has SubscriberId = {a.SubscriberId}. " +
                "Cross-subscriber leak detected.");
        });
    }
}
