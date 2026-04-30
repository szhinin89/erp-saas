using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Application.Products.UseCases.GetProducts;
using ERP.Application.Products.UseCases.GetProductById;
using ERP.Application.Products.UseCases.GetProductFullReport;
using ERP.Application.Products.UseCases.GetProductReport;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

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
    private readonly GetProductFullReportHandler _getFullReportHandler;
    private readonly GetProductReportHandler _getReportHandler;

    public ProductsController(
        CreateProductHandler createHandler,
        GetProductsHandler getListHandler,
        GetProductByIdHandler getByIdHandler,
        GetProductFullReportHandler getFullReportHandler,
        GetProductReportHandler getReportHandler)
    {
        _createHandler  = createHandler;
        _getListHandler = getListHandler;
        _getByIdHandler = getByIdHandler;
        _getFullReportHandler = getFullReportHandler;
        _getReportHandler = getReportHandler;
    }

    /// <summary>Retorna todos los productos activos del tenant.</summary>
    /// <response code="200">Lista de productos (puede ser vacía).</response>
    /// <response code="401">Token JWT ausente o inválido.</response>
    [HttpGet]
    [Authorize(Policy = "perm:catalog.products.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getListHandler.HandleAsync(ct);
        return Ok(new ApiResponse<IReadOnlyList<ProductDto>>(
            Success: result.IsSuccess,
            Message: result.IsSuccess ? "OK" : result.Error ?? "Error",
            ResponseObject: result.Value ?? Array.Empty<ProductDto>()));
    }

    /// <summary>Retorna un producto por su ID.</summary>
    /// <param name="id">ID del producto (GUID).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Producto encontrado.</response>
    /// <response code="404">El producto no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:catalog.products.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.HandleAsync(id, ct);
        if (!result.IsSuccess)
        {
            return NotFound(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "No encontrado",
                ResponseObject: new { }));
        }

        return Ok(new ApiResponse<ProductDto?>(
            Success: true,
            Message: "OK",
            ResponseObject: result.Value));
    }

    /// <summary>Retorna el reporte completo (ficha técnica) de un producto.</summary>
    /// <response code="200">Reporte completo del producto.</response>
    /// <response code="404">El producto no existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}/full-report")]
    [Authorize(Policy = "perm:catalog.products.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductFullReportDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFullReport(Guid id, CancellationToken ct)
    {
        var result = await _getFullReportHandler.HandleAsync(id, ct);
        if (!result.IsSuccess)
        {
            return NotFound(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "No encontrado",
                ResponseObject: new { }));
        }

        return Ok(new ApiResponse<ProductFullReportDto?>(
            Success: true,
            Message: "OK",
            ResponseObject: result.Value));
    }

    /// <summary>Listado tipo reporte con filtros.</summary>
    [HttpGet("report")]
    [Authorize(Policy = "perm:catalog.products.view")]
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

        var result = await _getReportHandler.HandleAsync(filter, pageNumber, pageSize, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
        }

        var paged = new PagedResponse<ProductReportItemDto>(
            Items: result.Value.Items,
            PageNumber: result.Value.PageNumber,
            PageSize: result.Value.PageSize,
            TotalCount: result.Value.TotalCount);

        return Ok(new ApiResponse<PagedResponse<ProductReportItemDto>>(
            Success: true,
            Message: "OK",
            ResponseObject: paged));
    }

    /// <summary>Crea un nuevo producto en el catálogo del tenant.</summary>
    /// <remarks>
    /// Los campos `lineId`, `categoryId`, `subcategoryId`, etc. referencian
    /// IDs de catálogos maestros (aún no expuestos como endpoints propios).
    /// </remarks>
    /// <response code="201">Producto creado. La respuesta incluye el ID asignado.</response>
    /// <response code="400">El código de venta ya existe en el tenant.</response>
    [HttpPost]
    [Authorize(Policy = "perm:catalog.products.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<ProductDto?>(
                Success: true,
                Message: "Creado",
                ResponseObject: result.Value))
            : BadRequest(new ApiResponse<object>(
                Success: false,
                Message: result.Error ?? "Error",
                ResponseObject: new { }));
    }
}
