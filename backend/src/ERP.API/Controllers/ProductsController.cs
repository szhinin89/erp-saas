using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Application.Products.UseCases.DisableProduct;
using ERP.Application.Products.UseCases.EnableProduct;
using ERP.Application.Products.UseCases.GetProducts;
using ERP.Application.Products.UseCases.GetProductById;
using ERP.Application.Products.UseCases.UpdateProduct;
using ERP.Application.Products.DTOs;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[AppFeature("Productos", "perm:inventory.products.view", "📦", "/inventory/products", null, 38)]
[ApiController]
[Route("api/inventory/products")]
[Authorize]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "perm:inventory.products.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductsQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProductDto>());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventory.products.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = "perm:inventory.products.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventory.products.edit")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID de la ruta no coincide con el ID del comando.");

        var result = (Result<ProductDto>)await _mediator.Send(command, ct);
        return result.IsSuccess
            ? this.ApiOk(result.Value)
            : this.ApiBadRequest(result.Error ?? "Error al actualizar el producto");
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventory.products.edit")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DisableProductCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventory.products.edit")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new EnableProductCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
