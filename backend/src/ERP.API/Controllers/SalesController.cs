using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Application.Modules.Sales.UseCases.GetDailySalesReport;
using ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;
using ERP.Application.Modules.Sales.UseCases.GetSalesItemPricing;
using ERP.Application.Modules.Sales.UseCases.GetSalesReceiptPrintPayload;
using ERP.Application.Modules.Sales.UseCases.GetSalesRuntimeContext;
using ERP.Application.Modules.Sales.UseCases.PreviewSalesRepricing;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Ventas", $"perm:{SalesPermissions.View}", "💰", "/sales", null, 65)]
[ApiController]
[Route("api/v1/sales")]
[Authorize]
[Produces("application/json")]
public sealed class SalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Crea una factura de venta en estado borrador.
    /// </summary>
    /// <remarks>
    /// El borrador solo se guarda: no consume secuencial SRI, no mueve inventario, no genera cuenta
    /// por cobrar ni asientos. Todo eso ocurre al autorizar (<c>POST {id}/authorize</c>).
    ///
    /// Requiere que el usuario tenga una sesión de caja abierta: el punto de emisión y el tipo de
    /// emisión (electrónica o física) se toman de la caja de esa sesión, nunca del cuerpo. El punto
    /// de emisión debe existir y estar activo. El cliente debe existir, estar activo y tener el rol
    /// Cliente; las bodegas informadas deben pertenecer a la sucursal activa. Sin
    /// <c>docTypeCode</c> se usa «01» (factura).
    ///
    /// La condición de pago se resuelve en este orden: la explícita (<c>paymentTermId</c>), la del
    /// cliente y, si los pagos cubren el total, Contado. Si queda saldo pendiente sin condición
    /// explícita ni del cliente, se exige una fecha de vencimiento, un cronograma o una condición
    /// de crédito por defecto de la empresa. El cronograma se dimensiona sobre el saldo pendiente
    /// (total menos pagos aplicados), no sobre el total. Los pagos son opcionales; un pago por
    /// transferencia exige cuenta bancaria destino. La numeración inicial es provisional
    /// (<c>DRAFT-…</c>).
    /// </remarks>
    /// <param name="command">Cliente, fecha de emisión (<c>YYYY-MM-DD</c>), líneas y, opcionalmente, condición de pago, pagos y cronograma.</param>
    /// <response code="201">Borrador creado.</response>
    /// <response code="404">El cliente no existe.</response>
    /// <response code="422">No hay caja abierta o su punto de emisión no es utilizable; el cliente está inactivo o no tiene rol Cliente; bodega fuera de la sucursal; condición de pago, pago o línea inválidos; falta definir el crédito pendiente; o validación del cuerpo.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{SalesPermissions.Create}")]
    [ProducesResponseType(typeof(ApiResponse<SalesInvoiceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateSalesDraftCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Reemplaza el contenido de un borrador de venta (cliente, fecha, líneas, pagos y cronograma).
    /// </summary>
    /// <remarks>
    /// Solo se puede modificar una factura en borrador; una ya autorizada o anulada se rechaza con
    /// 422. La actualización es atómica: si cualquier validación falla después de empezar a
    /// modificar, no queda ningún cambio. Si cambia la condición de pago, el cronograma se regenera
    /// y descarta la personalización anterior. El <c>id</c> de la ruta debe coincidir con el
    /// <c>id</c> del cuerpo.
    /// </remarks>
    /// <param name="id">Id de la factura en borrador.</param>
    /// <param name="command">Contenido completo del borrador; su <c>id</c> debe coincidir con el de la ruta.</param>
    /// <response code="200">Borrador actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">La factura no existe en la sucursal activa, o el cliente no existe.</response>
    /// <response code="422">La factura ya no es un borrador; cliente inactivo; bodega fuera de la sucursal; condición de pago, pago o línea inválidos; o validación del cuerpo.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SalesInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdateSalesDraftCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// Obtiene el detalle de una factura de venta de la sucursal activa, con su estado electrónico.
    /// </summary>
    /// <param name="id">Id de la factura.</param>
    /// <response code="200">Detalle de la factura.</response>
    /// <response code="404">La factura no existe o pertenece a otra sucursal.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetSalesInvoiceByIdQuery(id), ct));

    /// <summary>
    /// Obtiene los datos necesarios para imprimir el comprobante (tirilla) de una factura autorizada.
    /// </summary>
    /// <remarks>
    /// Solo existe para facturas autorizadas de la sucursal activa: un borrador o una factura
    /// anulada responden 404.
    /// </remarks>
    /// <param name="invoiceId">Id de la factura.</param>
    /// <response code="200">Datos de impresión del comprobante.</response>
    /// <response code="404">La factura no existe, pertenece a otra sucursal o no está autorizada.</response>
    [HttpGet("invoices/{invoiceId:guid}/receipt-print-payload")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReceiptPrintPayloadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceiptPrintPayload(Guid invoiceId, CancellationToken ct) =>
        this.ToOkOrNotFound(
            await _mediator.Send(new GetSalesReceiptPrintPayloadQuery(invoiceId), ct)
        );

    /// <summary>
    /// Lista facturas de venta paginadas, de la más reciente a la más antigua.
    /// </summary>
    /// <remarks>
    /// No filtra por sucursal: exige una sucursal activa autorizada, pero el listado no se limita a
    /// ella. Se ordena por fecha de emisión descendente y, a igualdad, por fecha de creación
    /// descendente.
    /// </remarks>
    /// <param name="search">Texto opcional; coincide con el número de factura o con el nombre del cliente.</param>
    /// <param name="status">Estado opcional de la factura (sin distinguir mayúsculas). Un valor desconocido se ignora y no filtra.</param>
    /// <param name="pageNumber">Página, empezando en 1.</param>
    /// <param name="pageSize">Elementos por página.</param>
    /// <response code="200">Página de facturas con el total.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetSalesInvoiceListQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Reporte básico de ventas por rango de fechas (piloto Sumak). Sin fechas, usa el día
    /// actual de la empresa (su zona horaria). Company-scoped — ver GetDailySalesReportQuery.
    /// </summary>
    /// <remarks>
    /// Las filas incluyen facturas en cualquier estado, con su estado real; los totales suman
    /// únicamente las autorizadas (los borradores y las anuladas no cuentan como venta del
    /// período).
    /// </remarks>
    /// <param name="dateFrom">Fecha inicial de emisión (<c>YYYY-MM-DD</c>); por defecto, hoy en la zona horaria de la empresa.</param>
    /// <param name="dateTo">Fecha final de emisión (<c>YYYY-MM-DD</c>); por defecto, hoy en la zona horaria de la empresa.</param>
    /// <response code="200">Filas del período y totales.</response>
    /// <response code="422">La fecha inicial es posterior a la final.</response>
    [HttpGet("report")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetDailyReport(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetDailySalesReportQuery(dateFrom, dateTo), ct),
            "OK"
        );

    /// <summary>
    /// Aplica un porcentaje de descuento global a todas las líneas de un borrador.
    /// </summary>
    /// <remarks>
    /// Solo sobre borradores. La empresa puede deshabilitar los descuentos manuales o limitar su
    /// porcentaje máximo en sus preferencias de ventas; un porcentaje 0 siempre se acepta cuando
    /// está dentro del rango 0–100.
    /// </remarks>
    /// <param name="id">Id de la factura en borrador.</param>
    /// <param name="request">Porcentaje de descuento (0 a 100).</param>
    /// <response code="200">Borrador con el descuento aplicado.</response>
    /// <response code="400">El porcentaje está fuera del rango 0–100.</response>
    /// <response code="404">La factura no existe en la sucursal activa.</response>
    /// <response code="422">La factura ya no es un borrador, la empresa no permite descuentos manuales o el porcentaje supera el máximo permitido.</response>
    [HttpPost("{id:guid}/apply-discount")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SalesInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ApplyGlobalDiscount(
        Guid id,
        [FromBody] SalesApplyDiscountRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ApplySalesDiscountCommand(id, request.DiscountPct), ct)
        );

    /// <summary>
    /// Autoriza (confirma) una factura de venta en borrador y ejecuta sus efectos.
    /// </summary>
    /// <remarks>
    /// Transición Borrador → Autorizada. Es idempotente: si la factura ya está autorizada responde
    /// 200 con su estado actual sin repetir efectos. Una factura anulada se rechaza con 422.
    ///
    /// Antes de cualquier efecto se validan: condición de pago activa; fecha de emisión no futura
    /// ni anterior a 90 días (fecha de la empresa); códigos IVA/ICE; destino de cada cobro (cuenta
    /// bancaria activa de la empresa, caja activa de la sesión, o el propio medio de pago); política
    /// fiscal de Consumidor Final; consistencia entre condición de pago y método de pago; ítems de
    /// la empresa con bodega según su naturaleza; stock disponible cuando la política de la empresa
    /// lo exige; y los requisitos de emisión (electrónica o física) del punto de emisión.
    ///
    /// Efectos, en una sola transacción: asigna el número secuencial SRI; congela líneas y totales;
    /// registra egresos de inventario (Kardex) por línea con bodega; genera la cuenta por cobrar
    /// por el saldo pendiente si la venta es a crédito; y registra la obligación contable, cuyo
    /// fallo posterior queda registrado como fallido sin revertir la venta. Después del commit se ejecuta
    /// la emisión (envío al SRI o comprobante físico): si falla, la factura sigue autorizada
    /// internamente y el motivo viaja en <c>electronicIssueError</c> de esta misma respuesta.
    /// </remarks>
    /// <param name="id">Id de la factura en borrador.</param>
    /// <response code="200">Factura autorizada (o ya autorizada), con su estado electrónico.</response>
    /// <response code="404">La factura no existe en la sucursal activa.</response>
    /// <response code="422">La factura está anulada, o falla alguna de las validaciones previas (condición de pago, fecha, impuestos, destino de cobro, política fiscal, ítems/bodegas, stock, requisitos de emisión o secuencial fuera de rango).</response>
    [HttpPost("{id:guid}/authorize")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SalesInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AuthorizeInvoice(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new AuthorizeSalesInvoiceCommand(id), ct));

    /// <summary>
    /// Anula localmente una factura de venta autorizada y revierte sus efectos.
    /// </summary>
    /// <remarks>
    /// Transición Autorizada → Anulada, en una sola transacción: si algo falla no queda ningún
    /// cambio. Solo se anulan facturas autorizadas y con motivo. No se permite si existe una
    /// devolución autorizada de la factura, si su cuenta por cobrar no admite la
    /// cancelación, si su comprobante electrónico ya está autorizado por el SRI (se debe registrar
    /// una devolución/nota de crédito) o si está en proceso ante el SRI.
    ///
    /// Efectos: cancela la cuenta por cobrar si existe, registra el reingreso de inventario por
    /// línea con bodega (movimiento de devolución de venta, fechado en el día de la empresa) y
    /// levanta el evento de anulación que reversa los asientos contables.
    /// </remarks>
    /// <param name="id">Id de la factura.</param>
    /// <param name="request">Motivo de la anulación (obligatorio).</param>
    /// <response code="200">Factura anulada.</response>
    /// <response code="400">El motivo está vacío (se evalúa sobre una factura autorizada).</response>
    /// <response code="404">La factura no existe en la sucursal activa.</response>
    /// <response code="422">La factura no está autorizada, tiene una devolución autorizada, su comprobante electrónico no permite anulación local, o su cuenta por cobrar no puede cancelarse.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{SalesPermissions.Update}")]
    [ProducesResponseType(typeof(ApiResponse<SalesInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelInvoice(
        Guid id,
        [FromBody] CancelSalesRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new CancelSalesInvoiceCommand(id, request.Reason), ct)
        );

    /// <summary>
    /// Devuelve los valores por defecto para inicializar una nueva factura de venta.
    /// </summary>
    /// <remarks>
    /// Incluye tipo de comprobante, forma de pago SRI, punto de emisión, bodega y condición de pago
    /// por defecto, los valores de respaldo y las advertencias de configuración. La bodega por
    /// defecto se resuelve en el servidor según la sucursal activa.
    /// </remarks>
    /// <response code="200">Valores por defecto de la factura.</response>
    [HttpGet("invoice-defaults")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SalesInvoiceDefaultsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoiceDefaults(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSalesInvoiceDefaultsQuery(), ct));

    /// <summary>
    /// Contexto agregado para Ventas y futuras pantallas (POS, facturación rápida, pedidos, app
    /// móvil): política fiscal de Consumidor Final + defaults de factura, en un solo GET.
    /// </summary>
    /// <response code="200">Política fiscal vigente y valores por defecto de la factura.</response>
    [HttpGet("runtime-context")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SalesRuntimeContextDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuntimeContext(CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetSalesRuntimeContextQuery(), ct));

    /// <summary>
    /// Busca ítems vendibles para agregarlos como línea de una factura, con su precio resuelto.
    /// </summary>
    /// <remarks>
    /// Con menos de 2 caracteres en <c>q</c> responde una lista vacía. El tamaño de página se limita
    /// al rango 1–20. El precio de cada resultado lo resuelve el motor de precios, con el cliente
    /// informado si lo hay.
    /// </remarks>
    /// <param name="q">Texto de búsqueda (mínimo 2 caracteres).</param>
    /// <param name="warehouseId">Bodega opcional para consultar existencias; debe pertenecer a la sucursal activa.</param>
    /// <param name="pageSize">Máximo de resultados; se limita a 1–20.</param>
    /// <param name="customerId">Cliente opcional para resolver su lista de precios.</param>
    /// <response code="200">Ítems encontrados (posiblemente vacío).</response>
    /// <response code="422">La bodega no pertenece a la sucursal activa o no se pudo resolver el precio de algún ítem.</response>
    [HttpGet("item-search")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<InvoiceItemSearchResultDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SearchItems(
        [FromQuery] string? q,
        [FromQuery] Guid? warehouseId,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? customerId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SearchItemsForInvoiceQuery(
                    q ?? string.Empty,
                    warehouseId,
                    pageSize,
                    customerId
                ),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Precio e impuestos oficiales de un ítem, resueltos vía el Pricing Engine v2
    /// (IPricingResolver) — se invoca en el momento puntual en que el usuario
    /// selecciona un producto para agregarlo como línea de venta.
    /// </summary>
    /// <param name="itemId">Id del ítem.</param>
    /// <param name="customerId">Cliente opcional para resolver su lista de precios; sin él se usa la lista por defecto de la empresa.</param>
    /// <response code="200">Precio, impuestos y descuento máximo del ítem.</response>
    /// <response code="404">El ítem no existe.</response>
    /// <response code="422">El ítem está inactivo o no está habilitado para venta, o no se pudo resolver su precio.</response>
    [HttpGet("items/{itemId:guid}/pricing")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<SalesItemPricingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetItemPricing(
        Guid itemId,
        [FromQuery] Guid? customerId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetSalesItemPricingQuery(itemId, customerId), ct)
        );

    /// <summary>
    /// SALES-CUSTOMER-REPRICING-PREVIEW-06C1: previsualiza cómo cambiaría el precio de un
    /// conjunto de ítems si el cliente pasara de OldCustomerId a NewCustomerId — solo lectura, no
    /// aplica ningún cambio en ninguna factura. Pensado para el flujo de cambio de cliente con
    /// líneas ya cargadas (aún no implementado en el frontend).
    /// </summary>
    /// <param name="query">Ítems y clientes anterior y nuevo (ambos opcionales).</param>
    /// <response code="200">Comparación de precio por ítem entre el cliente anterior y el nuevo.</response>
    /// <response code="422">No se pudo resolver el precio de los ítems para alguno de los dos clientes.</response>
    [HttpPost("pricing/repricing-preview")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<SalesRepricingPreviewItemDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreviewRepricing(
        [FromBody] PreviewSalesRepricingQuery query,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(query, ct));
}

public record SalesApplyDiscountRequest(decimal DiscountPct);

public record CancelSalesRequest(string Reason);
