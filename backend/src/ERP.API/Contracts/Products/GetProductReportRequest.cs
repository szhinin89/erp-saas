namespace ERP.API.Contracts;

/// <summary>
/// Contrato explícito para filtros de reporte de productos.
/// 
/// Separa la responsabilidad:
/// - API: Recibe GetProductReportRequest (este DTO)
/// - Application: Usa ProductReportFilter (parámetro de negocio)
/// - Controller: Mapea GetProductReportRequest → ProductReportFilter
/// 
/// Ventajas:
/// - Contrato de API claro y autodocumentado (Swagger)
/// - Validaciones centralizadas en el DTO
/// - Separación entre "request HTTP" y "filtro de negocio"
/// - Fácil de versionar si el API cambia
/// 
/// Uso en ProductsController.GetReport():
/// ```csharp
/// [HttpGet("report")]
/// public async Task<IActionResult> GetReport(
///     [FromQuery] GetProductReportRequest request,
///     CancellationToken ct = default)
/// {
///     var filter = request.ToFilter();
///     var result = await _mediator.Send(
///         new GetProductReportQuery(filter, request.PageNumber, request.PageSize), ct);
///     // ...
/// }
/// ```
/// </summary>
public class GetProductReportRequest
{
    /// <summary>Búsqueda por código, nombre o descripción.</summary>
    public string? Search { get; set; }

    /// <summary>Código de venta (SKU público).</summary>
    public string? SaleCode { get; set; }

    /// <summary>Código de compra (proveedor).</summary>
    public string? PurchaseCode { get; set; }

    /// <summary>Código de barras.</summary>
    public string? Barcode { get; set; }

    /// <summary>Filtro: Solo productos marcados como favoritos.</summary>
    public bool? IsFavorite { get; set; }

    /// <summary>Filtro: Solo productos disponibles para venta.</summary>
    public bool? IsForSale { get; set; }

    /// <summary>Filtro: Solo productos activos.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Filtro: Solo productos visibles en e-commerce.</summary>
    public bool? IsEcommerceActive { get; set; }

    /// <summary>Filtro: Solo servicios (no productos físicos).</summary>
    public bool? IsService { get; set; }

    /// <summary>Filtro por línea de producto (ID GUID).</summary>
    public Guid? LineId { get; set; }

    /// <summary>Filtro por categoría principal (ID GUID).</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Filtro por subcategoría (ID GUID).</summary>
    public Guid? SubcategoryId { get; set; }

    /// <summary>Filtro por marca (ID GUID).</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Filtro por tipo de producto (ID GUID).</summary>
    public Guid? ProductTypeId { get; set; }

    /// <summary>Número de página (base 1). Default: 1.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Tamaño de página. Default: 20, máximo: 100.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Convierte este request a un filter de dominio.</summary>
    public Domain.Products.Interfaces.ProductReportFilter ToFilter()
    {
        return new Domain.Products.Interfaces.ProductReportFilter(
            Search: Search,
            SaleCode: SaleCode,
            PurchaseCode: PurchaseCode,
            Barcode: Barcode,
            IsFavorite: IsFavorite,
            IsForSale: IsForSale,
            IsActive: IsActive,
            IsEcommerceActive: IsEcommerceActive,
            IsService: IsService,
            LineId: LineId,
            CategoryId: CategoryId,
            SubcategoryId: SubcategoryId,
            BrandId: BrandId,
            ProductTypeId: ProductTypeId
        );
    }
}
