using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Modules.Cash.UseCases;
using ERP.Domain.Modules.Cash;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>Conciliación bancaria, caja chica y flujo de efectivo (tenant).</summary>
[Modulo("Caja y bancos", "perm:caja.extractos.view", "🏦", "/caja", null, 60)]
[ApiController]
[Route("api/caja")]
[Authorize]
[Produces("application/json")]
public sealed class CajaController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExtractoParser _parser;

    public CajaController(IMediator mediator, IExtractoParser parser)
    {
        _mediator = mediator;
        _parser   = parser;
    }

    [HttpGet("cuentas-bancarias")]
    [Authorize(Policy = "perm:caja.extractos.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CuentaBancariaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCuentasBancarias(CancellationToken ct)
    {
        var r = await _mediator.Send(new ListCuentasBancariasQuery(), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<CuentaBancariaDto>());
    }

    [HttpPost("cuentas-bancarias")]
    [Authorize(Policy = "perm:caja.extractos.create")]
    [ProducesResponseType(typeof(ApiResponse<CuentaBancariaDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CrearCuentaBancaria([FromBody] CrearCuentaBancariaRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearCuentaBancariaCommand(
                body.Nombre,
                body.NumeroCuenta,
                body.TipoCuenta,
                body.Moneda,
                body.SaldoInicial,
                body.CuentaContableId),
            ct);
        return this.ToCreatedOrBadRequest(r, "Creado");
    }

    [HttpGet("extractos")]
    [Authorize(Policy = "perm:caja.extractos.view")]
    public async Task<IActionResult> ListExtractos([FromQuery] Guid cuentaBancariaId, CancellationToken ct)
    {
        var r = await _mediator.Send(new ListExtractosPorCuentaQuery(cuentaBancariaId), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<ExtractoBancarioDto>());
    }

    [HttpGet("extractos/{id:guid}")]
    [Authorize(Policy = "perm:caja.extractos.view")]
    public async Task<IActionResult> GetExtracto(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetExtractoDetalleQuery(id), ct);
        return this.ToOkOrBadRequest(r);
    }

    [HttpPost("extractos/importar")]
    [Authorize(Policy = "perm:caja.extractos.create")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportarExtracto([FromForm] ImportarExtractoForm form, CancellationToken ct)
    {
        if (form.Archivo is null || form.Archivo.Length == 0)
            return BadRequest(new ApiResponse<object>(false, "Archivo vacío.", new { }));

        await using var stream = form.Archivo.OpenReadStream();
        IReadOnlyList<MovimientoExtractoParseRow> movs;
        try
        {
            movs = await _parser.ParseAsync(stream, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>(false, $"No se pudo leer el archivo: {ex.Message}", new { }));
        }

        var cmd = new ImportarExtractoBancarioCommand(
            form.CuentaBancariaId,
            form.PeriodoDesde,
            form.PeriodoHasta,
            form.SaldoInicialExtracto,
            form.SaldoFinalExtracto,
            movs);

        var r = await _mediator.Send(cmd, ct);
        return this.ToCreatedOrBadRequest(r, "Importado");
    }

    [HttpGet("extractos/{extractoId:guid}/sugerencias-conciliacion")]
    [Authorize(Policy = "perm:caja.extractos.view")]
    public async Task<IActionResult> SugerirConciliacion(Guid extractoId, CancellationToken ct)
    {
        var r = await _mediator.Send(new SugerirConciliacionQuery(extractoId), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<SugerenciaConciliacionDto>());
    }

    public sealed record ConciliarRequest(Guid AsientoContableId);

    [HttpPost("movimientos/{movimientoId:guid}/conciliar")]
    [Authorize(Policy = "perm:caja.conciliar")]
    public async Task<IActionResult> ConciliarMovimiento(Guid movimientoId, [FromBody] ConciliarRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ConciliarMovimientoBancarioCommand(movimientoId, body.AsientoContableId), ct);
        return this.ToOkOrBadRequest(r, "Conciliado", () => false);
    }

    [HttpGet("cajas-chicas")]
    [Authorize(Policy = "perm:caja.cajachica.view")]
    public async Task<IActionResult> ListCajasChicas(CancellationToken ct)
    {
        var r = await _mediator.Send(new ListCajasChicasQuery(), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<CajaChicaDto>());
    }

    [HttpPost("cajas-chicas")]
    [Authorize(Policy = "perm:caja.cajachica.create")]
    public async Task<IActionResult> CrearCajaChica([FromBody] CrearCajaChicaRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearCajaChicaCommand(body.Nombre, body.SaldoAsignado, body.CuentaBancariaIdReposicion, body.CuentaContableCajaId),
            ct);
        return this.ToCreatedOrBadRequest(r, "Creado");
    }

    [HttpPost("caja-chica/gastos")]
    [Authorize(Policy = "perm:caja.cajachica.edit")]
    public async Task<IActionResult> CrearGastoCajaChica([FromBody] CrearGastoCajaChicaRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearGastoCajaChicaCommand(
                body.CajaChicaId,
                body.Fecha,
                body.Concepto,
                body.Monto,
                body.TipoComprobante,
                body.NumeroComprobante),
            ct);
        return this.ToCreatedOrBadRequest(r, "Registrado");
    }

    [HttpPost("caja-chica/arqueos")]
    [Authorize(Policy = "perm:caja.arqueos.perform")]
    public async Task<IActionResult> CrearArqueo([FromBody] CrearArqueoCajaRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(
            new CrearArqueoCajaCommand(body.CajaChicaId, body.FechaArqueo, body.EfectivoFisico, body.Observaciones),
            ct);
        return this.ToCreatedOrBadRequest(r, "Creado");
    }

    [HttpPost("caja-chica/arqueos/{arqueoId:guid}/aprobar")]
    [Authorize(Policy = "perm:caja.arqueos.perform")]
    public async Task<IActionResult> AprobarArqueo(Guid arqueoId, CancellationToken ct)
    {
        var r = await _mediator.Send(new AprobarArqueoCajaCommand(arqueoId), ct);
        return this.ToOkOrBadRequest(r, "Aprobado");
    }

    [HttpPost("caja-chica/reposicion")]
    [Authorize(Policy = "perm:caja.cajachica.edit")]
    public async Task<IActionResult> Reposicion([FromBody] ReposicionCajaChicaRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new ReposicionCajaChicaCommand(body.CajaChicaId, body.Monto), ct);
        return this.ToOkOrBadRequest(r, "OK");
    }

    [HttpGet("flujo-efectivo/real")]
    [Authorize(Policy = "perm:caja.flujo.view")]
    public async Task<IActionResult> FlujoEfectivoReal([FromQuery] DateTime desde, [FromQuery] DateTime hasta, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetFlujoEfectivoRealQuery(desde, hasta), ct);
        return this.ToOkOrBadRequest(r, "OK", () => Array.Empty<FlujoEfectivoDiaDto>());
    }

    public sealed class ImportarExtractoForm
    {
        public IFormFile Archivo { get; set; } = null!;
        public Guid CuentaBancariaId { get; set; }
        public DateTime PeriodoDesde { get; set; }
        public DateTime PeriodoHasta { get; set; }
        public decimal SaldoInicialExtracto { get; set; }
        public decimal SaldoFinalExtracto { get; set; }
    }

    public sealed record CrearCuentaBancariaRequest(
        string Nombre,
        string NumeroCuenta,
        string TipoCuenta,
        string Moneda,
        decimal SaldoInicial,
        Guid? CuentaContableId);

    public sealed record CrearCajaChicaRequest(
        string Nombre,
        decimal SaldoAsignado,
        Guid? CuentaBancariaIdReposicion,
        Guid? CuentaContableCajaId);

    public sealed record CrearGastoCajaChicaRequest(
        Guid CajaChicaId,
        DateTime Fecha,
        string Concepto,
        decimal Monto,
        string TipoComprobante,
        string? NumeroComprobante);

    public sealed record CrearArqueoCajaRequest(
        Guid CajaChicaId,
        DateTime FechaArqueo,
        decimal EfectivoFisico,
        string? Observaciones);

    public sealed record ReposicionCajaChicaRequest(Guid CajaChicaId, decimal Monto);
}
