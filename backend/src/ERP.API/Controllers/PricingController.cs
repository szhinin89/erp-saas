using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.UseCases.CustomerPriceListContext;
using ERP.Application.Modules.Pricing.UseCases.PriceListCustomers;
using ERP.Application.Modules.Pricing.UseCases.PriceListItems;
using ERP.Application.Modules.Pricing.UseCases.PriceLists;
using ERP.Application.Modules.Pricing.UseCases.PricingRules;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature("Listas de Precios", $"perm:{PricingPermissions.View}", "💰", "/pricing", null, 50)]
[ApiController]
[Route("api/v1/pricing")]
[Authorize]
[Produces("application/json")]
public sealed class PricingController : ControllerBase
{
    private readonly IMediator _mediator;

    public PricingController(IMediator mediator) => _mediator = mediator;

    // ══════════════════════════════════════════════════════════════════════
    // PRICE LISTS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista las listas de precios de la empresa, con filtros opcionales por estado y por texto.
    /// </summary>
    /// <param name="isActive">Si se envía, devuelve solo las listas activas (<c>true</c>) o las deshabilitadas (<c>false</c>).</param>
    /// <param name="search">Texto de búsqueda opcional que se aplica en el repositorio de listas.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Listas de precios (puede estar vacía).</response>
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PriceListDto>>), StatusCodes.Status200OK)]
    [HttpGet("price-lists")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPriceLists(
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPriceListsQuery(isActive, search), ct),
            "OK"
        );

    /// <summary>
    /// Obtiene una lista de precios por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la lista de precios.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Lista de precios.</response>
    /// <response code="404">La lista de precios no existe.</response>
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpGet("price-lists/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPriceListById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPriceListByIdQuery(id), ct));

    /// <summary>
    /// Crea una lista de precios.
    /// </summary>
    /// <remarks>
    /// El código debe ser único. Con <c>isDefault = true</c> se rechaza si ya hay otra lista predeterminada activa:
    /// para cambiar de predeterminada se usa <c>set-default</c>. La creación y la marca de predeterminada se
    /// aplican en la misma transacción. Una regla general exige <c>ruleValue</c> cuando se indica <c>ruleType</c>.
    /// </remarks>
    /// <param name="command">Código, nombre, moneda (3 caracteres), vigencia opcional y regla general opcional (tipo y valor).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Lista creada.</response>
    /// <response code="409">El código ya existe en el tenant.</response>
    /// <response code="422">Datos inválidos (nombre, moneda, vigencia o regla general), o se pidió <c>isDefault</c> y ya existe otra lista predeterminada activa.</response>
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost("price-lists")]
    [Authorize(Policy = $"perm:{PricingPermissions.Create}")]
    public async Task<IActionResult> CreatePriceList(
        [FromBody] CreatePriceListCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Actualiza los datos de una lista de precios (nombre, moneda, vigencia, regla general y marca de predeterminada).
    /// </summary>
    /// <remarks>
    /// Si <c>isDefault</c> cambia, la marca se aplica en la misma transacción que el resto de la edición; con <c>false</c> la
    /// empresa queda sin lista predeterminada (se aplica el precio base). Si la marca se rechaza, no se guarda ningún cambio de la edición.
    /// </remarks>
    /// <param name="id">Identificador de la lista; debe coincidir con <c>command.id</c>.</param>
    /// <param name="command">Nuevos datos de la lista. El código no se modifica.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Lista actualizada.</response>
    /// <response code="400">El identificador de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">La lista de precios no existe.</response>
    /// <response code="422">Datos inválidos, ya existe otra lista predeterminada activa, o se intentó marcar como predeterminada una lista deshabilitada.</response>
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPut("price-lists/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> UpdatePriceList(
        Guid id,
        [FromBody] UpdatePriceListCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// Habilita una lista de precios deshabilitada.
    /// </summary>
    /// <param name="id">Identificador de la lista de precios.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Lista habilitada (<c>data = true</c>).</response>
    /// <response code="404">La lista de precios no existe.</response>
    /// <response code="422">La lista ya está activa.</response>
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPatch("price-lists/{id:guid}/enable")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> EnablePriceList(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnablePriceListCommand(id), ct));

    /// <summary>
    /// Deshabilita una lista de precios (baja lógica; nunca se elimina).
    /// </summary>
    /// <param name="id">Identificador de la lista de precios.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Lista deshabilitada (<c>data = true</c>).</response>
    /// <response code="404">La lista de precios no existe.</response>
    /// <response code="422">La lista ya está deshabilitada o es un registro sembrado por el sistema que no puede deshabilitarse.</response>
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPatch("price-lists/{id:guid}/disable")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> DisablePriceList(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisablePriceListCommand(id), ct));

    /// <summary>
    /// Marca una lista de precios como predeterminada de la empresa.
    /// </summary>
    /// <remarks>
    /// Único punto de escritura de «lista de precios predeterminada», consumido también por Configuración → Ventas.
    /// La lista predeterminada anterior se desmarca y la indicada se marca en una sola transacción, con auditoría.
    /// </remarks>
    /// <param name="id">Identificador de la lista que pasa a ser la predeterminada.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Lista predeterminada actualizada; devuelve la lista marcada.</response>
    /// <response code="404">La lista de precios no existe.</response>
    /// <response code="422">No se puede marcar como predeterminada una lista deshabilitada.</response>
    [ProducesResponseType(typeof(ApiResponse<PriceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPatch("price-lists/{id:guid}/set-default")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> SetDefaultPriceList(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new SetDefaultPriceListCommand(id), ct));

    /// <summary>
    /// Lista los ítems activos asignados a una lista de precios (identidad y precio base), sin reglas ni excepciones.
    /// </summary>
    /// <remarks>
    /// Solo incluye asignaciones activas de ítems activos; <c>baseSalePrice</c> puede ser nulo. No valida que la lista exista:
    /// un identificador desconocido devuelve una lista vacía. El consumidor debe combinar el resultado con <c>pricing-rules</c> para conocer las excepciones.
    /// </remarks>
    /// <param name="id">Identificador de la lista de precios.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Ítems asignados, ordenados por SKU (puede estar vacía).</response>
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PriceListAssignedItemDto>>), StatusCodes.Status200OK)]
    [HttpGet("price-lists/{id:guid}/assigned-items")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetAssignedItems(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetItemsAssignedToPriceListQuery(id), ct),
            "OK"
        );

    // ══════════════════════════════════════════════════════════════════════
    // PRICE LIST CUSTOMERS (PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B) — todavía NO
    // consumido por Sales, solo administración desde /products/pricing.
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Devuelve el contexto de listas de precios de un cliente: la lista propia asignada y la predeterminada de la empresa.
    /// </summary>
    /// <remarks>
    /// Solo lectura, pensado para la cabecera de Ventas (trazabilidad de precios). Si la lista del cliente y la predeterminada
    /// son la misma, solo se informa la del cliente y la predeterminada queda nula.
    /// </remarks>
    /// <param name="customerId">Identificador del cliente.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Contexto de listas del cliente; los campos son nulos cuando no hay lista propia o predeterminada.</response>
    [ProducesResponseType(typeof(ApiResponse<CustomerPriceListContextDto>), StatusCodes.Status200OK)]
    [HttpGet("customers/{customerId:guid}/price-list-context")]
    [Authorize(Policy = $"perm:{SalesPermissions.View}")]
    public async Task<IActionResult> GetCustomerPriceListContext(
        Guid customerId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCustomerPriceListContextQuery(customerId), ct),
            "OK"
        );

    /// <summary>
    /// Lista los clientes con asignación activa a una lista de precios.
    /// </summary>
    /// <param name="id">Identificador de la lista de precios.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Clientes asignados, ordenados por nombre (puede estar vacía).</response>
    /// <response code="404">La lista de precios no existe.</response>
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PriceListCustomerDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpGet("price-lists/{id:guid}/customers")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPriceListCustomers(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new GetPriceListCustomersQuery(id), ct), "OK");

    /// <summary>
    /// Asigna un cliente a una lista de precios.
    /// </summary>
    /// <remarks>
    /// Un cliente tiene como máximo una lista activa. Si ya tiene otra y <c>confirmSwitch</c> es <c>false</c>, responde 200 con
    /// <c>status = Conflict</c> (y los datos de la lista actual) sin escribir nada; reenviar con <c>confirmSwitch = true</c> tras la
    /// confirmación del usuario desactiva la anterior y activa la nueva en una sola operación (<c>Switched</c>). Repetir la asignación
    /// a la misma lista es idempotente (<c>AlreadyActive</c>).
    /// </remarks>
    /// <param name="id">Identificador de la lista de precios destino.</param>
    /// <param name="request">Cliente a asignar y, opcionalmente, la confirmación del cambio de lista (<c>confirmSwitch</c>).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la asignación en <c>data.status</c>: <c>Assigned</c>, <c>AlreadyActive</c>, <c>Conflict</c> o <c>Switched</c>.</response>
    /// <response code="404">La lista de precios o el cliente no existen.</response>
    /// <response code="422">Identificadores vacíos.</response>
    [ProducesResponseType(typeof(ApiResponse<PriceListCustomerAssignResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost("price-lists/{id:guid}/customers")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> AssignCustomerToPriceList(
        Guid id,
        [FromBody] AssignPriceListCustomerRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new AssignCustomerToPriceListCommand(id, request.CustomerId, request.ConfirmSwitch),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Quita la asignación activa de un cliente a una lista de precios (baja lógica; nunca borrado físico).
    /// </summary>
    /// <param name="id">Identificador de la lista de precios.</param>
    /// <param name="customerId">Identificador del cliente.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Asignación desactivada (<c>data = true</c>).</response>
    /// <response code="404">El cliente no tiene una asignación activa a esa lista.</response>
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpDelete("price-lists/{id:guid}/customers/{customerId:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> RemovePriceListCustomer(
        Guid id,
        Guid customerId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new DisablePriceListCustomerCommand(id, customerId), ct)
        );

    // ══════════════════════════════════════════════════════════════════════
    // PRICING RULES
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista las excepciones de precio (reglas por ítem) de una lista de precios o de un ítem.
    /// </summary>
    /// <remarks>
    /// Debe enviarse al menos uno de los dos filtros. Cada fila incluye <c>isActive</c>, y la fecha y el usuario de la última
    /// modificación provienen de la auditoría de la regla.
    /// </remarks>
    /// <param name="priceListId">Filtra por lista de precios. Tiene prioridad si también se envía <c>itemId</c>.</param>
    /// <param name="itemId">Filtra por ítem (se usa solo si no se envía <c>priceListId</c>).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Excepciones de precio con su última modificación (puede estar vacía).</response>
    /// <response code="422">No se indicó <c>priceListId</c> ni <c>itemId</c>.</response>
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PricingRuleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpGet("pricing-rules")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPricingRules(
        [FromQuery] Guid? priceListId = null,
        [FromQuery] Guid? itemId = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetPricingRulesQuery(priceListId, itemId), ct),
            "OK"
        );

    /// <summary>
    /// Obtiene una excepción de precio por su identificador.
    /// </summary>
    /// <param name="id">Identificador de la regla de precio.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Regla de precio.</response>
    /// <response code="404">La regla no existe.</response>
    [ProducesResponseType(typeof(ApiResponse<PricingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpGet("pricing-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.View}")]
    public async Task<IActionResult> GetPricingRuleById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetPricingRuleByIdQuery(id), ct));

    /// <summary>
    /// Crea una excepción de precio para un ítem en una lista, o actualiza el valor de una ya activa.
    /// </summary>
    /// <remarks>
    /// <c>Created</c>: se creó la regla. <c>AlreadyActive</c>: ya existía activa y se actualizó su tipo y valor. <c>ExistsInactive</c>:
    /// existe una regla deshabilitada con la misma clave; no se modifica nada y se devuelven su tipo y valor para que el cliente decida
    /// si reactivarla con <c>pricing-rules/enable</c> (nunca se reactiva en silencio). Los tres casos responden 201 con el mismo envelope.
    /// La regla exige que el ítem esté asignado de forma activa a la lista.
    /// </remarks>
    /// <param name="command">Lista, ítem, tipo de regla y valor (un precio fijo no puede ser negativo).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Operación procesada; <c>data.status</c> indica <c>Created</c>, <c>AlreadyActive</c> o <c>ExistsInactive</c>.</response>
    /// <response code="404">La lista de precios no existe.</response>
    /// <response code="422">Datos inválidos, o el ítem no está asignado a la lista (o su asignación está deshabilitada).</response>
    [ProducesResponseType(typeof(ApiResponse<PricingRuleSetResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost("pricing-rules")]
    [Authorize(Policy = $"perm:{PricingPermissions.Create}")]
    public async Task<IActionResult> SetPricingRule(
        [FromBody] SetPricingRuleCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Reactiva de forma explícita una excepción de precio deshabilitada.
    /// </summary>
    /// <remarks>
    /// Nunca se dispara automáticamente desde <c>pricing-rules</c> (alta/edición): la reactivación es una decisión explícita.
    /// </remarks>
    /// <param name="command">Lista e ítem que identifican la regla (la clave única de la excepción).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Regla reactivada.</response>
    /// <response code="404">No existe una regla con esa clave.</response>
    /// <response code="422">Identificadores vacíos, el ítem no está asignado de forma activa a la lista, o la regla ya está activa.</response>
    [ProducesResponseType(typeof(ApiResponse<PricingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost("pricing-rules/enable")]
    [Authorize(Policy = $"perm:{PricingPermissions.Update}")]
    public async Task<IActionResult> EnablePricingRule(
        [FromBody] EnablePricingRuleCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>
    /// Deshabilita una excepción de precio (baja lógica; la regla nunca se elimina físicamente).
    /// </summary>
    /// <remarks>
    /// El valor previo queda en la auditoría de la regla. Para volver a usarla se reactiva con <c>pricing-rules/enable</c>.
    /// </remarks>
    /// <param name="id">Identificador de la regla de precio.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Regla deshabilitada (<c>data = true</c>).</response>
    /// <response code="404">La regla no existe.</response>
    /// <response code="422">La regla ya está deshabilitada.</response>
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpDelete("pricing-rules/{id:guid}")]
    [Authorize(Policy = $"perm:{PricingPermissions.Delete}")]
    public async Task<IActionResult> RemovePricingRule(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new RemovePricingRuleCommand(id), ct));
}

public sealed record AssignPriceListCustomerRequest(Guid CustomerId, bool ConfirmSwitch = false);
