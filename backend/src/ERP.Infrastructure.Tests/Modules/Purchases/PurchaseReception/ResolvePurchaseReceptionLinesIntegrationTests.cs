using System.Globalization;
using System.Text;
using ERP.Application;
using ERP.Application.Access.Authorization;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Catalogs;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

/// <summary>
/// COMPRAS-METODO-ZH-01B — resolución masiva de una factura XML de 100 líneas contra PostgreSQL real,
/// por el pipeline MediatR real (validación + scopes) y los handlers reales de Items: 60 códigos ya
/// conocidos, 10 vinculados a productos existentes, 24 productos nuevos (6 también llegan como caja
/// x12 con otro código). Verifica creación, presentaciones, equivalencias aprendidas, trazabilidad
/// del XML, todo-o-nada y el reconocimiento automático en las siguientes facturas.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class ResolvePurchaseReceptionLinesIntegrationTests : IAsyncLifetime
{
    private const int KnownCount = 60;
    private const int LinkCount = 10;
    private const int NewCount = 24;
    private const int BoxCount = 6;
    private const string UnitUom = "19";
    private const string BoxUom = "02";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_resolve_reception_lines_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    private ServiceProvider _services = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _itemTypeId;
    private Guid[] _brands = [];
    private Guid[] _categories = [];
    private Guid _parentCategoryId;
    private readonly Dictionary<string, (Guid ItemId, Guid BaseLevelId)> _linkTargets = new();
    private int _invoiceSequence;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _services = BuildServices();
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddDbContext<ErpDbContext>(o =>
            o.UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
        );
        services.AddScoped(_ => Mock.Of<ICurrentTenant>(t => t.TenantId == _tenantId));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(() => _companyId));
        services.AddScoped(_ =>
            Mock.Of<ICurrentBranch>(b =>
                b.BranchId == _branchId && b.HasBranchContext && b.IsAuthenticated
            )
        );
        services.AddScoped(_ =>
            Mock.Of<ICurrentUser>(u =>
                u.UserId == _userId && u.IsAuthenticated && u.Role == "Admin"
            )
        );
        services.AddScoped(_ => AllowCompany());
        services.AddScoped(_ => AllowBranch());
        services.AddScoped(_ =>
            Mock.Of<IRuntimePermissionAuthorizer>(a =>
                a.IsAuthorizedAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ) == Task.FromResult(true)
            )
        );
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => _tenantId,
            () => _companyId,
            _userId
        ));
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<ICategoryNodeRepository, CategoryNodeRepository>();
        services.AddScoped<IItemCatalogRepository, ItemCatalogRepository>();
        services.AddScoped<IItemTypeRepository, ItemTypeRepository>();
        services.AddScoped<
            IPurchaseReceptionDocumentRepository,
            PurchaseReceptionDocumentRepository
        >();
        services.AddScoped<IPurchaseInvoiceRepository, PurchaseInvoiceRepository>();
        services.AddScoped<IBusinessPartnerRepository, BusinessPartnerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<ISriCatalogResolver, SriCatalogResolver>();
        services.AddScoped<IItemMatchConfirmationService, ItemMatchConfirmationService>();
        services.AddScoped<IPurchaseReceptionAutoMatcher, PurchaseReceptionAutoMatcher>();
        services.AddScoped<IItemMatchFinder, ItemMatchFinder>();
        services.AddScoped<IPurchaseXmlDraftParser, PurchaseXmlDraftParser>();
        // ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A: ConfirmPurchaseHandler inicia la transmisión de la
        // retención tras el commit; este test confirma sin retención y no cablea ElectronicDocuments.
        services.AddScoped(_ =>
            ERP.Infrastructure.Tests.TestData.RetentionElectronicTestWiring.NoOpTransmission()
        );
        ConfigurePurchaseServices(services);
        return services.BuildServiceProvider();
    }

    private ICompanyAccessGuard AllowCompany()
    {
        var guard = new Mock<ICompanyAccessGuard>();
        guard
            .Setup(g => g.RequireActiveTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(_tenantId));
        guard
            .Setup(g =>
                g.RequireMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CompanyAccessContext>.Success(
                    new CompanyAccessContext(_userId, _tenantId, _companyId, "Admin", true, true)
                )
            );
        guard
            .Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<CompanyAccessContext>.Success(
                    new CompanyAccessContext(_userId, _tenantId, _companyId, "Admin", true, true)
                )
            );
        return guard.Object;
    }

    private IBranchAccessGuard AllowBranch()
    {
        var guard = new Mock<IBranchAccessGuard>();
        guard
            .Setup(g => g.RequireBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(
                        _userId,
                        _tenantId,
                        _companyId,
                        _branchId,
                        "Matriz",
                        true
                    )
                )
            );
        // BranchScopeBehavior valida la sucursal activa vía RequireCurrentBranchAsync.
        guard
            .Setup(g => g.RequireCurrentBranchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(
                        _userId,
                        _tenantId,
                        _companyId,
                        _branchId,
                        "Matriz",
                        true
                    )
                )
            );
        return guard.Object;
    }

    private async Task SeedAsync(ErpDbContext db)
    {
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "B01",
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
            isMainBranch: true,
            createdBy: _userId,
            companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id,
            "04",
            "1710034065001",
            1,
            "Distribuidora XML",
            _userId
        );
        var itemType = ItemTypeDefinition.Create(tenant.Id, "MERCH", "Mercadería", 1, _userId);
        var brands = new[]
        {
            Brand.Create(tenant.Id, "MA", "Marca A", _userId),
            Brand.Create(tenant.Id, "MB", "Marca B", _userId),
        };
        var parent = ItemCategoryNode.Create(
            tenant.Id,
            "ABA",
            "Abarrotes",
            CategoryNodeLevel.Family,
            _userId
        );
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.Set<ItemTypeDefinition>().Add(itemType);
        db.Brands.AddRange(brands);
        db.ItemCategoryNodes.Add(parent);
        await db.SaveChangesAsync();
        var leaves = new[]
        {
            ItemCategoryNode.Create(
                tenant.Id,
                "GRA",
                "Granos",
                CategoryNodeLevel.Category,
                _userId,
                parent.Id
            ),
            ItemCategoryNode.Create(
                tenant.Id,
                "BEB",
                "Bebidas",
                CategoryNodeLevel.Category,
                _userId,
                parent.Id
            ),
        };
        db.ItemCategoryNodes.AddRange(leaves);
        await db.SaveChangesAsync();

        _branchId = branch.Id;
        _supplierId = supplier.Id;
        _itemTypeId = itemType.Id;
        _brands = brands.Select(b => b.Id).ToArray();
        _categories = leaves.Select(c => c.Id).ToArray();
        _parentCategoryId = parent.Id;

        // Known equivalences (auto-resolved) and existing products without a supplier code (to link).
        for (var i = 0; i < KnownCount + LinkCount; i++)
        {
            var known = i < KnownCount;
            var item = Item.Create(
                tenant.Id,
                $"EX-{i:D3}",
                $"Existente {i}",
                $"Existente {i}",
                itemType.Id,
                UnitUom,
                ItemTaxConfig.Create("0", "0"),
                ItemSaleConfig.Create(true),
                ItemStockConfig.Create(true),
                _userId,
                baseSalePrice: 5m
            );
            item.ReplacePackagingLevels(
                [("Unidad", 1, 1m, UnitUom, null, null, true, true, true)],
                _userId
            );
            if (known)
                item.AddSupplierCode(
                    $"K-{i:D3}",
                    true,
                    supplier.Id,
                    _userId,
                    item.PackagingLevels[0].Id
                );
            else
                _linkTargets[$"L-{i - KnownCount:D3}"] = (item.Id, item.PackagingLevels[0].Id);
            db.Items.Add(item);
        }
        await db.SaveChangesAsync();
    }

    // ── XML helpers ──────────────────────────────────────────────────────────────────────────

    private static IEnumerable<(
        string Code,
        string Description,
        decimal Qty,
        decimal Price
    )> InvoiceLines()
    {
        for (var i = 0; i < KnownCount; i++)
            yield return ($"K-{i:D3}", $"PRODUCTO CONOCIDO {i}", 2m, 1.50m);
        for (var i = 0; i < LinkCount; i++)
            yield return ($"L-{i:D3}", $"ART. EXISTENTE SIN CODIGO {i}", 3m, 2m);
        for (var i = 0; i < NewCount; i++)
            yield return ($"N-{i:D3}", $"PROD NUEVO {i} 500GR", 4m, 1m);
        for (var i = 0; i < BoxCount; i++)
            yield return ($"B-{i:D3}", $"PROD NUEVO {i} 500GR CJ X12", 1m, 11.40m);
    }

    private static string BuildXml(
        string sequential,
        IEnumerable<(string Code, string Description, decimal Qty, decimal Price)> lines
    )
    {
        static string N(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var detail = new StringBuilder();
        var total = 0m;
        foreach (var (code, description, qty, price) in lines)
        {
            var subtotal = qty * price;
            total += subtotal;
            detail.Append(
                $"<detalle><codigoPrincipal>{code}</codigoPrincipal><descripcion>{description}</descripcion>"
                    + $"<cantidad>{N(qty)}</cantidad><precioUnitario>{N(price)}</precioUnitario><descuento>0.00</descuento>"
                    + $"<precioTotalSinImpuesto>{N(subtotal)}</precioTotalSinImpuesto><impuestos><impuesto><codigo>2</codigo>"
                    + $"<codigoPorcentaje>0</codigoPorcentaje><tarifa>0</tarifa><baseImponible>{N(subtotal)}</baseImponible>"
                    + "<valor>0.00</valor></impuesto></impuestos></detalle>"
            );
        }
        return "<factura><infoTributaria><ruc>1710034065001</ruc><razonSocial>Distribuidora XML</razonSocial>"
            + $"<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>{sequential}</secuencial>"
            + "</infoTributaria><infoFactura><fechaEmision>27/09/2026</fechaEmision>"
            + $"<totalSinImpuestos>{N(total)}</totalSinImpuestos><importeTotal>{N(total)}</importeTotal></infoFactura>"
            + $"<detalles>{detail}</detalles></factura>";
    }

    /// <summary>Descarga + procesamiento real del XML (mismo matching automático de producción).</summary>
    private async Task<Guid> ReceiveInvoiceAsync(bool repeatBox = false)
    {
        var sequential = $"{++_invoiceSequence:D9}";
        var xml = BuildXml(
            sequential,
            repeatBox
                ? InvoiceLines().Concat(InvoiceLines().Where(l => l.Code == "B-000"))
                : InvoiceLines()
        );
        await using var scope = _services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var document = PurchaseReceptionDocument.Create(
            _tenantId,
            _companyId,
            _branchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1710034065001",
            "Distribuidora XML",
            _supplierId,
            sequential.PadLeft(49, '7'),
            $"001-001-{sequential}",
            new DateOnly(2026, 9, 27),
            null,
            0m,
            0m,
            0m,
            _userId
        );
        var processor = new PurchaseReceptionDetailProcessor(
            sp.GetRequiredService<IPurchaseXmlDraftParser>(),
            sp.GetRequiredService<IItemRepository>(),
            sp.GetRequiredService<IItemMatchFinder>(),
            NullLogger<PurchaseReceptionDetailProcessor>.Instance
        );
        var processed = await processor.ProcessAsync(
            document.Id,
            _tenantId,
            _supplierId,
            xml,
            CancellationToken.None
        );
        document.AttachSriAuthorization(
            sequential.PadLeft(49, '7'),
            DateTime.UtcNow,
            xml,
            DateTime.UtcNow,
            processed.Lines,
            _userId,
            processed.DocTypeCode,
            processed.SriPaymentMethodCode,
            processed.Processing
        );
        var repo = sp.GetRequiredService<IPurchaseReceptionDocumentRepository>();
        await repo.AddAsync(document);
        await repo.SaveChangesAsync();
        return document.Id;
    }

    private async Task<PurchaseReceptionDocument> LoadAsync(Guid documentId)
    {
        await using var scope = _services.CreateAsyncScope();
        return (
            await scope
                .ServiceProvider.GetRequiredService<IPurchaseReceptionDocumentRepository>()
                .GetByIdAsync(_tenantId, documentId)
        )!;
    }

    private async Task<ResolvePurchaseReceptionLinesResultDto> ResolveAsync(
        ResolvePurchaseReceptionLinesCommand command
    )
    {
        await using var scope = _services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!;
    }

    // ── Batch built the way the UI does: bulk-assigned fields + individual corrections ──────

    private ResolvePurchaseReceptionLinesCommand BuildBatch(
        PurchaseReceptionDocument document,
        string skuPrefix
    )
    {
        var byCode = document
            .Lines.GroupBy(l => l.SupplierCode!)
            .ToDictionary(g => g.Key, g => g.First());
        var newItems = Enumerable
            .Range(0, NewCount)
            .Select(i => new ResolveReceptionNewItemInput(
                Key: $"n{i}",
                Sku: $"{skuPrefix}{i:D3}",
                // ERP name normalized by the user; the XML description stays on the reception line.
                ShortName: $"Producto nuevo {i} 500 g",
                Description: $"Producto nuevo {i} presentación 500 g",
                ItemTypeId: _itemTypeId,
                CategoryNodeId: _categories[i % 2],
                BrandId: _brands[i < 12 ? 0 : 1],
                DefaultUomCode: UnitUom,
                Barcode: $"77{skuPrefix.GetHashCode() & 0xFFFF:D5}{i:D6}",
                BarcodeType: "Internal",
                SaleVatCode: "0",
                PurchaseVatCode: "0",
                ExciseTaxCode: null,
                BaseSalePrice: 1.35m
            ))
            .ToList();

        var lines = new List<ResolveReceptionLineInput>();
        for (var i = 0; i < LinkCount; i++)
        {
            var (itemId, levelId) = _linkTargets[$"L-{i:D3}"];
            lines.Add(new(byCode[$"L-{i:D3}"].Id, ItemId: itemId, PackagingLevelId: levelId));
        }
        for (var i = 0; i < NewCount; i++)
            lines.Add(new(byCode[$"N-{i:D3}"].Id, NewItemKey: $"n{i}"));
        for (var i = 0; i < BoxCount; i++)
            lines.Add(
                new(
                    byCode[$"B-{i:D3}"].Id,
                    NewItemKey: $"n{i}",
                    PresentationFactor: 12m,
                    PresentationName: "Caja x12",
                    PresentationUomCode: BoxUom
                )
            );
        return new ResolvePurchaseReceptionLinesCommand(newItems, lines);
    }

    // ── Tests ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invoice_of_100_lines_resolves_exceptions_in_bulk_and_next_invoices_are_recognized()
    {
        var firstId = await ReceiveInvoiceAsync();
        var pendingBeforeId = await ReceiveInvoiceAsync(); // downloaded before resolving (same batch)
        var first = await LoadAsync(firstId);
        first.Lines.Should().HaveCount(100);
        first
            .Lines.Count(l => l.MatchStatus == ItemMatchStatus.AutoMatched)
            .Should()
            .Be(KnownCount);
        var exceptions = first.Lines.Count(l => l.ItemId is null);
        exceptions.Should().Be(LinkCount + NewCount + BoxCount);

        var outcome = await ResolveAsync(BuildBatch(first, "NV-"));

        outcome.Applied.Should().BeTrue(string.Join(" | ", outcome.Errors.Select(e => e.Message)));
        outcome.ItemsCreated.Should().Be(NewCount);
        outcome.LinesLinked.Should().Be(exceptions);
        outcome.EquivalencesLearned.Should().Be(exceptions);
        outcome
            .Lines.Where(l => l.ConversionFactor == 12m)
            .Should()
            .HaveCount(BoxCount)
            .And.OnlyContain(l => l.UomCode == BoxUom && l.BaseUomCode == UnitUom);

        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var created = await db
                .Items.Include(i => i.PackagingLevels)
                .Include(i => i.SupplierCodes)
                .Where(i => i.Code.SKU.StartsWith("NV-"))
                .ToListAsync();
            created.Should().HaveCount(NewCount);
            created.Count(i => i.BrandId == _brands[0]).Should().Be(12);
            created.Count(i => i.CategoryNodeId == _categories[0]).Should().Be(NewCount / 2);
            var withBox = created.Single(i => i.Code.SKU == "NV-000");
            withBox.Code.ShortName.Should().Be("Producto nuevo 0 500 g");
            withBox.PackagingLevels.Should().HaveCount(2);
            var unit = withBox.PackagingLevels.Single(p => p.IsBaseUnit);
            var box = withBox.PackagingLevels.Single(p => !p.IsBaseUnit);
            (unit.BaseQuantity, unit.UomCode).Should().Be((1m, UnitUom));
            (box.BaseQuantity, box.UomCode, box.Name).Should().Be((12m, BoxUom, "Caja x12"));
            withBox
                .SupplierCodes.Single(c => c.Code == "N-000")
                .PackagingLevelId.Should()
                .Be(unit.Id);
            withBox
                .SupplierCodes.Single(c => c.Code == "B-000")
                .PackagingLevelId.Should()
                .Be(box.Id);
            created
                .Single(i => i.Code.SKU == "NV-020")
                .PackagingLevels.Should()
                .ContainSingle(p => p.IsBaseUnit);
            var linked = await db
                .Items.Include(i => i.SupplierCodes)
                .SingleAsync(i => i.Id == _linkTargets["L-000"].ItemId);
            linked
                .SupplierCodes.Single(c => c.Code == "L-000")
                .PackagingLevelId.Should()
                .Be(_linkTargets["L-000"].BaseLevelId);
        }

        var resolved = await LoadAsync(firstId);
        resolved.Lines.Should().HaveCount(100).And.OnlyContain(l => l.ItemId != null);
        resolved
            .Lines.Single(l => l.SupplierCode == "N-000")
            .Description.Should()
            .Be(
                "PROD NUEVO 0 500GR",
                "the supplier XML description is kept for traceability; only the ERP name was normalized"
            );

        // Same batch, downloaded earlier: its pending lines learn the new equivalences.
        await using (var scope = _services.CreateAsyncScope())
        {
            var repo =
                scope.ServiceProvider.GetRequiredService<IPurchaseReceptionDocumentRepository>();
            var earlier = (await repo.GetByIdAsync(_tenantId, pendingBeforeId))!;
            (
                await scope
                    .ServiceProvider.GetRequiredService<IPurchaseReceptionAutoMatcher>()
                    .RefreshAsync(earlier, CancellationToken.None)
            )
                .Should()
                .Be(exceptions);
            await repo.SaveChangesAsync();
        }
        (await LoadAsync(pendingBeforeId))
            .Lines.Should()
            .OnlyContain(l => l.MatchStatus == ItemMatchStatus.AutoMatched);

        // A new XML with the same codes needs no intervention; the box code keeps its conversion.
        var next = await LoadAsync(await ReceiveInvoiceAsync());
        next.Lines.Should()
            .HaveCount(100)
            .And.OnlyContain(l => l.MatchStatus == ItemMatchStatus.AutoMatched);
        await using (var scope = _services.CreateAsyncScope())
        {
            var match = await scope
                .ServiceProvider.GetRequiredService<IItemRepository>()
                .GetSupplierCodeMatchAsync(_supplierId, "B-003", _tenantId);
            (match!.PackagingBaseQuantity, match.PackagingUomCode, match.BaseUomCode)
                .Should()
                .Be((12m, BoxUom, UnitUom));
        }
    }

    [Fact]
    public async Task Invalid_rows_are_all_reported_and_nothing_is_persisted()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        var batch = BuildBatch(document, "BAD-");
        var newItems = batch.NewItems.ToList();
        newItems[1] = newItems[1] with { Sku = "EX-000" }; // SKU already exists
        newItems[2] = newItems[2] with { Sku = newItems[3].Sku }; // SKU repeated in the batch
        newItems[4] = newItems[4] with { BaseSalePrice = 0m }; // required sale price
        var itemsBefore = await CountItemsAsync();

        var outcome = await ResolveAsync(batch with { NewItems = newItems });

        outcome.Applied.Should().BeFalse();
        outcome.Errors.Select(e => e.NewItemKey).Should().Contain(["n1", "n3", "n4"]);
        (await CountItemsAsync()).Should().Be(itemsBefore);
        (await LoadAsync(document.Id))
            .Lines.Count(l => l.ItemId is null)
            .Should()
            .Be(LinkCount + NewCount + BoxCount);
    }

    [Fact]
    public async Task Failure_while_executing_rolls_back_the_whole_batch()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        var batch = BuildBatch(document, "RB-");
        var newItems = batch.NewItems.ToList();
        // Passes pre-validation but CreateItem rejects a non-leaf category on the 6th product.
        newItems[5] = newItems[5] with
        {
            CategoryNodeId = _parentCategoryId,
        };
        var itemsBefore = await CountItemsAsync();

        await using var failedScope = _services.CreateAsyncScope();
        var failed = await failedScope
            .ServiceProvider.GetRequiredService<IMediator>()
            .Send(batch with { NewItems = newItems });
        var outcome = failed.Value!;
        outcome.Applied.Should().BeFalse();
        outcome.Errors.Should().ContainSingle().Which.NewItemKey.Should().Be("n5");
        (await failedScope.ServiceProvider.GetRequiredService<ErpDbContext>().SaveChangesAsync())
            .Should()
            .Be(0, "a later save in the same scope must not resurrect rolled-back entities");
        (await CountItemsAsync())
            .Should()
            .Be(itemsBefore, "the five products created before the failure are rolled back");
        await using var scope = _services.CreateAsyncScope();
        (
            await scope
                .ServiceProvider.GetRequiredService<IItemRepository>()
                .SupplierCodeExistsAsync(_supplierId, "L-000", _tenantId)
        )
            .Should()
            .BeFalse();
        (await LoadAsync(document.Id))
            .Lines.Count(l => l.ItemId is null)
            .Should()
            .Be(LinkCount + NewCount + BoxCount);
    }

    private async Task<int> CountItemsAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ErpDbContext>().Items.CountAsync();
    }

    [Fact]
    public async Task Unselected_repeated_code_returns_complete_automatic_presentation_snapshot()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync(repeatBox: true));
        var batch = BuildBatch(document, "AUTO-");
        // Match the actual UI default SKU: supplier code, including a purchase by box.
        batch = batch with
        {
            NewItems = batch
                .NewItems.Select((item, i) => i == 0 ? item with { Sku = "B-000" } : item)
                .ToList(),
        };
        var outcome = await ResolveAsync(batch);
        outcome.Applied.Should().BeTrue(string.Join(" | ", outcome.Errors.Select(e => e.Message)));
        outcome.LinesAutoMatched.Should().Be(1);
        outcome.LinesLinked.Should().Be(40);
        outcome.Lines.Should().HaveCount(41);
        var automatic = outcome.Lines.Single(l => l.MatchStatus == "AUTO_MATCHED");
        automatic.ConversionFactor.Should().Be(12m);
        automatic.UomCode.Should().Be(BoxUom);
        automatic.PackagingLevelId.Should().NotBeNull();
        (await LoadAsync(document.Id)).Lines.Should().OnlyContain(l => l.ItemId != null);
    }

    [Fact]
    public async Task Concurrent_batches_with_same_skus_have_one_winner_and_no_partial_items()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        var batch = BuildBatch(document, "RACE-");
        var before = await CountItemsAsync();
        var results = await Task.WhenAll(ResolveAsync(batch), ResolveAsync(batch));
        results.Count(r => r.Applied).Should().Be(1);
        results.Single(r => !r.Applied).Errors.Should().NotBeEmpty();
        (await CountItemsAsync()).Should().Be(before + NewCount);
        (await LoadAsync(document.Id)).Lines.Should().OnlyContain(l => l.ItemId != null);
    }

    [Fact]
    public async Task Concurrent_different_products_for_same_supplier_codes_roll_back_losing_batch()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        var before = await CountItemsAsync();
        var results = await Task.WhenAll(
            ResolveAsync(BuildBatch(document, "WIN-A-")),
            ResolveAsync(BuildBatch(document, "WIN-B-"))
        );
        results.Count(r => r.Applied).Should().Be(1);
        results.Single(r => !r.Applied).Errors.Should().NotBeEmpty();
        (await CountItemsAsync()).Should().Be(before + NewCount);
    }

    [Fact]
    public async Task Tenant_and_company_context_cannot_resolve_foreign_reception()
    {
        var document = await LoadAsync(await ReceiveInvoiceAsync());
        var batch = BuildBatch(document, "SCOPE-");
        var before = await CountItemsAsync();
        var originalCompany = _companyId;
        _companyId = Guid.NewGuid();
        await using (var scope = _services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(batch);
            result.IsSuccess.Should().BeFalse();
        }
        _companyId = originalCompany;
        var originalTenant = _tenantId;
        _tenantId = Guid.NewGuid();
        await using (var scope = _services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(batch);
            result.IsSuccess.Should().BeFalse();
        }
        _tenantId = originalTenant;
        (await CountItemsAsync()).Should().Be(before);
        (await LoadAsync(document.Id))
            .Lines.Count(l => l.ItemId is null)
            .Should()
            .Be(LinkCount + NewCount + BoxCount);
    }
}
