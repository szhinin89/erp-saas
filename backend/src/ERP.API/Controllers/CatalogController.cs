using ERP.API.Attributes;
using ERP.API.Contracts;
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

    /// <summary>
    /// Lista las unidades de medida del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de unidades de medida (código y nombre) que usan los ítems como unidad base y de
    /// presentación. Requiere el permiso de administración del catálogo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-uom")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriUomDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriUoms(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSriUomsQuery(), cancellationToken), "OK");

    /// <summary>
    /// Lista las tarifas de IVA del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de tarifas de IVA que se asignan a los ítems para venta y compra. Cualquier usuario
    /// autenticado puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-vat-rates")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriVatRateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriVatRates(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriVatRatesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista las tarifas de ICE del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de tarifas del impuesto a los consumos especiales. Cualquier usuario autenticado puede
    /// consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-ice-rates")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriIceRateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriIceRates(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriIceRatesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los códigos de retención del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Devuelve los códigos de retención activos, con su tipo de impuesto, código, nombre y porcentaje. Cualquier
    /// usuario autenticado puede consultarlo.
    /// </remarks>
    /// <param name="taxType">Tipo de impuesto por el que se filtra; omitido, devuelve los códigos de todos los tipos.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-retention-codes")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriRetentionCodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriRetentionCodes(
        [FromQuery] string? taxType = null,
        CancellationToken cancellationToken = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriRetentionCodesQuery(taxType), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los sustentos tributarios del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de sustentos tributarios usados en compras y retenciones. Cualquier usuario autenticado
    /// puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-tax-support-codes")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriTaxSupportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriTaxSupportCodes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriTaxSupportCodesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los tipos de comprobante del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de tipos de documento fiscal. Cualquier usuario autenticado puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-doc-types")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriDocTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriDocTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriDocTypesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista las formas de pago del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de formas de pago del SRI. Cualquier usuario autenticado puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-payment-methods")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriPaymentMethodDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriPaymentMethods(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriPaymentMethodsQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los tipos de proveedor del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de tipos de proveedor del SRI. Cualquier usuario autenticado puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-supplier-types")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriSupplierTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriSupplierTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriSupplierTypesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los regímenes tributarios del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de regímenes tributarios. Cualquier usuario autenticado puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-tax-regimes")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriTaxRegimeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriTaxRegimes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriTaxRegimesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los tipos de entidad legal.
    /// </summary>
    /// <remarks>
    /// Catálogo global de tipos de entidad legal. Cualquier usuario autenticado puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("legal-entity-types")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LegalEntityTypeCatalogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLegalEntityTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCatalogLegalEntityTypesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los tipos de código de barras.
    /// </summary>
    /// <remarks>
    /// Catálogo global de tipos de código de barras que admite un ítem. Cualquier usuario autenticado puede
    /// consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("barcode-types")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CatalogBarcodeTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBarcodeTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCatalogBarcodeTypesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los estados de margen de un ítem.
    /// </summary>
    /// <remarks>
    /// Catálogo global de estados de margen que usa el análisis de rentabilidad de los ítems. Cualquier usuario
    /// autenticado puede consultarlo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("item-margin-statuses")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CatalogItemMarginStatusDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemMarginStatuses(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCatalogItemMarginStatusesQuery(), cancellationToken),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // SRI ID TYPES
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista los tipos de identificación del catálogo SRI.
    /// </summary>
    /// <remarks>
    /// Catálogo global de tipos de identificación (cédula, RUC, pasaporte, etc.). Requiere el permiso de
    /// administración del catálogo.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    [HttpGet("sri-id-types")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriIdTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSriIdTypes(CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSriIdTypesQuery(), cancellationToken),
            "OK"
        );

    /// <summary>
    /// Lista los tipos de identificación del SRI para un uso.
    /// </summary>
    /// <remarks>
    /// Filtra el catálogo de tipos de identificación según el uso. El valor de <c>usage</c> no distingue mayúsculas
    /// y debe ser <c>Customer</c>, <c>Supplier</c>, <c>Employee</c> o <c>Carrier</c>; otro valor responde 400.
    /// Requiere el permiso de administración del catálogo.
    /// </remarks>
    /// <param name="usage">Uso de la identificación: <c>Customer</c>, <c>Supplier</c>, <c>Employee</c> o <c>Carrier</c>.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo solicitado.</response>
    /// <response code="400">El uso indicado no es un valor válido (<c>Customer</c>, <c>Supplier</c>, <c>Employee</c> o <c>Carrier</c>).</response>
    [HttpGet("sri-id-types/by-usage/{usage}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SriIdTypeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
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
