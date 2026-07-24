using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.Brands;
using ERP.Application.Items.UseCases.AttributeGroups;
using ERP.Application.Items.UseCases.AttributeDefinitions;
using ERP.Application.Items.UseCases.CategoryNodes;
using ERP.Application.Modules.Catalog.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.SriCatalogs.Enums;

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
    public async Task<IActionResult> GetSriUoms(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriUomsQuery(), cancellationToken), "OK");

    [HttpGet("sri-vat-rates")]
    [Authorize]
    public async Task<IActionResult> GetSriVatRates(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriVatRatesQuery(), cancellationToken), "OK");

    [HttpGet("sri-ice-rates")]
    [Authorize]
    public async Task<IActionResult> GetSriIceRates(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriIceRatesQuery(), cancellationToken), "OK");

    [HttpGet("sri-retention-codes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriRetentionCodes(
        [FromQuery] string? taxType = null, CancellationToken cancellationToken = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriRetentionCodesQuery(taxType), cancellationToken), "OK");

    [HttpGet("sri-tax-support-codes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriTaxSupportCodes(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriTaxSupportCodesQuery(), cancellationToken), "OK");

    [HttpGet("sri-doc-types")]
    [Authorize]
    public async Task<IActionResult> GetSriDocTypes(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriDocTypesQuery(), cancellationToken), "OK");

    [HttpGet("sri-payment-methods")]
    [Authorize]
    public async Task<IActionResult> GetSriPaymentMethods(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriPaymentMethodsQuery(), cancellationToken), "OK");

    [HttpGet("sri-supplier-types")]
    [Authorize]
    public async Task<IActionResult> GetSriSupplierTypes(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriSupplierTypesQuery(), cancellationToken), "OK");

    [HttpGet("sri-tax-regimes")]
    [Authorize]
    public async Task<IActionResult> GetSriTaxRegimes(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriTaxRegimesQuery(), cancellationToken), "OK");

    [HttpGet("person-types")]
    [Authorize]
    public async Task<IActionResult> GetPersonTypes(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetCatalogPersonTypesQuery(), cancellationToken), "OK");

    [HttpGet("barcode-types")]
    [Authorize]
    public async Task<IActionResult> GetBarcodeTypes(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetCatalogBarcodeTypesQuery(), cancellationToken), "OK");

    [HttpGet("item-margin-statuses")]
    [Authorize]
    public async Task<IActionResult> GetItemMarginStatuses(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetCatalogItemMarginStatusesQuery(), cancellationToken), "OK");

    // ══════════════════════════════════════════════════════════════════════
    // SRI ID TYPES
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("sri-id-types")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriIdTypes(CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetSriIdTypesQuery(), cancellationToken), "OK");

    [HttpGet("sri-id-types/by-usage/{usage}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetSriIdTypesByUsage(string usage, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<IdentificationUsageType>(usage, true, out var usageType))
            return this.ApiBadRequest($"Uso '{usage}' no válido. Valores: Customer, Supplier, Employee, Carrier.");

        return this.ToOkOrBadRequest(await _mediator.Send(new GetSriIdTypesByUsageQuery(usageType), cancellationToken), "OK");
    }

    // ══════════════════════════════════════════════════════════════════════
    // BRANDS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("brands")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetBrands(
        [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetBrandsQuery(isActive), cancellationToken), "OK");

    [HttpGet("brands/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetBrandById(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrNotFound(await _mediator.Send(new GetBrandByIdQuery(id), cancellationToken));

    [HttpPost("brands")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateBrand(
        [FromBody] CreateBrandCommand command, CancellationToken cancellationToken)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("brands/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateBrand(
        Guid id, [FromBody] UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("brands/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableBrand(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableBrandCommand(id), cancellationToken));

    [HttpPatch("brands/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableBrand(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableBrandCommand(id), cancellationToken));

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE GROUPS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("attribute-groups")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeGroups(
        [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetAttributeGroupsQuery(isActive), cancellationToken), "OK");

    [HttpGet("attribute-groups/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeGroupById(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrNotFound(await _mediator.Send(new GetAttributeGroupByIdQuery(id), cancellationToken));

    [HttpPost("attribute-groups")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateAttributeGroup(
        [FromBody] CreateAttributeGroupCommand command, CancellationToken cancellationToken)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("attribute-groups/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateAttributeGroup(
        Guid id, [FromBody] UpdateAttributeGroupCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("attribute-groups/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableAttributeGroup(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableAttributeGroupCommand(id), cancellationToken));

    [HttpPatch("attribute-groups/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableAttributeGroup(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableAttributeGroupCommand(id), cancellationToken));

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE DEFINITIONS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("attribute-definitions")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeDefinitions(
        [FromQuery] Guid? groupId = null, [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetAttributeDefinitionsQuery(groupId, isActive), cancellationToken), "OK");

    [HttpGet("attribute-definitions/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetAttributeDefinitionById(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrNotFound(await _mediator.Send(new GetAttributeDefinitionByIdQuery(id), cancellationToken));

    [HttpPost("attribute-definitions")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateAttributeDefinition(
        [FromBody] CreateAttributeDefinitionCommand command, CancellationToken cancellationToken)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("attribute-definitions/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateAttributeDefinition(
        Guid id, [FromBody] UpdateAttributeDefinitionCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("attribute-definitions/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableAttributeDefinition(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableAttributeDefinitionCommand(id), cancellationToken));

    [HttpPatch("attribute-definitions/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableAttributeDefinition(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableAttributeDefinitionCommand(id), cancellationToken));

    // ══════════════════════════════════════════════════════════════════════
    // CATEGORY NODES (unified tree)
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("category-nodes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetCategoryTree(
        [FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetCategoryTreeQuery(includeInactive), cancellationToken));

    [HttpGet("category-nodes/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> GetCategoryNodeById(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrNotFound(await _mediator.Send(new GetCategoryNodeByIdQuery(id), cancellationToken));

    [HttpPost("category-nodes")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> CreateCategoryNode(
        [FromBody] CreateCategoryNodeCommand command, CancellationToken cancellationToken)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    [HttpPut("category-nodes/{id:guid}")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> UpdateCategoryNode(
        Guid id, [FromBody] UpdateCategoryNodeCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("category-nodes/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> DisableCategoryNode(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableCategoryNodeCommand(id), cancellationToken));

    [HttpPatch("category-nodes/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{CatalogPermissions.Manage}")]
    public async Task<IActionResult> EnableCategoryNode(Guid id, CancellationToken cancellationToken)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableCategoryNodeCommand(id), cancellationToken));
}
