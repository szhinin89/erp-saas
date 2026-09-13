using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
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
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) — regresión del bug de impuestos
/// duplicados en <c>sales_invoice_detail_taxes</c> (Lote 1, Fase 1). Causa raíz confirmada:
/// <see cref="SalesInvoiceRepository.GetByIdAsync"/> incluía <c>Lines</c> pero NO
/// <c>Lines.Taxes</c> — cuando <c>AuthorizeSalesInvoiceHandler</c> recargaba la factura y volvía a
/// llamar <see cref="SalesInvoiceDetail.ApplyTaxes"/> sobre una línea ya persistida (para
/// "recalcular impuestos con nombres actualizados"), EF Core no tenía trackeada la fila de impuesto
/// ya existente en BD: <c>UpsertTaxRow</c> hacía <c>_taxes.RemoveAll(...)</c> sobre una colección
/// vacía (nunca cargada) y luego <c>_taxes.Add(...)</c> con un Id nuevo — SaveChanges insertaba una
/// segunda fila sin borrar la primera. El fix agrega <c>.ThenInclude(l =&gt; l.Taxes)</c> a
/// <c>GetByIdAsync</c>, para que la colección quede trackeada y <c>RemoveAll</c> marque la fila vieja
/// para DELETE antes del INSERT de la nueva.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesInvoiceDetailTaxDuplicationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sales_tax_dup_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            _createdBy,
            companyId: company.Id
        );
        var customer = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Cliente Test",
            _createdBy
        );
        var establishment = Establishment.Create(
            tenant.Id,
            branchId: branch.Id,
            company.Id,
            code: "001",
            name: "Matriz Test",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            "CAJA-01",
            "Caja Principal",
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id,
            company.Id,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            tenant.Id,
            company.Id,
            branch.Id,
            _createdBy,
            cashRegister.Id,
            "CAJA-01",
            "Caja Principal",
            emissionPoint.Id,
            "001",
            0m,
            _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            // Mismo interceptor que la DI de producción (ERP.Infrastructure/DependencyInjection.cs):
            // sin él, un hijo NUEVO (aquí, la fila de SalesInvoiceDetailTax que ApplyTaxes agrega vía
            // UpsertTaxRow) añadido a un agregado YA TRACKEADO (SalesInvoice recargado por
            // GetByIdAsync) queda mal clasificado como Modified en vez de Added — con Guid como PK
            // (client-generated, no database-generated), EF no puede distinguir "nuevo" de
            // "existente" solo por el valor de la clave, y termina emitiendo un UPDATE que falla con
            // DbUpdateConcurrencyException contra el xmin del agregado raíz. No relacionado con el
            // bug de impuestos duplicados que este archivo prueba — es infraestructura de tracking
            // ya existente que el test necesita replicar para reflejar el comportamiento real de
            // AuthorizeSalesInvoiceHandler (que sí resuelve ErpDbContext con este interceptor).
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    /// <summary>Crea y persiste un Draft con una línea con IVA &gt; 0% (15%, tasa real — no el 0%
    /// que ocultaba el bug en dev) ya con impuestos aplicados una vez, igual que
    /// <c>SalesLineBuilder</c> lo hace al crear el borrador.</summary>
    private async Task<Guid> SeedDraftWithVatLineAsync(string invoiceNumber, decimal unitPrice = 100m)
    {
        await using var db = CreateContext();

        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            Guid.NewGuid(),
            "Contado",
            installments: 1,
            daysBetween: 0
        );

        var inv = SalesInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            customer,
            invoiceNumber: invoiceNumber,
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: _cashSessionId
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto con IVA real",
            quantity: 1,
            unitPrice: unitPrice,
            vatCode: "4",
            uomCode: "UNIT"
        );
        // Simula lo que SalesLineBuilder hace al crear el Draft: primera aplicación de impuestos.
        line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _createdBy);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            _tenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            inv.Lines.Single().TaxInclusiveTotal
        );
        inv.ReplacePayments(new[] { payment }, _createdBy);

        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        return inv.Id;
    }

    /// <summary>Cuenta filas de <c>sales_invoice_detail_taxes</c> agrupadas por
    /// (sales_invoice_detail_id, tax_code) para la línea dada — el SQL de verificación post-fix
    /// pedido en el ticket (máximo 1 fila por grupo).</summary>
    private async Task<int> CountVatRowsForLineAsync(Guid lineId)
    {
        await using var db = CreateContext();
        return await db.Database
            .SqlQuery<int>(
                $"SELECT COUNT(*)::int AS \"Value\" FROM sales_invoice_detail_taxes WHERE sales_invoice_detail_id = {lineId} AND tax_code = '2'"
            )
            .SingleAsync();
    }

    [Fact]
    public async Task Recargar_la_linea_y_reaplicar_ApplyTaxes_una_segunda_vez_no_duplica_la_fila_de_IVA()
    {
        // Reproduce exactamente el escenario del bug: Draft ya persistido con 1 fila de IVA (creada
        // arriba, simulando SalesLineBuilder), luego Authorize recarga la factura y vuelve a llamar
        // ApplyTaxes (AuthorizeSalesUseCases.cs "recalcular impuestos con nombres actualizados").
        var invoiceId = await SeedDraftWithVatLineAsync("TAX-DUP-001");

        await using var db1 = CreateContext();
        var repo1 = new SalesInvoiceRepository(db1, new FixedCurrentCompany(_companyId));
        var reloaded = await repo1.GetByIdAsync(_tenantId, invoiceId);
        reloaded.Should().NotBeNull();
        var lineId = reloaded!.Lines.Single().Id;

        // Precondición: tras el primer guardado, ya hay exactamente 1 fila de IVA.
        (await CountVatRowsForLineAsync(lineId)).Should().Be(1);

        // Segunda invocación de ApplyTaxes sobre la línea recargada — mismo patrón que
        // AuthorizeSalesInvoiceHandler.
        reloaded.Lines.Single().ApplyTaxes("4", 15m, "IVA 15% (recalculado)", null, 0m, null);
        await repo1.SaveChangesAsync();

        // Verificación a nivel de dominio (tras SaveChanges, en el mismo contexto).
        reloaded.Lines.Single().Taxes.Should().ContainSingle(t => t.TaxCode == "2");

        // Verificación a nivel de BD — SQL de verificación post-fix del ticket: máximo 1 fila por
        // grupo (sales_invoice_detail_id, tax_code).
        (await CountVatRowsForLineAsync(lineId)).Should().Be(1);

        // Recargar en un contexto completamente nuevo confirma la cardinalidad persistida.
        await using var db2 = CreateContext();
        var repo2 = new SalesInvoiceRepository(db2, new FixedCurrentCompany(_companyId));
        var reReloaded = await repo2.GetByIdAsync(_tenantId, invoiceId);
        reReloaded!.Lines.Single().Taxes.Should().ContainSingle(t => t.TaxCode == "2");
        reReloaded.Lines.Single().VatAmount.Should().Be(15m);
    }

    [Fact]
    public async Task Autorizar_via_SalesInvoice_Authorize_deja_exactamente_una_fila_de_IVA_por_linea()
    {
        // Cubre el flujo completo de dominio (Create draft → Authorize), no solo ApplyTaxes suelto.
        var invoiceId = await SeedDraftWithVatLineAsync("TAX-DUP-002");

        await using var db = CreateContext();
        var repo = new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        var inv = await repo.GetByIdAsync(_tenantId, invoiceId);
        inv.Should().NotBeNull();
        var lineId = inv!.Lines.Single().Id;

        // Mismo paso que AuthorizeSalesInvoiceHandler antes de Authorize(): re-aplica impuestos.
        inv.Lines.Single().ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        inv.Authorize(_createdBy, cashApplied: inv.Lines.Single().TaxInclusiveTotal);
        await repo.SaveChangesAsync();

        (await CountVatRowsForLineAsync(lineId)).Should().Be(1);
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
