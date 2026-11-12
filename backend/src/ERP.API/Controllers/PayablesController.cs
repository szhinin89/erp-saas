using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// PAYABLES-READ-API-11 — API de solo lectura sobre la CxP genérica (<c>AccountsPayable</c>),
/// única fuente viva de saldo para Compras y Gastos desde PAYABLES-PURCHASE-MIGRATION-10. Sin
/// endpoints de escritura: no hay abonos/pagos aquí (eso queda para un ticket futuro de
/// PagoCabecera/PagoDetalle) — mismo patrón de solo-lectura que <c>PurchasePayablesController</c>/
/// <c>SalesReceivablesController</c>. Sin <c>[AppFeature]</c>/<c>[NavItem]</c> deliberadamente:
/// todavía no existe una pantalla propia para esta API transversal.
/// </summary>
[ApiController]
[Route("api/v1/payables")]
[Authorize]
[Produces("application/json")]
public sealed class PayablesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayablesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Obtiene el detalle de una cuenta por pagar con sus cuotas.</summary>
    /// <param name="id">Identificador de la cuenta por pagar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Cuenta por pagar encontrada.</response>
    /// <response code="404">La cuenta por pagar no existe.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PayablesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<AccountsPayableDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetAccountsPayableByIdQuery(id), ct));

    /// <summary>Lista cuentas por pagar de la empresa con filtros y paginación.</summary>
    /// <param name="supplierId">Filtra por proveedor.</param>
    /// <param name="originType">Origen del documento; un valor desconocido se ignora y no filtra.</param>
    /// <param name="status">Estado de la cuenta (sin distinguir mayúsculas); un valor desconocido se ignora y no filtra.</param>
    /// <param name="dueDateFrom">Vencimiento desde (fecha de negocio, <c>YYYY-MM-DD</c>).</param>
    /// <param name="dueDateTo">Vencimiento hasta (fecha de negocio, <c>YYYY-MM-DD</c>).</param>
    /// <param name="search">Texto de búsqueda libre.</param>
    /// <param name="page">Número de página, desde 1.</param>
    /// <param name="pageSize">Tamaño de página (25 por defecto).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Página de cuentas por pagar con el total de coincidencias.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{PayablesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<AccountsPayablesListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? originType = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? dueDateFrom = null,
        [FromQuery] DateOnly? dueDateTo = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetAccountsPayablesListQuery(
                    supplierId,
                    originType,
                    status,
                    dueDateFrom,
                    dueDateTo,
                    search,
                    page,
                    pageSize
                ),
                ct
            ),
            "OK"
        );
}
