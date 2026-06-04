using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.SupplierCatalog.DTOs;
using ERP.Application.SupplierCatalog.UseCases.RegisterSupplierItem;
using ERP.Application.SupplierCatalog.UseCases.GetItemSuppliers;

namespace ERP.API.Controllers;

[AppFeature("Catálogo Proveedores", "perm:supplier-catalog.manage", "🏭", "/supplier-catalog", null, 52)]
[ApiController]
[Route("api/supplier-catalog")]
[Authorize]
[Produces("application/json")]
public sealed class SupplierCatalogController : ControllerBase
{
    private readonly IMediator _mediator;
    public SupplierCatalogController(IMediator mediator) => _mediator = mediator;

    [HttpGet("by-item/{itemId:guid}")]
    [Authorize(Policy = "perm:supplier-catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SupplierItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByItem(Guid itemId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetItemSuppliersQuery(itemId), ct);
        return this.ToOkOrBadRequest(result, "OK");
    }

    [HttpPost]
    [Authorize(Policy = "perm:supplier-catalog.manage")]
    [ProducesResponseType(typeof(ApiResponse<SupplierItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterSupplierItemCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Registro de proveedor creado");
    }
}
