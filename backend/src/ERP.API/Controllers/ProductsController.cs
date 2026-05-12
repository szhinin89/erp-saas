using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Application.Products.UseCases.DisableProduct;
using ERP.Application.Products.UseCases.EnableProduct;
using ERP.Application.Products.UseCases.GetProductReport;
using ERP.Application.Products.UseCases.GetProducts;
using ERP.Application.Products.UseCases.GetProductById;
using ERP.Application.Products.UseCases.GetProductFullReport;
using ERP.Application.Products.UseCases.UpdateProduct;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión del catálogo de productos del tenant autenticado.
/// Todos los endpoints filtran automáticamente por el tenant del JWT.
/// </summary>
[Modulo("Productos", "perm:inventario.products.view", "📦", "/products", null, 38)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly DisableProductHandler _disable;
    private readonly EnableProductHandler _enable;

    public ProductsController(
        IMediator mediator,
        DisableProductHandler disable,
        EnableProductHandler enable)
    {
        _mediator = mediator;
        _disable  = disable;
        _enable   = enable;
    }

    /// <summary>Retorna todos los productos activos del tenant.</summary>
    /// <response code="200">Lista de productos (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:inventario.products.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductsQuery(), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProductDto>());
    }

    /// <summary>Retorna un producto por su ID.</summary>
    /// <param name="id">ID del producto (GUID).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Producto encontrado.</response>
    /// <response code="404">El producto no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:inventario.products.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Retorna el reporte completo (ficha técnica) de un producto.</summary>
    /// <response code="200">Reporte completo del producto.</response>
    /// <response code="404">El producto no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}/full-report")]
    [Authorize(Policy = "perm:inventario.products.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductFullReportDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFullReport(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductFullReportQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Listado tipo reporte con filtros.</summary>
    [HttpGet("report")]
    [Authorize(Policy = "perm:inventario.products.view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ProductReportItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetReport(
        [FromQuery] string? search,
        [FromQuery] string? saleCode,
        [FromQuery] string? purchaseCode,
        [FromQuery] string? barcode,
        [FromQuery] bool? isFavorite,
        [FromQuery] bool? isForSale,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isEcommerceActive,
        [FromQuery] bool? isService,
        [FromQuery] Guid? lineId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? subcategoryId,
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? productTypeId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new ProductReportFilter(
            Search: search,
            SaleCode: saleCode,
            PurchaseCode: purchaseCode,
            Barcode: barcode,
            IsFavorite: isFavorite,
            IsForSale: isForSale,
            IsActive: isActive,
            IsEcommerceActive: isEcommerceActive,
            IsService: isService,
            LineId: lineId,
            CategoryId: categoryId,
            SubcategoryId: subcategoryId,
            BrandId: brandId,
            ProductTypeId: productTypeId);

        var result = await _mediator.Send(new GetProductReportQuery(filter, pageNumber, pageSize), ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(result.Error ?? "Error");
        if (result.Value is null)
            return this.ApiUnprocessableEntity("Respuesta de paginación inválida.");

        var paged = new PagedResponse<ProductReportItemDto>(
            Items: result.Value.Items,
            PageNumber: result.Value.PageNumber,
            PageSize: result.Value.PageSize,
            TotalCount: result.Value.TotalCount);

        return this.ApiOk(paged);
    }

    /// <summary>Crea un nuevo producto en el catálogo del tenant.</summary>
    /// <remarks>
    /// Los campos `lineId`, `categoryId`, `subcategoryId`, etc. referencian
    /// IDs de catálogos maestros (aún no expuestos como endpoints propios).
    /// </remarks>
    /// <response code="201">Producto creado. La respuesta incluye el ID asignado.</response>
    /// <response code="400">El código de venta ya existe en el tenant.</response>
    [HttpPost]
    [Authorize(Policy = "perm:inventario.products.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Actualiza un producto existente.</summary>
    /// <param name="id">ID del producto a actualizar.</param>
    /// <param name="command">Datos actualizados del producto.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Producto actualizado correctamente.</response>
    /// <response code="404">Producto no encontrado.</response>
    /// <response code="422">Datos inválidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:inventario.products.edit")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductCommand command,
        CancellationToken ct)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID de la ruta no coincide con el ID del comando.");

        var result = (Result<ProductDto>)await _mediator.Send(command, ct);
        return result.IsSuccess
            ? this.ApiOk(result.Value)
            : this.ApiBadRequest(result.Error ?? "Error al actualizar el producto");
    }

    /// <summary>Deshabilita un producto (no lo elimina).</summary>
    /// <param name="id">ID del producto.</param>
    /// <response code="200">Producto deshabilitado.</response>
    /// <response code="400">El producto ya está deshabilitado o no existe.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:inventario.products.edit")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        var result = await _disable.HandleAsync(id, ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    /// <summary>Habilita un producto previamente deshabilitado.</summary>
    /// <param name="id">ID del producto.</param>
    /// <response code="200">Producto habilitado.</response>
    /// <response code="400">El producto ya está activo o no existe.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:inventario.products.edit")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
    {
        var result = await _enable.HandleAsync(id, ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
