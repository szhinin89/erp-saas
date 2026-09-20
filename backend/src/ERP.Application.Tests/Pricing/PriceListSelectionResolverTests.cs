using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Pricing.Services;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// PRICING-PRICE-LIST-SELECTION-CANDIDATES-05A1 — IPriceListSelectionResolver devuelve una
/// LISTA ORDENADA de candidatos (Customer primero, CompanyDefault después), nunca una única
/// "ganadora": la lista del cliente puede no tener asignado (PriceListItem) el ítem que se está
/// vendiendo, y esa verificación NO le corresponde a este resolver (sería acoplar selección de
/// lista con PricingCalculation/PricingResolver). Todavía NO integrado con Sales.
/// </summary>
public sealed class PriceListSelectionResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateOnly CompanyToday = new(2026, 9, 20);

    private sealed class Fixture
    {
        public Mock<IPriceListCustomerRepository> CustomerLists { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICompanyClock> CompanyClock { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            CompanyClock
                .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CompanyToday);
            CustomerLists
                .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PriceListCustomer>());
        }

        public PriceListSelectionResolver Build() =>
            new(CustomerLists.Object, PriceLists.Object, Tenant.Object, Company.Object, CompanyClock.Object);
    }

    private static PriceList CreateList(
        string code,
        bool isDefault,
        DateOnly? validFrom = null,
        DateOnly? validUntil = null
    ) =>
        PriceList.Create(
            TenantId, CompanyId, code, $"Lista {code}", "USD",
            isDefault: isDefault, createdBy: UserId,
            validFrom: validFrom, validUntil: validUntil
        );

    [Fact]
    public async Task Cliente_con_lista_propia_y_default_distintas_devuelve_2_candidatos_en_orden()
    {
        var f = new Fixture();
        var customerList = CreateList("VIP", isDefault: false);
        var defaultList = CreateList("GEN", isDefault: true);
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, customerList.Id, CustomerId, UserId);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, customerList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerList);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].PriceListId.Should().Be(customerList.Id);
        result[0].Source.Should().Be(PriceListSelectionSource.Customer);
        result[1].PriceListId.Should().Be(defaultList.Id);
        result[1].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Cliente_sin_relacion_devuelve_solo_el_candidato_default()
    {
        var f = new Fixture();
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].PriceListId.Should().Be(defaultList.Id);
        result[0].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Lista_del_cliente_inaplicable_devuelve_solo_el_candidato_default()
    {
        var f = new Fixture();
        var customerList = CreateList("VIP", isDefault: false);
        customerList.Disable(UserId);
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, customerList.Id, CustomerId, UserId);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, customerList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerList);
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].PriceListId.Should().Be(defaultList.Id);
        result[0].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Relacion_deshabilitada_devuelve_solo_el_candidato_default()
    {
        var f = new Fixture();
        var customerList = CreateList("VIP", isDefault: false);
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, customerList.Id, CustomerId, UserId);
        assignment.Disable(UserId);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Lista_del_cliente_vencida_devuelve_solo_el_candidato_default()
    {
        var f = new Fixture();
        var customerList = CreateList("VIP", isDefault: false,
            validFrom: new DateOnly(2026, 1, 1), validUntil: new DateOnly(2026, 9, 19));
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, customerList.Id, CustomerId, UserId);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, customerList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerList);
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Lista_del_cliente_futura_devuelve_solo_el_candidato_default()
    {
        var f = new Fixture();
        var customerList = CreateList("VIP", isDefault: false,
            validFrom: new DateOnly(2026, 10, 1), validUntil: new DateOnly(2026, 12, 31));
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, customerList.Id, CustomerId, UserId);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, customerList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerList);
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
    }

    [Fact]
    public async Task Cliente_igual_a_default_devuelve_un_unico_candidato_sin_duplicar()
    {
        var f = new Fixture();
        var sharedList = CreateList("GEN", isDefault: true);
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, sharedList.Id, CustomerId, UserId);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, sharedList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedList);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(sharedList);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].PriceListId.Should().Be(sharedList.Id);
        // Cuando ambas coinciden, el origen reportado es Customer (más específico) — nunca se
        // reporta dos veces la misma PriceList con orígenes distintos.
        result[0].Source.Should().Be(PriceListSelectionSource.Customer);
    }

    [Fact]
    public async Task Sin_lista_de_cliente_y_sin_default_valida_devuelve_coleccion_vacia()
    {
        var f = new Fixture();
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((PriceList?)null);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Lista_de_cliente_inaplicable_y_default_tambien_inaplicable_devuelve_coleccion_vacia()
    {
        var f = new Fixture();
        var customerList = CreateList("VIP", isDefault: false);
        customerList.Disable(UserId);
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, customerList.Id, CustomerId, UserId);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, customerList.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerList);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((PriceList?)null);

        var result = await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Usa_CompanyClock_para_vigencia_nunca_DateTime_UtcNow_directo()
    {
        var f = new Fixture();
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        f.CompanyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Resuelve_siempre_con_el_TenantId_y_CompanyId_ambientales_fail_closed()
    {
        var f = new Fixture();
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        await f.Build().ResolveAsync(CustomerId, CancellationToken.None);

        f.CustomerLists.Verify(
            r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.PriceLists.Verify(
            r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Company.VerifyGet(c => c.CompanyId, Times.AtLeastOnce);
    }

    [Fact]
    public async Task CustomerId_null_omite_la_consulta_de_cliente_y_devuelve_solo_default()
    {
        // PRICING-CONTEXT-NULL-CUSTOMER-05C1: null es "sin cliente" — nunca consulta
        // PriceListCustomer, ni siquiera con un Guid.Empty sentinel.
        var f = new Fixture();
        var defaultList = CreateList("GEN", isDefault: true);
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(null, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].PriceListId.Should().Be(defaultList.Id);
        result[0].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
        f.CustomerLists.Verify(
            r => r.GetByCustomerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CustomerId_null_y_sin_default_valida_devuelve_coleccion_vacia()
    {
        var f = new Fixture();
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync((PriceList?)null);

        var result = await f.Build().ResolveAsync(null, CancellationToken.None);

        result.Should().BeEmpty();
        f.CustomerLists.Verify(
            r => r.GetByCustomerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Guid_Empty_explicito_se_trata_como_cualquier_otro_customerId_sin_caso_especial()
    {
        // PRICING-CONTEXT-NULL-CUSTOMER-05C1: Guid.Empty NO es un sentinel mágico de "sin
        // cliente" — se consulta PriceListCustomer normalmente para ese id; como ningún cliente
        // real tiene ese Guid, simplemente no encuentra asignación (mismo resultado que
        // Cliente_sin_relacion_devuelve_solo_el_candidato_default, pero con Guid.Empty explícito).
        var f = new Fixture();
        var defaultList = CreateList("GEN", isDefault: true);
        f.CustomerLists
            .Setup(r => r.GetByCustomerAsync(TenantId, Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceListCustomer>());
        f.PriceLists.Setup(r => r.GetDefaultAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultList);

        var result = await f.Build().ResolveAsync(Guid.Empty, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Source.Should().Be(PriceListSelectionSource.CompanyDefault);
        f.CustomerLists.Verify(
            r => r.GetByCustomerAsync(TenantId, Guid.Empty, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
