using System.Text.Json;
using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.Brands;
using ERP.Application.Items.UseCases.CategoryNodes;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Catalogs;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using ERP.Infrastructure.Persistence.Repositories.Items;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.InitialLoad;

public sealed class InitialLoadPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("il1b_atomic")
        .WithUsername("erp").WithPassword("erp_test_secret").Build();
    public string ConnectionString => _postgres.GetConnectionString();
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = new ErpDbContext(new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(ConnectionString).Options, Mock.Of<ICurrentTenant>(), Mock.Of<IPublisher>(), Mock.Of<ICurrentCompany>());
        await db.Database.MigrateAsync();
        if (!await db.SriUoms.AnyAsync(x => x.Code == "19"))
            db.SriUoms.Add(new SriUom { Code = "19", Name = "Unidad", Abbrev = "UN" });
        if (!await db.Set<BarcodeTypeDefinition>().AnyAsync(x => x.Code == "Internal"))
            db.Set<BarcodeTypeDefinition>().Add(new BarcodeTypeDefinition { Code = "Internal", Name = "Interno" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}

/// <summary>PostgreSQL 16 + migraciones completas. Comandos, validadores, repositorios y UoW reales.
/// Solo contexto, publisher ajeno a esta prueba y lectores no usados son fakes; fallos de storage se inyectan explícitamente.</summary>
[Trait("Category", "PostgreSql")]
public sealed partial class ConfirmItemsAtomicPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    private readonly ServiceProvider _services;
    private readonly string _connectionString;
    private static long _nextTaxNumber = 1790012345;
    private int _outboxBaseline;
    private Guid _tenant;
    private Guid _company;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _type;
    private Guid _category;
    private Guid _brand;
    private string? _failureSku;
    private bool _cancel;
    private int _createdDuringConfirmation;

    public ConfirmItemsAtomicPostgreSqlTests(InitialLoadPostgresFixture postgres)
    {
        _connectionString = postgres.ConnectionString;
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(() => _tenant);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(x => x.CompanyId).Returns(() => _company);
        company.SetupGet(x => x.HasCompanyContext).Returns(() => _company != Guid.Empty);
        var publisher = new Mock<IPublisher>();
        publisher.Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Callback<INotification, CancellationToken>((message, _) =>
            {
                if (message is ERP.Domain.Modules.Items.Events.ItemCreatedEvent) _createdDuringConfirmation++;
            }).Returns(Task.CompletedTask);
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(() => _tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(() => _company);
        ctx.SetupGet(x => x.UserId).Returns(_user);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(CreateItemCommand).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient<IValidator<CreateItemCommand>, CreateItemCommandValidator>();
        services.AddTransient<IValidator<ERP.Application.Items.UseCases.UpdateItem.UpdateItemCommand>, ERP.Application.Items.UseCases.UpdateItem.UpdateItemCommandValidator>();
        services.AddTransient<IValidator<CreateCategoryNodeCommand>, CreateCategoryNodeCommandValidator>();
        services.AddTransient<IValidator<CreateBrandCommand>, CreateBrandCommandValidator>();
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(ctx.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user));
        services.AddSingleton(publisher.Object);
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor(), new ItemFailureInterceptor(this), new BatchLockObserver(this)));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<ICategoryNodeRepository, CategoryNodeRepository>();
        services.AddScoped<IItemCatalogRepository, ItemCatalogRepository>();
        services.AddScoped<IItemTypeRepository, ItemTypeRepository>();
        services.AddScoped<ISriCatalogResolver, SriCatalogResolver>();
        services.AddScoped<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        var config = new Mock<ICatalogConfigurationResolver>();
        config.Setup(x => x.ResolveMaxCategoryDepthAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(3);
        services.AddSingleton(config.Object);
        services.AddSingleton(Mock.Of<IItemImportSheetReader>());
        var files = new Mock<ERP.Application.Common.Interfaces.IFileStorage>();
        files.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1]));
        services.AddSingleton(files.Object);
        services.AddSingleton(Mock.Of<IBusinessPartnerRepository>());
        services.AddScoped<ItemImportProcessor>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IImportBatchRowRepository, ImportBatchRowRepository>();
        services.AddScoped<IImportBatchIssueRepository, ImportBatchIssueRepository>();
        services.AddScoped<IReadOnlyDictionary<ImportType, IImportProcessor>>(sp =>
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Items] = sp.GetRequiredService<ItemImportProcessor>() });
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL1B", "il1b-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber).ToString() + "001", "IL1B S.A.", createdBy: _user);
        _company = company.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        var type = ItemTypeDefinition.Create(_tenant, "Physical", "Físico", 1, _user);
        _type = type.Id;
        db.Set<ItemTypeDefinition>().Add(type);
        var category = ItemCategoryNode.Create(_tenant, "EXISTING", "Existing Category", CategoryNodeLevel.Category, _user);
        _category = category.Id;
        category.SetPath("/" + category.Id);
        db.Set<ItemCategoryNode>().Add(category);
        var brand = Brand.Create(_tenant, "EXISTING", "Existing Brand", _user);
        _brand = brand.Id;
        db.Set<Brand>().Add(brand);
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() => await _services.DisposeAsync();

    private ParsedItemRow Row(string sku, string category = "Existing Category", string brand = "Existing Brand") =>
        new(sku, sku, sku, _type, "19", category, brand, [new("BC-" + sku, "Internal")],
            "4", "0", 10m, true, null, null, null);

    private async Task<Guid> BatchAsync(bool auto, params ParsedItemRow[] rows)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.Items, _user, autoCreateCatalogValues: auto);
        batch.AttachFile("test.xlsx", "test.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        batch.BeginValidating(_user);
        batch.CompleteValidation(rows.Length, rows.Length, 0, 0, _user);
        db.ImportBatches.Add(batch);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = ImportBatchRow.Create(_tenant, _company, batch.Id, i + 1, "{}", _user);
            row.SetParsedData(JsonSerializer.Serialize(rows[i]), false, _user);
            db.ImportBatchRows.Add(row);
        }
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private async Task<Result<ImportBatchConfirmResultDto>> ConfirmAsync(Guid batch)
    {
        await using var scope = _services.CreateAsyncScope();
        _outboxBaseline = await scope.ServiceProvider.GetRequiredService<ErpDbContext>().OutboxMessages.CountAsync();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ConfirmImportBatchCommand(batch));
    }

    private async Task AssertRolledBackAsync(Guid batch, int baselineItems = 0)
    {
        // Contexto y conexión nuevos: no confiar en el tracker de la request que falló.
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.Items.CountAsync()).Should().Be(baselineItems);
        (await db.OutboxMessages.CountAsync()).Should().Be(_outboxBaseline, "los eventos persistidos también pertenecen a la transacción");
        (await db.Set<ItemCategoryNode>().CountAsync()).Should().Be(1);
        (await db.Set<Brand>().CountAsync()).Should().Be(1);
        (await db.ItemVariantBarcodes.CountAsync()).Should().Be(baselineItems);
        (await db.ImportBatchRows.Where(r => r.ImportBatchId == batch && r.IsImported).CountAsync()).Should().Be(0);
        var persisted = await db.ImportBatches.SingleAsync(b => b.Id == batch);
        persisted.Status.Should().Be(ImportStatus.Validated);
        persisted.ImportedRows.Should().Be(0);
        persisted.ConfirmedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exito_commit_unico_existing_off_o_catalogos_nuevos_on(bool auto)
    {
        var category = auto ? "New Category" : "Existing Category";
        var brand = auto ? "New Brand" : "Existing Brand";
        var batch = await BatchAsync(auto, Row("OK1", category, brand), Row("OK2", category, brand));
        var result = await ConfirmAsync(batch);
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        result.Value.ImportedRows.Should().Be(2);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.Items.CountAsync()).Should().Be(2);
        (await db.Set<ItemCategoryNode>().CountAsync()).Should().Be(auto ? 2 : 1);
        (await db.Set<Brand>().CountAsync()).Should().Be(auto ? 2 : 1);
        (await db.ImportBatchRows.CountAsync(r => r.ImportBatchId == batch && r.IsImported)).Should().Be(2);
    }

    [Theory]
    [InlineData("category", false)]
    [InlineData("brand", false)]
    [InlineData("category", true)]
    [InlineData("brand", true)]
    public async Task Catalogo_desaparecido_tras_preview_respeta_on_off(string kind, bool auto)
    {
        var batch = await BatchAsync(auto, Row("MISSING"));
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            if (kind == "category") await db.Set<ItemCategoryNode>().Where(c => c.Id == _category).ExecuteDeleteAsync();
            else await db.Set<Brand>().Where(b => b.Id == _brand).ExecuteDeleteAsync();
        }
        var result = await ConfirmAsync(batch);
        result.IsSuccess.Should().Be(auto, result.Error);
        await using var verify = _services.CreateAsyncScope();
        var saved = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await saved.Items.CountAsync()).Should().Be(auto ? 1 : 0);
        (await saved.Set<ItemCategoryNode>().CountAsync()).Should().Be(kind == "category" && !auto ? 0 : 1);
        (await saved.Set<Brand>().CountAsync()).Should().Be(kind == "brand" && !auto ? 0 : 1);
        (await saved.ImportBatchRows.AnyAsync(r => r.IsImported)).Should().Be(auto);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("brand")]
    [InlineData("child")]
    [InlineData("ancestor")]
    public async Task Relectura_rechaza_clasificacion_invalidada_tras_preview(string kind)
    {
        var batch = await BatchAsync(true, Row("INVALID"));
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var category = await db.Set<ItemCategoryNode>().SingleAsync(c => c.Id == _category);
            if (kind == "category") category.Disable(_user);
            if (kind == "brand") (await db.Set<Brand>().SingleAsync(b => b.Id == _brand)).Disable(_user);
            if (kind == "child")
            {
                var child = ItemCategoryNode.Create(_tenant, "CHILD", "Child", CategoryNodeLevel.Category, _user, parentId: _category);
                child.SetPath(category.Path + "/" + child.Id);
                db.Set<ItemCategoryNode>().Add(child);
            }
            if (kind == "ancestor")
            {
                var parent = ItemCategoryNode.Create(_tenant, "PARENT", "Parent", CategoryNodeLevel.Category, _user);
                parent.SetPath("/" + parent.Id);
                parent.Disable(_user);
                db.Set<ItemCategoryNode>().Add(parent);
                category.SetPath(parent.Path + "/" + category.Id);
            }
            await db.SaveChangesAsync();
        }
        var result = await ConfirmAsync(batch);
        result.IsSuccess.Should().BeFalse();
        await using var verify = _services.CreateAsyncScope();
        var saved = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await saved.Items.CountAsync()).Should().Be(0);
        (await saved.ImportBatchRows.AnyAsync(r => r.IsImported)).Should().BeFalse();
        (await saved.ImportBatches.SingleAsync(b => b.Id == batch)).Status.Should().Be(ImportStatus.Validated);
    }

    [Fact]
    public async Task Fallo_de_dominio_en_segunda_fila_revierte_items_catalogos_y_marcas_de_filas()
    {
        var batch = await BatchAsync(true, Row("FIRST", "First Category", "First Brand"), Row("DUP", "Second Category", "Second Brand"));
        await using (var scope = _services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CreateItemCommand(
                "DUP", "Existing", "Existing", _type, "19", _category, _brand, [new("EXISTING-BC", "Internal", true)], SaleVatCode: "4", PurchaseVatCode: "0"));
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        _createdDuringConfirmation = 0;
        var resultConfirm = await ConfirmAsync(batch);
        resultConfirm.IsSuccess.Should().BeFalse();
        _createdDuringConfirmation.Should().Be(1, "la primera fila llegó a SaveChanges real antes del rechazo de la segunda");
        await AssertRolledBackAsync(batch, baselineItems: 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fallo_tecnico_o_cancelacion_despues_de_escrituras_revierte_todo(bool cancellation)
    {
        var batch = await BatchAsync(true, Row("FIRST", "First Category", "First Brand"), Row("FAIL", "Second Category", "Second Brand"));
        _failureSku = "FAIL";
        _cancel = cancellation;
        if (cancellation)
        {
            var act = () => ConfirmAsync(batch);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        else
        {
            var result = await ConfirmAsync(batch);
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().NotContain("Injected");
        }
        _createdDuringConfirmation.Should().Be(1);
        await AssertRolledBackAsync(batch);
    }

    [Fact]
    public async Task Fallo_en_segunda_pagina_revierte_las_200_filas_de_la_primera()
    {
        var rows = Enumerable.Range(1, 200).Select(i => Row("PAGE-" + i)).Append(Row("FAIL")).ToArray();
        var batch = await BatchAsync(false, rows);
        _failureSku = "FAIL";
        var result = await ConfirmAsync(batch);
        result.IsSuccess.Should().BeFalse();
        _createdDuringConfirmation.Should().Be(200);
        await AssertRolledBackAsync(batch);
    }

    private sealed class ItemFailureInterceptor(ConfirmItemsAtomicPostgreSqlTests test) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (test._failStaging && eventData.Context!.ChangeTracker.Entries<ImportBatchRow>()
                .Any(e => e.State == EntityState.Added))
            {
                if (test._cancel) throw new OperationCanceledException();
                throw new InvalidOperationException("Injected staging failure");
            }
            if (test._failureSku is not null && eventData.Context!.ChangeTracker.Entries<Item>()
                .Any(e => e.State == EntityState.Added && e.Entity.Code.SKU == test._failureSku))
            {
                if (test._cancel) throw new OperationCanceledException();
                throw new InvalidOperationException("Injected storage failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
