using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ERP.API.Tests.Support;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.API.Tests.Integration;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F4 — aplicación y reversa sobre una CxC creada por el proceso oficial de
/// saldos iniciales (fecha de apertura, plantilla, carga, validación, confirmación y contabilización
/// por HTTP) en una empresa sin operaciones previas, contra PostgreSQL real.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CustomerAdvanceOpeningBalanceApiTests : IAsyncLifetime
{
    private const string Url = "/api/v1/finance/customer-advances";
    private const string InitialLoad = "/api/v1/initial-load";
    private const string CustomerIdentification = "1710034065";
    private const decimal Balance = 150.75m;

    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _customerId;
    private Guid _transferMethodId;
    private Guid _bankAccountId;
    private Guid _advancesAccountId;
    private Guid _receivablesAccountId;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    /// <summary>Fecha de apertura de saldos: origen contable de la CxC inicial.</summary>
    private static DateOnly Opening => Today.AddDays(-2);

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
        await SeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Una_CxC_de_saldo_inicial_admite_aplicar_desde_la_apertura_y_revertir()
    {
        var (receivableId, batchId) = await LoadOpeningReceivableAsync();
        var opening = await OkAsync(
            await _client.PostAsync($"{InitialLoad}/batches/{batchId}/opening-posting", null)
        );
        opening.GetProperty("status").GetString().Should().Be("Posted");
        var advance = await CreatedAsync(
            await _client.PostAsJsonAsync(
                Url,
                new
                {
                    customerId = _customerId,
                    amount = 100m,
                    advanceDate = Today.AddDays(-10),
                    paymentMethodId = _transferMethodId,
                    companyBankAccountId = _bankAccountId,
                    clientRequestId = Guid.NewGuid(),
                }
            )
        );
        var advanceId = advance.GetProperty("id").GetGuid();
        var before = await ReceivableAsync(receivableId);
        before.Should().Be(new State(Balance, 0m, Balance, "pending", 0m, Balance));

        var key = Guid.NewGuid();
        Task<HttpResponseMessage> ApplyOnAsync(DateOnly effectiveDate) =>
            _client.PostAsJsonAsync(
                $"{Url}/{advanceId}/applications",
                new
                {
                    effectiveDate,
                    lines = new[] { new { receivableId, amount = 60.25m } },
                    clientRequestId = key,
                }
            );

        // Posterior a la emisión del documento y al anticipo, pero anterior a la apertura.
        var early = await ApplyOnAsync(Opening.AddDays(-1));
        var earlyBody = await early.Content.ReadAsStringAsync();
        early.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, earlyBody);
        earlyBody.Should().Contain("anterior a la fecha de origen");
        (await ReceivableAsync(receivableId)).Should().Be(before);
        (await MovementsAsync(advanceId)).Should().ContainSingle();
        (
            await QueryAsync(db =>
                db.AccountingPostings.IgnoreQueryFilters()
                    .CountAsync(p => p.FactType == "CustomerAdvanceApplied")
            )
        )
            .Should()
            .Be(0);

        // En la fecha de apertura, con la misma clave que el rechazo no consumió.
        var applied = await OkAsync(await ApplyOnAsync(Opening));

        applied.GetProperty("availableAmount").GetDecimal().Should().Be(39.75m);
        (await ReceivableAsync(receivableId))
            .Should()
            .Be(new State(Balance, 60.25m, 90.50m, "pending", 60.25m, Balance));
        var application = (await MovementsAsync(advanceId)).Last();
        application.MovementType.Should().Be(CustomerAdvanceMovementType.Application);
        (application.Amount, application.ReceivableId, application.InstallmentId)
            .Should()
            .Be((60.25m, (Guid?)receivableId, (Guid?)null));
        await AssertEntryAsync(
            application.Id,
            (_advancesAccountId, 60.25m, 0m),
            (_receivablesAccountId, 0m, 60.25m)
        );

        var reversed = await OkAsync(
            await _client.PostAsJsonAsync(
                $"{Url}/{advanceId}/applications/{key}/reverse",
                new
                {
                    reason = "Error de aplicación",
                    reference = "ACTA-SI",
                    clientRequestId = Guid.NewGuid(),
                }
            )
        );

        reversed.GetProperty("availableAmount").GetDecimal().Should().Be(100m);
        (await ReceivableAsync(receivableId)).Should().Be(before);
        var movements = await MovementsAsync(advanceId);
        movements
            .Select(m => (m.MovementType, m.Amount))
            .Should()
            .Equal(
                (CustomerAdvanceMovementType.Received, 100m),
                (CustomerAdvanceMovementType.Application, 60.25m),
                (CustomerAdvanceMovementType.ReversalOfApplication, 60.25m)
            );
        movements[2].ReceivableId.Should().Be(receivableId);

        var history = await OkAsync(await _client.GetAsync($"{Url}/by-receivable/{receivableId}"));
        history.GetArrayLength().Should().Be(1);
        history[0].GetProperty("isReversed").GetBoolean().Should().BeTrue();

        // Contabilidad: el asiento de la aplicación queda espejado y la cuenta por cobrar vuelve
        // al saldo de la apertura.
        var entries = await QueryAsync(db =>
            db.JournalEntries.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(j => j.Lines)
                .Where(j => j.CompanyId == _companyId)
                .ToListAsync()
        );
        var original = entries.Single(e => e.SourceEventId == application.Id);
        entries
            .Single(e => e.OriginalJournalEntryId == original.Id)
            .Lines.Select(l => (l.AccountId, l.Debit, l.Credit))
            .Should()
            .BeEquivalentTo(
                new[] { (_advancesAccountId, 0m, 60.25m), (_receivablesAccountId, 60.25m, 0m) }
            );
        entries
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == _receivablesAccountId)
            .Sum(l => l.Debit - l.Credit)
            .Should()
            .Be(Balance);
        (
            await QueryAsync(async db =>
                (
                    await db.Payments.IgnoreQueryFilters().CountAsync(),
                    await db.CashMovements.IgnoreQueryFilters().CountAsync()
                )
            )
        )
            .Should()
            .Be((0, 0));
    }

    // ── Proceso oficial de saldo inicial ─────────────────────────────────

    private async Task<(Guid ReceivableId, Guid BatchId)> LoadOpeningReceivableAsync()
    {
        await OkAsync(
            await _client.PutAsJsonAsync(
                $"{InitialLoad}/opening-balance-date",
                new { openingBalanceDate = Opening }
            )
        );
        var batch = await CreatedAsync(
            await _client.PostAsJsonAsync(
                $"{InitialLoad}/batches",
                new { importType = "InitialReceivables" }
            )
        );
        var batchId = batch.GetProperty("id").GetGuid();
        var template = await _client.GetByteArrayAsync(
            $"{InitialLoad}/templates/InitialReceivables"
        );

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(FillTemplate(template));
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        );
        content.Add(file, "file", "cxc-inicial.xlsx");
        await OkAsync(await _client.PostAsync($"{InitialLoad}/batches/{batchId}/upload", content));
        var validated = await OkAsync(
            await _client.PostAsync($"{InitialLoad}/batches/{batchId}/validate", null)
        );
        validated.GetProperty("validRows").GetInt32().Should().Be(1, validated.ToString());
        await OkAsync(await _client.PostAsync($"{InitialLoad}/batches/{batchId}/confirm", null));

        var receivable = await QueryAsync(db =>
            db.SalesReceivables.IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(r => r.ImportBatchId == batchId)
        );
        receivable.Origin.Should().Be(SalesReceivableOrigin.InitialBalance);
        receivable.InvoiceId.Should().BeNull();
        receivable.IssueDate.Should().Be(Today.AddDays(-30));
        return (receivable.Id, batchId);
    }

    /// <summary>Plantilla oficial con su fila de ejemplo reemplazada.</summary>
    private static byte[] FillTemplate(byte[] template)
    {
        using var workbook = new XLWorkbook(new MemoryStream(template));
        var sheet = workbook.Worksheets.First();
        string[] row =
        [
            "05",
            CustomerIdentification,
            "001-001-000000777",
            Today.AddDays(-30).ToString("yyyy-MM-dd"),
            Today.AddDays(15).ToString("yyyy-MM-dd"),
            "150.75",
            "USD",
            Opening.ToString("yyyy-MM-dd"),
        ];
        for (var i = 0; i < row.Length; i++)
            sheet.Cell(2, i + 1).Value = row[i];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ── Comprobaciones ───────────────────────────────────────────────────

    private sealed record State(
        decimal Original,
        decimal Paid,
        decimal BalanceDue,
        string Status,
        decimal InstallmentsPaid,
        decimal InstallmentsAmount
    );

    private Task<State> ReceivableAsync(Guid id) =>
        QueryAsync(async db =>
        {
            var r = await db
                .SalesReceivables.IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Installments)
                .SingleAsync(x => x.Id == id);
            return new State(
                r.OriginalAmount,
                r.PaidAmount,
                r.BalanceDue,
                r.Status,
                r.Installments.Sum(i => i.PaidAmount),
                r.Installments.Sum(i => i.Amount)
            );
        });

    private Task<List<CustomerAdvanceMovement>> MovementsAsync(Guid advanceId) =>
        QueryAsync(async db =>
            (
                await db
                    .CustomerAdvanceMovements.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(m => m.CustomerAdvanceId == advanceId)
                    .ToListAsync()
            )
                .OrderBy(m => m.CreatedAtUtc)
                .ToList()
        );

    private async Task AssertEntryAsync(
        Guid sourceId,
        params (Guid Account, decimal Debit, decimal Credit)[] lines
    )
    {
        var (posting, entry) = await QueryAsync(async db =>
            (
                await db
                    .AccountingPostings.IgnoreQueryFilters()
                    .SingleAsync(p => p.SourceEventId == sourceId),
                await db
                    .JournalEntries.IgnoreQueryFilters()
                    .Include(j => j.Lines)
                    .SingleAsync(j => j.SourceEventId == sourceId)
            )
        );
        posting.Status.Should().Be(AccountingPostingStatus.Posted);
        posting.FactType.Should().Be("CustomerAdvanceApplied");
        entry.Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).Should().BeEquivalentTo(lines);
    }

    // ── Infraestructura ──────────────────────────────────────────────────

    private static async Task<JsonElement> DataAsync(
        HttpResponseMessage http,
        HttpStatusCode status
    )
    {
        var body = await http.Content.ReadAsStringAsync();
        http.StatusCode.Should().Be(status, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }

    private static Task<JsonElement> OkAsync(HttpResponseMessage http) =>
        DataAsync(http, HttpStatusCode.OK);

    private static Task<JsonElement> CreatedAsync(HttpResponseMessage http) =>
        DataAsync(http, HttpStatusCode.Created);

    private async Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    /// <summary>Empresa nueva, sin operaciones: maestros mínimos y bootstrap contable oficial.</summary>
    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var admin = Guid.NewGuid();
        var tenant = Tenant.Create("CA-SI", $"ca-si-{Guid.NewGuid():N}", admin);
        var user = IdentityUser.Create(
            $"ca-si-{Guid.NewGuid():N}",
            "Admin",
            "CA",
            $"ca-si-{Guid.NewGuid():N}@example.com",
            "hash",
            admin
        );
        db.Tenants.Add(tenant);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;

        var company = Company.CreateManaged(
            _tenantId,
            CompanyIdentityUpdateFixture.ValidRuc(),
            "Empresa Saldos Iniciales S.A.",
            createdBy: admin
        );
        db.AccountingOperationalPolicies.Add(
            AccountingOperationalPolicy.Create(
                _tenantId,
                company.Id,
                AccountingOperationalMode.Enabled,
                new DateOnly(1900, 1, 1),
                "Test",
                admin
            )
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _companyId = company.Id;
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableCompany.CompanyId = _companyId;
        _factory.MutableUser.UserId = user.Id;

        db.CompanyPrecisionPolicies.Add(
            CompanyPrecisionPolicy.CreateStandardCommercial(_tenantId, _companyId, admin)
        );
        var branch = Branch.Create(
            tenantId: _tenantId,
            name: "Matriz",
            address: "Av. 1",
            code: "B01",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: admin,
            companyId: _companyId
        );
        db.Branches.Add(branch);
        var customer = BusinessPartner.Create(
            _tenantId,
            "05",
            CustomerIdentification,
            1,
            "Cliente Saldo Inicial",
            admin
        );
        var transfer = PaymentMethod.Create(
            _tenantId,
            "TRANSF",
            "Transferencia",
            false,
            false,
            1,
            admin,
            PaymentMethodDetailType.Transfer
        );
        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", admin);
        db.BusinessPartners.Add(customer);
        db.PaymentMethods.Add(transfer);
        db.Add(bank);
        await db.SaveChangesAsync();
        db.BusinessPartnerRoles.Add(
            BusinessPartnerRole.Create(_tenantId, customer.Id, RoleType.Customer, admin)
        );
        var membership = CompanyUserMembership.Create(_companyId, user.Id, "Admin", null, admin);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(_tenantId, _companyId, membership.Id, branch.Id, admin)
        );
        await db.SaveChangesAsync();

        await new AccountingBootstrapStep(
            db,
            new AlwaysTodayCompanyClock(),
            NullLogger<AccountingBootstrapStep>.Instance
        ).ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, admin));

        var accounts = (
            await db
                .Accounts.IgnoreQueryFilters()
                .Where(a => a.CompanyId == _companyId)
                .ToListAsync()
        ).ToDictionary(a => a.Code.Value, a => a.Id);
        var bankAccount = CompanyBankAccount.Create(
            _tenantId,
            _companyId,
            bank.Id,
            BankAccountType.Checking,
            "2200000002",
            "Cuenta anticipos",
            accounts["1.1.02.001"],
            admin
        );
        db.Add(bankAccount);
        await db.SaveChangesAsync();

        _customerId = customer.Id;
        _transferMethodId = transfer.Id;
        _bankAccountId = bankAccount.Id;
        _advancesAccountId = accounts["2.1.01.002"];
        _receivablesAccountId = accounts["1.1.03.001"];

        // Primeros días de enero: las fechas pasadas de la prueba caen en el año anterior.
        var past = Today.AddDays(-10);
        if (
            !await db
                .AccountingPeriods.IgnoreQueryFilters()
                .AnyAsync(p => p.CompanyId == _companyId && p.FiscalYear == past.Year)
        )
        {
            db.AccountingPeriods.Add(
                AccountingPeriod.Create(
                    _tenantId,
                    _companyId,
                    past.Year,
                    1,
                    new DateOnly(past.Year, 1, 1),
                    new DateOnly(past.Year, 12, 31),
                    admin
                )
            );
            await db.SaveChangesAsync();
        }

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, user.Id, _companyId, "Admin")
        );
        _client.DefaultRequestHeaders.Add("X-Company-Id", _companyId.ToString());
        _client.DefaultRequestHeaders.Add("X-Branch-Id", branch.Id.ToString());
    }
}
