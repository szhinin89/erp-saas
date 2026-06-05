using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Tests HTTP end-to-end: WebApplicationFactory + JWT simulado + rutas reales de VentasController.
/// Verifican status codes, Content-Type y estructura de la respuesta JSON.
/// </summary>
public sealed class SalesInvoicesHttpTests
{
    // Ã¢â€â‚¬Ã¢â€â‚¬ Setup Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private static async Task<(IntegrationTestWebAppFactory Factory, System.Net.Http.HttpClient Client, IntegrationSeedData.SeedResult Seed)>
        CreateClientAsync(decimal stockInicial = 10m)
    {
        var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await SalesEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial, ct: CancellationToken.None);

        var token  = TestJwtFactory.CreateSessionJwt(seed.SubscriberId, seed.UserId, seed.CompanyId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return (factory, client, seed);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ GET /api/sales/invoices Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public async Task Ventas_GetAll_con_admin_token_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/sales/invoices");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }

    [Fact]
    public async Task Ventas_GetAll_sin_token_responde_401()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/sales/invoices");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ventas_GetAll_con_filtro_estado_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/sales/invoices?Status = Borrador&pageSize=5");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ GET /api/sales/invoices/{id} Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public async Task Ventas_GetById_factura_inexistente_responde_200_con_payload_nulo()
    {
        // El handler retorna Success(null) para "no encontrado" Ã¢â€ â€™ 200 con payload null.
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync($"/api/sales/invoices/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
        body.Should().Contain("\"responseObject\":null");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ POST /api/sales/invoices Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public async Task Ventas_Crear_con_payload_valido_responde_201()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        using var scope = factory.Services.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var clienteId  = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        var payload = new
        {
            businessPartnerId = clienteId,
            warehouseId = seed.WarehouseId,
            branchId = sucursalId,
            items = new[] { new { productId = seed.ProductId, quantity = 1m, unitPrice = 10.0m } }
        };

        var res = await client.PostAsJsonAsync("/api/sales/invoices", payload);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
        body.Should().Contain("\"message\":\"Creado\"");
    }

    [Fact]
    public async Task Ventas_Crear_con_body_invalido_responde_422_o_400()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        // Enviar command con Items vacÃƒÂ­os Ã¢â‚¬â€ FluentValidation deberÃƒÂ­a rechazar
        var payload = new
        {
            clienteId  = Guid.Empty,
            bodegaId   = Guid.Empty,
            sucursalId = Guid.Empty,
            items      = Array.Empty<object>()
        };

        var res = await client.PostAsJsonAsync("/api/sales/invoices", payload);

        // FluentValidation lanza ValidationException Ã¢â€ â€™ ExceptionMiddleware Ã¢â€ â€™ 422
        ((int)res.StatusCode).Should().BeOneOf(400, 422);
        var body = await res.Content.ReadAsStringAsync();
        // ExceptionMiddleware usa formato {status, message, errors} en 422
        // ApiResponse usa {success, message} en 400 Ã¢â‚¬â€ ambos incluyen "message"
        body.Should().Contain("message");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ GET /api/sales/invoices/stock Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public async Task Ventas_GetStock_SinParametros_Retorna400()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/sales/invoices/stock");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("productoId");
    }

    [Fact]
    public async Task Ventas_GetStock_con_producto_y_bodega_validos_retorna_200()
    {
        var (factory, client, seed) = await CreateClientAsync(stockInicial: 5m);
        await using var _ = factory;

        var res = await client.GetAsync(
            $"/api/sales/invoices/stock?productoId={seed.ProductId}&bodegaId={seed.WarehouseId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var ro = doc.RootElement.GetProperty("responseObject");
        ro.GetProperty("availableQty").GetDecimal().Should().Be(5m);
        ro.GetProperty("totalQty").GetDecimal().Should().Be(5m);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ PATCH /api/sales/invoices/{id}/validar Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public async Task Ventas_Validar_factura_borrador_responde_200()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        using var scope    = factory.Services.CreateScope();
        var db             = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator       = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        var clienteId      = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId     = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        var crear = await mediator.Send(
            new ERP.Application.Sales.UseCases.CreateSale.CreateSaleCommand(
                clienteId, seed.WarehouseId, sucursalId,
                new List<ERP.Application.Sales.UseCases.CreateSale.SaleItemDto>
                    { new(seed.ProductId, 1m, 10m) }),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var res = await client.PatchAsync($"/api/sales/invoices/{crear.Value}/validar", null);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
        body.Should().Contain("\"message\":\"Validado\"");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ GET /api/configuracion-sri Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public async Task ConfiguracionSRI_Get_tenant_configurado_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/settings/sri");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"companyRuc\":\"9999999999999\"");
    }

    [Fact]
    public async Task Ventas_Imprimir_factura_autorizada_responde_html_con_configuracion_facturacion()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var clienteId = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var branchId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        var config = SubscriberBillingProfile.Create(
            subscriberId:        seed.SubscriberId,
            identificationType:  "04",
            identificationNumber: "1790016910001",
            legalName:           "Razon Social Test",
            address:             "Av. Prueba 123",
            createdBy:           seed.UserId,
            tradeName:           "Comercial Test",
            phone:               "0999999999",
            email:               "test@example.com",
            requiresAccounting:  true,
            specialTaxpayer:     "1234",
            footerText:          "Gracias por su compra",
            receiptWidth:        80);

        db.SubscriberBillingProfiles.Add(config);

        var facturaId = await SalesEndToEndHelpers.SeedAuthorizedInvoiceAsync(
            db,
            seed,
            clienteId,
            seed.CompanyId,
            subtotal: 100m,
            taxTotal: 12m,
            total: 112m,
            sequential: "000000001",
            lineDescription: "Producto de prueba",
            ct: CancellationToken.None);

        var res = await client.GetAsync($"/api/sales/invoices/{facturaId}/imprimir");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        var html = await res.Content.ReadAsStringAsync();
        html.Should().Contain("<html");
        html.Should().Contain("Razon Social Test");
        html.Should().Contain("Comercial Test");
        html.Should().Contain("Gracias por su compra");
        html.Should().Contain("Producto de prueba");
        html.Should().Contain("max-width: 80mm");
    }

    [Fact]
    public async Task Ventas_Imprimir_factura_autorizada_sin_config_facturacion_usa_valores_default()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var clienteId = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var branchId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        var facturaId = await SalesEndToEndHelpers.SeedAuthorizedInvoiceAsync(
            db,
            seed,
            clienteId,
            seed.CompanyId,
            subtotal: 50m,
            taxTotal: 6m,
            total: 56m,
            sequential: "000000002",
            lineDescription: "Item default",
            ct: CancellationToken.None);

        var res = await client.GetAsync($"/api/sales/invoices/{facturaId}/imprimir");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await res.Content.ReadAsStringAsync();
        html.Should().Contain("<html");
        html.Should().Contain("DEMO COMPANY");
        html.Should().Contain("Item default");
    }
}










