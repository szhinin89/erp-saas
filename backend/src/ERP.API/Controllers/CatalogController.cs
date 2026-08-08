using ERP.API.Attributes;
using ERP.API.Extensions;
using ERP.Application.Modules.Catalog.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.SriCatalogs.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Catálogo de Ítems", $"perm:{CatalogPermissions.Manage}", "🏷️", "/catalog", null, 40)]
[ApiController]
[Route("api/v1/catalog")]
[Authorize]
[Produces("application/json")]
public sealed class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // SRI LOOKUPS (global, read-only catalogs)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("sri-uom")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriUoms(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSriUomsQuery(), cancellationToken), "OK");

    [HttpGet("sri-vat-rates")]
    [Authorize]
    public async Task<IActionResult> GetSriVatRates(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriVatRatesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("sri-ice-rates")]
    [Authorize]
    public async Task<IActionResult> GetSriIceRates(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriIceRatesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("sri-retention-codes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriRetentionCodes(
        [FromQuery] string? taxType = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriRetentionCodesQuery(taxType), cancellationToken),
            "OK"
        );

    [HttpGet("sri-tax-support-codes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriTaxSupportCodes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriTaxSupportCodesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("sri-doc-types")]
    [Authorize]
    public async Task<IActionResult> GetSriDocTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriDocTypesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("sri-payment-methods")]
    [Authorize]
    public async Task<IActionResult> GetSriPaymentMethods(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriPaymentMethodsQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("sri-supplier-types")]
    [Authorize]
    public async Task<IActionResult> GetSriSupplierTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriSupplierTypesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("sri-tax-regimes")]
    [Authorize]
    public async Task<IActionResult> GetSriTaxRegimes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriTaxRegimesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("legal-entity-types")]
    [Authorize]
    public async Task<IActionResult> GetLegalEntityTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCatalogLegalEntityTypesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("barcode-types")]
    [Authorize]
    public async Task<IActionResult> GetBarcodeTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCatalogBarcodeTypesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("item-margin-statuses")]
    [Authorize]
    public async Task<IActionResult> GetItemMarginStatuses(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCatalogItemMarginStatusesQuery(), cancellationToken),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // SRI ID TYPES
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("sri-id-types")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriIdTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriIdTypesQuery(), cancellationToken),
            "OK"
        );

    [HttpGet("sri-id-types/by-usage/{usage}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriIdTypesByUsage(
        string usage,
        CancellationToken cancellationToken
    )
    {
        if (!Enum.TryParse<IdentificationUsageType>(usage, true, out var usageType))
            return this.ApiBadRequest(
                $"Uso '{usage}' no válido. Valores: Customer, Supplier, Employee, Carrier."
            );

        return this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriIdTypesByUsageQuery(usageType), cancellationToken),
            "OK"
        );
    }
}
