using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

/// <summary>
/// PURCHASE-CREDIT-NOTE-AFFECTED-INVOICE-RESOLVES-CANCELLED-01 (reabierto) — reproduce exactamente
/// el escenario reportado contra PostgreSQL real (Testcontainers): una PurchaseInvoice
/// Cancelled y una Confirmed comparten proveedor+número (001-001-000031760); el documento de
/// recepción de la NC tiene su PROPIO número (001-001-000010350, nunca debe usarse para resolver)
/// y <c>ModifiedDocumentNumber</c> = 001-001-000031760 (el numDocModificado real). El handler de
/// <c>xml-view</c> — el único punto de lectura que se puede consultar en cualquier momento sin
/// volver a subir el TXT — debe resolver siempre la Confirmed vigente, nunca la Cancelled
/// histórica ni caer en un valor obsoleto calculado en un import anterior. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class GetPurchaseReceptionXmlViewAffectedInvoiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_xmlview_affected_invoice_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();

    private sealed record TenantContext(Guid TenantId, Guid CompanyId, Guid BranchId, Guid SupplierId, Guid PaymentTermId);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId = default, Guid companyId = default)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => companyId)
        );
    }

    private async Task<TenantContext> SeedTenantAsync()
    {
        await using var db = CreateContext();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            $"17{Random.Shared.Next(10000000, 99999999)}001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var branch = Branch.Create(
            tenantId: tenant.Id,
            name: "Matriz",
            address: "Av. Principal 123",
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
            createdBy: _userId,
            companyId: company.Id
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var supplier = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        var paymentTerm = PaymentTerm.Create(
            tenant.Id,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        db.BusinessPartners.Add(supplier);
        db.Add(paymentTerm);
        await db.SaveChangesAsync();

        return new TenantContext(tenant.Id, company.Id, branch.Id, supplier.Id, paymentTerm.Id);
    }

    private PurchaseInvoice BuildConfirmedInvoice(TenantContext ctx, string invoiceNumber)
    {
        var inv = PurchaseInvoice.CreateDraft(
            ctx.TenantId,
            ctx.CompanyId,
            ctx.BranchId,
            ctx.SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            invoiceNumber,
            DateOnly.FromDateTime(DateTime.UtcNow),
            _userId,
            ctx.PaymentTermId,
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            ctx.TenantId,
            "Producto 1",
            quantity: 10,
            unitPrice: 10.00m,
            vatCode: "10",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);
        return inv;
    }

    [Fact]
    public async Task XmlView_de_una_NC_resuelve_AffectedPurchaseId_a_la_Confirmed_vigente_nunca_a_la_Cancelled_historica()
    {
        var ctx = await SeedTenantAsync();
        const string affectedInvoiceNumber = "001-001-000031760";
        const string creditNoteOwnNumber = "001-001-000010350";

        Guid cancelledInvoiceId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = BuildConfirmedInvoice(ctx, affectedInvoiceNumber);
            db1.PurchaseInvoices.Add(first);
            await db1.SaveChangesAsync();
            cancelledInvoiceId = first.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == cancelledInvoiceId);
            first.Cancel("Anulada por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        // "Reprocesar la misma recepción" — nueva compra Confirmed con el mismo proveedor+número.
        Guid confirmedInvoiceId;
        await using (var db2 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var second = BuildConfirmedInvoice(ctx, affectedInvoiceNumber);
            db2.PurchaseInvoices.Add(second);
            await db2.SaveChangesAsync();
            confirmedInvoiceId = second.Id;
        }

        Guid receptionDocumentId;
        await using (var db3 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var doc = PurchaseReceptionDocument.Create(
                ctx.TenantId,
                ctx.CompanyId,
                ctx.BranchId,
                PurchaseReceptionSourceDocType.CreditNote,
                supplierRuc: "1710034065001",
                supplierName: "Proveedor Test",
                supplierId: ctx.SupplierId,
                accessKey: $"AK-{Guid.NewGuid():N}",
                invoiceNumber: creditNoteOwnNumber, // número PROPIO de la NC — nunca debe usarse para resolver
                issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
                authorizationDate: DateTime.UtcNow,
                subtotal: 10m,
                vatAmount: 1.5m,
                totalAmount: 11.5m,
                createdBy: _userId,
                modifiedDocumentNumber: affectedInvoiceNumber // numDocModificado real del XML
            );
            db3.PurchaseReceptionDocuments.Add(doc);
            await db3.SaveChangesAsync();
            receptionDocumentId = doc.Id;
        }

        var handlerDb = CreateContext(ctx.TenantId, ctx.CompanyId);
        var handler = new GetPurchaseReceptionXmlViewHandler(
            new PurchaseReceptionDocumentRepository(handlerDb, new FixedCurrentCompany(() => ctx.CompanyId)),
            new PurchaseInvoiceRepository(handlerDb, new FixedCurrentCompany(() => ctx.CompanyId)),
            new FixedCurrentTenant(() => ctx.TenantId)
        );

        var result = await handler.Handle(
            new GetPurchaseReceptionXmlViewQuery(receptionDocumentId),
            CancellationToken.None
        );
        await handlerDb.DisposeAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.AffectedPurchaseExists.Should().BeTrue();
        result.Value.AffectedPurchaseId.Should().Be(
            confirmedInvoiceId,
            because: "la NC debe apuntar a la compra Confirmed activa, nunca a la Cancelled histórica"
        );
        result.Value.AffectedPurchaseId.Should().NotBe(cancelledInvoiceId);
        result.Value.ModifiedDocumentNumber.Should().Be(affectedInvoiceNumber);
        result.Value.DocumentNumber.Should().Be(
            creditNoteOwnNumber,
            because: "DocumentNumber sigue siendo el número PROPIO de la NC — no se confunde con el afectado"
        );
    }

    [Fact]
    public async Task XmlView_de_una_NC_con_solo_compra_Cancelled_no_resuelve_ninguna_factura_afectada()
    {
        var ctx = await SeedTenantAsync();
        const string affectedInvoiceNumber = "001-001-000031761";

        Guid cancelledInvoiceId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var only = BuildConfirmedInvoice(ctx, affectedInvoiceNumber);
            db1.PurchaseInvoices.Add(only);
            await db1.SaveChangesAsync();
            cancelledInvoiceId = only.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var only = await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == cancelledInvoiceId);
            only.Cancel("Anulada por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        Guid receptionDocumentId;
        await using (var db2 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var doc = PurchaseReceptionDocument.Create(
                ctx.TenantId,
                ctx.CompanyId,
                ctx.BranchId,
                PurchaseReceptionSourceDocType.CreditNote,
                supplierRuc: "1710034065001",
                supplierName: "Proveedor Test",
                supplierId: ctx.SupplierId,
                accessKey: $"AK-{Guid.NewGuid():N}",
                invoiceNumber: "001-001-000010351",
                issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
                authorizationDate: DateTime.UtcNow,
                subtotal: 10m,
                vatAmount: 1.5m,
                totalAmount: 11.5m,
                createdBy: _userId,
                modifiedDocumentNumber: affectedInvoiceNumber
            );
            db2.PurchaseReceptionDocuments.Add(doc);
            await db2.SaveChangesAsync();
            receptionDocumentId = doc.Id;
        }

        var handlerDb = CreateContext(ctx.TenantId, ctx.CompanyId);
        var handler = new GetPurchaseReceptionXmlViewHandler(
            new PurchaseReceptionDocumentRepository(handlerDb, new FixedCurrentCompany(() => ctx.CompanyId)),
            new PurchaseInvoiceRepository(handlerDb, new FixedCurrentCompany(() => ctx.CompanyId)),
            new FixedCurrentTenant(() => ctx.TenantId)
        );

        var result = await handler.Handle(
            new GetPurchaseReceptionXmlViewQuery(receptionDocumentId),
            CancellationToken.None
        );
        await handlerDb.DisposeAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.AffectedPurchaseExists.Should().BeFalse();
        result.Value.AffectedPurchaseId.Should().BeNull();
    }

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
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
