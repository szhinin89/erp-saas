using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Purchases;

/// <summary>
/// PURCHASE-CREDIT-NOTE-DISCOUNT-SEQUENCE-NO-MATCH-01 — reproduce contra PostgreSQL real (no
/// mocks) el reporte "al guardar una NC por descuento con un resumen fiscal de IVA 0% falla con
/// 'Sequence contains no matching element'". Los tests con repositorios mockeados no reproducían
/// el error (el mock de <c>GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync</c> nunca ejerce la
/// query LINQ real de <c>PurchaseCreditNoteRepository</c>), así que esta clase usa un contenedor
/// PostgreSQL real para ejercer exactamente el mismo camino que producción.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseCreditNoteDiscountIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_credit_note_discount_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenantId: _tenantId,
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
            companyId: _companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branchId = branch.Id;

        var supplier = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Proveedor Test", _userId);
        db.BusinessPartners.Add(supplier);
        var paymentTerm = PaymentTerm.Create(_tenantId, "CONT", "Contado", installments: 1, daysBetweenInstallments: 0, _userId);
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    private async Task<Guid> SeedConfirmedInvoiceWithZeroPercentVatAsync(decimal lineTotal = 100m)
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto exento de IVA",
            quantity: 1m,
            unitPrice: lineTotal,
            vatCode: "0",
            uomCode: "UNIT"
        );
        line.ApplyTaxes("0", 0m, "IVA", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);

        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            inv.Id,
            "01",
            inv.InvoiceNumber,
            inv.IssueDate,
            inv.IssueDate,
            _userId
        );
        payable.AddInstallment(1, inv.IssueDate.AddDays(30), lineTotal);

        db.PurchaseInvoices.Add(inv);
        db.Add(payable);
        await db.SaveChangesAsync();
        return inv.Id;
    }

    [Fact]
    public async Task CreateDraft_NC_por_descuento_con_IVA_0_por_ciento_se_guarda_correctamente()
    {
        var invoiceId = await SeedConfirmedInvoiceWithZeroPercentVatAsync(100m);

        await using var db = CreateContext();
        var invoiceRepo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId));
        var invoice = await invoiceRepo.GetByIdAsync(_tenantId, invoiceId, CancellationToken.None);
        invoice.Should().NotBeNull();
        var summary = invoice!.TaxSummaries.Should().ContainSingle().Which;
        summary.VatRate.Should().Be(0m);

        var handler = new CreateDraftPurchaseCreditNoteHandler(
            new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId)),
            invoiceRepo,
            new AccountsPayableRepository(db),
            new ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseReceptionDocumentRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_userId),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId))
        );

        var result = await handler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(),
                invoiceId,
                null,
                PurchaseCreditNoteApplicationType.Discount,
                $"001-001-{Random.Shared.Next(100000, 999999)}",
                null,
                null,
                null,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Descuento por pronto pago",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(summary.Id, 3.00m) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.TaxSummaries.Should().ContainSingle();
        result.Value.TaxSummaries[0].TaxableBase.Should().Be(3.00m);
        result.Value.TotalAmount.Should().Be(3.00m);
        result.Value.LinkedPurchaseReturnId.Should().BeNull();

        // PURCHASE-CREDIT-NOTE-DISCOUNT-SEQUENCE-NO-MATCH-01 — la causa raíz real: el usuario ve
        // el error justo después de "guardar" porque el frontend navega de inmediato a
        // /purchases/credit-notes/{id}, que dispara este GET en una request nueva (DbContext
        // nuevo, la NC se relee de la base). GetByIdAsync cargaba TaxSummaries sin
        // ThenInclude(Taxes) — VatCode/VatRate (CreditNoteMap.ToDto) son _taxes.First(...), y con
        // Taxes vacío eso lanzaba "Sequence contains no matching element" (InvalidOperationException,
        // reportado como DOMAIN_RULE_VIOLATION por el middleware global).
        await using var freshDb = CreateContext();
        var getByIdHandler = new GetPurchaseCreditNoteByIdHandler(
            new PurchaseCreditNoteRepository(freshDb, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(freshDb, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(freshDb),
            new ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseReceptionDocumentRepository(
                freshDb,
                new FixedCurrentCompany(() => _companyId)
            ),
            new PurchaseReturnRepository(freshDb, new FixedCurrentCompany(() => _companyId)),
            new ERP.Infrastructure.Persistence.Repositories.Items.ItemRepository(freshDb),
            new ERP.Infrastructure.Persistence.Repositories.WarehouseRepository(
                freshDb,
                new FixedCurrentCompany(() => _companyId)
            ),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId)
        );

        var getResult = await getByIdHandler.Handle(
            new GetPurchaseCreditNoteByIdQuery(result.Value.Id),
            CancellationToken.None
        );

        getResult.IsSuccess.Should().BeTrue(getResult.Error);
        getResult.Value!.TaxSummaries.Should().ContainSingle();
        getResult.Value.TaxSummaries[0].VatRate.Should().Be(0m);
        getResult.Value.TaxSummaries[0].TaxableBase.Should().Be(3.00m);
    }

    [Fact]
    public async Task Authorize_NC_por_descuento_reduce_CxP_sin_crear_PurchaseReturn_ni_Kardex()
    {
        var invoiceId = await SeedConfirmedInvoiceWithZeroPercentVatAsync(100m);

        await using var db = CreateContext();
        var invoiceRepo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId));
        var invoice = await invoiceRepo.GetByIdAsync(_tenantId, invoiceId, CancellationToken.None);
        var summary = invoice!.TaxSummaries.Should().ContainSingle().Which;
        var creditNoteRepo = new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId));
        var payableRepo = new AccountsPayableRepository(db);
        var returnRepo = new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId));

        var payableBefore = await payableRepo.GetByOriginAsync(
            _tenantId, _companyId, AccountsPayableOriginType.PurchaseInvoice, invoiceId, CancellationToken.None);
        var outstandingBeforeAuthorize = payableBefore!.OutstandingAmount;
        outstandingBeforeAuthorize.Should().Be(100m, "seed: 1 cuota de 100, sin pagos registrados");

        var createHandler = new CreateDraftPurchaseCreditNoteHandler(
            creditNoteRepo,
            invoiceRepo,
            payableRepo,
            new ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseReceptionDocumentRepository(
                db, new FixedCurrentCompany(() => _companyId)),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_userId),
            returnRepo
        );
        var created = await createHandler.Handle(
            new CreateDraftPurchaseCreditNoteCommand(
                Guid.NewGuid(), invoiceId, null, PurchaseCreditNoteApplicationType.Discount,
                $"001-001-{Random.Shared.Next(100000, 999999)}", null, null, null,
                DateOnly.FromDateTime(DateTime.UtcNow), "Descuento por pronto pago",
                Array.Empty<PurchaseCreditNoteDraftLineInput>(),
                new[] { new PurchaseCreditNoteTaxSummaryLineInput(summary.Id, 3.00m) }),
            CancellationToken.None
        );
        created.IsSuccess.Should().BeTrue(created.Error);

        await using var authDb = CreateContext();
        var authorizeHandler = new AuthorizePurchaseCreditNoteHandler(
            new PurchaseCreditNoteRepository(authDb, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(authDb, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(authDb),
            new PurchaseReturnRepository(authDb, new FixedCurrentCompany(() => _companyId)),
            new ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseReceptionDocumentRepository(
                authDb, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(authDb),
            new RealDatabaseExceptionTranslator(),
            // No se llama (IrbpnrAmount = 0 para esta NC) — descuento sin IRBPNR nunca evalúa el
            // guard de PostingRule; cualquier implementación basta.
            Mock.Of<ERP.Application.Modules.Accounting.Posting.IPostingEngine>(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_userId)
        );

        var authorized = await authorizeHandler.Handle(
            new AuthorizePurchaseCreditNoteCommand(created.Value!.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        authorized.IsSuccess.Should().BeTrue(authorized.Error);
        authorized.Value!.Status.Should().Be("Authorized");
        authorized.Value.AppliedToPayableAmount.Should().Be(3.00m);

        // Reduce CxP — el saldo pendiente de la factura afectada baja exactamente el monto de la NC.
        // Se relee vía el repositorio (no un DbSet crudo): AccountsPayable.OutstandingAmount se
        // deriva sumando Installments — sin el Include que ya aplica el repositorio, esa colección
        // llega vacía y el cálculo daría 0 sin que eso sea un bug de negocio.
        await using var verifyDb = CreateContext();
        var payableAfter = await new AccountsPayableRepository(verifyDb).GetByOriginAsync(
            _tenantId, _companyId, AccountsPayableOriginType.PurchaseInvoice, invoiceId, CancellationToken.None);
        payableAfter!.OutstandingAmount.Should().Be(outstandingBeforeAuthorize - 3.00m);

        // No crea PurchaseReturn (nunca mueve inventario/Kardex): NC por descuento no genera
        // ninguna fila en purchase_returns para esta factura.
        var returnsForInvoice = await verifyDb.PurchaseReturns.AsNoTracking()
            .Where(r => r.PurchaseInvoiceId == invoiceId)
            .ToListAsync(CancellationToken.None);
        returnsForInvoice.Should().BeEmpty();
    }

    // ── Test doubles mínimos ─────────────────────────────────────────────

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

    private sealed class FixedCurrentBranch(Guid branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId;
        public bool IsAuthenticated => true;
        public bool HasBranchContext => true;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private sealed class RealDatabaseExceptionTranslator : IDatabaseExceptionTranslator
    {
        public bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    info = new DatabaseUniqueViolationInfo(
                        pg.SqlState,
                        pg.ConstraintName,
                        pg.TableName,
                        pg.MessageText
                    );
                    return true;
                }
            }
            info = null!;
            return false;
        }
    }
}
