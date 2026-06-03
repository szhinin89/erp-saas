using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.SecurityTests.Infrastructure;

/// <summary>
/// Siembra datos aislados para múltiples tenants en el harness de ataques.
/// Cada TenantProfile es completamente independiente — no comparte datos con otros.
/// </summary>
internal static class SecurityTestSeeder
{
    private static readonly Guid SeedActor = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");

    /// <summary>
    /// Siembra el estado completo para ataque simulation:
    /// - Subscriber A: Company A1 + Company A2 (mismo subscriber, 2 empresas)
    /// - Subscriber B: Company B1 (subscriber separado)
    /// </summary>
    public static async Task<AttackTestState> SeedAsync(
        IServiceProvider sp, CancellationToken ct = default)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        // ── SUBSCRIBER A ─────────────────────────────────────────────────────
        var subA = Subscriber.Create("Subscriber-A", "sub-a", SeedActor);
        db.Subscribers.Add(subA);
        await db.SaveChangesAsync(ct);

        var companyA1 = Company.CreateFromSubscriber(subA.Id, "1790016919001", "Company A1", "Quito");
        var companyA2 = Company.CreateFromSubscriber(subA.Id, "1790016919002", "Company A2", "Guayaquil");
        db.Companies.AddRange(companyA1, companyA2);
        await db.SaveChangesAsync(ct);

        // ── SUBSCRIBER B ─────────────────────────────────────────────────────
        var subB = Subscriber.Create("Subscriber-B", "sub-b", SeedActor);
        db.Subscribers.Add(subB);
        await db.SaveChangesAsync(ct);

        var companyB1 = Company.CreateFromSubscriber(subB.Id, "1790016919003", "Company B1", "Cuenca");
        db.Companies.Add(companyB1);
        await db.SaveChangesAsync(ct);

        // ── DATA A1: BusinessPartner + Account ────────────────────────────────
        var bpA1 = BusinessPartner.Create(subA.Id, "04", "1790000000001", "Cliente A1 Corp", SeedActor);
        db.BusinessPartners.Add(bpA1);

        var accA1 = Account.Create(subA.Id, companyA1.Id, "4.1.01", "Ventas A1",
            AccountType.Revenue, AccountNature.Credit, SeedActor);
        db.Accounts.Add(accA1);
        await db.SaveChangesAsync(ct);

        // ── DATA A2: BusinessPartner + Account ────────────────────────────────
        var bpA2 = BusinessPartner.Create(subA.Id, "04", "1790000000002", "Cliente A2 Corp", SeedActor);
        db.BusinessPartners.Add(bpA2);

        var accA2 = Account.Create(subA.Id, companyA2.Id, "4.1.01", "Ventas A2",
            AccountType.Revenue, AccountNature.Credit, SeedActor);
        db.Accounts.Add(accA2);
        await db.SaveChangesAsync(ct);

        // ── DATA B1: BusinessPartner + Account ────────────────────────────────
        var bpB1 = BusinessPartner.Create(subB.Id, "04", "1790000000003", "Cliente B1 Corp", SeedActor);
        db.BusinessPartners.Add(bpB1);

        var accB1 = Account.Create(subB.Id, companyB1.Id, "4.1.01", "Ventas B1",
            AccountType.Revenue, AccountNature.Credit, SeedActor);
        db.Accounts.Add(accB1);
        await db.SaveChangesAsync(ct);

        return new AttackTestState(
            SubscriberAId: subA.Id,
            SubscriberBId: subB.Id,
            CompanyA1Id: companyA1.Id,
            CompanyA2Id: companyA2.Id,
            CompanyB1Id: companyB1.Id,
            BpA1Id: bpA1.Id,
            BpA2Id: bpA2.Id,
            BpB1Id: bpB1.Id,
            AccA1Id: accA1.Id,
            AccA2Id: accA2.Id,
            AccB1Id: accB1.Id);
    }
}

/// <summary>Estado del ataque simulation — IDs de todos los tenants y sus datos.</summary>
internal sealed record AttackTestState(
    Guid SubscriberAId,
    Guid SubscriberBId,
    Guid CompanyA1Id,
    Guid CompanyA2Id,
    Guid CompanyB1Id,
    Guid BpA1Id,
    Guid BpA2Id,
    Guid BpB1Id,
    Guid AccA1Id,
    Guid AccA2Id,
    Guid AccB1Id);
