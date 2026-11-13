using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Purchases.UseCases.GetPurchaseItemContext;
using ERP.Application.Modules.Purchases.UseCases.GetPurchasesBySupplierReport;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Compras", $"perm:{PurchasePermissions.View}", "🛒", "/purchases", null, 60)]
[ApiController]
[Route("api/v1/purchases")]
[Authorize]
[Produces("application/json")]
public sealed class PurchasesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchasesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Crea una compra en estado Draft.
    /// </summary>
    /// <remarks>
    /// Responde 201. Exige un proveedor activo con configuración SRI y una condición de pago con al menos una
    /// cuota. Cada línea debe tener la bodega en la sucursal de la compra, un ítem de la empresa y una presentación
    /// activa del ítem. Un borrador no genera inventario, cuentas por pagar, contabilidad ni retención.
    /// </remarks>
    /// <param name="command">Proveedor, condición de pago, fechas, datos del documento y líneas.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Borrador de compra creado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{PurchasePermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreatePurchaseDraftCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Actualiza una compra en Draft.
    /// </summary>
    /// <remarks>
    /// Reemplaza los datos del borrador. El id de la ruta debe coincidir con el <c>id</c> del cuerpo; si no,
    /// responde 400. Flete y otros costos no se envían aquí: se gestionan con <c>cost-distributions</c>.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="command">Datos completos del borrador, con su id.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Borrador actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el id del cuerpo, o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdatePurchaseDraftCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// Obtiene una compra con su detalle.
    /// </summary>
    /// <param name="id">Id de la compra.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Compra.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPurchaseByIdQuery(id), ct));

    /// <summary>
    /// Busca una compra por la clave de acceso del comprobante.
    /// </summary>
    /// <param name="accessKey">Clave de acceso del comprobante electrónico del proveedor.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Compra del comprobante.</response>
    [HttpGet("by-access-key/{accessKey}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseAccessKeyLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAccessKey(string accessKey, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPurchaseByAccessKeyQuery(accessKey), ct));

    /// <summary>
    /// Lista las compras con filtros y paginación.
    /// </summary>
    /// <param name="search">Texto libre de búsqueda.</param>
    /// <param name="status">Filtra por estado de la compra.</param>
    /// <param name="pageNumber">Número de página, base 1.</param>
    /// <param name="pageSize">Cantidad de compras por página.</param>
    /// <param name="supplierId">Filtra por proveedor.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página de compras.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? supplierId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchaseListQuery(search, status, pageNumber, pageSize, supplierId),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Reporte de compras por proveedor.
    /// </summary>
    /// <remarks>
    /// Sin fechas usa el día actual (UTC). Alcance de empresa.
    /// </remarks>
    /// <param name="dateFrom">Fecha de negocio inicial (<c>YYYY-MM-DD</c>).</param>
    /// <param name="dateTo">Fecha de negocio final (<c>YYYY-MM-DD</c>).</param>
    /// <param name="supplierId">Filtra por proveedor; omitido, incluye todos.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Reporte de compras por proveedor.</response>
    [HttpGet("report")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<PurchasesBySupplierReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierReport(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] Guid? supplierId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchasesBySupplierReportQuery(dateFrom, dateTo, supplierId),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Aplica un descuento global a un borrador de compra.
    /// </summary>
    /// <remarks>
    /// El porcentaje va de 0 a 100.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="request">Porcentaje de descuento global.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Descuento aplicado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/apply-discount")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyGlobalDiscount(
        Guid id,
        [FromBody] ApplyDiscountRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ApplyGlobalDiscountCommand(id, request.DiscountPct), ct)
        );

    /// <summary>
    /// Recalcula los importes de un borrador de compra.
    /// </summary>
    /// <param name="id">Id de la compra.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Compra recalculada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/recalculate")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recalculate(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new RecalculatePurchaseCommand(id), ct));

    /// <summary>
    /// Crea o reemplaza una distribución de costo de la compra.
    /// </summary>
    /// <remarks>
    /// Crea o reemplaza —nunca suma— la distribución indicada; reintentar el mismo cuerpo no duplica importes ni
    /// cambia la revisión. Solo se permite sobre un borrador. El tipo de costo es un texto sensible a mayúsculas
    /// (<c>Freight</c> u <c>OtherCost</c>); otro valor responde 400. Flete y otros costos tienen como única fuente
    /// la distribución con sus asignaciones por línea destino.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="distributionId">Id de la distribución de costo (lo fija el cliente).</param>
    /// <param name="request">Tipo de costo, revisión esperada, importe, líneas destino y línea origen opcional.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Distribución de costo guardada.</response>
    /// <response code="400">El tipo de costo no es válido (<c>Freight</c> u <c>OtherCost</c>), o la operación incumple una regla de negocio.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/cost-distributions/{distributionId:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceCostDistribution(
        Guid id,
        Guid distributionId,
        [FromBody] ReplaceCostDistributionRequest request,
        CancellationToken ct
    )
    {
        // Payload string ("Freight"/"OtherCost"), convertido con TryParse case-sensitive.
        if (!Enum.TryParse<PurchaseCostType>(request.CostType, ignoreCase: false, out var costType)
            || !Enum.IsDefined(costType))
            return this.ApiBadRequest(
                $"El tipo de costo '{request.CostType}' no es válido. Valores permitidos: Freight, OtherCost."
            );

        return this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReplacePurchaseCostDistributionCommand(
                    id,
                    distributionId,
                    costType,
                    request.ExpectedRevision,
                    request.Amount,
                    request.TargetLineIds,
                    request.SourceLineId
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Elimina una distribución de costo de la compra.
    /// </summary>
    /// <remarks>
    /// Solo se permite sobre un borrador. Exige la revisión esperada de la distribución.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="distributionId">Id de la distribución de costo.</param>
    /// <param name="expectedRevision">Revisión de la distribución que el cliente conoce; protege contra ediciones concurrentes.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Distribución de costo eliminada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpDelete("{id:guid}/cost-distributions/{distributionId:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCostDistribution(
        Guid id,
        Guid distributionId,
        [FromQuery] long expectedRevision,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new RemovePurchaseCostDistributionCommand(id, distributionId, expectedRevision),
                ct
            )
        );

    /// <summary>
    /// Carga los PVP vigentes de las líneas del borrador.
    /// </summary>
    /// <param name="id">Id de la compra.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">PVP cargados.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/load-pvp")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LoadPvpSnapshots(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new LoadPvpSnapshotsCommand(id), ct));

    /// <summary>
    /// Confirma una compra Draft.
    /// </summary>
    /// <remarks>
    /// En una sola transacción, todo o nada: congela costos e impuestos, genera una entrada de inventario por cada
    /// línea de producto al costo unitario con flete y gastos incluidos, crea la cuenta por pagar si el total es
    /// mayor que cero, puede emitir la retención y actualiza el precio base de venta del ítem cuando el PVP de la
    /// línea difiere. El cuerpo es opcional: puede traer el cronograma de pagos y la retención. Se rechaza si la
    /// compra no está en Draft o no tiene líneas, si la condición de pago está inactiva, si hay descuadre no
    /// justificable contra el XML autorizado o si una línea de producto no tiene bodega. La transmisión de la
    /// retención ocurre después del commit.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="request">Cronograma de pagos y retención opcionales.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Compra confirmada.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmPurchase(
        Guid id,
        [FromBody] ConfirmPurchaseRequest? request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ConfirmPurchaseCommand(id, request?.Schedule, request?.Retention),
                ct
            )
        );

    /// <summary>
    /// Anula una compra confirmada.
    /// </summary>
    /// <remarks>
    /// Requiere motivo (máximo 500 caracteres). Se rechaza si la compra no está confirmada, tiene pagos aplicados,
    /// una devolución autorizada, saldo a favor aplicado, una nota de crédito aplicada o una salida de inventario
    /// posterior a la entrada original. Reversa el inventario y cancela la cuenta por pagar y la retención emitida.
    /// Anular localmente no anula la factura del proveedor ante el SRI.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="request">Motivo de la anulación y, opcionalmente, la solicitud de anulación SRI de la retención.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Compra anulada.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPurchase(
        Guid id,
        [FromBody] CancelPurchaseRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CancelPurchaseCommand(id, request.Reason, request.RequestSriAnnulment),
                ct
            )
        );

    /// <summary>
    /// Descarta un borrador de compra.
    /// </summary>
    /// <remarks>
    /// Baja lógica: cambia el estado a <c>Cancelled</c> y no elimina el registro. Solo se permite desde Draft; una
    /// compra confirmada se anula con <c>POST {id}/cancel</c>. No hay reversos porque un borrador nunca generó
    /// inventario, cuenta por pagar, contabilidad ni retención.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="reason">Motivo opcional (máximo 500 caracteres).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Borrador descartado.</response>
    /// <response code="404">La compra no existe en la sucursal activa.</response>
    /// <response code="422">La compra ya no está en borrador.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DiscardDraft(
        Guid id,
        [FromQuery] string? reason,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(new DiscardPurchaseDraftCommand(id, reason), ct));

    /// <summary>
    /// Contexto completo de un ítem para el detalle de compra.
    /// </summary>
    /// <remarks>
    /// Reúne en una sola consulta la información que el detalle de compra necesita del ítem.
    /// </remarks>
    /// <param name="itemId">Id del ítem.</param>
    /// <param name="warehouseId">Bodega para la que se consulta el contexto; opcional.</param>
    /// <param name="supplierId">Proveedor para el que se consulta el contexto; opcional.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contexto del ítem.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("items/context")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<PurchaseItemContextDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemContext(
        [FromQuery] Guid itemId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? supplierId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetPurchaseItemContextQuery(itemId, warehouseId, supplierId),
                ct
            )
        );

    /// <summary>
    /// Actualiza el PVP de una línea de la compra.
    /// </summary>
    /// <param name="id">Id de la compra.</param>
    /// <param name="lineId">Id de la línea de la compra.</param>
    /// <param name="request">Nuevo PVP de la línea.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">PVP de la línea actualizado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/lines/{lineId:guid}/update-pvp")]
    [Authorize(Policy = $"perm:{PurchasePermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLinePvp(
        Guid id,
        Guid lineId,
        [FromBody] UpdatePvpRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new UpdateLinePvpCommand(id, lineId, request.NewPvp), ct)
        );

    // ══════════════════════════════════════════════════════════════════════
    // RETENCIONES
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vista previa de la retención de una compra en borrador.
    /// </summary>
    /// <remarks>
    /// Muestra la elegibilidad y los montos propuestos. La retención se emite dentro de <c>POST {id}/confirm</c>;
    /// no existe una emisión posterior.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Vista previa de la retención.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}/retention-preview")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<RetentionPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRetentionPreview(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new CalculateRetentionQuery(id), ct));

    // ══════════════════════════════════════════════════════════════════════
    // RetentionDocument transversal (Modules/Retentions) — única vía de retenciones para Compras
    // desde PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E. La EMISIÓN ocurre solo dentro de
    // ConfirmPurchase (ZH-PURCHASE-RETENTION-CONFIRM-01) y la ANULACIÓN solo como consecuencia de
    // anular la compra (CancelPurchase → RetentionCanceller, ZH-RETENTION-CANCELLATION-LIFECYCLE-01:
    // la retención forma parte de la confirmación y Cancelled es terminal). Aquí queda solo la
    // lectura, que reutiliza GetRetentionBySourceQuery fijando SourceDocumentType=PurchaseInvoice y
    // SourceDocumentId=id desde la RUTA (nunca desde el body) con la policy de Compras.

    /// <summary>
    /// Retención activa de una compra.
    /// </summary>
    /// <remarks>
    /// Devuelve la retención transversal activa sobre la compra, si existe. Que todavía no se haya emitido ninguna
    /// es un estado normal: responde 200 con <c>data</c> nulo, no un error.
    /// </remarks>
    /// <param name="id">Id de la compra.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Retención de la compra, o <c>data</c> nulo si no hay ninguna.</response>
    [HttpGet("{id:guid}/retention")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<RetentionDocumentDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRetention(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetRetentionBySourceQuery(RetentionSourceDocumentType.PurchaseInvoice, id),
                ct
            )
        );

    // ══════════════════════════════════════════════════════════════════════
    // RESUMEN FISCAL POR IMPUESTO (FLOW-READY-02D.1)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resumen fiscal persistido de una factura de compra.
    /// </summary>
    /// <remarks>
    /// Agrupado por combinación de impuesto (código y tarifa de IVA e ICE). Se genera al confirmar la compra, desde
    /// las líneas ya congeladas, y nunca se recalcula desde catálogos vigentes. Vacío si la factura sigue en
    /// borrador.
    /// </remarks>
    /// <param name="invoiceId">Id de la factura de compra.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resumen fiscal de la factura.</response>
    /// <response code="404">La factura no existe.</response>
    [HttpGet("/api/v1/purchases/invoices/{invoiceId:guid}/tax-summaries")]
    [Authorize(Policy = $"perm:{PurchasePermissions.View}")]
    [ProducesResponseType(
        typeof(Contracts.ApiResponse<IReadOnlyList<PurchaseInvoiceTaxSummaryDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(Contracts.ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaxSummaries(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetPurchaseInvoiceTaxSummariesQuery(invoiceId), ct)
        );
}

public record UpdatePvpRequest(decimal NewPvp);

public record ApplyDiscountRequest(decimal DiscountPct);

public record ReplaceCostDistributionRequest(
    string CostType,
    long ExpectedRevision,
    decimal? Amount,
    List<Guid> TargetLineIds,
    Guid? SourceLineId = null
);

/// <summary>
/// <see cref="RequestSriAnnulment"/> (ZH-RETENTION-SRI-ANNULMENT-01): si la retención de la compra ya
/// está AUTORIZADA, <c>true</c> inicia la anulación ante el SRI (la compra sigue confirmada hasta que el
/// SRI confirme ANULADO) en lugar de responder <c>ELECTRONIC_DOCUMENT_REQUIRES_SRI_ANNULMENT</c>.
/// </summary>
public record CancelPurchaseRequest(string Reason, bool RequestSriAnnulment = false);

/// <summary>
/// <see cref="Retention"/>: ZH-PURCHASE-RETENTION-CONFIRM-01 — intención opcional de emitir la
/// retención en esta misma confirmación (mismo contrato <see cref="RetentionIntent"/> que Gastos).
/// Ausente o null confirma exactamente igual que antes, sin retención.
/// </summary>
public record ConfirmPurchaseRequest(
    List<ConfirmScheduleInput>? Schedule = null,
    RetentionIntent? Retention = null
);
