using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.ClassificationCatalogs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Lectura de los 6 catálogos de clasificación de Customer (CLASS-BP-CATALOGS-01):
/// categoría, segmento, rating de crédito, loyalty tier, formato de factura preferido y
/// clasificación tributaria/comercial. Reemplazan los arrays hardcodeados del frontend y los
/// HashSet fijos de <c>CustomerRoleConfig</c>. Solo GET — CRUD administrativo queda fuera de
/// alcance de este bloque (bloque futuro).
/// </summary>
[ApiController]
[Route("api/v1/catalog/classifications")]
[Authorize]
[Produces("application/json")]
public sealed class CatalogClassificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogClassificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista las categorías de cliente activas.
    /// </summary>
    /// <remarks>
    /// Clasificación comercial de clientes.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Categorías de cliente.</response>
    [HttpGet("customer-categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassificationCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerCategories(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerCategoriesQuery(), ct));

    /// <summary>
    /// Lista los segmentos de cliente activos.
    /// </summary>
    /// <remarks>
    /// Clasificación comercial de clientes.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Segmentos de cliente.</response>
    [HttpGet("customer-segments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassificationCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerSegments(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerSegmentsQuery(), ct));

    /// <summary>
    /// Lista las calificaciones crediticias de cliente activas.
    /// </summary>
    /// <remarks>
    /// Clasificación de riesgo crediticio de clientes.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Calificaciones crediticias.</response>
    [HttpGet("customer-credit-ratings")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassificationCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerCreditRatings(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerCreditRatingsQuery(), ct));

    /// <summary>
    /// Lista los niveles de fidelización activos.
    /// </summary>
    /// <remarks>
    /// Clasificación de clientes por nivel de fidelización.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Niveles de fidelización.</response>
    [HttpGet("loyalty-tiers")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassificationCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoyaltyTiers(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveLoyaltyTiersQuery(), ct));

    /// <summary>
    /// Lista los formatos de factura de cliente activos.
    /// </summary>
    /// <remarks>
    /// Formatos de factura configurables para un cliente.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Formatos de factura.</response>
    [HttpGet("customer-invoice-formats")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassificationCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerInvoiceFormats(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerInvoiceFormatsQuery(), ct));

    /// <summary>
    /// Lista las clasificaciones de cliente activas.
    /// </summary>
    /// <remarks>
    /// Clasificaciones generales de clientes.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Clasificaciones de cliente.</response>
    [HttpGet("customer-classifications")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassificationCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerClassifications(CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetActiveCustomerClassificationsQuery(), ct)
        );
}
