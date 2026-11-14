using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md, 2026-07-30): la pantalla de consulta de CxC
/// (/finance/receivables) consume este controller. Sin [AppFeature]: AppFeatureDiscoveryService
/// usa Permission como clave única de menú (Dictionary&lt;string, DiscoveryRow&gt;) — reutilizar
/// $"perm:{SalesPermissions.View}" (ya usado por SalesController para "Ventas") colisionaría y
/// silenciosamente ocultaría uno de los dos ítems de menú. Registrar un permiso granular nuevo
/// (p. ej. finance.receivables.view) queda fuera de este alcance — ver informe de cierre P0-03.
/// La pantalla es alcanzable hoy solo por URL directa. La liquidación (registrar cobro) vive en
/// FinancePaymentsController/RegisterCollectionCommand, no aquí.
/// </summary>
[ApiController]
[Route("api/v1/sales-receivables")]
[Authorize]
[Produces("application/json")]
public sealed class SalesReceivablesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesReceivablesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Obtiene la cuenta por cobrar generada por una factura de venta, con sus cuotas.</summary>
    /// <remarks>
    /// Exige sucursal activa autorizada, pero no filtra por sucursal. El estado persistido solo distingue
    /// <c>pending</c> y <c>cancelled</c>; la etiqueta de estado (Anulada, Pagada, Vencida, Parcial, Pendiente) se
    /// calcula en el servidor a partir del saldo y la mora.
    /// </remarks>
    /// <param name="invoiceId">Id de la factura de venta.</param>
    /// <response code="200">Cuenta por cobrar de la factura.</response>
    /// <response code="404">La factura no tiene cuenta por cobrar.</response>
    [HttpGet("by-invoice/{invoiceId:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReceivableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByInvoice(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetReceivableByInvoiceQuery(invoiceId), ct));

    /// <summary>Lista las cuentas por cobrar de ventas, paginadas y ordenadas de la más reciente a la más antigua.</summary>
    /// <remarks>
    /// Exige sucursal activa autorizada. Filtro <c>status</c>: <c>pending</c> (no anulada y con saldo), <c>paid</c> (no
    /// anulada y saldo en cero) o cualquier otro valor, que se compara literalmente con el estado persistido (por
    /// ejemplo <c>cancelled</c>); un valor desconocido no coincide con ninguna fila y devuelve la página vacía.
    /// <c>pageNumber</c> debe ser ≥ 1 y <c>pageSize</c> estar entre 1 y 100; fuera de rango responde 422 <c>VALIDATION_ERROR</c>.
    /// </remarks>
    /// <param name="search">Texto de búsqueda opcional.</param>
    /// <param name="status">Filtro de estado opcional (<c>pending</c>, <c>paid</c>, <c>cancelled</c>).</param>
    /// <param name="pageNumber">Página (base 1; por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página (1–100; por defecto 25).</param>
    /// <response code="200">Página de cuentas por cobrar con el total.</response>
    /// <response code="422">Paginación fuera de rango.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<ReceivablesListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetReceivablesListQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );
}
