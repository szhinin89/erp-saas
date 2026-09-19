using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Sales;

/// <summary>
/// CASH-SESSION-COLLECTION-SUMMARY-UX-02 — suite de integración (PostgreSQL 16 real vía
/// Testcontainers) para <see cref="SalesInvoiceRepository.GetCollectionSummaryByCashSessionAsync"/>.
/// Cubre específicamente el riesgo de traducción EF de esta proyección: <c>SelectMany</c> sobre
/// <c>Payments</c> combinado con acceso condicional al owned type <c>PaymentTransferDetail</c>
/// (<c>p.TransferDetail == null ? null : p.TransferDetail.X</c>) — un patrón que EF Core podría
/// fallar en traducir a SQL y forzar evaluación en cliente (o lanzar). Los tests de Application
/// (con mocks) no detectan esto porque nunca pasan por el proveedor real de EF/Npgsql.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CashSessionCollectionSummaryRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_cash_collection_summary_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _otherBranchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _emissionPointId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            true, _createdBy, companyId: company.Id
        );
        var otherBranch = Branch.Create(
            tenant.Id, "Sucursal Sur", "Av. Sur 456", "002",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            false, _createdBy, companyId: company.Id
        );
        var customer = BusinessPartner.Create(tenant.Id, "05", "1710034065", 1, "Cliente Test", _createdBy);
        var establishment = Establishment.Create(
            tenant.Id, branchId: branch.Id, company.Id, code: "001", name: "Matriz Test",
            address: "Av. Principal 123", phone: null, isMain: true, createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            tenant.Id, company.Id, branch.Id, "CAJA-01", "Caja Principal", _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.AddRange(branch, otherBranch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id, company.Id, establishment.Id, code: "001", name: "PE-001",
            emissionType: EmissionType.Electronic, isDefault: true, createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id, company.Id, branch.Id, _createdBy, cashRegister.Id,
            "CAJA-01", "Caja Principal", emissionPoint.Id, "001", 0m, _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
        _emissionPointId = emissionPoint.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private SalesInvoice BuildDraft(
        string invoiceNumber,
        decimal unitPrice,
        Guid? branchIdOverride = null,
        Guid? cashSessionIdOverride = null
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0);

        var inv = SalesInvoice.CreateDraft(
            _tenantId,
            _companyId,
            branchIdOverride ?? _branchId,
            _customerId,
            customer,
            invoiceNumber: invoiceNumber,
            issueDate: new DateOnly(2026, 9, 18),
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: cashSessionIdOverride ?? _cashSessionId
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id, _tenantId, "Producto Test", quantity: 1, unitPrice: unitPrice, vatCode: "0", uomCode: "UNIT"
        );
        line.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _createdBy);
        return inv;
    }

    [Fact]
    public async Task Devuelve_una_fila_por_pago_de_facturas_autorizadas_e_incluye_datos_de_transferencia_reales()
    {
        await using var db = CreateContext();

        // Factura 1: Efectivo, autorizada.
        var cashInvoice = BuildDraft("001-001-000000001", 10m);
        var cashPayment = SalesInvoicePayment.Create(
            cashInvoice.Id, _tenantId, Guid.NewGuid(), "EFECTIVO", "Efectivo", cashInvoice.Lines.Single().TaxInclusiveTotal
        );
        cashInvoice.ReplacePayments(new[] { cashPayment }, _createdBy);
        cashInvoice.Authorize(_createdBy, cashApplied: cashInvoice.Lines.Single().TaxInclusiveTotal);

        // Factura 2: Transferencia con TransferDetail completo (banco real vía CompanyBankAccountId
        // simulado — el repo no necesita que la cuenta exista, solo lee el owned type del pago).
        var accountingAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create("1.1.02.001"), "Banco Pichincha Cta. Cte.",
            null, AccountType.Asset, AccountNature.Debit, allowsPosting: true, _createdBy
        );
        db.Accounts.Add(accountingAccount);
        await db.SaveChangesAsync();
        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", null, _createdBy);
        var bankAccount = CompanyBankAccount.Create(
            _tenantId, _companyId, bank.Id, BankAccountType.Checking, "2200123456",
            "Cuenta corriente Pichincha", accountingAccount.Id, _createdBy
        );
        db.Banks.Add(bank);
        db.CompanyBankAccounts.Add(bankAccount);
        await db.SaveChangesAsync();

        var transferInvoice = BuildDraft("001-001-000000002", 20m);
        var transferPayment = SalesInvoicePayment.Create(
            transferInvoice.Id, _tenantId, Guid.NewGuid(), "TRANSFERENCIA", "Transferencia Bancaria",
            transferInvoice.Lines.Single().TaxInclusiveTotal
        );
        transferPayment.SetTransferDetail(
            PaymentTransferDetail.Create(transferPayment.Id, bankAccount.Id, "TRX-000123", new DateOnly(2026, 9, 18))
        );
        transferInvoice.ReplacePayments(new[] { transferPayment }, _createdBy);
        transferInvoice.Authorize(_createdBy, cashApplied: transferInvoice.Lines.Single().TaxInclusiveTotal);

        // Factura 3: Draft (sin autorizar) — el repo debe excluirla.
        var draftInvoice = BuildDraft("DRAFT-001", 5m);

        // Factura 4: autorizada, pero de OTRA sucursal — el repo debe excluirla (fail-closed por branch).
        var otherBranchInvoice = BuildDraft("002-001-000000001", 8m, branchIdOverride: _otherBranchId);
        var otherBranchPayment = SalesInvoicePayment.Create(
            otherBranchInvoice.Id, _tenantId, Guid.NewGuid(), "EFECTIVO", "Efectivo",
            otherBranchInvoice.Lines.Single().TaxInclusiveTotal
        );
        otherBranchInvoice.ReplacePayments(new[] { otherBranchPayment }, _createdBy);
        otherBranchInvoice.Authorize(_createdBy, cashApplied: otherBranchInvoice.Lines.Single().TaxInclusiveTotal);

        db.SalesInvoices.AddRange(cashInvoice, transferInvoice, draftInvoice, otherBranchInvoice);
        await db.SaveChangesAsync();

        var repo = new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        var rows = await repo.GetCollectionSummaryByCashSessionAsync(_tenantId, _branchId, _cashSessionId);

        rows.Should().HaveCount(2, "solo las 2 facturas autorizadas de ESTA sucursal deben aparecer");
        rows.Should().OnlyContain(r => r.InvoiceNumber != "DRAFT-001" && r.InvoiceNumber != "002-001-000000001");

        var cashRow = rows.Single(r => r.InvoiceNumber == "001-001-000000001");
        cashRow.PaymentMethodCode.Should().Be("EFECTIVO");
        cashRow.GrandTotal.Should().Be(10m);
        cashRow.Amount.Should().Be(10m);
        cashRow.TransferCompanyBankAccountId.Should().BeNull();
        cashRow.TransferReceiptNumber.Should().BeNull();
        cashRow.Reference.Should().BeNull();

        var transferRow = rows.Single(r => r.InvoiceNumber == "001-001-000000002");
        transferRow.PaymentMethodCode.Should().Be("TRANSFERENCIA");
        transferRow.GrandTotal.Should().Be(20m);
        transferRow.Amount.Should().Be(20m);
        transferRow.TransferCompanyBankAccountId.Should().Be(bankAccount.Id);
        transferRow.TransferReceiptNumber.Should().Be("TRX-000123");
        transferRow.TransferDate.Should().Be(new DateOnly(2026, 9, 18));
        transferRow.TransferLegacyBankName.Should().BeNull();
    }

    [Fact]
    public async Task Factura_con_dos_pagos_produce_dos_filas_con_el_mismo_GrandTotal()
    {
        await using var db = CreateContext();

        var inv = BuildDraft("001-001-000000003", 30m);
        var cashPayment = SalesInvoicePayment.Create(
            inv.Id, _tenantId, Guid.NewGuid(), "EFECTIVO", "Efectivo", 10m
        );
        var transferPayment = SalesInvoicePayment.Create(
            inv.Id, _tenantId, Guid.NewGuid(), "TRANSFERENCIA", "Transferencia Bancaria", 20m
        );
        inv.ReplacePayments(new[] { cashPayment, transferPayment }, _createdBy);
        inv.Authorize(_createdBy, cashApplied: 30m);

        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        var repo = new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        var rows = await repo.GetCollectionSummaryByCashSessionAsync(_tenantId, _branchId, _cashSessionId);

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.InvoiceId == inv.Id && r.GrandTotal == 30m);
        rows.Sum(r => r.Amount).Should().Be(30m);
    }

    /// <summary>
    /// CASH-SESSION-LIST-SUMMARY-01 — la proyección plural (varias sesiones a la vez, usada por el
    /// listado de turnos) debe traer todas las facturas de todas las sesiones pedidas en un solo
    /// query, sin mezclar las filas de una sesión con las de otra.
    /// </summary>
    [Fact]
    public async Task GetCollectionSummaryByCashSessionsAsync_trae_facturas_de_varias_sesiones_sin_mezclarlas()
    {
        await using var db = CreateContext();

        var secondCashRegister = CashRegister.Create(
            _tenantId, _companyId, _branchId, "CAJA-02", "Caja Secundaria", _createdBy
        );
        db.CashRegisters.Add(secondCashRegister);
        await db.SaveChangesAsync();
        var secondCashier = Guid.NewGuid();
        var secondSession = CashSession.Open(
            _tenantId, _companyId, _branchId, secondCashier, secondCashRegister.Id,
            "CAJA-02", "Caja Secundaria", _emissionPointId, "001", 0m, _createdBy
        );
        db.CashSessions.Add(secondSession);
        await db.SaveChangesAsync();

        var invA = BuildDraft("001-001-000000010", 12m);
        var payA = SalesInvoicePayment.Create(invA.Id, _tenantId, Guid.NewGuid(), "EFECTIVO", "Efectivo", 12m);
        invA.ReplacePayments(new[] { payA }, _createdBy);
        invA.Authorize(_createdBy, cashApplied: 12m);

        var invB = BuildDraft("001-001-000000011", 25m, cashSessionIdOverride: secondSession.Id);
        var payB = SalesInvoicePayment.Create(invB.Id, _tenantId, Guid.NewGuid(), "EFECTIVO", "Efectivo", 25m);
        invB.ReplacePayments(new[] { payB }, _createdBy);
        invB.Authorize(_createdBy, cashApplied: 25m);

        db.SalesInvoices.AddRange(invA, invB);
        await db.SaveChangesAsync();

        var repo = new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        var rows = await repo.GetCollectionSummaryByCashSessionsAsync(
            _tenantId, _branchId, new[] { _cashSessionId, secondSession.Id }
        );

        rows.Should().HaveCount(2);
        rows.Single(r => r.CashSessionId == _cashSessionId).InvoiceNumber.Should().Be("001-001-000000010");
        rows.Single(r => r.CashSessionId == secondSession.Id).InvoiceNumber.Should().Be("001-001-000000011");
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
