using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.ItemFamilies;
using ERP.Application.Items.UseCases.ItemCategories;
using ERP.Application.Items.UseCases.ItemSubcategories;
using ERP.Application.Items.UseCases.Brands;
using ERP.Application.Items.UseCases.AttributeGroups;
using ERP.Application.Items.UseCases.AttributeDefinitions;
using ERP.Domain.Modules.Items.Enums;

namespace ERP.API.Controllers;

[AppFeature("Catálogo de Ítems", "perm:catalog.manage", "🏷️", "/catalog", null, 40)]
[ApiController]
[Route("api/catalog")]
[Authorize]
[Produces("application/json")]
public sealed class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;
    public CatalogController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // ITEM FAMILIES
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("item-families")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemFamilyDto>>), 200)]
    public async Task<IActionResult> GetItemFamilies(
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetItemFamiliesQuery(isActive), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("item-families/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<ItemFamilyDto>), 200)]
    public async Task<IActionResult> GetItemFamilyById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetItemFamilyByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost("item-families")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<ItemFamilyDto>), 201)]
    public async Task<IActionResult> CreateItemFamily(
        [FromBody] CreateItemFamilyCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Familia creada");
    }

    [HttpPut("item-families/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<ItemFamilyDto>), 200)]
    public async Task<IActionResult> UpdateItemFamily(
        Guid id, [FromBody] UpdateItemFamilyCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID de la ruta no coincide.");
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result, "Actualizado");
    }

    [HttpPatch("item-families/{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> EnableItemFamily(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableItemFamilyCommand(id), ct), "Habilitado");

    [HttpPatch("item-families/{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> DisableItemFamily(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableItemFamilyCommand(id), ct), "Deshabilitado");

    // ══════════════════════════════════════════════════════════════════════
    // ITEM CATEGORIES
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("item-categories")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemCategoryDto>>), 200)]
    public async Task<IActionResult> GetItemCategories(
        [FromQuery] Guid? familyId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetItemCategoriesQuery(familyId, isActive), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("item-categories/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> GetItemCategoryById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetItemCategoryByIdQuery(id), ct));

    [HttpPost("item-categories")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> CreateItemCategory(
        [FromBody] CreateItemCategoryCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct), "Categoría creada");

    [HttpPut("item-categories/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> UpdateItemCategory(
        Guid id, [FromBody] UpdateItemCategoryCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct), "Actualizado");
    }

    [HttpPatch("item-categories/{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> EnableItemCategory(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableItemCategoryCommand(id), ct), "Habilitado");

    [HttpPatch("item-categories/{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> DisableItemCategory(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableItemCategoryCommand(id), ct), "Deshabilitado");

    // ══════════════════════════════════════════════════════════════════════
    // ITEM SUBCATEGORIES
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("item-subcategories")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemSubcategoryDto>>), 200)]
    public async Task<IActionResult> GetItemSubcategories(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] bool? isActive   = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetItemSubcategoriesQuery(categoryId, isActive), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpGet("item-subcategories/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> GetItemSubcategoryById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetItemSubcategoryByIdQuery(id), ct));

    [HttpPost("item-subcategories")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> CreateItemSubcategory(
        [FromBody] CreateItemSubcategoryCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct), "Subcategoría creada");

    [HttpPut("item-subcategories/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> UpdateItemSubcategory(
        Guid id, [FromBody] UpdateItemSubcategoryCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct), "Actualizado");
    }

    [HttpPatch("item-subcategories/{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> EnableItemSubcategory(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableItemSubcategoryCommand(id), ct), "Habilitado");

    [HttpPatch("item-subcategories/{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> DisableItemSubcategory(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableItemSubcategoryCommand(id), ct), "Deshabilitado");

    // ══════════════════════════════════════════════════════════════════════
    // BRANDS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("brands")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BrandDto>>), 200)]
    public async Task<IActionResult> GetBrands(
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetBrandsQuery(isActive), ct), "OK");

    [HttpGet("brands/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> GetBrandById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetBrandByIdQuery(id), ct));

    [HttpPost("brands")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> CreateBrand(
        [FromBody] CreateBrandCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct), "Marca creada");

    [HttpPut("brands/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> UpdateBrand(
        Guid id, [FromBody] UpdateBrandCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct), "Actualizado");
    }

    [HttpPatch("brands/{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> EnableBrand(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableBrandCommand(id), ct), "Habilitado");

    [HttpPatch("brands/{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> DisableBrand(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableBrandCommand(id), ct), "Deshabilitado");

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE GROUPS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("attribute-groups")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttributeGroupDto>>), 200)]
    public async Task<IActionResult> GetAttributeGroups(
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetAttributeGroupsQuery(isActive), ct), "OK");

    [HttpGet("attribute-groups/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> GetAttributeGroupById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetAttributeGroupByIdQuery(id), ct));

    [HttpPost("attribute-groups")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> CreateAttributeGroup(
        [FromBody] CreateAttributeGroupCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct), "Grupo creado");

    [HttpPut("attribute-groups/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> UpdateAttributeGroup(
        Guid id, [FromBody] UpdateAttributeGroupCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct), "Actualizado");
    }

    [HttpPatch("attribute-groups/{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> EnableAttributeGroup(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableAttributeGroupCommand(id), ct), "Habilitado");

    [HttpPatch("attribute-groups/{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> DisableAttributeGroup(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableAttributeGroupCommand(id), ct), "Deshabilitado");

    // ══════════════════════════════════════════════════════════════════════
    // ATTRIBUTE DEFINITIONS
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("attribute-definitions")]
    [Authorize(Policy = "perm:catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AttributeDefinitionDto>>), 200)]
    public async Task<IActionResult> GetAttributeDefinitions(
        [FromQuery] Guid? groupId  = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
        => this.ToOkOrBadRequest(await _mediator.Send(new GetAttributeDefinitionsQuery(groupId, isActive), ct), "OK");

    [HttpGet("attribute-definitions/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> GetAttributeDefinitionById(Guid id, CancellationToken ct)
        => this.ToOkOrNotFound(await _mediator.Send(new GetAttributeDefinitionByIdQuery(id), ct));

    [HttpPost("attribute-definitions")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> CreateAttributeDefinition(
        [FromBody] CreateAttributeDefinitionCommand command, CancellationToken ct)
        => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct), "Atributo creado");

    [HttpPut("attribute-definitions/{id:guid}")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> UpdateAttributeDefinition(
        Guid id, [FromBody] UpdateAttributeDefinitionCommand command, CancellationToken ct)
    {
        if (id != command.Id) return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct), "Actualizado");
    }

    [HttpPatch("attribute-definitions/{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> EnableAttributeDefinition(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new EnableAttributeDefinitionCommand(id), ct), "Habilitado");

    [HttpPatch("attribute-definitions/{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.manage")]
    public async Task<IActionResult> DisableAttributeDefinition(Guid id, CancellationToken ct)
        => this.ToOkOrBadRequest(await _mediator.Send(new DisableAttributeDefinitionCommand(id), ct), "Deshabilitado");
}
