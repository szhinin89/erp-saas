using ERP.Application;
using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Pricing;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Pricing;

/// <summary>
/// PRICING-CUSTOMER-PRICE-LIST-FOUNDATION-05A — integración PostgreSQL real (Testcontainers)
/// para la invariante de negocio "como máximo UNA PriceListCustomer ACTIVA por (Tenant, Company,
/// Customer)" (garantizada por índice único parcial, no solo en memoria) y para el aislamiento
/// Tenant+Company fail-closed que ya provee <c>ForOperationalScope</c>.
/// </summary>
public sealed class PriceListCustomerRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_price_list_customer_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private ServiceProvider _serviceProvider = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _priceListAId;
    private Guid _priceListBId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddApplication();
        // PriceList.Create/Disable levantan eventos de dominio auditados (PriceListAuditHandler)
        // — se registra la misma infraestructura genérica de auditoría que usa el resto de la
        // app para que MediatR pueda resolver ese handler al hacer SaveChangesAsync.
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped(typeof(IAuditReader<>), typeof(EfAuditReader<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => _tenantId,
            () => _companyId,
            _userId
        ));
        services.AddDbContext<ErpDbContext>(
            (sp, options) => options.UseNpgsql(_postgres.GetConnectionString())
        );
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(() => _tenantId));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(() => _companyId));
        services.AddScoped<IPriceListCustomerRepository, PriceListCustomerRepository>();

        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var listA = PriceList.Create(_tenantId, _companyId, "A", "Lista A", "USD", isDefault: false, createdBy: _userId);
        var listB = PriceList.Create(_tenantId, _companyId, "B", "Lista B", "USD", isDefault: false, createdBy: _userId);
        db.PriceLists.AddRange(listA, listB);
        await db.SaveChangesAsync();
        _priceListAId = listA.Id;
        _priceListBId = listB.Id;
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task No_permite_dos_relaciones_activas_para_el_mismo_cliente_en_la_misma_empresa()
    {
        var customerId = Guid.NewGuid();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        db.PriceListCustomers.Add(
            PriceListCustomer.Create(_tenantId, _companyId, _priceListAId, customerId, _userId)
        );
        await db.SaveChangesAsync();

        db.PriceListCustomers.Add(
            PriceListCustomer.Create(_tenantId, _companyId, _priceListBId, customerId, _userId)
        );
        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Reactivar_la_misma_lista_no_viola_la_unicidad_de_activos()
    {
        var customerId = Guid.NewGuid();
        Guid assignmentId;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = PriceListCustomer.Create(_tenantId, _companyId, _priceListAId, customerId, _userId);
            db.PriceListCustomers.Add(assignment);
            await db.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = await db.PriceListCustomers.FirstAsync(a => a.Id == assignmentId);
            assignment.Disable(_userId);
            await db.SaveChangesAsync();
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = await db.PriceListCustomers.FirstAsync(a => a.Id == assignmentId);
            assignment.Enable(_userId);
            var act = async () => await db.SaveChangesAsync();
            await act.Should().NotThrowAsync();
        }
    }

    [Fact]
    public async Task Mismo_cliente_puede_tener_relaciones_activas_en_companies_distintas()
    {
        var customerId = Guid.NewGuid();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var otherCompany = Company.CreateManaged(_tenantId, "1790012345002", "Otra S.A.", createdBy: _userId);
        db.Companies.Add(otherCompany);
        var otherCompanyList = PriceList.Create(
            _tenantId, otherCompany.Id, "A", "Lista A (otra empresa)", "USD",
            isDefault: false, createdBy: _userId
        );
        db.PriceLists.Add(otherCompanyList);
        await db.SaveChangesAsync();

        db.PriceListCustomers.Add(
            PriceListCustomer.Create(_tenantId, _companyId, _priceListAId, customerId, _userId)
        );
        db.PriceListCustomers.Add(
            PriceListCustomer.Create(_tenantId, otherCompany.Id, otherCompanyList.Id, customerId, _userId)
        );
        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetByCustomerAsync_respeta_el_aislamiento_de_tenant_y_empresa()
    {
        // Mismo CustomerId Guid en ambos tenants — simula colisión de ids entre tenants
        // distintos (BusinessPartner es tenant-scoped, dos tenants nunca comparten identidad,
        // pero el Guid como tal podría coincidir) — el aislamiento debe sostenerse igual.
        var customerId = Guid.NewGuid();
        Guid otherTenantId;
        Guid otherCompanyId;
        Guid otherListId;

        await using (var seedScope = _serviceProvider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ErpDbContext>();

            db.PriceListCustomers.Add(
                PriceListCustomer.Create(_tenantId, _companyId, _priceListAId, customerId, _userId)
            );

            var otherTenant = Tenant.Create("Otro Tenant", $"other-{Guid.NewGuid():N}"[..16], _userId);
            db.Tenants.Add(otherTenant);
            var otherCompany = Company.CreateManaged(otherTenant.Id, "1790012345003", "Otro S.A.", createdBy: _userId);
            db.Companies.Add(otherCompany);
            await db.SaveChangesAsync();
            var otherList = PriceList.Create(
                otherTenant.Id, otherCompany.Id, "A", "Lista otro tenant", "USD",
                isDefault: false, createdBy: _userId
            );
            db.PriceLists.Add(otherList);
            await db.SaveChangesAsync();
            db.PriceListCustomers.Add(
                PriceListCustomer.Create(otherTenant.Id, otherCompany.Id, otherList.Id, customerId, _userId)
            );
            await db.SaveChangesAsync();

            otherTenantId = otherTenant.Id;
            otherCompanyId = otherCompany.Id;
            otherListId = otherList.Id;
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPriceListCustomerRepository>();
            var resultsForOurTenant = await repo.GetByCustomerAsync(_tenantId, customerId, CancellationToken.None);
            resultsForOurTenant.Should().ContainSingle().Which.PriceListId.Should().Be(_priceListAId);
        }

        // Cambia el contexto ambiental (Tenant+Company) a la "otra" empresa — mismo mecanismo
        // fail-closed (ForOperationalScope) que usa cualquier request real autenticado contra
        // esa empresa, nunca el mismo repositorio "viendo" ambos lados a la vez.
        _tenantId = otherTenantId;
        _companyId = otherCompanyId;
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPriceListCustomerRepository>();
            var resultsForOtherTenant = await repo.GetByCustomerAsync(otherTenantId, customerId, CancellationToken.None);
            resultsForOtherTenant.Should().ContainSingle().Which.PriceListId.Should().Be(otherListId);
        }
    }

    [Fact]
    public async Task Si_falla_la_nueva_asignacion_la_desactivacion_de_la_anterior_tambien_se_revierte()
    {
        // PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B: el "switch" (desactivar A + activar/crear B) se
        // hace con UN solo SaveChangesAsync — mismo mecanismo transaccional de EF/Postgres que ya
        // prueba esta suite para la unicidad. Se fuerza el fallo de la mitad "nueva" (FK a una
        // PriceList inexistente) para comprobar que la mitad "vieja" (Disable) no queda huérfana.
        var customerId = Guid.NewGuid();
        Guid assignmentId;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = PriceListCustomer.Create(_tenantId, _companyId, _priceListAId, customerId, _userId);
            db.PriceListCustomers.Add(assignment);
            await db.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = await db.PriceListCustomers.FirstAsync(a => a.Id == assignmentId);
            assignment.Disable(_userId);
            db.PriceListCustomers.Add(
                PriceListCustomer.Create(_tenantId, _companyId, Guid.NewGuid(), customerId, _userId)
            );

            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }

        await using (var verify = _serviceProvider.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
            var assignment = await db.PriceListCustomers.FirstAsync(a => a.Id == assignmentId);
            assignment.IsActive.Should().BeTrue("el Disable() de la mitad 'vieja' debe revertirse junto con el INSERT fallido");
        }
    }
}
