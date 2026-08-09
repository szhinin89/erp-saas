using ERP.API.Extensions;
using ERP.Application.MasterData.UseCases.ClassificationCatalogs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Lectura de los 12 catálogos de clasificación de BusinessPartner (CLASS-BP-CATALOGS-01):
/// categorías/segmentos/ratings de cliente y proveedor, loyalty tier, formato de factura
/// preferido, clasificación tributaria/comercial, tipo de proveedor, riesgo, tipo de bien
/// principal. Reemplazan los 12 arrays hardcodeados del frontend y los HashSet fijos de
/// <c>CustomerRoleConfig</c>/<c>SupplierClassificationConfig</c>. Solo GET — CRUD administrativo
/// queda fuera de alcance de este bloque (bloque futuro).
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

    [HttpGet("customer-categories")]
    public async Task<IActionResult> GetCustomerCategories(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerCategoriesQuery(), ct));

    [HttpGet("customer-segments")]
    public async Task<IActionResult> GetCustomerSegments(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerSegmentsQuery(), ct));

    [HttpGet("customer-credit-ratings")]
    public async Task<IActionResult> GetCustomerCreditRatings(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerCreditRatingsQuery(), ct));

    [HttpGet("loyalty-tiers")]
    public async Task<IActionResult> GetLoyaltyTiers(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveLoyaltyTiersQuery(), ct));

    [HttpGet("customer-invoice-formats")]
    public async Task<IActionResult> GetCustomerInvoiceFormats(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerInvoiceFormatsQuery(), ct));

    [HttpGet("customer-classifications")]
    public async Task<IActionResult> GetCustomerClassifications(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveCustomerClassificationsQuery(), ct));

    [HttpGet("supplier-categories")]
    public async Task<IActionResult> GetSupplierCategories(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveSupplierCategoriesQuery(), ct));

    [HttpGet("supplier-types")]
    public async Task<IActionResult> GetSupplierTypes(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveSupplierTypesQuery(), ct));

    [HttpGet("supplier-risks")]
    public async Task<IActionResult> GetSupplierRisks(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveSupplierRisksQuery(), ct));

    [HttpGet("supplier-ratings")]
    public async Task<IActionResult> GetSupplierRatings(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveSupplierRatingsQuery(), ct));

    [HttpGet("primary-good-types")]
    public async Task<IActionResult> GetPrimaryGoodTypes(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActivePrimaryGoodTypesQuery(), ct));

    [HttpGet("supplier-segments")]
    public async Task<IActionResult> GetSupplierSegments(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetActiveSupplierSegmentsQuery(), ct));
}
