using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Application.Products.UseCases.GetProducts;
using ERP.Application.Products.UseCases.GetProductById;
using ERP.Application.Products.DTOs;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión del catálogo de productos del tenant autenticado.
/// Todos los endpoints filtran automáticamente por el tenant del JWT.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly CreateProductHandler  _createHandler;
    private readonly GetProductsHandler    _getListHandler;
    private readonly GetProductByIdHandler _getByIdHandler;

    public ProductsController(
        CreateProductHandler createHandler,
        GetProductsHandler getListHandler,
        GetProductByIdHandler getByIdHandler)
    {
        _createHandler  = createHandler;
        _getListHandler = getListHandler;
        _getByIdHandler = getByIdHandler;
    }

    /// <summary>Retorna todos los productos activos del tenant.</summary>
    /// <response code="200">Lista de productos (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getListHandler.HandleAsync(ct);
        return Ok(result.Value);
    }

    /// <summary>Retorna un producto por su ID.</summary>
    /// <param name="id">ID del producto (GUID).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Producto encontrado.</response>
    /// <response code="404">El producto no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Crea un nuevo producto en el catálogo del tenant.</summary>
    /// <remarks>
    /// Los campos `lineId`, `categoryId`, `subcategoryId`, etc. referencian
    /// IDs de catálogos maestros (aún no expuestos como endpoints propios).
    /// </remarks>
    /// <response code="201">Producto creado. La respuesta incluye el ID asignado.</response>
    /// <response code="400">El código de venta ya existe en el tenant.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }
}
