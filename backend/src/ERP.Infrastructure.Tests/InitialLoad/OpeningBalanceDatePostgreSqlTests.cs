using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.OpeningBalanceDate;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using BranchEntity = ERP.Domain.Branches.Entities.Branch;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-5A — PostgreSQL 16 + migraciones completas. Configuración real de Company.OpeningBalanceDate
/// (handlers, repositorio y lector de operaciones/aperturas reales; solo el guard de acceso es fake):
/// set inicial, corrección antes de aperturas, rechazo tras apertura confirmada y tras operación
/// real, y compatibilidad con la apertura de inventario de Sumak al 2026-09-30.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class OpeningBalanceDatePostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    private static readonly DateOnly Sept30 = new(2026, 9, 30);
    private static readonly DateOnly Aug31 = new(2026, 8, 31);
    private static long _nextTaxNumber = 1790098000;
    private readonly ServiceProvider _services;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _otherCompany;
    private Guid _branch;
    private Guid _otherBranch;
    private readonly Dictionary<Guid, (Guid Item, Guid Warehouse)> _stock = new();

    public OpeningBalanceDatePostgreSqlTests(InitialLoadPostgresFixture postgres)
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(() => _tenant);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(x => x.CompanyId).Returns(() => _company);
        company.SetupGet(x => x.HasCompanyContext).Returns(() => _company != Guid.Empty);
        var guard = new Mock<ICompanyAccessGuard>();
        guard.Setup(x => x.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result<CompanyAccessContext>.Success(
                new CompanyAccessContext(_user, _tenant, _company, "Admin", true, true)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(SetOpeningBalanceDateCommand).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<SetOpeningBalanceDateCommand>(ServiceLifetime.Transient);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(guard.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il5a@test" && x.FullName == "IL5A"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor()));
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IOpeningBalanceConstraintsReader, OpeningBalanceConstraintsReader>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOpeningJournalEntryPostingRepository, OpeningJournalEntryPostingRepository>();
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL5A-OBD", "il5o-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "Sumak", createdBy: _user);
        var other = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "Otra", createdBy: _user);
        _company = company.Id;
        _otherCompany = other.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Tenants.Add(tenant);
        db.Companies.AddRange(company, other);
        await db.SaveChangesAsync();
        var branch = BranchEntity.Create(tenantId: _tenant, name: "Matriz", address: "Av. 1", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: true, createdBy: _user, companyId: _company);
        var otherBranch = BranchEntity.Create(tenantId: _tenant, name: "Otra", address: "Av. 2", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: true, createdBy: _user, companyId: _otherCompany);
        db.Branches.AddRange(branch, otherBranch);
        var type = ItemTypeDefinition.Create(_tenant, "MERCH", "Mercadería", 1, _user);
        db.Set<ItemTypeDefinition>().Add(type);
        await db.SaveChangesAsync();
        _branch = branch.Id;
        _otherBranch = otherBranch.Id;
        foreach (var (companyId, branchId) in new[] { (_company, _branch), (_otherCompany, _otherBranch) })
        {
            var item = Item.Create(_tenant, "IL5O-" + companyId.ToString("N")[..6], "Producto", "Producto", type.Id,
                "UNIT", ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: "4"), ItemSaleConfig.Create(isForSale: true),
                ItemStockConfig.Create(stockControlEnabled: true), _user, companyId: companyId);
            var warehouse = Warehouse.Create(_tenant, branchId, "Bodega", "BOD-01", null, null, null, null, null, null,
                null, null, null, _user, companyId, isMain: true);
            db.Set<Item>().Add(item);
            db.Warehouses.Add(warehouse);
            _stock[companyId] = (item.Id, warehouse.Id);
        }
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    private async Task<Result<OpeningBalanceDateDto>> SendAsync(IRequest<Result<OpeningBalanceDateDto>> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private Task<Result<OpeningBalanceDateDto>> SetAsync(DateOnly date) => SendAsync(new SetOpeningBalanceDateCommand(date));

    private async Task<DateOnly?> StoredDateAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ErpDbContext>().Companies.IgnoreQueryFilters()
            .Where(c => c.Id == _company).Select(c => c.OpeningBalanceDate).SingleAsync();
    }

    private async Task SeedAsync(Action<ErpDbContext> add)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        add(db);
        await db.SaveChangesAsync();
    }

    private StockMovement Movement(Guid companyId, StockMovementType type, DateOnly effective) =>
        StockMovement.Create(_tenant, companyId == _company ? _branch : _otherBranch, _stock[companyId].Item,
            _stock[companyId].Warehouse, type, 10m, "UNIT", 0m, Random.Shared.NextInt64(1, long.MaxValue), 2.5m, 25m,
            effective, "IL5A", null, null, _user, companyId, unitCost: 2.5m);

    [Fact]
    public async Task Set_inicial_y_lectura()
    {
        (await SendAsync(new GetOpeningBalanceDateQuery())).Value!.OpeningBalanceDate.Should().BeNull();

        var result = await SetAsync(Sept30);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.OpeningBalanceDate.Should().Be(Sept30);
        result.Value.IsLocked.Should().BeFalse();
        (await StoredDateAsync()).Should().Be(Sept30);
    }

    [Fact]
    public async Task Correccion_antes_de_cualquier_apertura_u_operacion()
    {
        (await SetAsync(Sept30)).IsSuccess.Should().BeTrue();
        // Un borrador de venta o la actividad de OTRA empresa no son operación real de esta.
        await SeedAsync(db => db.StockMovements.Add(Movement(_otherCompany, StockMovementType.SaleExit, Aug31)));

        var result = await SetAsync(Aug31);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await StoredDateAsync()).Should().Be(Aug31);
    }

    [Fact]
    public async Task Rechazo_tras_saldo_inicial_de_cxc_confirmado()
    {
        (await SetAsync(Sept30)).IsSuccess.Should().BeTrue();
        var customer = BusinessPartner.Create(_tenant, "04", "1790016919001", null, "Cliente S.A.", _user);
        await SeedAsync(db =>
        {
            var batch = ImportBatch.Create(_tenant, _company, ImportType.InitialReceivables, _user);
            db.BusinessPartners.Add(customer);
            db.ImportBatches.Add(batch);
            db.SalesReceivables.Add(SalesReceivable.CreateInitialBalance(_tenant, _company, _branch, customer.Id,
                "FAC-1", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), 10m, batch.Id, _user));
        });

        var result = await SetAsync(Aug31);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("2026-09-30").And.Contain("corrija o reabra");
        (await StoredDateAsync()).Should().Be(Sept30);
        (await SendAsync(new GetOpeningBalanceDateQuery())).Value!.ConfirmedOpeningDates.Should().Equal(Sept30);
    }

    [Fact]
    public async Task Rechazo_tras_la_primera_operacion_real()
    {
        (await SetAsync(Sept30)).IsSuccess.Should().BeTrue();
        var establishment = Establishment.Create(_tenant, branchId: _branch, _company, code: "001", name: "Matriz",
            address: "Av. 1", phone: null, isMain: true, createdBy: _user);
        var register = CashRegister.Create(_tenant, _company, _branch, "CAJA-01", "Caja", _user);
        await SeedAsync(db =>
        {
            db.Establishments.Add(establishment);
            db.CashRegisters.Add(register);
        });
        var emissionPoint = EmissionPoint.Create(_tenant, _company, establishment.Id, code: "001", name: "PE-001",
            emissionType: EmissionType.Electronic, isDefault: true, createdBy: _user);
        await SeedAsync(db => db.EmissionPoints.Add(emissionPoint));
        await SeedAsync(db => db.CashSessions.Add(CashSession.Open(_tenant, _company, _branch, _user, register.Id,
            "CAJA-01", "Caja", emissionPoint.Id, "001", 0m, _user)));

        var result = await SetAsync(Aug31);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("definitiva");
        (await StoredDateAsync()).Should().Be(Sept30);
        var status = (await SendAsync(new GetOpeningBalanceDateQuery())).Value!;
        status.HasRealOperations.Should().BeTrue();
        status.IsLocked.Should().BeTrue();
        (await SetAsync(Sept30)).IsSuccess.Should().BeTrue("repetir la fecha vigente es idempotente");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Compatibilidad_Sumak_acepta_2026_09_30_y_rechaza_otra_fecha(bool alreadyOperating)
    {
        // Inventario Inicial (IL-4) confirmado al 2026-09-30 antes de existir el campo.
        await SeedAsync(db => db.StockMovements.Add(Movement(_company, StockMovementType.InitialBalance, Sept30)));
        if (alreadyOperating)
            await SeedAsync(db => db.StockMovements.Add(Movement(_company, StockMovementType.SaleExit, new DateOnly(2026, 10, 4))));

        var other = await SetAsync(Aug31);
        var sept30 = await SetAsync(Sept30);

        other.IsSuccess.Should().BeFalse();
        other.Error.Should().Contain("2026-09-30");
        sept30.IsSuccess.Should().BeTrue(sept30.Error);
        (await StoredDateAsync()).Should().Be(Sept30);
    }
}
