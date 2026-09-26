using ERP.Application.Common;
using ERP.Application.Modules.Caja.FundingRequests;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Caja;

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B — PostgreSQL 16 real: tabla, constraints e índices de
/// <c>cash_funding_requests</c> (migración real), repositorio con alcance empresa/tenant, jsonb
/// roundtrip con hash estable, idempotencia por ClientRequestId, unicidad del pago que consume la
/// solicitud, lock FOR UPDATE que serializa transacciones y persistencia de originador/ejecutor en
/// SupplierPayment.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CashFundingRequestRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_cash_funding_request_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _cashier = Guid.NewGuid();
    private readonly Guid _requester = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _otherCompanyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _cashRegisterId;
    private Guid _cashSessionId;
    private Guid _bankMethodId;
    private Guid _cashMethodId;
    private Guid _bankAccountId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _cashier);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _cashier);
        var other = Company.CreateManaged(tenant.Id, "1790098765001", "Otra S.A.", createdBy: _cashier);
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, other);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherCompanyId = other.Id;

        var branch = Branch.Create(
            _tenantId, "Matriz", "Av. Principal 123", "001", null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, true, _cashier,
            companyId: _companyId);
        var supplier = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Proveedor Test", _cashier);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
        _branchId = branch.Id;
        _supplierId = supplier.Id;

        var ledger = Account.Create(_tenantId, _companyId, AccountCode.Create("1.1.02.001"), "Bancos",
            null, AccountType.Asset, AccountNature.Debit, allowsPosting: true, createdBy: _cashier);
        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", _cashier);
        var bankMethod = PaymentMethod.Create(_tenantId, "TRANSFER", "Transferencia", true, false, 1, _cashier, PaymentMethodDetailType.Transfer);
        var cashMethod = PaymentMethod.Create(_tenantId, "CASH", "Efectivo", false, false, 2, _cashier, affectsPhysicalCash: true);
        db.Accounts.Add(ledger);
        db.Banks.Add(bank);
        db.PaymentMethods.AddRange(bankMethod, cashMethod);
        await db.SaveChangesAsync();
        var bankAccount = CompanyBankAccount.Create(_tenantId, _companyId, bank.Id, BankAccountType.Checking,
            "2200123456", "Banco Pichincha CTE", ledger.Id, _cashier);
        db.CompanyBankAccounts.Add(bankAccount);

        var register = CashRegister.Create(_tenantId, _companyId, _branchId, "CAJA-01", "Caja Principal", _cashier);
        register.SetAccountingAccount(ledger.Id, _cashier);
        db.CashRegisters.Add(register);
        var establishment = Establishment.Create(_tenantId, _branchId, _companyId, "001", "Matriz", "Av. Principal 123", null, isMain: true, _cashier);
        db.Set<Establishment>().Add(establishment);
        await db.SaveChangesAsync();
        var emissionPoint = EmissionPoint.Create(_tenantId, _companyId, establishment.Id, "001", "Punto 1",
            ERP.Domain.Modules.Company.Enums.EmissionType.Electronic, isDefault: true, _cashier);
        db.Set<EmissionPoint>().Add(emissionPoint);
        await db.SaveChangesAsync();
        var session = CashSession.Open(_tenantId, _companyId, _branchId, _cashier, register.Id, "CAJA-01",
            "Caja Principal", emissionPoint.Id, "001", 500m, _cashier);
        db.Set<CashSession>().Add(session);
        await db.SaveChangesAsync();

        _cashRegisterId = register.Id;
        _cashSessionId = session.Id;
        _bankMethodId = bankMethod.Id;
        _cashMethodId = cashMethod.Id;
        _bankAccountId = bankAccount.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid? companyId = null) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => companyId ?? _companyId)
        );

    /// <summary>Pago mixto: banco 120 + efectivo 80 de la caja del cajero, preparado por el solicitante.</summary>
    private RegisterSupplierPaymentCommand MixedIntent() =>
        new(
            _supplierId,
            new DateOnly(2026, 9, 20),
            200m,
            null,
            [
                new SupplierPaymentMethodLineRequest(_bankMethodId, _bankAccountId, null, 120m, "OP-1", TransactionDate: new DateOnly(2026, 9, 20)),
                new SupplierPaymentMethodLineRequest(_cashMethodId, null, _cashRegisterId, 80m),
            ],
            [],
            [],
            ConfirmUnappliedAmount: true
        );

    private CashFundingRequest NewRequest(Guid? clientRequestId = null, Guid? requester = null)
    {
        var snapshot = CashFundingPaymentSnapshot.FromIntent(MixedIntent());
        return CashFundingRequest.Create(
            _tenantId, _companyId, _branchId, _cashRegisterId, _cashSessionId, _supplierId,
            snapshot.TotalAmount, CashFundingPaymentSnapshot.CashAmountFor(snapshot, _cashRegisterId),
            requester ?? _requester,
            CashFundingPaymentSnapshot.Serialize(snapshot),
            CashFundingPaymentSnapshot.CurrentVersion,
            CashFundingPaymentSnapshot.ComputeHash(snapshot),
            clientRequestId ?? Guid.NewGuid());
    }

    private async Task<CashFundingRequest> AddAsync(CashFundingRequest request)
    {
        await using var db = CreateContext();
        await new CashFundingRequestRepository(db, new FixedCurrentCompany(() => _companyId)).AddAsync(request);
        await db.SaveChangesAsync();
        return request;
    }

    private async Task<SupplierPayment> AddPaymentAsync(Guid createdBy, Guid confirmedBy)
    {
        await using var db = CreateContext();
        var payment = SupplierPayment.Create(
            _tenantId, _companyId, _branchId, _supplierId, new DateOnly(2026, 9, 20), 50m,
            $"SP-{Guid.NewGuid():N}"[..12], null,
            [new SupplierPaymentMethodLineInput(_bankMethodId, _bankAccountId, null, 50m, "OP-9", TransactionDate: new DateOnly(2026, 9, 20))],
            [], [], createdBy, unappliedAmountConfirmed: true, allowWithoutPayable: true, confirmedBy: confirmedBy);
        db.SupplierPayments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pending_se_persiste_y_el_payload_jsonb_reproduce_la_misma_huella()
    {
        var created = await AddAsync(NewRequest());

        await using var db = CreateContext();
        var loaded = await new CashFundingRequestRepository(db, new FixedCurrentCompany(() => _companyId))
            .GetByIdAsync(_tenantId, created.Id);

        loaded.Should().NotBeNull();
        (loaded!.Status, loaded.CashAmount, loaded.TotalAmount, loaded.RequestedByUserId, loaded.PayloadVersion)
            .Should().Be((CashFundingRequestStatus.Pending, 80m, 200m, _requester, 1));
        var reread = CashFundingPaymentSnapshot.Deserialize(loaded.PaymentPayload, loaded.PayloadVersion);
        CashFundingPaymentSnapshot.ComputeHash(reread).Should().Be(loaded.PayloadHash, "jsonb normaliza el texto, no la intención");
        CashFundingPaymentSnapshot.ToIntent(reread).Should().BeEquivalentTo(MixedIntent(), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Otra_empresa_del_mismo_tenant_no_ve_la_solicitud()
    {
        var created = await AddAsync(NewRequest());

        await using var db = CreateContext(_otherCompanyId);
        var repo = new CashFundingRequestRepository(db, new FixedCurrentCompany(() => _otherCompanyId));
        (await repo.GetByIdAsync(_tenantId, created.Id)).Should().BeNull();
        await using var tx = await db.Database.BeginTransactionAsync();
        (await repo.GetByIdForUpdateAsync(_tenantId, created.Id)).Should().BeNull();
        (await repo.ListBySessionAsync(_tenantId, _cashSessionId, CashFundingRequestStatus.Pending)).Should().BeEmpty();
    }

    [Fact]
    public async Task ClientRequestId_es_unico_por_tenant_y_permite_resolver_el_reintento()
    {
        var clientRequestId = Guid.NewGuid();
        var created = await AddAsync(NewRequest(clientRequestId));

        await using (var db = CreateContext())
        {
            var found = await new CashFundingRequestRepository(db, new FixedCurrentCompany(() => _companyId))
                .GetByClientRequestIdAsync(_tenantId, clientRequestId);
            found!.Id.Should().Be(created.Id);
        }

        var duplicate = () => AddAsync(NewRequest(clientRequestId));
        (await duplicate.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>().Which.SqlState.Should().Be("23505");
    }

    [Fact]
    public async Task Un_pago_no_puede_consumir_dos_solicitudes()
    {
        var payment = await AddPaymentAsync(_requester, _cashier);
        var first = NewRequest();
        first.Fulfill(_cashier, payment.Id);
        await AddAsync(first);

        var second = NewRequest();
        second.Fulfill(_cashier, payment.Id);
        var act = () => AddAsync(second);

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>().Which.ConstraintName.Should().Be("uq_cash_funding_requests_supplier_payment");
    }

    [Theory]
    [InlineData("UPDATE cash_funding_requests SET status = 2 WHERE id = @id", "chk_cash_funding_requests_payment_only_when_fulfilled")]
    [InlineData("UPDATE cash_funding_requests SET status = 3 WHERE id = @id", "chk_cash_funding_requests_resolution_consistency")]
    [InlineData("UPDATE cash_funding_requests SET cash_amount = 0 WHERE id = @id", "chk_cash_funding_requests_cash_amount_positive")]
    [InlineData("UPDATE cash_funding_requests SET total_amount = 10 WHERE id = @id", "chk_cash_funding_requests_total_covers_cash")]
    public async Task La_BD_rechaza_estados_inconsistentes(string sql, string constraint)
    {
        var created = await AddAsync(NewRequest());
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", created.Id);

        var act = () => command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(constraint);
    }

    [Fact]
    public async Task FOR_UPDATE_serializa_dos_transacciones_y_la_segunda_ve_el_estado_vigente()
    {
        var created = await AddAsync(NewRequest());

        await using var first = CreateContext();
        await using var firstTx = await first.Database.BeginTransactionAsync();
        var firstRepo = new CashFundingRequestRepository(first, new FixedCurrentCompany(() => _companyId));
        var locked = await firstRepo.GetByIdForUpdateAsync(_tenantId, created.Id);
        locked!.Reject(_cashier, "Sin efectivo");

        var secondTask = Task.Run(async () =>
        {
            await using var second = CreateContext();
            await using var secondTx = await second.Database.BeginTransactionAsync();
            var request = await new CashFundingRequestRepository(second, new FixedCurrentCompany(() => _companyId))
                .GetByIdForUpdateAsync(_tenantId, created.Id);
            var status = request!.Status;
            await secondTx.CommitAsync();
            return status;
        });

        await Task.Delay(1500);
        secondTask.IsCompleted.Should().BeFalse("la segunda transacción espera el lock de la primera");

        await first.SaveChangesAsync();
        await firstTx.CommitAsync();

        (await secondTask).Should().Be(CashFundingRequestStatus.Rejected,
            "tras el lock se recarga el estado vigente: la solicitud ya no está Pending");
    }

    [Fact]
    public async Task Consultas_por_sesion_y_por_solicitante_filtran_por_estado()
    {
        var otherRequester = Guid.NewGuid();
        var pendingA = await AddAsync(NewRequest());
        var pendingB = await AddAsync(NewRequest(requester: otherRequester));
        var rejected = NewRequest();
        rejected.Reject(_cashier, "No");
        await AddAsync(rejected);

        await using var db = CreateContext();
        var repo = new CashFundingRequestRepository(db, new FixedCurrentCompany(() => _companyId));

        (await repo.ListBySessionAsync(_tenantId, _cashSessionId, CashFundingRequestStatus.Pending))
            .Select(r => r.Id).Should().BeEquivalentTo([pendingA.Id, pendingB.Id]);
        (await repo.ListBySessionAsync(_tenantId, _cashSessionId, CashFundingRequestStatus.Rejected))
            .Select(r => r.Id).Should().Equal(rejected.Id);
        (await repo.ListByRequesterAsync(_tenantId, otherRequester, CashFundingRequestStatus.Pending))
            .Select(r => r.Id).Should().Equal(pendingB.Id);
    }

    [Fact]
    public async Task SupplierPayment_persiste_originador_y_ejecutor()
    {
        var payment = await AddPaymentAsync(createdBy: _requester, confirmedBy: _cashier);

        await using var db = CreateContext();
        var loaded = await db.SupplierPayments.AsNoTracking().SingleAsync(p => p.Id == payment.Id);
        (loaded.CreatedBy, loaded.ConfirmedByUserId).Should().Be((_requester, _cashier));
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
