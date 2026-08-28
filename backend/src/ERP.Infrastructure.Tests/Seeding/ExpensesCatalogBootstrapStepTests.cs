using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// EXPENSES-CATALOG-BOOTSTRAP-09-FIX: cubre <see cref="ExpensesCatalogBootstrapStep"/> (jerarquía,
/// mapeo de cuentas correcto/incorrecto, idempotencia) y <see cref="ExpensesCatalogBackfillService"/>
/// (creación y corrección para companies existentes). Usa InMemory (no Testcontainers): ambos solo
/// hacen LINQ/Add/SaveChanges estándar, sin SQL específico de Postgres — mismo criterio que
/// <see cref="AccountingBootstrapStepTests"/>.
/// </summary>
public sealed class ExpensesCatalogBootstrapStepTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private ErpDbContext NewDbContext(string dbName, Guid? companyId = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)
            )
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId ?? _companyId)
        );
    }

    private static async Task SeedFullRetailChartAsync(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid actorId
    )
    {
        var accountingStep = new AccountingBootstrapStep(db, NullLogger<AccountingBootstrapStep>.Instance);
        await accountingStep.ExecuteAsync(new CompanyBootstrapContext(tenantId, companyId, actorId));
    }

    [Fact]
    public async Task Primera_ejecucion_crea_jerarquia_type_category_subcategory()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
        var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var nodes = await db.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Should().Contain(n => n.Level == ExpenseCategoryNodeLevel.Type && n.Name == "Gastos administrativos");
        nodes.Should().Contain(n => n.Level == ExpenseCategoryNodeLevel.Category && n.Name == "Servicios basicos");
        nodes.Should().Contain(n => n.Level == ExpenseCategoryNodeLevel.Subcategory && n.Name == "Energia electrica");

        var subcategory = nodes.Single(n => n.Name == "Energia electrica");
        var category = nodes.Single(n => n.Id == subcategory.ParentId);
        category.Name.Should().Be("Servicios basicos");
        var type = nodes.Single(n => n.Id == category.ParentId);
        type.Name.Should().Be("Gastos administrativos");

        nodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Subcategory)
            .Should()
            .HaveCount(ExpensesCatalogBootstrapStep.TemplateItemCount);
    }

    [Fact]
    public async Task Solo_subcategory_tiene_accountingaccountid()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
        var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var nodes = await db.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Where(n => n.Level != ExpenseCategoryNodeLevel.Subcategory)
            .Should()
            .OnlyContain(n => n.AccountingAccountId == null);
        nodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Subcategory)
            .Should()
            .OnlyContain(n => n.AccountingAccountId != null);
    }

    [Fact]
    public async Task Segunda_corrida_no_duplica()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var nodes = await verifyDb.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Type).Should().HaveCount(5);
        nodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Category).Should().HaveCount(18);
        nodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Subcategory)
            .Should()
            .HaveCount(ExpensesCatalogBootstrapStep.TemplateItemCount);
    }

    [Theory]
    [InlineData("Energia electrica", "6.1.01.003")]
    [InlineData("Arriendo de oficina", "6.1.01.004")]
    [InlineData("Papeleria y utiles", "6.1.01.002")]
    [InlineData("Licencias de software", "6.1.01.007")]
    [InlineData("Servicios contables", "6.1.01.005")]
    [InlineData("Mantenimiento de oficina", "6.1.01.006")]
    [InlineData("Taxis y movilizacion local", "6.1.01.008")]
    public async Task Cada_subcategoria_de_gastos_administrativos_apunta_a_la_cuenta_correcta(
        string subcategoryName,
        string expectedAccountCode
    )
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
        var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);

        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var expectedAccount = await db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code.Value == expectedAccountCode
        );
        var subcategory = await db.ExpenseCategoryNodes.SingleAsync(n =>
            n.CompanyId == _companyId && n.Name == subcategoryName
        );
        subcategory.AccountingAccountId.Should().Be(expectedAccount.Id);
    }

    [Fact]
    public async Task Si_falta_el_accountcode_no_crea_esa_subcategoria_y_continua()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
            var missing = await db.Accounts.SingleAsync(a =>
                a.CompanyId == _companyId && a.Code.Value == "6.1.01.003"
            );
            db.Accounts.Remove(missing);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));
        }

        await using var verifyDb = NewDbContext(dbName);
        var nodes = await verifyDb.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Should().NotContain(n => n.Name == "Energia electrica");
        nodes.Should().NotContain(n => n.Name == "Agua potable");
        nodes.Should().Contain(n => n.Name == "Arriendo de oficina");
        nodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Subcategory)
            .Should()
            .HaveCount(ExpensesCatalogBootstrapStep.TemplateItemCount - 6);
    }

    [Fact]
    public async Task Si_cuenta_existe_pero_no_es_expense_no_la_usa()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        db.Accounts.Add(
            ERP.Domain.Modules.Accounting.Entities.Account.Create(
                _tenantId,
                _companyId,
                ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("6.1.01.002"),
                "Suministros de oficina (tipo incorrecto)",
                parentAccountId: null,
                accountType: AccountType.Asset,
                nature: AccountNature.Debit,
                allowsPosting: true,
                createdBy: _actorId
            )
        );
        await db.SaveChangesAsync();

        var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var nodes = await db.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Should().NotContain(n => n.Name == "Papeleria y utiles");
    }

    [Fact]
    public async Task Si_cuenta_no_permite_asiento_no_la_usa()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        db.Accounts.Add(
            ERP.Domain.Modules.Accounting.Entities.Account.Create(
                _tenantId,
                _companyId,
                ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("6.1.01.002"),
                "Suministros de oficina (no postable)",
                parentAccountId: null,
                accountType: AccountType.Expense,
                nature: AccountNature.Debit,
                allowsPosting: false,
                createdBy: _actorId
            )
        );
        await db.SaveChangesAsync();

        var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var nodes = await db.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Should().NotContain(n => n.Name == "Papeleria y utiles");
    }

    [Fact]
    public async Task Si_cuenta_esta_inactiva_no_la_usa()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var account = ERP.Domain.Modules.Accounting.Entities.Account.Create(
            _tenantId,
            _companyId,
            ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("6.1.01.002"),
            "Suministros de oficina (inactiva)",
            parentAccountId: null,
            accountType: AccountType.Expense,
            nature: AccountNature.Debit,
            allowsPosting: true,
            createdBy: _actorId
        );
        account.Disable(_actorId);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var nodes = await db.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Should().NotContain(n => n.Name == "Papeleria y utiles");
    }

    [Fact]
    public async Task No_usa_cuenta_de_otra_company()
    {
        var dbName = Guid.NewGuid().ToString();
        var otherCompanyId = Guid.NewGuid();
        await using var db = NewDbContext(dbName);
        db.Accounts.Add(
            ERP.Domain.Modules.Accounting.Entities.Account.Create(
                _tenantId,
                otherCompanyId,
                ERP.Domain.Modules.Accounting.ValueObjects.AccountCode.Create("6.1.01.002"),
                "Suministros de oficina (otra empresa)",
                parentAccountId: null,
                accountType: AccountType.Expense,
                nature: AccountNature.Debit,
                allowsPosting: true,
                createdBy: _actorId
            )
        );
        await db.SaveChangesAsync();

        var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
        await step.ExecuteAsync(new CompanyBootstrapContext(_tenantId, _companyId, _actorId));

        var nodes = await db.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Should().NotContain(n => n.Name == "Papeleria y utiles");
        var otherCompanyNodes = await db.ExpenseCategoryNodes
            .Where(n => n.CompanyId == otherCompanyId)
            .ToListAsync();
        otherCompanyNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task Backfill_crea_catalogo_en_company_activa_existente()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
            var company = ERP.Domain.Modules.Company.Entities.Company.CreateFromTenant(
                _tenantId,
                "1710034065001",
                "Empresa Backfill Test"
            );
            typeof(ERP.Domain.Modules.Company.Entities.Company)
                .GetProperty(nameof(ERP.Domain.Modules.Company.Entities.Company.Id))!
                .SetValue(company, _companyId);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            var backfill = new ExpensesCatalogBackfillService(
                db,
                new FakeHostEnvironment(isProduction: false),
                step,
                NullLogger<ExpensesCatalogBackfillService>.Instance
            );
            await backfill.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var nodes = await verifyDb.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        nodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Subcategory)
            .Should()
            .HaveCount(ExpensesCatalogBootstrapStep.TemplateItemCount);
    }

    [Fact]
    public async Task Backfill_no_hace_nada_en_production()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
            var company = ERP.Domain.Modules.Company.Entities.Company.CreateFromTenant(
                _tenantId,
                "1710034065001",
                "Empresa Backfill Test"
            );
            typeof(ERP.Domain.Modules.Company.Entities.Company)
                .GetProperty(nameof(ERP.Domain.Modules.Company.Entities.Company.Id))!
                .SetValue(company, _companyId);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            var backfill = new ExpensesCatalogBackfillService(
                db,
                new FakeHostEnvironment(isProduction: true),
                step,
                NullLogger<ExpensesCatalogBackfillService>.Instance
            );
            await backfill.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        (await verifyDb.ExpenseCategoryNodes.CountAsync(n => n.CompanyId == _companyId)).Should().Be(0);
    }

    [Fact]
    public async Task Backfill_corrige_mapeo_base_incorrecto_sin_borrar_ni_desactivar()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid wrongAccountId;
        Guid correctAccountId;
        Guid subcategoryId;

        await using (var db = NewDbContext(dbName))
        {
            await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
            var company = ERP.Domain.Modules.Company.Entities.Company.CreateFromTenant(
                _tenantId,
                "1710034065001",
                "Empresa Backfill Test"
            );
            typeof(ERP.Domain.Modules.Company.Entities.Company)
                .GetProperty(nameof(ERP.Domain.Modules.Company.Entities.Company.Id))!
                .SetValue(company, _companyId);
            db.Companies.Add(company);

            // Simula el bug corregido por EXPENSES-CATALOG-BOOTSTRAP-09-FIX: sembrar manualmente el
            // catálogo con el mapeo ANTERIOR (incorrecto) — "Servicios basicos" apuntando a
            // 6.1.01.002 en lugar de 6.1.01.003.
            var type = ERP.Domain.Modules.Expenses.Entities.ExpenseCategoryNode.CreateType(
                _tenantId,
                _companyId,
                "GT-001",
                "Gastos administrativos",
                _actorId
            );
            db.ExpenseCategoryNodes.Add(type);
            var category = ERP.Domain.Modules.Expenses.Entities.ExpenseCategoryNode.CreateCategory(
                _tenantId,
                _companyId,
                type,
                "GC-001",
                "Servicios basicos",
                _actorId
            );
            db.ExpenseCategoryNodes.Add(category);

            wrongAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "6.1.01.002")
            ).Id;
            correctAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "6.1.01.003")
            ).Id;

            var subcategory = ERP.Domain.Modules.Expenses.Entities.ExpenseCategoryNode.CreateSubcategory(
                _tenantId,
                _companyId,
                category,
                "GS-001",
                "Energia electrica",
                wrongAccountId,
                _actorId,
                "Servicio basico deducible con comprobante autorizado",
                isDeductible: true,
                requiresInvoice: true
            );
            db.ExpenseCategoryNodes.Add(subcategory);
            subcategoryId = subcategory.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            var backfill = new ExpensesCatalogBackfillService(
                db,
                new FakeHostEnvironment(isProduction: false),
                step,
                NullLogger<ExpensesCatalogBackfillService>.Instance
            );
            await backfill.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var corrected = await verifyDb.ExpenseCategoryNodes.SingleAsync(n => n.Id == subcategoryId);
        corrected.AccountingAccountId.Should().Be(correctAccountId);
        corrected.AccountingAccountId.Should().NotBe(wrongAccountId);
        corrected.IsActive.Should().BeTrue();

        // El resto del catálogo (58 subcategorías restantes) debe haberse creado por el paso de
        // creación del backfill, sin duplicar la que ya existía corregida.
        var allNodes = await verifyDb.ExpenseCategoryNodes.Where(n => n.CompanyId == _companyId).ToListAsync();
        allNodes.Where(n => n.Level == ExpenseCategoryNodeLevel.Subcategory)
            .Should()
            .HaveCount(ExpensesCatalogBootstrapStep.TemplateItemCount);
        allNodes.Should().OnlyContain(n => n.IsActive);
    }

    [Fact]
    public async Task Backfill_no_toca_subcategorias_personalizadas_fuera_del_template()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid customNodeId;
        Guid customAccountId;

        await using (var db = NewDbContext(dbName))
        {
            await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
            var company = ERP.Domain.Modules.Company.Entities.Company.CreateFromTenant(
                _tenantId,
                "1710034065001",
                "Empresa Backfill Test"
            );
            typeof(ERP.Domain.Modules.Company.Entities.Company)
                .GetProperty(nameof(ERP.Domain.Modules.Company.Entities.Company.Id))!
                .SetValue(company, _companyId);
            db.Companies.Add(company);

            var customType = ERP.Domain.Modules.Expenses.Entities.ExpenseCategoryNode.CreateType(
                _tenantId,
                _companyId,
                "GT-CUSTOM",
                "Gastos personalizados del cliente",
                _actorId
            );
            db.ExpenseCategoryNodes.Add(customType);
            var customCategory = ERP.Domain.Modules.Expenses.Entities.ExpenseCategoryNode.CreateCategory(
                _tenantId,
                _companyId,
                customType,
                "GC-CUSTOM",
                "Categoria personalizada",
                _actorId
            );
            db.ExpenseCategoryNodes.Add(customCategory);

            customAccountId = (
                await db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code.Value == "6.1.01.002")
            ).Id;
            var customSubcategory = ERP.Domain.Modules.Expenses.Entities.ExpenseCategoryNode.CreateSubcategory(
                _tenantId,
                _companyId,
                customCategory,
                "GS-CUSTOM",
                "Subcategoria personalizada del cliente",
                customAccountId,
                _actorId,
                "Nodo de negocio propio del cliente, fuera del Template",
                isDeductible: true,
                requiresInvoice: true
            );
            db.ExpenseCategoryNodes.Add(customSubcategory);
            customNodeId = customSubcategory.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            var backfill = new ExpensesCatalogBackfillService(
                db,
                new FakeHostEnvironment(isProduction: false),
                step,
                NullLogger<ExpensesCatalogBackfillService>.Instance
            );
            await backfill.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        var customNode = await verifyDb.ExpenseCategoryNodes.SingleAsync(n => n.Id == customNodeId);
        customNode.Name.Should().Be("Subcategoria personalizada del cliente");
        customNode.AccountingAccountId.Should().Be(customAccountId);
        customNode.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Backfill_no_toca_otros_modulos_del_erp()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var db = NewDbContext(dbName))
        {
            await SeedFullRetailChartAsync(db, _tenantId, _companyId, _actorId);
            var company = ERP.Domain.Modules.Company.Entities.Company.CreateFromTenant(
                _tenantId,
                "1710034065001",
                "Empresa Backfill Test"
            );
            typeof(ERP.Domain.Modules.Company.Entities.Company)
                .GetProperty(nameof(ERP.Domain.Modules.Company.Entities.Company.Id))!
                .SetValue(company, _companyId);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var accountsBefore = 0;
        await using (var db = NewDbContext(dbName))
        {
            accountsBefore = await db.Accounts.CountAsync(a => a.CompanyId == _companyId);
        }

        await using (var db = NewDbContext(dbName))
        {
            var step = new ExpensesCatalogBootstrapStep(db, NullLogger<ExpensesCatalogBootstrapStep>.Instance);
            var backfill = new ExpensesCatalogBackfillService(
                db,
                new FakeHostEnvironment(isProduction: false),
                step,
                NullLogger<ExpensesCatalogBackfillService>.Instance
            );
            await backfill.EnsureAsync();
        }

        await using var verifyDb = NewDbContext(dbName);
        // El backfill de gastos nunca crea/modifica cuentas contables (solo las lee) ni entidades
        // de Compras/Inventario/Kardex/POS/Pagos a proveedores/CxP/Payment/Collections.
        (await verifyDb.Accounts.CountAsync(a => a.CompanyId == _companyId)).Should().Be(accountsBefore);
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

    private sealed class FakeHostEnvironment(bool isProduction) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction
            ? Environments.Production
            : Environments.Development;
        public string ApplicationName { get; set; } = "ERP.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
    }
}
