using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-INVENTORY-STOCK-ITEM-LOOKUP-01 — <c>GET /api/v1/items?tracksStock=</c>: la búsqueda canónica
/// de Items filtra por control de stock en la fuente (antes del orden y de la paginación), para
/// que el picker de Ajustes/Transferencias no pierda un ítem válido detrás de una página de
/// coincidencias sin stock. Sin el parámetro, el endpoint responde igual que antes.
/// HTTP real + PostgreSQL real.
/// </summary>
public sealed class ItemLookupStockFilterFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private Guid _itemTypeId;
    private Guid _foreignItemTypeId;

    public WebApplicationFactory<Program> App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid ForeignTenantId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();

        Guid companyId,
            userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tenant = Tenant.Create("ZH-StockLookup", $"zh-sl-{Guid.NewGuid():N}", _adminId);
            var foreign = Tenant.Create(
                "ZH-StockLookup-Otro",
                $"zh-so-{Guid.NewGuid():N}",
                _adminId
            );
            db.Tenants.AddRange(tenant, foreign);
            await db.SaveChangesAsync();
            TenantId = tenant.Id;
            ForeignTenantId = foreign.Id;

            var company = Company.CreateManaged(
                TenantId,
                $"179{TenantId:N}"[..13],
                "Empresa Stock Lookup",
                createdBy: _adminId
            );
            db.Companies.Add(company);
            var user = IdentityUser.Create(
                $"sl-{Guid.NewGuid():N}"[..12],
                "Inv",
                "Test",
                $"sl-{Guid.NewGuid():N}@test.com",
                "TEST_PASSWORD_HASH",
                _adminId
            );
            db.IdentityUsers.Add(user);
            var itemType = ItemTypeDefinition.Create(TenantId, "MERCH", "Mercadería", 1, _adminId);
            var foreignType = ItemTypeDefinition.Create(
                ForeignTenantId,
                "MERCH",
                "Mercadería",
                1,
                _adminId
            );
            db.Set<ItemTypeDefinition>().AddRange(itemType, foreignType);
            await db.SaveChangesAsync();
            db.CompanyUserMemberships.Add(
                CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, _adminId)
            );
            await db.SaveChangesAsync();
            companyId = company.Id;
            userId = user.Id;
            _itemTypeId = itemType.Id;
            _foreignItemTypeId = foreignType.Id;
        }

        App = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()
            )
        );
        Client = App.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, companyId, "Admin")
        );
        _factory.MutableTenant.TenantId = TenantId;
        _factory.MutableCompany.CompanyId = companyId;
        _factory.MutableUser.UserId = userId;
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
        await _factory.DisposeAsync();
    }

    /// <summary>Crea un ítem; <paramref name="foreign"/> = en otro tenant.</summary>
    public async Task SeedAsync(
        string sku,
        bool tracksStock,
        string? description = null,
        bool active = true,
        bool foreign = false
    )
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var item = Item.Create(
            foreign ? ForeignTenantId : TenantId,
            sku,
            shortName: $"Producto {sku}",
            description: description ?? $"Descripción {sku}",
            itemTypeId: foreign ? _foreignItemTypeId : _itemTypeId,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: null),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: tracksStock),
            createdBy: _adminId
        );
        if (!active)
            item.Disable(_adminId);
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
    }
}

[Trait("Category", "PostgreSql")]
public sealed class ItemLookupStockFilterHttpTests : IClassFixture<ItemLookupStockFilterFixture>
{
    private readonly ItemLookupStockFilterFixture _f;

    public ItemLookupStockFilterHttpTests(ItemLookupStockFilterFixture f) => _f = f;

    private sealed record Row(string Sku, bool TracksStock);

    private sealed record Page(List<Row> Items, int TotalCount);

    private async Task<Page> GetAsync(string query)
    {
        var response = await _f.Client.GetAsync($"/api/v1/items?{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, text);
        var data = JsonDocument.Parse(text).RootElement.GetProperty("data");
        var items = data.GetProperty("items")
            .EnumerateArray()
            .Select(i => new Row(
                i.GetProperty("sku").GetString()!,
                i.GetProperty("tracksStock").GetBoolean()
            ))
            .ToList();
        return new Page(items, data.GetProperty("totalCount").GetInt32());
    }

    private static string Term() => $"L{Guid.NewGuid():N}"[..9].ToUpperInvariant();

    /// <summary>12 coincidencias sin stock que ordenan antes (A01..A12) y una con stock al final (Z01).</summary>
    private async Task<string> SeedHiddenStockItemAsync()
    {
        var term = Term();
        for (var i = 1; i <= 12; i++)
            await _f.SeedAsync($"{term}-A{i:D2}", tracksStock: false);
        await _f.SeedAsync($"{term}-Z01", tracksStock: true);
        return term;
    }

    [Fact]
    public async Task Sin_filtro_el_resultado_no_cambia_y_la_primera_pagina_no_trae_el_item_con_stock()
    {
        var term = await SeedHiddenStockItemAsync();

        var page = await GetAsync($"search={term}&isActive=true&pageSize=12");

        // Reproducción de la causa: filtrar esta página en el cliente deja 0 ítems con stock aunque
        // exista uno (Z01) — está en la página 2 del orden por SKU.
        page.TotalCount.Should().Be(13);
        page.Items.Should().HaveCount(12);
        page.Items.Should().OnlyContain(r => !r.TracksStock);
        page.Items.Select(r => r.Sku).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task Con_tracksStock_true_solo_devuelve_items_con_control_de_stock_desde_la_primera_pagina()
    {
        var term = await SeedHiddenStockItemAsync();

        var page = await GetAsync($"search={term}&isActive=true&pageSize=12&tracksStock=true");

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Should().Be(new Row($"{term}-Z01", true));
    }

    [Fact]
    public async Task Con_tracksStock_false_solo_devuelve_items_sin_control_de_stock()
    {
        var term = await SeedHiddenStockItemAsync();

        var page = await GetAsync($"search={term}&isActive=true&pageSize=50&tracksStock=false");

        page.TotalCount.Should().Be(12);
        page.Items.Should().OnlyContain(r => !r.TracksStock);
    }

    [Fact]
    public async Task La_paginacion_y_el_orden_se_aplican_despues_del_filtro()
    {
        var term = Term();
        await _f.SeedAsync($"{term}-A01", tracksStock: false);
        await _f.SeedAsync($"{term}-B01", tracksStock: true);
        await _f.SeedAsync($"{term}-C01", tracksStock: false);
        await _f.SeedAsync($"{term}-D01", tracksStock: true);
        await _f.SeedAsync($"{term}-E01", tracksStock: true);

        var first = await GetAsync(
            $"search={term}&isActive=true&tracksStock=true&pageSize=2&pageNumber=1"
        );
        var second = await GetAsync(
            $"search={term}&isActive=true&tracksStock=true&pageSize=2&pageNumber=2"
        );

        first.TotalCount.Should().Be(3);
        first.Items.Select(r => r.Sku).Should().Equal($"{term}-B01", $"{term}-D01");
        second.Items.Select(r => r.Sku).Should().Equal($"{term}-E01");
    }

    [Fact]
    public async Task Busqueda_por_descripcion_combinada_con_el_filtro_e_isActive()
    {
        var term = Term();
        await _f.SeedAsync(
            $"X{Guid.NewGuid():N}"[..12],
            tracksStock: true,
            description: $"Arroz {term} grano"
        );
        await _f.SeedAsync(
            $"Y{Guid.NewGuid():N}"[..12],
            tracksStock: false,
            description: $"Arroz {term} servicio"
        );
        await _f.SeedAsync(
            $"W{Guid.NewGuid():N}"[..12],
            tracksStock: true,
            description: $"Arroz {term} inactivo",
            active: false
        );

        var page = await GetAsync($"search={term}&isActive=true&tracksStock=true");

        page.Items.Should().ContainSingle().Which.TracksStock.Should().BeTrue();
    }

    [Fact]
    public async Task El_filtro_no_cruza_tenants()
    {
        var term = Term();
        await _f.SeedAsync($"{term}-OWN", tracksStock: true);
        await _f.SeedAsync($"{term}-FOREIGN", tracksStock: true, foreign: true);

        var page = await GetAsync($"search={term}&isActive=true&tracksStock=true");

        page.Items.Select(r => r.Sku).Should().Equal($"{term}-OWN");
    }
}
