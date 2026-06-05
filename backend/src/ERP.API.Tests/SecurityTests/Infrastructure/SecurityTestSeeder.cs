using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Purchasing.Entities;
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

        // ── DATA A1: BusinessPartner + Account + PurchaseOrder + ExpenseInvoice ─
        var bpA1 = BusinessPartner.Create(subA.Id, "04", "1790012344001", PersonType.Legal, "Cliente A1 Corp", SeedActor);
        db.BusinessPartners.Add(bpA1);

        var accA1 = Account.Create(subA.Id, companyA1.Id, "4.1.01", "Ventas A1",
            AccountType.Revenue, AccountNature.Credit, SeedActor);
        db.Accounts.Add(accA1);

        var poA1 = PurchaseOrder.Create(subA.Id, companyA1.Id, 1, bpA1.Id,
            DateTime.UtcNow.AddDays(30), null, null, null, SeedActor);
        db.PurchaseOrders.Add(poA1);

        var expA1 = ExpenseInvoice.CreateManual(subA.Id, companyA1.Id, bpA1.Id,
            DateTime.UtcNow.Date, "Gasto A1", "Operaciones", 10m, 1.2m, 11.2m, null, SeedActor);
        db.ExpenseInvoices.Add(expA1);

        await db.SaveChangesAsync(ct);

        // ── DATA A2: BusinessPartner + Account + PurchaseOrder + ExpenseInvoice ─
        var bpA2 = BusinessPartner.Create(subA.Id, "04", "1790012352001", PersonType.Legal, "Cliente A2 Corp", SeedActor);
        db.BusinessPartners.Add(bpA2);

        var accA2 = Account.Create(subA.Id, companyA2.Id, "4.1.01", "Ventas A2",
            AccountType.Revenue, AccountNature.Credit, SeedActor);
        db.Accounts.Add(accA2);

        var poA2 = PurchaseOrder.Create(subA.Id, companyA2.Id, 1, bpA2.Id,
            DateTime.UtcNow.AddDays(30), null, null, null, SeedActor);
        db.PurchaseOrders.Add(poA2);

        var expA2 = ExpenseInvoice.CreateManual(subA.Id, companyA2.Id, bpA2.Id,
            DateTime.UtcNow.Date, "Gasto A2", "Operaciones", 10m, 1.2m, 11.2m, null, SeedActor);
        db.ExpenseInvoices.Add(expA2);

        await db.SaveChangesAsync(ct);

        // ── DATA B1: BusinessPartner + Account + PurchaseOrder + ExpenseInvoice ─
        var bpB1 = BusinessPartner.Create(subB.Id, "04", "1790012360001", PersonType.Legal, "Cliente B1 Corp", SeedActor);
        db.BusinessPartners.Add(bpB1);

        var accB1 = Account.Create(subB.Id, companyB1.Id, "4.1.01", "Ventas B1",
            AccountType.Revenue, AccountNature.Credit, SeedActor);
        db.Accounts.Add(accB1);

        var poB1 = PurchaseOrder.Create(subB.Id, companyB1.Id, 1, bpB1.Id,
            DateTime.UtcNow.AddDays(30), null, null, null, SeedActor);
        db.PurchaseOrders.Add(poB1);

        var expB1 = ExpenseInvoice.CreateManual(subB.Id, companyB1.Id, bpB1.Id,
            DateTime.UtcNow.Date, "Gasto B1", "Operaciones", 10m, 1.2m, 11.2m, null, SeedActor);
        db.ExpenseInvoices.Add(expB1);

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
            AccB1Id: accB1.Id,
            PoA1Id: poA1.Id,
            PoA2Id: poA2.Id,
            PoB1Id: poB1.Id,
            ExpA1Id: expA1.Id,
            ExpA2Id: expA2.Id,
            ExpB1Id: expB1.Id);
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
    Guid AccB1Id,
    Guid PoA1Id,
    Guid PoA2Id,
    Guid PoB1Id,
    Guid ExpA1Id,
    Guid ExpA2Id,
    Guid ExpB1Id);

