using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Devolución de venta (P0-01). Expone los casos de uso ya implementados de
/// <c>SalesReturn</c> (Application) — Draft (crear/editar/cancelar), consulta y
/// autorización — sin permisos nuevos: reutiliza <see cref="SalesPermissions"/>
/// (mismo criterio que <see cref="SalesController"/> para Factura). Controller delgado:
/// solo mapea HTTP → MediatR → <c>Result</c>; toda la lógica de negocio (validación de
/// remanente, congelamiento de líneas, asignación de reembolso, reversión de inventario)
/// vive en <c>SalesReturn</c>/<c>AuthorizeSalesReturnHandler</c>, reutilizada sin cambios.
/// </summary>
[ApiController]
[Route("api/v1/sales/returns")]
[Authorize]
[Produces("application/json")]
public sealed class SalesReturnController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesReturnController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // DRAFT
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Crea un borrador de devolución contra una factura autorizada. No requiere
    /// <c>CustomerId</c> ni datos fiscales de línea: la devolución hereda el cliente y el
    /// snapshot fiscal (precio, descuento, VAT/ICE) directamente de la factura original —
    /// nunca se recalculan impuestos aquí (Infraestructura CLOSED — Configuración
    /// Tributaria). No autoriza ni afecta inventario/Caja/CxC/Contabilidad; eso ocurre
    /// recién en <see cref="Authorize"/>.
    /// </summary>
    /// <response code="201">Borrador creado.</response>
    /// <response code="404">La factura origen no existe.</response>
    /// <response code="422">
    /// Motivo vacío, factura no autorizada, línea que no pertenece a la factura, o cantidad
    /// que excede el remanente devolvible.
    /// </response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SalesPermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateSalesReturnDraftCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Reemplaza por completo las líneas de un borrador de devolución. El motivo no es
    /// editable (fijado al crear el Draft). Solo permitido mientras la devolución esté en
    /// estado <c>Draft</c>.
    /// </summary>
    /// <response code="200">Borrador actualizado.</response>
    /// <response code="404">La devolución o la factura origen no existen.</response>
    /// <response code="422">
    /// La devolución ya no está en borrador, línea que no pertenece a la factura, o
    /// cantidad que excede el remanente devolvible.
    /// </response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdateSalesReturnDraftCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// Cancela un borrador de devolución (soft — cambia su estado a <c>Cancelled</c>, nunca
    /// elimina el registro). Solo permitido desde <c>Draft</c>; una devolución ya
    /// <c>Authorized</c> no puede cancelarse por esta vía.
    /// </summary>
    /// <response code="200">Borrador cancelado.</response>
    /// <response code="404">La devolución no existe.</response>
    /// <response code="422">La devolución ya no está en borrador (autorizada o ya cancelada).</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelDraft(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CancelSalesReturnDraftCommand(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // CONSULTAS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Obtiene una devolución de venta por Id, con sus líneas y asignaciones de reembolso.</summary>
    /// <response code="200">Devolución encontrada.</response>
    /// <response code="404">La devolución no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetSalesReturnByIdQuery(id), ct));

    /// <summary>Lista devoluciones de venta paginadas, con filtro opcional por texto y estado.</summary>
    /// <response code="200">Listado paginado.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetSalesReturnListQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Líneas de una factura autorizada candidatas a devolución, con el remanente ya
    /// calculado (cantidad original − cantidades ya devueltas en devoluciones
    /// <c>Authorized</c> previas). Usado por el formulario de creación del Draft para
    /// mostrar qué y cuánto se puede devolver.
    /// </summary>
    /// <response code="200">Líneas devolvibles de la factura.</response>
    /// <response code="404">La factura no existe.</response>
    [HttpGet("/api/v1/sales/invoices/{invoiceId:guid}/returnable-lines")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<ReturnableLineDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReturnableLines(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetReturnableLinesByInvoiceQuery(invoiceId), ct));

    // ══════════════════════════════════════════════════════════════════════
    // AUTORIZACIÓN
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Autoriza la devolución: congela sus líneas, valida que la suma de las asignaciones
    /// de reembolso (<c>RefundAllocations</c>) coincida exactamente con el total devuelto
    /// (sin prorrateo automático — el operador indica explícitamente cuánto va a efectivo y
    /// cuánto a crédito de CxC), revierte el inventario correspondiente y ejecuta el
    /// reembolso (Caja/CxC) en la misma transacción. Emite además, si corresponde, la Nota
    /// de Crédito electrónica asociada.
    /// </summary>
    /// <response code="200">Devolución autorizada.</response>
    /// <response code="404">La devolución o la factura origen no existen.</response>
    /// <response code="422">
    /// Sin líneas, sin asignaciones de reembolso, suma de asignaciones distinta al total,
    /// cantidad que excede el remanente revalidado bajo lock, forma de reembolso inválida,
    /// o falla de ejecución del reembolso (p. ej. sin sesión de caja abierta para efectivo).
    /// </response>
    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Authorize(
        Guid id,
        [FromBody] AuthorizeSalesReturnRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new AuthorizeSalesReturnCommand(id, request.RefundAllocations),
                ct
            )
        );
}

/// <summary>Cuerpo de <see cref="SalesReturnController.Authorize"/> — el Id va en la ruta.</summary>
public sealed record AuthorizeSalesReturnRequest(
    List<AuthorizeSalesReturnRefundAllocationInput> RefundAllocations
);
