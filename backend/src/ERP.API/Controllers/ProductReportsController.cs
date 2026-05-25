using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Products.DTOs;
using ERP.Application.Products.UseCases.GetProductFullReport;
using ERP.Application.Products.UseCases.GetProductReport;
using ERP.Domain.Products.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/inventory/products")]
[Authorize]
[Produces("application/json")]
public sealed class ProductReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id:guid}/full-report")]
    [Authorize(Policy = "perm:inventory.products.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductFullReportDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFullReport(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductFullReportQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpGet("report")]
    [Authorize(Policy = "perm:inventory.products.view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ProductReportItemDto>>), StatusCodes.Status200OK)]
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

        return this.ApiOk(new PagedResponse<ProductReportItemDto>(
            Items: result.Value.Items,
            PageNumber: result.Value.PageNumber,
            PageSize: result.Value.PageSize,
            TotalCount: result.Value.TotalCount));
    }
}
