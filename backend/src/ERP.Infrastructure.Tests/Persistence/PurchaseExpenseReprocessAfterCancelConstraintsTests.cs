using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — mismo estándar cerrado para
/// <c>PurchaseCreditNote</c> (<see cref="PurchaseCreditNoteConstraintsTests"/>) aplicado ahora a
/// <see cref="PurchaseInvoice"/> y <see cref="ExpenseDocument"/> contra PostgreSQL real
/// (Testcontainers, sin mocks): un documento Cancelled es historial, nunca cuenta como duplicado ni
/// bloquea reprocesar la misma recepción/AccessKey/número+proveedor; dos documentos activos
/// (Draft/Confirmed) para la misma clave siguen colisionando. Cubre también el trigger cruzado
/// <c>enforce_purchase_expense_exclusivity</c> (AccessKey compartido entre Purchases y Expenses).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseExpenseReprocessAfterCancelConstraintsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_expense_reprocess_constraints_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();

    private sealed record TenantContext(
        Guid TenantId,
        Guid CompanyId,
        Guid BranchId,
        Guid SupplierId,
        Guid PaymentTermId,
        Guid WarehouseId,
        Guid ItemId,
        Guid ExpenseAccountId,
        Guid ExpenseSubcategoryId
    );

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
        var warehouse = Warehouse.Create(
            tenant.Id,
            branch.Id,
            "Bodega Principal",
            "BOD-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            company.Id,
            isMain: true
        );
        db.BusinessPartners.Add(supplier);
        db.Add(paymentTerm);
        db.Add(warehouse);
        await db.SaveChangesAsync();

        var itemType = ItemTypeDefinition.Create(tenant.Id, "MERCH", "Mercadería", 1, _userId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            tenant.Id,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "10", purchaseVatCode: "10"),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _userId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();

        var expenseAccount = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create($"5.1.{Guid.NewGuid():N}"[..8]),
            "Gasto Operativo Test",
            null,
            AccountType.Expense,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _userId
        );
        db.Accounts.Add(expenseAccount);
        await db.SaveChangesAsync();

        var type = ExpenseCategoryNode.CreateType(tenant.Id, company.Id, "TIPO-TEST", "Tipo Gasto Test", _userId);
        db.ExpenseCategoryNodes.Add(type);
        await db.SaveChangesAsync();
        var category = ExpenseCategoryNode.CreateCategory(tenant.Id, company.Id, type, "CAT-TEST", "Categoria Test", _userId);
        db.ExpenseCategoryNodes.Add(category);
        await db.SaveChangesAsync();
        var subcategory = ExpenseCategoryNode.CreateSubcategory(
            tenant.Id, company.Id, category, "SUB-TEST", "Subcategoria Test", expenseAccount.Id, _userId
        );
        db.ExpenseCategoryNodes.Add(subcategory);
        await db.SaveChangesAsync();

        return new TenantContext(
            tenant.Id,
            company.Id,
            branch.Id,
            supplier.Id,
            paymentTerm.Id,
            warehouse.Id,
            item.Id,
            expenseAccount.Id,
            subcategory.Id
        );
    }

    private PurchaseInvoice BuildConfirmedInvoice(
        TenantContext ctx,
        string invoiceNumber,
        string? accessKey = null
    )
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
            30,
            accessKey: accessKey,
            globalWarehouseId: ctx.WarehouseId
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            ctx.TenantId,
            "Producto 1",
            quantity: 10,
            unitPrice: 10.00m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: ctx.ItemId,
            warehouseId: ctx.WarehouseId
        );
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);
        return inv;
    }

    private async Task<Guid> CreateReceptionDocumentAsync(TenantContext ctx, string accessKey)
    {
        await using var db = CreateContext(ctx.TenantId, ctx.CompanyId);
        var doc = PurchaseReceptionDocument.Create(
            ctx.TenantId,
            ctx.CompanyId,
            ctx.BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            supplierRuc: "1710034065001",
            supplierName: "Proveedor Test",
            supplierId: ctx.SupplierId,
            accessKey: accessKey,
            invoiceNumber: "001-001-000000099",
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            authorizationDate: DateTime.UtcNow,
            subtotal: 100m,
            vatAmount: 15m,
            totalAmount: 115m,
            createdBy: _userId
        );
        db.PurchaseReceptionDocuments.Add(doc);
        await db.SaveChangesAsync();
        return doc.Id;
    }

    private ExpenseDocument BuildConfirmedExpense(
        TenantContext ctx,
        string documentNumber,
        string? accessKey = null,
        Guid? receptionDocumentId = null
    )
    {
        var doc = ExpenseDocument.CreateDraft(
            ctx.TenantId,
            ctx.CompanyId,
            ctx.BranchId,
            ctx.SupplierId,
            "Proveedor Test",
            "1234567890001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow),
            "01",
            documentNumber,
            ctx.PaymentTermId,
            "Contado",
            1,
            30,
            _userId,
            receptionDocumentId: receptionDocumentId,
            accessKey: accessKey
        );
        var line = ExpenseLine.Create(
            doc.Id,
            ctx.TenantId,
            ctx.ExpenseSubcategoryId,
            ctx.ExpenseAccountId,
            "Linea de gasto",
            1m,
            100m,
            "10",
            10m
        );
        doc.ReplaceLines(new[] { line }, _userId);
        doc.Confirm(
            new Dictionary<Guid, (Guid AccountId, string? Code, string? Name)>
            {
                [line.Id] = (ctx.ExpenseAccountId, null, null),
            },
            _userId
        );
        return doc;
    }

    // ── PurchaseInvoice.AccessKey ──────────────────────────────────────────

    [Fact]
    public async Task PurchaseInvoice_AccessKey_con_la_primera_compra_Cancelled_no_colisiona()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        Guid firstId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = BuildConfirmedInvoice(ctx, "001-001-000000101", accessKey);
            db1.PurchaseInvoices.Add(first);
            await db1.SaveChangesAsync();
            firstId = first.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == firstId);
            first.Cancel("Anulada por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        var second = BuildConfirmedInvoice(ctx, "001-001-000000102", accessKey);
        db2.PurchaseInvoices.Add(second);
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        var repo = new PurchaseInvoiceRepository(
            CreateContext(ctx.TenantId, ctx.CompanyId),
            new FixedCurrentCompany(() => ctx.CompanyId)
        );
        var active = await repo.GetByAccessKeyAsync(ctx.TenantId, accessKey);
        active.Should().NotBeNull();
        active!.Id.Should().Be(second.Id);
        (await repo.GetLatestCancelledIdByAccessKeyAsync(ctx.TenantId, accessKey))
            .Should()
            .Be(firstId, "el historial se conserva para \"Ver compra anulada\"");
    }

    [Fact]
    public async Task PurchaseInvoice_AccessKey_con_dos_compras_activas_sigue_lanzando_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000103", accessKey));
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000104", accessKey));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── PurchaseInvoice.CompanyId+SupplierId+InvoiceNumber ─────────────────

    [Fact]
    public async Task PurchaseInvoice_SupplierYNumero_con_la_primera_compra_Cancelled_no_colisiona()
    {
        var ctx = await SeedTenantAsync();
        const string invoiceNumber = "001-001-000000201";

        Guid firstId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = BuildConfirmedInvoice(ctx, invoiceNumber);
            db1.PurchaseInvoices.Add(first);
            await db1.SaveChangesAsync();
            firstId = first.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == firstId);
            first.Cancel("Anulada por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, invoiceNumber));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        await using var verify = CreateContext(ctx.TenantId, ctx.CompanyId);
        var invoicesForNumber = await verify
            .PurchaseInvoices.Where(x =>
                x.SupplierId == ctx.SupplierId && x.InvoiceNumber == invoiceNumber
            )
            .ToListAsync();
        invoicesForNumber.Should().HaveCount(2, "el historial se mantiene — la compra anulada nunca se borra");
        invoicesForNumber.Should().ContainSingle(x => x.Status == PurchaseStatus.Cancelled);
        invoicesForNumber.Should().ContainSingle(x => x.Status == PurchaseStatus.Confirmed);
    }

    [Fact]
    public async Task PurchaseInvoice_SupplierYNumero_con_dos_compras_activas_sigue_lanzando_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        const string invoiceNumber = "001-001-000000202";

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, invoiceNumber));
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, invoiceNumber));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── ExpenseDocument.AccessKey ──────────────────────────────────────────

    [Fact]
    public async Task ExpenseDocument_AccessKey_con_el_primer_gasto_Cancelled_no_colisiona()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        Guid firstId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = BuildConfirmedExpense(ctx, "001-001-000000301", accessKey);
            db1.ExpenseDocuments.Add(first);
            await db1.SaveChangesAsync();
            firstId = first.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = await dbCancel.ExpenseDocuments.SingleAsync(x => x.Id == firstId);
            first.Cancel("Anulado por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        var second = BuildConfirmedExpense(ctx, "001-001-000000302", accessKey);
        db2.ExpenseDocuments.Add(second);
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        var repo = new ExpenseDocumentRepository(
            CreateContext(ctx.TenantId, ctx.CompanyId),
            new FixedCurrentCompany(() => ctx.CompanyId)
        );
        (await repo.ExistsByAccessKeyAsync(ctx.TenantId, accessKey)).Should().BeTrue();
        (await repo.GetLatestCancelledIdByAccessKeyAsync(ctx.TenantId, accessKey))
            .Should()
            .Be(firstId, "el historial se conserva para \"Ver gasto anulado\"");
    }

    [Fact]
    public async Task ExpenseDocument_AccessKey_con_dos_gastos_activos_sigue_lanzando_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000303", accessKey));
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000304", accessKey));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── ExpenseDocument.ReceptionDocumentId ─────────────────────────────────

    [Fact]
    public async Task ExpenseDocument_ReceptionDocumentId_con_el_primer_gasto_Cancelled_no_colisiona()
    {
        var ctx = await SeedTenantAsync();
        var receptionDocumentId = await CreateReceptionDocumentAsync(ctx, $"AK-{Guid.NewGuid():N}");

        Guid firstId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = BuildConfirmedExpense(
                ctx,
                "001-001-000000401",
                receptionDocumentId: receptionDocumentId
            );
            db1.ExpenseDocuments.Add(first);
            await db1.SaveChangesAsync();
            firstId = first.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = await dbCancel.ExpenseDocuments.SingleAsync(x => x.Id == firstId);
            first.Cancel("Anulado por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.ExpenseDocuments.Add(
            BuildConfirmedExpense(
                ctx,
                "001-001-000000402",
                receptionDocumentId: receptionDocumentId
            )
        );
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        var repo = new ExpenseDocumentRepository(
            CreateContext(ctx.TenantId, ctx.CompanyId),
            new FixedCurrentCompany(() => ctx.CompanyId)
        );
        (await repo.ExistsByReceptionDocumentIdAsync(ctx.TenantId, receptionDocumentId))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ExpenseDocument_ReceptionDocumentId_con_dos_gastos_activos_sigue_lanzando_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        var receptionDocumentId = await CreateReceptionDocumentAsync(ctx, $"AK-{Guid.NewGuid():N}");

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.ExpenseDocuments.Add(
                BuildConfirmedExpense(
                    ctx,
                    "001-001-000000403",
                    receptionDocumentId: receptionDocumentId
                )
            );
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.ExpenseDocuments.Add(
            BuildConfirmedExpense(
                ctx,
                "001-001-000000404",
                receptionDocumentId: receptionDocumentId
            )
        );
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── ExpenseDocument.CompanyId+SupplierId+DocumentType+DocumentNumber ───

    [Fact]
    public async Task ExpenseDocument_SupplierTipoYNumero_con_el_primer_gasto_Cancelled_no_colisiona()
    {
        var ctx = await SeedTenantAsync();
        const string documentNumber = "001-001-000000501";

        Guid firstId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = BuildConfirmedExpense(ctx, documentNumber);
            db1.ExpenseDocuments.Add(first);
            await db1.SaveChangesAsync();
            firstId = first.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = await dbCancel.ExpenseDocuments.SingleAsync(x => x.Id == firstId);
            first.Cancel("Anulado por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, documentNumber));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        var repo = new ExpenseDocumentRepository(
            CreateContext(ctx.TenantId, ctx.CompanyId),
            new FixedCurrentCompany(() => ctx.CompanyId)
        );
        var active = await repo.GetBySupplierAndDocumentNumberAsync(
            ctx.TenantId,
            ctx.SupplierId,
            "01",
            documentNumber
        );
        active.Should().NotBeNull();
    }

    [Fact]
    public async Task ExpenseDocument_SupplierTipoYNumero_con_dos_gastos_activos_sigue_lanzando_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        const string documentNumber = "001-001-000000502";

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, documentNumber));
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, documentNumber));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── Trigger cruzado enforce_purchase_expense_exclusivity ───────────────

    [Fact]
    public async Task Trigger_cruzado_ignora_una_compra_Cancelled_al_crear_un_gasto_con_el_mismo_AccessKey()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        Guid invoiceId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var invoice = BuildConfirmedInvoice(ctx, "001-001-000000601", accessKey);
            db1.PurchaseInvoices.Add(invoice);
            await db1.SaveChangesAsync();
            invoiceId = invoice.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var invoice = await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == invoiceId);
            invoice.Cancel("Anulada por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000602", accessKey));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "una compra Cancelled con el mismo AccessKey ya no debe bloquear crear el gasto"
        );
    }

    [Fact]
    public async Task Trigger_cruzado_bloquea_un_gasto_si_ya_existe_una_compra_activa_con_el_mismo_AccessKey()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000603", accessKey));
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000604", accessKey));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Trigger_cruzado_ignora_un_gasto_Cancelled_al_crear_una_compra_con_el_mismo_AccessKey()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        Guid expenseId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var expense = BuildConfirmedExpense(ctx, "001-001-000000605", accessKey);
            db1.ExpenseDocuments.Add(expense);
            await db1.SaveChangesAsync();
            expenseId = expense.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var expense = await dbCancel.ExpenseDocuments.SingleAsync(x => x.Id == expenseId);
            expense.Cancel("Anulado por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000606", accessKey));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "un gasto Cancelled con el mismo AccessKey ya no debe bloquear crear la compra"
        );
    }

    [Fact]
    public async Task Trigger_cruzado_bloquea_una_compra_si_ya_existe_un_gasto_activo_con_el_mismo_AccessKey()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000607", accessKey));
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000608", accessKey));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── ZH-PURCHASE-EXPENSE-EXCLUSIVITY-RESTORE-01 ─────────────────────────
    // El trigger es raw SQL de InitialEnterpriseBaseline (no está en el modelo EF). Estos casos
    // exigen que el bloqueo venga de ESE trigger (constraint uq_purchase_expense_access_key), no de
    // otro índice.

    private static async Task<PostgresException> SaveExpectingExclusivityViolation(ErpDbContext db, string message)
    {
        var ex = await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        return AssertExclusivityViolation(ex.Which.InnerException, message);
    }

    private static PostgresException AssertExclusivityViolation(Exception? exception, string message)
    {
        var pg = exception.Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        pg.ConstraintName.Should().Be("uq_purchase_expense_access_key");
        pg.MessageText.Should().Be(message);
        return pg;
    }

    [Fact]
    public async Task Exclusividad_bloquea_ambos_sentidos_con_el_constraint_que_traducen_los_handlers()
    {
        var ctx = await SeedTenantAsync();
        var purchaseKey = $"AK-{Guid.NewGuid():N}";
        var expenseKey = $"AK-{Guid.NewGuid():N}";

        await using (var db = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000701", purchaseKey));
            db.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000702", expenseKey));
            await db.SaveChangesAsync();
        }

        await using (var dbExpense = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            dbExpense.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000703", purchaseKey));
            await SaveExpectingExclusivityViolation(dbExpense, "La factura ya fue registrada como compra.");
        }

        await using var dbPurchase = CreateContext(ctx.TenantId, ctx.CompanyId);
        dbPurchase.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000704", expenseKey));
        await SaveExpectingExclusivityViolation(dbPurchase, "La factura ya fue registrada como gasto.");
    }

    [Fact]
    public async Task Reproceso_tras_anular_cambia_de_compra_a_gasto_y_el_gasto_activo_vuelve_a_bloquear_compras()
    {
        var ctx = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        Guid invoiceId;
        await using (var db = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var invoice = BuildConfirmedInvoice(ctx, "001-001-000000711", accessKey);
            db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            (await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == invoiceId)).Cancel("Era un gasto", _userId);
            await dbCancel.SaveChangesAsync();
        }
        await using (var dbExpense = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            dbExpense.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000712", accessKey));
            await FluentActions.Invoking(() => dbExpense.SaveChangesAsync()).Should().NotThrowAsync();
        }

        await using var dbRepurchase = CreateContext(ctx.TenantId, ctx.CompanyId);
        dbRepurchase.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000713", accessKey));
        await SaveExpectingExclusivityViolation(dbRepurchase, "La factura ya fue registrada como gasto.");
    }

    [Fact]
    public async Task Mismo_AccessKey_en_tenants_distintos_no_interfiere()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();
        var accessKey = $"AK-{Guid.NewGuid():N}";

        await using (var dbA = CreateContext(tenantA.TenantId, tenantA.CompanyId))
        {
            dbA.PurchaseInvoices.Add(BuildConfirmedInvoice(tenantA, "001-001-000000721", accessKey));
            await dbA.SaveChangesAsync();
        }

        await using var dbB = CreateContext(tenantB.TenantId, tenantB.CompanyId);
        dbB.ExpenseDocuments.Add(BuildConfirmedExpense(tenantB, "001-001-000000722", accessKey));
        await FluentActions.Invoking(() => dbB.SaveChangesAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Documentos_sin_AccessKey_no_participan_de_la_exclusividad()
    {
        var ctx = await SeedTenantAsync();

        await using var db = CreateContext(ctx.TenantId, ctx.CompanyId);
        db.PurchaseInvoices.Add(BuildConfirmedInvoice(ctx, "001-001-000000731", accessKey: null));
        db.ExpenseDocuments.Add(BuildConfirmedExpense(ctx, "001-001-000000732", accessKey: null));

        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Update_hacia_un_AccessKey_conflictivo_se_bloquea_en_ambas_tablas()
    {
        var ctx = await SeedTenantAsync();
        var purchaseKey = $"AK-{Guid.NewGuid():N}";
        var expenseKey = $"AK-{Guid.NewGuid():N}";
        var purchase = BuildConfirmedInvoice(ctx, "001-001-000000741", purchaseKey);
        var expense = BuildConfirmedExpense(ctx, "001-001-000000742", expenseKey);

        await using (var db = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db.PurchaseInvoices.Add(purchase);
            db.ExpenseDocuments.Add(expense);
            await db.SaveChangesAsync();
        }

        // UPDATE directo: la garantía vive en BD (trigger BEFORE UPDATE OF tenant_id, access_key),
        // independiente de qué flujo de la app cambie la clave.
        await using var dbUpdate = CreateContext(ctx.TenantId, ctx.CompanyId);
        var toExpenseKey = await FluentActions
            .Invoking(() => dbUpdate.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE purchase_invoices SET access_key = {expenseKey} WHERE id = {purchase.Id}"))
            .Should()
            .ThrowAsync<PostgresException>();
        AssertExclusivityViolation(toExpenseKey.Which, "La factura ya fue registrada como gasto.");

        var toPurchaseKey = await FluentActions
            .Invoking(() => dbUpdate.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE expense_documents SET access_key = {purchaseKey} WHERE id = {expense.Id}"))
            .Should()
            .ThrowAsync<PostgresException>();
        AssertExclusivityViolation(toPurchaseKey.Which, "La factura ya fue registrada como compra.");

        (await dbUpdate.PurchaseInvoices.AsNoTracking().SingleAsync(x => x.Id == purchase.Id)).AccessKey.Should().Be(purchaseKey);
        (await dbUpdate.ExpenseDocuments.AsNoTracking().SingleAsync(x => x.Id == expense.Id)).AccessKey.Should().Be(expenseKey);
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
}
