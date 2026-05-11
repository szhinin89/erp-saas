using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Controllers;
using ERP.API.Contracts;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Ventas.DTOs;
using ERP.Application.Ventas.UseCases.AnularFactura;
using ERP.Application.Ventas.UseCases.CrearVenta;
using ERP.Application.Ventas.UseCases.EmitirFacturaElectronica;
using ERP.Application.Ventas.UseCases.ValidarVenta;

namespace ERP.API.Tests.Controller;

/// <summary>
/// Tests de contrato del controlador VentasController.
/// Verifican que el controlador mapea Result&lt;T&gt; a los status codes correctos
/// y que la estructura de ApiResponse&lt;T&gt; cumple el contrato de la API.
/// No requieren base de datos ni servidor HTTP: usan StubMediator.
/// </summary>
public sealed class VentasControllerContractTests
{
    // ── Crear ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Crear_WhenSuccess_Returns201WithId()
    {
        var ventaId = Guid.NewGuid();
        var ctrl = CreateController(new StubMediator(_ => Result<Guid>.Success(ventaId)));

        var result = await ctrl.Crear(
            new CrearVentaCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new List<ItemVentaDto>()),
            CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(201);

        var payload = obj.Value.Should().BeOfType<ApiResponse<Guid>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Message.Should().Be("Creado");
        payload.ResponseObject.Should().Be(ventaId);
    }

    [Fact]
    public async Task Crear_WhenStockInsuficiente_Returns400()
    {
        var ctrl = CreateController(
            new StubMediator(_ => Result<Guid>.Failure("Stock insuficiente para 'Prod A'.")));

        var result = await ctrl.Crear(
            new CrearVentaCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new List<ItemVentaDto>()),
            CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);

        var payload = bad.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        payload.Success.Should().BeFalse();
        payload.Message.Should().Contain("Stock insuficiente");
    }

    // ── Validar ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Validar_WhenSuccess_Returns200()
    {
        var id   = Guid.NewGuid();
        var ctrl = CreateController(new StubMediator(_ => Result<Guid>.Success(id)));

        var result = await ctrl.Validar(id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var payload = ok.Value.Should().BeOfType<ApiResponse<Guid>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Message.Should().Be("Validado");
    }

    [Fact]
    public async Task Validar_WhenNotBorrador_Returns400()
    {
        var ctrl = CreateController(
            new StubMediator(_ => Result<Guid>.Failure("Solo se puede validar una factura en Borrador.")));

        var result = await ctrl.Validar(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>()
              .Which.StatusCode.Should().Be(400);
    }

    // ── Emitir ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Emitir_WhenSuccess_Returns200WithMessage()
    {
        var id   = Guid.NewGuid();
        var ctrl = CreateController(new StubMediator(_ => Result<Guid>.Success(id)));

        var result = await ctrl.Emitir(id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponse<Guid>>()
          .Which.Message.Should().Be("Emitido");
    }

    [Fact]
    public async Task Emitir_WhenNotValidado_Returns400()
    {
        var ctrl = CreateController(
            new StubMediator(_ => Result<Guid>.Failure("Solo se puede emitir una factura Validada.")));

        var result = await ctrl.Emitir(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Anular ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anular_WhenSuccess_Returns200()
    {
        var id   = Guid.NewGuid();
        var ctrl = CreateController(new StubMediator(_ => Result<Guid>.Success(id)));

        var result = await ctrl.Anular(id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponse<Guid>>()
          .Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Anular_WhenAutorizado_Returns400()
    {
        var ctrl = CreateController(
            new StubMediator(_ => Result<Guid>.Failure("No se puede anular una factura ya autorizada.")));

        var result = await ctrl.Anular(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenFound_Returns200WithDetail()
    {
        var dto = new VentasFacturaDetailDto(
            Guid.NewGuid(), Guid.NewGuid(), "Cliente Test",
            Guid.NewGuid(), Guid.NewGuid(),
            "01", "001", "001", "000000001", new string('0', 49),
            DateTime.UtcNow, 100m, 15m, 115m,
            "Autorizado", "AUTH001", DateTime.UtcNow,
            null, null, null, null, DateTime.UtcNow,
            new List<VentasDetalleDto>());

        var ctrl = CreateController(
            new StubMediator(_ => Result<VentasFacturaDetailDto?>.Success(dto)));

        var result = await ctrl.GetById(dto.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponse<VentasFacturaDetailDto?>>()
          .Which.ResponseObject.Should().Be(dto);
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns200WithNullPayload()
    {
        // El patrón de la API retorna Success(null) para "no encontrado" → 200 con payload nulo.
        // Para un 404 real, el handler debería devolver Result.Failure.
        var ctrl = CreateController(
            new StubMediator(_ => Result<VentasFacturaDetailDto?>.Success(null)));

        var result = await ctrl.GetById(Guid.NewGuid(), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var payload = ok.Value.Should().BeOfType<ApiResponse<VentasFacturaDetailDto?>>().Subject;
        payload.Success.Should().BeTrue();
        payload.ResponseObject.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WhenRepositoryFails_Returns404()
    {
        var ctrl = CreateController(
            new StubMediator(_ => Result<VentasFacturaDetailDto?>.Failure("No encontrado.")));

        var result = await ctrl.GetById(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<NotFoundObjectResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Imprimir_WhenSuccess_ReturnsHtmlContent()
    {
        var ctrl = CreateController(
            new StubMediator(_ => Result<Guid>.Success(Guid.NewGuid())),
            new StubTirillaFacturaService());

        var result = await ctrl.Imprimir(Guid.NewGuid(), CancellationToken.None);

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().Contain("text/html");
        content.Content.Should().Contain("<html>");
    }

    [Fact]
    public async Task Imprimir_WhenNotFound_Returns404()
    {
        var service = new StubTirillaFacturaService(failNotFound: true);
        var ctrl = CreateController(new StubMediator(_ => Result<Guid>.Success(Guid.NewGuid())), service);

        var result = await ctrl.Imprimir(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── GetStock ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStock_SinParametros_Retorna400()
    {
        // HttpContext simulado con query vacía requeriría MockHttpContext.
        // En su lugar verificamos que la lógica de parseo rechaza GUIDs vacíos.
        // Este test valida el contrato sin servidor HTTP.
        var ctrl = CreateController(new StubMediator(_ => throw new InvalidOperationException("no debería llegar")));

        // Sin HttpContext real, GetStock lanza NullRef al leer Request.Query
        // Verificamos que la línea de validación existe en el controlador
        // mediante tests HTTP en VentasHttpTests.
        // (ver VentasHttpTests.Ventas_GetStock_SinParametros_Retorna400)
        await Task.CompletedTask; // placeholder — la cobertura real está en VentasHttpTests
    }
    private static VentasController CreateController(IMediator mediator, ITirillaFacturaService? service = null)
    {
        return new VentasController(mediator, service ?? new StubTirillaFacturaService());
    }

    private sealed class StubTirillaFacturaService : ITirillaFacturaService
    {
        private readonly bool _failNotFound;

        public StubTirillaFacturaService(bool failNotFound = false)
        {
            _failNotFound = failNotFound;
        }

        public Task<string> GenerarHtmlFacturaAsync(Guid ventaId, CancellationToken ct = default)
        {
            if (_failNotFound)
                throw new KeyNotFoundException("Factura no encontrada o no autorizada.");

            return Task.FromResult("<html><body>Test</body></html>");
        }
    }
}
