using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ERP.API.Tests.Integration;

// CASO-20: real HTTP pipeline and disposable PostgreSQL; reuse the existing fixture.
[Trait("Category", "PostgreSql")]
public sealed class CollectionReversalTechnicalTests(FinancialCommandIdempotencyFixture f, ITestOutputHelper output)
    : IClassFixture<FinancialCommandIdempotencyFixture>
{
    private const string Url = "/api/v1/finance/collections";
    private async Task<Guid> Collect(HttpClient client, Guid ar, decimal amount, Guid? cash = null, Guid? bank = null)
    {
        var response = await client.PostAsJsonAsync(Url, new {
            customerId = f.CustomerId, amount, paymentDate = f.Today, paymentMethodId = f.CashMethodId,
            reference = "CASO-20", cashRegisterId = cash, companyBankAccountId = bank, clientRequestId = Guid.NewGuid(),
            lines = new[] { new { documentId = ar, installmentId = (Guid?)null, appliedAmount = amount } }
        });
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"Collect HTTP {(int)response.StatusCode}: {body}");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }
    private async Task<HttpStatusCode> Reverse(HttpClient client, Guid payment)
    {
        var response = await client.PostAsJsonAsync($"{Url}/{payment}/reverse", new { reason = " CASO-20 corrección técnica " });
        output.WriteLine($"Reverse {payment}: HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response.StatusCode;
    }

    [Fact]
    public async Task Permission_denied_and_explicit_permission_granted()
    {
        var op = await f.CreateOperatorAsync();
        var ar = await f.CreateReceivableAsync(100m, installments: 2);
        var id = await Collect(f.CreateClient(op.UserId), ar, 40m);
        var denied = await f.CreateUserWithPermissionsAsync("finance.create");
        (await Reverse(f.CreateClient(denied, "Cajero"), id)).Should().Be(HttpStatusCode.Forbidden);
        var allowed = await f.CreateUserWithPermissionsAsync("finance.collections.reverse");
        (await Reverse(f.CreateClient(allowed, "Cajero"), id)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Concurrent_reversal_audit_installments_and_new_collection()
    {
        var op = await f.CreateOperatorAsync();
        var ar = await f.CreateReceivableAsync(100m, installments: 2);
        var client = f.CreateClient(op.UserId);
        var id = await Collect(client, ar, 75m);
        var start = DateTime.UtcNow;
        var responses = await Task.WhenAll(Reverse(client, id), Reverse(client, id));
        responses.Count(x => x == HttpStatusCode.OK).Should().Be(1);
        responses.Single(x => x != HttpStatusCode.OK).Should().Be(HttpStatusCode.UnprocessableEntity);
        using (var scope = f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            output.WriteLine($"DB={db.Database.GetDbConnection().Database}; Server={db.Database.GetDbConnection().DataSource}");
            var p = await db.Payments.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == id);
            p.Status.Should().Be(PaymentStatus.Reversed);
            p.ReverseReason.Should().Be("CASO-20 corrección técnica");
            p.UpdatedBy.Should().Be(op.UserId);
            p.ReversedAtUtc.Should().NotBeNull();
            p.ReversedAtUtc!.Value.Should().BeOnOrAfter(start);
            p.ReversedAtUtc.Value.Kind.Should().Be(DateTimeKind.Utc);
            p.Lines.Should().ContainSingle();
            var r = await db.SalesReceivables.AsNoTracking().SingleAsync(x => x.Id == ar);
            r.PaidAmount.Should().Be(0m); r.BalanceDue.Should().Be(100m);
            var installments = await db.SalesReceivableInstallments.AsNoTracking().Where(x => x.ReceivableId == ar).ToListAsync();
            installments.Should().HaveCount(2);
            installments.Sum(x => x.PaidAmount).Should().Be(0m);
            installments.Sum(x => x.OutstandingAmount).Should().Be(100m);
            var original = await db.JournalEntries.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.SourceEventId == id && x.SourceEventType == "CollectionApplied");
            var reversal = await db.JournalEntries.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.OriginalJournalEntryId == original.Id);
            reversal.Lines.Select(x => new { x.AccountId, Debit = x.Credit, Credit = x.Debit })
                .Should().BeEquivalentTo(original.Lines.Select(x => new { x.AccountId, x.Debit, x.Credit }));
            output.WriteLine($"Persisted payment={id}, actor={p.UpdatedBy}, UTC={p.ReversedAtUtc:O}, original={original.Id}, reversal={reversal.Id}; paid=0 balance=100");
        }
        var corrected = await Collect(client, ar, 25m);
        corrected.Should().NotBe(id);
        using var verify = f.CreateDbScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await dbVerify.SalesReceivables.AsNoTracking().SingleAsync(x => x.Id == ar)).PaidAmount.Should().Be(25m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Foreign_company_or_tenant_cannot_reverse(bool tenant)
    {
        var op = await f.CreateOperatorAsync();
        var ar = await f.CreateReceivableAsync(100m);
        var client = f.CreateClient(op.UserId);
        var id = await Collect(client, ar, 40m);
        var company = f.Factory.Services.GetRequiredService<MutableCurrentCompany>();
        var currentTenant = f.Factory.Services.GetRequiredService<MutableCurrentTenant>();
        var savedCompany = company.CompanyId; var savedTenant = currentTenant.TenantId;
        try
        {
            if (tenant) currentTenant.TenantId = Guid.NewGuid(); else company.CompanyId = Guid.NewGuid();
            (await Reverse(client, id)).Should().Be(HttpStatusCode.Forbidden);
        }
        finally { company.CompanyId = savedCompany; currentTenant.TenantId = savedTenant; }
        using var scope = f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.Payments.AsNoTracking().SingleAsync(x => x.Id == id)).Status.Should().Be(PaymentStatus.Applied);
        (await db.SalesReceivables.AsNoTracking().SingleAsync(x => x.Id == ar)).PaidAmount.Should().Be(40m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_cash_or_bank_without_trace_is_rejected(bool bank)
    {
        var op = await f.CreateOperatorAsync();
        var ar = await f.CreateReceivableAsync(100m);
        Guid id;
        using (var scope = f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            Guid? bankId = bank ? await BankAccount(db, op.UserId) : null;
            var p = Payment.Create(f.TenantId, f.CompanyId, PaymentDirection.Collection, f.CustomerId,
                40m, f.Today, f.CashMethodId, "Historical fixture", op.UserId, bankId, bank ? null : op.CashRegisterId);
            p.AddApplicationLine(ar, null, 40m); p.Apply(op.UserId);
            (await db.SalesReceivables.SingleAsync(x => x.Id == ar)).RegisterCollection(40m, op.UserId);
            db.Payments.Add(p); await db.SaveChangesAsync(); id = p.Id;
        }
        (await Reverse(f.CreateClient(op.UserId), id)).Should().Be(HttpStatusCode.UnprocessableEntity);
        using var verify = f.CreateDbScope();
        var check = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await check.Payments.AsNoTracking().SingleAsync(x => x.Id == id)).Status.Should().Be(PaymentStatus.Applied);
        (await check.SalesReceivables.AsNoTracking().SingleAsync(x => x.Id == ar)).PaidAmount.Should().Be(40m);
    }

    private async Task<Guid> BankAccount(ErpDbContext db, Guid actor)
    {
        var bank = Bank.Create(f.TenantId, Guid.NewGuid().ToString("N")[..12], "Caso20 Bank", null, actor);
        db.Set<Bank>().Add(bank); await db.SaveChangesAsync();
        var account = CompanyBankAccount.Create(f.TenantId, f.CompanyId, bank.Id, BankAccountType.Checking,
            "123456789", "Caso20 test", f.CashAccountId, actor);
        db.CompanyBankAccounts.Add(account); await db.SaveChangesAsync(); return account.Id;
    }

    [Fact]
    public async Task Valid_company_context_cannot_reverse_foreign_company_payment()
    {
        var op = await f.CreateOperatorAsync();
        var ar = await f.CreateReceivableAsync(100m, foreignCompany: true);
        Guid id;
        using (var scope = f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var receivable = await db.SalesReceivables.IgnoreQueryFilters()
                .SingleAsync(x => x.Id == ar && x.TenantId == f.TenantId);
            db.AccountingOperationalPolicies.Add(AccountingOperationalPolicy.Create(f.TenantId,
                receivable.CompanyId, AccountingOperationalMode.Enabled, new DateOnly(1900, 1, 1),
                "Isolated foreign-company test fixture", op.UserId));
            await db.SaveChangesAsync();
            var p = Payment.Create(f.TenantId, receivable.CompanyId, PaymentDirection.Collection,
                f.CustomerId, 40m, f.Today, f.CashMethodId, "Foreign company fixture", op.UserId);
            p.AddApplicationLine(ar, null, 40m); p.Apply(op.UserId);
            receivable.RegisterCollection(40m, op.UserId);
            db.Payments.Add(p);
            var context = f.Factory.Services.GetRequiredService<MutableCurrentCompany>();
            var ownCompany = context.CompanyId;
            try
            {
                context.CompanyId = receivable.CompanyId;
                await db.SaveChangesAsync(); id = p.Id;
            }
            finally { context.CompanyId = ownCompany; }
        }
        (await Reverse(f.CreateClient(op.UserId), id)).Should().Be(HttpStatusCode.NotFound);
        using var verify = f.CreateDbScope();
        var check = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await check.Payments.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id && x.TenantId == f.TenantId))
            .Status.Should().Be(PaymentStatus.Applied);
    }

    [Fact]
    public async Task Bank_destination_reversal_preserves_bank_and_cash_operational_state()
    {
        var op = await f.CreateOperatorAsync();
        var ar = await f.CreateReceivableAsync(100m);
        Guid bank;
        string before;
        int movements;
        using (var scope = f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            bank = await BankAccount(db, op.UserId);
        }
        using (var persisted = f.CreateDbScope())
        {
            var db = persisted.ServiceProvider.GetRequiredService<ErpDbContext>();
            before = JsonSerializer.Serialize(await db.CompanyBankAccounts.AsNoTracking().SingleAsync(x => x.Id == bank));
            movements = await db.CashMovements.CountAsync();
        }
        var client = f.CreateClient(op.UserId);
        var id = await Collect(client, ar, 40m, bank: bank);
        (await Reverse(client, id)).Should().Be(HttpStatusCode.OK);
        using var verify = f.CreateDbScope();
        var check = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        JsonSerializer.Serialize(await check.CompanyBankAccounts.AsNoTracking().SingleAsync(x => x.Id == bank)).Should().Be(before);
        (await check.CashMovements.CountAsync()).Should().Be(movements);
        (await check.Payments.AsNoTracking().SingleAsync(x => x.Id == id)).Status.Should().Be(PaymentStatus.Reversed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_cash_session_is_preserved_and_linked_movement_blocks(bool linked)
    {
        var op = await f.CreateOperatorAsync(openingCash: 0m);
        var ar = await f.CreateReceivableAsync(100m);
        var client = f.CreateClient(op.UserId);
        var id = await Collect(client, ar, 40m, op.CashRegisterId);
        string before;
        using (var scope = f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var session = await db.CashSessions.Include(x => x.Movements).SingleAsync(x => x.Id == op.CashSessionId);
            if (linked) session.RecordMovement(CashMovementType.SaleIncome, 40m, "Historical operational reference", op.UserId,
                CashReferenceType.None, id);
            session.Close(op.UserId, []);
            await db.SaveChangesAsync();
        }
        using (var persisted = f.CreateDbScope())
        {
            var db = persisted.ServiceProvider.GetRequiredService<ErpDbContext>();
            var session = await db.CashSessions.AsNoTracking().Include(x => x.Movements).SingleAsync(x => x.Id == op.CashSessionId);
            before = JsonSerializer.Serialize(new { session.Status, session.ClosedAt, session.ClosedBy,
                session.ExpectedAmount, session.CountedAmount, session.Difference, session.CurrentBalance,
                Movements = session.Movements.OrderBy(x => x.Id) });
        }
        (await Reverse(client, id)).Should().Be(linked ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.OK);
        using var verify = f.CreateDbScope();
        var check = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        var after = await check.CashSessions.AsNoTracking().Include(x => x.Movements).SingleAsync(x => x.Id == op.CashSessionId);
        JsonSerializer.Serialize(new { after.Status, after.ClosedAt, after.ClosedBy,
            after.ExpectedAmount, after.CountedAmount, after.Difference, after.CurrentBalance,
            Movements = after.Movements.OrderBy(x => x.Id) }).Should().Be(before);
        (await check.Payments.AsNoTracking().SingleAsync(x => x.Id == id)).Status
            .Should().Be(linked ? PaymentStatus.Applied : PaymentStatus.Reversed);
    }

    [Fact]
    public async Task Closed_effective_period_blocks_and_preserves_payment_and_original()
    {
        var isolated = new FinancialCommandIdempotencyFixture();
        await isolated.InitializeAsync();
        try { await new CollectionReversalTechnicalTests(isolated, output).ClosedPeriodScenario(); }
        finally { await isolated.DisposeAsync(); }
    }

    private async Task ClosedPeriodScenario()
    {
        var op = await f.CreateOperatorAsync();
        var ar = await f.CreateReceivableAsync(100m);
        var client = f.CreateClient(op.UserId);
        var id = await Collect(client, ar, 40m);
        Guid original;
        using (var scope = f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            original = (await db.JournalEntries.SingleAsync(x => x.SourceEventId == id && x.SourceEventType == "CollectionApplied")).Id;
            var period = await db.AccountingPeriods.SingleAsync(x => x.StartDate <= f.Today && x.EndDate >= f.Today);
            period.Close(op.UserId, JournalEntryClosureReadiness.Ready);
            await db.SaveChangesAsync();
        }
        var status = await Reverse(client, id);
        ((int)status).Should().BeInRange(400, 499);
        using var verify = f.CreateDbScope();
        var check = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await check.Payments.AsNoTracking().SingleAsync(x => x.Id == id)).Status.Should().Be(PaymentStatus.Applied);
        (await check.SalesReceivables.AsNoTracking().SingleAsync(x => x.Id == ar)).PaidAmount.Should().Be(40m);
        (await check.JournalEntries.CountAsync(x => x.OriginalJournalEntryId == original)).Should().Be(0);
    }
}
