using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Ventas.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Ventas.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Tests HTTP end-to-end: WebApplicationFactory + JWT simulado + rutas reales de VentasController.
/// Verifican status codes, Content-Type y estructura de la respuesta JSON.
/// </summary>
public sealed class VentasHttpTests
{
    // ── Setup ─────────────────────────────────────────────────────────────

    private static async Task<(IntegrationTestWebAppFactory Factory, System.Net.Http.HttpClient Client, IntegrationSeedData.SeedResult Seed)>
        CreateClientAsync(decimal stockInicial = 10m)
    {
        var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial, ct: CancellationToken.None);

        var token  = TestJwtFactory.CreateSessionJwt(seed.TenantId, seed.UserId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return (factory, client, seed);
    }

    // ── GET /api/ventas ───────────────────────────────────────────────────

    [Fact]
    public async Task Ventas_GetAll_con_admin_token_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/ventas");

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

        var res = await client.GetAsync("/api/ventas");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ventas_GetAll_con_filtro_estado_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/ventas?estado=Borrador&pageSize=5");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }

    // ── GET /api/ventas/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task Ventas_GetById_factura_inexistente_responde_200_con_payload_nulo()
    {
        // El handler retorna Success(null) para "no encontrado" → 200 con payload null.
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync($"/api/ventas/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
        body.Should().Contain("\"responseObject\":null");
    }

    // ── POST /api/ventas ──────────────────────────────────────────────────

    [Fact]
    public async Task Ventas_Crear_con_payload_valido_responde_201()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        using var scope = factory.Services.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        var payload = new
        {
            clienteId  = clienteId,
            bodegaId   = seed.BodegaId,
            sucursalId = sucursalId,
            items      = new[] { new { productoId = seed.ProductId, cantidad = 1, precioUnitario = 10.0 } }
        };

        var res = await client.PostAsJsonAsync("/api/ventas", payload);

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

        // Enviar command con Items vacíos — FluentValidation debería rechazar
        var payload = new
        {
            clienteId  = Guid.Empty,
            bodegaId   = Guid.Empty,
            sucursalId = Guid.Empty,
            items      = Array.Empty<object>()
        };

        var res = await client.PostAsJsonAsync("/api/ventas", payload);

        // FluentValidation lanza ValidationException → ExceptionMiddleware → 422
        ((int)res.StatusCode).Should().BeOneOf(400, 422);
        var body = await res.Content.ReadAsStringAsync();
        // ExceptionMiddleware usa formato {status, message, errors} en 422
        // ApiResponse usa {success, message} en 400 — ambos incluyen "message"
        body.Should().Contain("message");
    }

    // ── GET /api/ventas/stock ─────────────────────────────────────────────

    [Fact]
    public async Task Ventas_GetStock_SinParametros_Retorna400()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/ventas/stock");
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
            $"/api/ventas/stock?productoId={seed.ProductId}&bodegaId={seed.BodegaId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var ro = doc.RootElement.GetProperty("responseObject");
        ro.GetProperty("cantidadDisponible").GetDecimal().Should().Be(5m);
        ro.GetProperty("cantidadTotal").GetDecimal().Should().Be(5m);
    }

    // ── PATCH /api/ventas/{id}/validar ────────────────────────────────────

    [Fact]
    public async Task Ventas_Validar_factura_borrador_responde_200()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        using var scope    = factory.Services.CreateScope();
        var db             = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator       = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        var clienteId      = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId     = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        var crear = await mediator.Send(
            new ERP.Application.Ventas.UseCases.CrearVenta.CrearVentaCommand(
                clienteId, seed.BodegaId, sucursalId,
                new List<ERP.Application.Ventas.UseCases.CrearVenta.ItemVentaDto>
                    { new(seed.ProductId, 1m, 10m) }),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var res = await client.PatchAsync($"/api/ventas/{crear.Value}/validar", null);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
        body.Should().Contain("\"message\":\"Validado\"");
    }

    // ── GET /api/configuracion-sri ────────────────────────────────────────

    [Fact]
    public async Task ConfiguracionSRI_Get_tenant_configurado_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/configuracion-sri");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"rucEmpresa\":\"9999999999999\"");
    }

    [Fact]
    public async Task Ventas_Imprimir_factura_autorizada_responde_html_con_configuracion_facturacion()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var clienteId = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var branchId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        var config = ConfiguracionFacturacion.Create(
            tenantId: seed.TenantId,
            razonSocial: "Razon Social Test",
            nombreComercial: "Comercial Test",
            ruc: "1790016910001",
            direccionMatriz: "Av. Prueba 123",
            telefono: "0999999999",
            correo: "test@example.com",
            obligadoContabilidad: true,
            contribuyenteEspecial: "1234",
            logoBase64: null,
            leyendaAdicional: "Gracias por su compra",
            anchoTirilla: 80,
            createdBy: seed.UserId);

        db.ConfiguracionFacturaciones.Add(config);

        var factura = VentasFactura.Create(
            seed.TenantId,
            branchId,
            clienteId,
            seed.BodegaId,
            tipoDocumento: "01",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            claveAcceso: new string('1', 48),
            fechaEmision: DateTime.UtcNow,
            subtotal: 100m,
            impuesto: 12m,
            total: 112m,
            xmlGeneradoPath: null,
            xmlAutorizacionPath: null,
            numeroAutorizacion: null,
            fechaAutorizacion: null,
            mensajeError: null,
            createdBy: seed.UserId);

        var detalle = VentasDetalle.Create(
            seed.TenantId,
            seed.ProductId,
            cantidad: 1m,
            precioUnitario: 100m,
            impuesto: 12m,
            descripcion: "Producto de prueba",
            createdBy: seed.UserId);
        detalle.AsignarFacturaId(factura.Id);
        factura.AgregarDetalle(detalle);
        factura.Validar(seed.UserId);
        factura.Autorizar(
            seed.UserId,
            numeroAutorizacion: "AUTH-123",
            fechaAutorizacion: DateTime.UtcNow,
            xmlGeneradoPath: null,
            xmlAutorizacionPath: null,
            asientoId: Guid.NewGuid());

        db.VentasFacturas.Add(factura);
        await db.SaveChangesAsync(CancellationToken.None);

        var res = await client.GetAsync($"/api/ventas/{factura.Id}/imprimir");

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

        var clienteId = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var branchId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        var factura = VentasFactura.Create(
            seed.TenantId,
            branchId,
            clienteId,
            seed.BodegaId,
            tipoDocumento: "01",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000002",
            claveAcceso: new string('1', 48),
            fechaEmision: DateTime.UtcNow,
            subtotal: 50m,
            impuesto: 6m,
            total: 56m,
            xmlGeneradoPath: null,
            xmlAutorizacionPath: null,
            numeroAutorizacion: null,
            fechaAutorizacion: null,
            mensajeError: null,
            createdBy: seed.UserId);

        var detalle = VentasDetalle.Create(
            seed.TenantId,
            seed.ProductId,
            cantidad: 1m,
            precioUnitario: 50m,
            impuesto: 6m,
            descripcion: "Item default",
            createdBy: seed.UserId);
        detalle.AsignarFacturaId(factura.Id);
        factura.AgregarDetalle(detalle);
        factura.Validar(seed.UserId);
        factura.Autorizar(
            seed.UserId,
            numeroAutorizacion: "AUTH-456",
            fechaAutorizacion: DateTime.UtcNow,
            xmlGeneradoPath: null,
            xmlAutorizacionPath: null,
            asientoId: Guid.NewGuid());

        db.VentasFacturas.Add(factura);
        await db.SaveChangesAsync(CancellationToken.None);

        var res = await client.GetAsync($"/api/ventas/{factura.Id}/imprimir");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await res.Content.ReadAsStringAsync();
        html.Should().Contain("<html");
        html.Should().Contain("EMPRESA DEMO");
        html.Should().Contain("Item default");
    }
}
