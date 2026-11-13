using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.UseCases.CodeConflicts;
using ERP.Application.Items.UseCases.DisableItem;
using ERP.Application.Items.UseCases.EnableItem;
using ERP.Application.Items.UseCases.GetItemById;
using ERP.Application.Items.UseCases.GetItemReport;
using ERP.Application.Items.UseCases.GetItems;
using ERP.Application.Items.UseCases.ResolveItem;
using ERP.Application.Items.UseCases.UpdateItem;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Ítems", $"perm:{InventoryPermissions.ItemsView}", "📦", "/inventory/items", null, 35)]
[ApiController]
[Route("api/v1/items")]
[Authorize]
[Produces("application/json")]
public sealed class ItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemsController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // LIST + REPORTS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista los ítems de la empresa con filtros y paginación.
    /// </summary>
    /// <remarks>
    /// Resultado paginado. Todos los filtros son opcionales y se combinan entre sí; el tamaño de página por defecto
    /// es 20.
    /// </remarks>
    /// <param name="search">Texto libre de búsqueda.</param>
    /// <param name="sku">Filtra por SKU.</param>
    /// <param name="isActive">Filtra por estado activo (<c>true</c>) o inactivo (<c>false</c>).</param>
    /// <param name="isForSale">Filtra por ítems habilitados (<c>true</c>) o no (<c>false</c>) para la venta.</param>
    /// <param name="isFavorite">Filtra por ítems marcados como favoritos.</param>
    /// <param name="isEcommerce">Filtra por ítems publicados (<c>true</c>) o no (<c>false</c>) para comercio electrónico.</param>
    /// <param name="itemTypeId">Filtra por tipo de ítem.</param>
    /// <param name="categoryNodeId">Filtra por nodo de categoría.</param>
    /// <param name="brandId">Filtra por marca.</param>
    /// <param name="barcode">Filtra por código de barras.</param>
    /// <param name="pageNumber">Número de página, base 1.</param>
    /// <param name="pageSize">Cantidad de ítems por página (20 por defecto).</param>
    /// <param name="participatesInInventory">Filtra por ítems que participan (<c>true</c>) o no (<c>false</c>) en inventario.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página de ítems.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<GetItemsResponse>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? sku = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isForSale = null,
        [FromQuery] bool? isFavorite = null,
        [FromQuery] bool? isEcommerce = null,
        [FromQuery] Guid? itemTypeId = null,
        [FromQuery] Guid? categoryNodeId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] string? barcode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? participatesInInventory = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetItemsQuery(
                search,
                sku,
                isActive,
                isForSale,
                isFavorite,
                isEcommerce,
                itemTypeId,
                categoryNodeId,
                brandId,
                barcode,
                pageNumber,
                pageSize,
                participatesInInventory
            ),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK");
    }

    /// <summary>
    /// Resuelve un ítem a partir de un código.
    /// </summary>
    /// <remarks>
    /// Busca, en este orden: SKU del ítem, SKU de una variante activa y código de barras activo. Si ningún ítem
    /// coincide responde 200 con <c>data</c> nulo (no 404). Un código vacío responde 422.
    /// </remarks>
    /// <param name="code">SKU, SKU de variante o código de barras del ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ítem encontrado, o <c>data</c> nulo si ningún ítem coincide.</response>
    [HttpGet("resolve/{code}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveItem(
        string code,
        CancellationToken cancellationToken
    ) => this.ToOkOrNotFound(await _mediator.Send(new ResolveItemQuery(code), cancellationToken));

    /// <summary>
    /// Diagnostica conflictos de códigos antes de guardar.
    /// </summary>
    /// <remarks>
    /// Consulta de solo lectura. Para cada SKU, SKU de variante, código de barras o código de proveedor indicado
    /// devuelve el ítem de la empresa que ya lo usa o el duplicado entre las propias líneas del lote.
    /// <c>ExcludeId</c> evita marcar como duplicado al propio ítem al editarlo. No sustituye la unicidad que
    /// garantiza la persistencia al guardar.
    /// </remarks>
    /// <param name="query">Códigos a diagnosticar y, opcionalmente, el ítem a excluir y el proveedor.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Conflictos de códigos encontrados.</response>
    [HttpPost("code-conflicts")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ItemCodeConflictDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CodeConflicts([FromBody] GetItemCodeConflictsQuery query,
        CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(query, cancellationToken));

    /// <summary>
    /// Devuelve el reporte de ítems con filtros y paginación.
    /// </summary>
    /// <remarks>
    /// Misma familia de filtros que el listado, con un tamaño de página por defecto de 50.
    /// </remarks>
    /// <param name="search">Texto libre de búsqueda.</param>
    /// <param name="sku">Filtra por SKU.</param>
    /// <param name="isActive">Filtra por estado activo (<c>true</c>) o inactivo (<c>false</c>).</param>
    /// <param name="isForSale">Filtra por ítems habilitados (<c>true</c>) o no (<c>false</c>) para la venta.</param>
    /// <param name="isFavorite">Filtra por ítems marcados como favoritos.</param>
    /// <param name="isEcommerce">Filtra por ítems publicados (<c>true</c>) o no (<c>false</c>) para comercio electrónico.</param>
    /// <param name="itemTypeId">Filtra por tipo de ítem.</param>
    /// <param name="categoryNodeId">Filtra por nodo de categoría.</param>
    /// <param name="brandId">Filtra por marca.</param>
    /// <param name="barcode">Filtra por código de barras.</param>
    /// <param name="pageNumber">Número de página, base 1.</param>
    /// <param name="pageSize">Cantidad de ítems por página (50 por defecto).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página del reporte de ítems.</response>
    [HttpGet("report")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<GetItemsResponse>), 200)]
    public async Task<IActionResult> GetReport(
        [FromQuery] string? search = null,
        [FromQuery] string? sku = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isForSale = null,
        [FromQuery] bool? isFavorite = null,
        [FromQuery] bool? isEcommerce = null,
        [FromQuery] Guid? itemTypeId = null,
        [FromQuery] Guid? categoryNodeId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] string? barcode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetItemReportQuery(
                search,
                sku,
                isActive,
                isForSale,
                isFavorite,
                isEcommerce,
                itemTypeId,
                categoryNodeId,
                brandId,
                barcode,
                pageNumber,
                pageSize
            ),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result, "OK");
    }

    // ══════════════════════════════════════════════════════════════════════
    // DETAIL
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene el detalle de un ítem.
    /// </summary>
    /// <remarks>
    /// Incluye variantes, códigos de barras, imágenes, conversiones, presentaciones, códigos de proveedor y
    /// sustitutos.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Detalle del ítem.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetItemByIdQuery(id), cancellationToken));

    /// <summary>
    /// Obtiene el reporte completo de un ítem.
    /// </summary>
    /// <remarks>
    /// Consolida en una sola respuesta la información del ítem para su reporte.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Reporte completo del ítem.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}/full-report")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsView}")]
    [ProducesResponseType(typeof(ApiResponse<ItemFullReportDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullReport(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetItemFullReportQuery(id), cancellationToken)
        );

    // ══════════════════════════════════════════════════════════════════════
    // CREATE / UPDATE
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Crea un ítem.
    /// </summary>
    /// <remarks>
    /// Responde 201 con el ítem creado. Exige tipo de ítem, unidad base, categoría (hoja y activa), marca activa,
    /// IVA de venta e IVA de compra, y al menos un código de barras con exactamente uno principal. Crea además la
    /// variante por defecto con el SKU del ítem y, para un producto, su presentación base «Unidad». El SKU es único
    /// por empresa; un SKU repetido se rechaza.
    /// </remarks>
    /// <param name="command">Datos del ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="201">Ítem creado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateItemCommand command,
        CancellationToken cancellationToken
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, cancellationToken));

    /// <summary>
    /// Actualiza un ítem.
    /// </summary>
    /// <remarks>
    /// El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no, responde 400. El tipo de ítem es
    /// inmutable después de la creación y no forma parte del cuerpo.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="command">Datos del ítem, con su id.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ítem actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateItemCommand command,
        CancellationToken cancellationToken
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Desactiva un ítem.
    /// </summary>
    /// <remarks>
    /// Baja lógica: el ítem no se elimina.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ítem desactivado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableItemCommand(id), cancellationToken));

    /// <summary>
    /// Activa un ítem.
    /// </summary>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ítem activado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableItemCommand(id), cancellationToken));
}
