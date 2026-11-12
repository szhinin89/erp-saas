using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Retentions.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[AppFeature(
    "Documentos de gastos",
    $"perm:{ExpensePermissions.DocumentsView}",
    "ReceiptText",
    "/expenses/documents",
    $"perm:{ExpensePermissions.CatalogView}",
    50,
    IsVisibleInMenu = false
)]
[ApiController]
[Route("api/v1/expenses/documents")]
[Authorize]
[Produces("application/json")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpensesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista paginada de documentos de gasto de la sucursal activa.</summary>
    /// <remarks>
    /// Solo devuelve gastos de la sucursal activa. <c>search</c> filtra por texto contenido en el número de documento,
    /// el nombre o la identificación del proveedor. Orden: fecha de emisión descendente.
    /// </remarks>
    /// <param name="search">Texto a buscar en número de documento, nombre o identificación del proveedor (opcional).</param>
    /// <param name="status">Estado: <c>Draft</c>, <c>Confirmed</c> o <c>Cancelled</c>, sin distinguir mayúsculas (opcional).</param>
    /// <param name="pageNumber">Número de página, desde 1 (por defecto 1).</param>
    /// <param name="pageSize">Tamaño de página, de 1 a 100 (por defecto 25).</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página de gastos con el total de coincidencias.</response>
    /// <response code="422"><c>status</c> desconocido, <c>pageNumber</c> menor que 1 o <c>pageSize</c> fuera de 1–100.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDocumentListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ListExpenseDocumentsQuery(search, status, pageNumber, pageSize),
                ct
            ),
            "OK"
        );

    /// <summary>Obtiene el detalle de un documento de gasto, con sus líneas.</summary>
    /// <param name="id">Id del documento de gasto.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Detalle del gasto.</response>
    /// <response code="404">El gasto no existe o pertenece a otra sucursal.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetExpenseDocumentByIdQuery(id), ct));

    /// <summary>Crea un gasto en estado borrador.</summary>
    /// <remarks>
    /// El borrador no genera cuenta por pagar, asiento contable ni retención: esos efectos ocurren al confirmar.
    /// La política de flujo documental de la empresa debe permitir borradores. El proveedor debe existir, estar activo y
    /// tener rol de proveedor activo; la condición de pago es la indicada o la predeterminada del proveedor, y el
    /// vencimiento no puede ser anterior a la emisión. Cada línea debe apuntar a una subcategoría de gasto activa.
    /// No se acepta una nota de crédito. No puede repetirse el mismo proveedor, tipo y número de documento, ni una
    /// clave de acceso SRI ya registrada como compra o gasto. Si se envían <c>receptionDocumentId</c> o <c>accessKey</c>,
    /// los datos deben coincidir con la factura recibida (proveedor, tipo, número y fecha de emisión) y se toma su
    /// autorización. El campo <c>retention</c> del cuerpo se ignora en este endpoint (solo lo usa la creación confirmada).
    /// </remarks>
    /// <param name="request">Cabecera y líneas del gasto.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Borrador creado.</response>
    /// <response code="404">El proveedor, una subcategoría de línea o el documento de recepción de origen no existen.</response>
    /// <response code="409">Gasto duplicado (proveedor, tipo y número) o clave de acceso SRI / factura recibida ya registrada como compra o gasto.</response>
    /// <response code="422">Datos inválidos: política de flujo, proveedor inactivo o sin rol, condición de pago, fechas, líneas o IVA.</response>
    [HttpPost]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsCreate}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDocumentDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateExpenseDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new CreateExpenseDraftCommand(
                    request.SupplierId,
                    request.IssueDate,
                    request.AccountingDate,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.PaymentTermId,
                    request.DueDate,
                    request.Lines,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.Notes,
                    request.TaxSupportCode,
                    request.ReceptionDocumentId,
                    request.AccessKey
                ),
                ct
            )
        );

    /// <summary>Edita un gasto en borrador.</summary>
    /// <remarks>
    /// Solo gastos en estado <c>Draft</c> de la sucursal activa. Reemplaza la cabecera y todas las líneas con las
    /// enviadas; no modifica la clave de acceso ni el documento de recepción de origen. Aplican las mismas reglas de
    /// proveedor, condición de pago, vencimiento y líneas que al crear el borrador.
    /// </remarks>
    /// <param name="id">Id del gasto.</param>
    /// <param name="request">Cabecera y líneas completas del gasto.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Borrador actualizado.</response>
    /// <response code="404">El gasto no existe en la sucursal activa, o el proveedor o una subcategoría de línea no existen.</response>
    /// <response code="409">Ya existe otro gasto con el mismo proveedor, tipo y número de documento.</response>
    /// <response code="422">El gasto no está en borrador o los datos son inválidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromBody] UpdateExpenseDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdateExpenseDraftCommand(
                    id,
                    request.SupplierId,
                    request.IssueDate,
                    request.AccountingDate,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.PaymentTermId,
                    request.DueDate,
                    request.Lines,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.Notes,
                    request.TaxSupportCode
                ),
                ct
            )
        );

    // RETENTIONS-ELIGIBILITY-01 — endpoint delgado (solo delega al mediator), de solo lectura.
    // Reutiliza el permiso de lectura de Gastos (DocumentsView) porque Retentions todavía no tiene
    // su propio catálogo de permisos finos (ver RETENTIONS-MODULE-DESIGN-01.md).
    /// <summary>Evalúa si un gasto en borrador es elegible para retención.</summary>
    /// <remarks>
    /// Solo lectura: no crea ni emite ninguna retención. Se evalúa antes de confirmar, por lo que solo acepta gastos
    /// en estado <c>Draft</c>. Cuando no es elegible, la respuesta incluye las razones.
    /// </remarks>
    /// <param name="id">Id del gasto en borrador.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de elegibilidad, bases retenibles y razones.</response>
    /// <response code="404">El gasto no existe o pertenece a otra sucursal.</response>
    /// <response code="422">El gasto no está en borrador.</response>
    [HttpGet("{id:guid}/retention-eligibility")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    [ProducesResponseType(typeof(ApiResponse<RetentionEligibilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetRetentionEligibility(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, id),
                ct
            )
        );

    // RETENTIONS-API-EXPENSES-01E — el body es opcional (sin body, o con Retention null, confirma sin
    // retención). El mapeo a RetentionIntent es 1:1, sin lógica de negocio en el controller: la
    // elegibilidad y el cálculo se resuelven en ConfirmExpenseDocumentHandler.
    /// <summary>Confirma un gasto en borrador, con retención opcional.</summary>
    /// <remarks>
    /// Pasa el gasto de <c>Draft</c> a <c>Confirmed</c> en una única operación atómica: congela la cuenta contable de
    /// cada línea y, si la política de la empresa genera cuentas por pagar al confirmar, crea la cuenta por pagar (una
    /// cuota por el total, con vencimiento en <c>dueDate</c> o en la fecha contable). Las obligaciones contables se
    /// registran con el gasto y se despachan después del commit: un fallo contable no revierte el gasto. Requiere que la
    /// condición de pago del borrador siga activa y que cada línea apunte a una subcategoría activa.
    /// Sin cuerpo (o con <c>retention</c> nulo) confirma sin retención. Con <c>retention.appliesRetention = true</c> se
    /// emite la retención y se aplica a la cuenta por pagar en la misma transacción (todo o nada); su transmisión
    /// electrónica empieza después del commit y su fallo no afecta al gasto. No es idempotente: un gasto ya confirmado
    /// responde 422.
    /// </remarks>
    /// <param name="id">Id del gasto en borrador.</param>
    /// <param name="request">Intención de retención (opcional).</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Gasto confirmado.</response>
    /// <response code="404">El gasto no existe en la sucursal activa, una subcategoría de línea no existe, o (con retención) el punto de emisión o el establecimiento no existen.</response>
    /// <response code="409">Ya existe una retención activa para el gasto.</response>
    /// <response code="422">El gasto no está en borrador, la política de flujo no permite confirmar, la condición de pago está desactivada, hay líneas inválidas o la retención no es elegible. Los fallos de emisión de la retención conservan su código.</response>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsConfirm}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(
        Guid id,
        [FromBody] ConfirmExpenseDocumentRequest? request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ConfirmExpenseDocumentCommand(id, ToRetentionIntent(request?.Retention)),
                ct
            )
        );

    /// <summary>Anula un gasto confirmado.</summary>
    /// <remarks>
    /// Solo gastos <c>Confirmed</c> de la sucursal activa. La operación es atómica: si el gasto tiene una retención
    /// activa se anula completa en la misma operación; si la política permite anular con reversa, se cancela la cuenta
    /// por pagar; el gasto pasa a <c>Cancelled</c> y se reversa su asiento contable (si no puede reversarse, se aborta
    /// todo). Se rechaza si la cuenta por pagar tiene saldo a favor de proveedor aplicado (hay que reversar primero esa
    /// aplicación) o si la retención no puede reversarse. Con <c>requestSriAnnulment = true</c> y una retención ya
    /// autorizada por el SRI no se anula: se registra la solicitud de anulación ante el SRI y el gasto sigue confirmado.
    /// </remarks>
    /// <param name="id">Id del gasto confirmado.</param>
    /// <param name="request">Motivo de anulación (obligatorio, máximo 500 caracteres) y solicitud de anulación SRI.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Gasto anulado, o solicitud de anulación SRI registrada (el gasto sigue confirmado).</response>
    /// <response code="404">El gasto no existe o pertenece a otra sucursal.</response>
    /// <response code="422">El gasto no está confirmado, la política no permite anular, el motivo es inválido, hay saldo a favor aplicado o la retención / el asiento contable no pueden reversarse.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsCancel}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelExpenseDocumentRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CancelExpenseDocumentCommand(id, request.Reason, request.RequestSriAnnulment),
                ct
            )
        );

    /// <summary>Crea un gasto directamente en estado confirmado, sin borrador previo.</summary>
    /// <remarks>
    /// Recibe el mismo cuerpo que el borrador y ejecuta en la misma operación la creación y la confirmación (cuenta
    /// por pagar, obligaciones contables y retención opcional, como en la confirmación). Se rechaza si la política de
    /// flujo documental de la empresa exige borrador. Aplican las reglas de proveedor, condición de pago, vencimiento
    /// y líneas del borrador, y no puede repetirse el mismo proveedor, tipo y número de documento.
    /// Los campos <c>receptionDocumentId</c> y <c>accessKey</c> del cuerpo se ignoran en este endpoint.
    /// </remarks>
    /// <param name="request">Cabecera, líneas y, opcionalmente, la intención de retención.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="201">Gasto creado y confirmado.</response>
    /// <response code="404">El proveedor o una subcategoría de línea no existen, o (con retención) el punto de emisión o el establecimiento no existen.</response>
    /// <response code="409">Gasto duplicado, o ya existe una retención activa para el gasto.</response>
    /// <response code="422">Datos inválidos, política de flujo que exige borrador, o retención no elegible.</response>
    [HttpPost("confirmed")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsConfirm}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDocumentDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateConfirmed(
        [FromBody] CreateExpenseDraftRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new CreateConfirmedExpenseCommand(
                    request.SupplierId,
                    request.IssueDate,
                    request.AccountingDate,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.PaymentTermId,
                    request.DueDate,
                    request.Lines,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.Notes,
                    request.TaxSupportCode,
                    ToRetentionIntent(request.Retention)
                ),
                ct
            )
        );

    // RETENTIONS-API-EXPENSES-01E — GetRetentionBySourceQuery devuelve Success(null) cuando no hay
    // retención activa (nunca NotFound: "no existe" es un estado normal), por lo que el 404 lo decide
    // este endpoint y no el handler. Mismo permiso de solo lectura que GetRetentionEligibility.
    /// <summary>Obtiene la retención activa asociada a un gasto.</summary>
    /// <remarks>
    /// Devuelve la retención no anulada del gasto, con su estado electrónico y, si existe, su solicitud de anulación.
    /// Si el gasto no tiene retención activa en la sucursal activa, responde 404.
    /// </remarks>
    /// <param name="id">Id del gasto.</param>
    /// /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Retención activa del gasto.</response>
    /// <response code="404">El gasto no tiene una retención activa (o pertenece a otra sucursal).</response>
    [HttpGet("{id:guid}/retention")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    [ProducesResponseType(typeof(ApiResponse<RetentionDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRetention(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, id),
            ct
        );

        if (!result.IsSuccess)
            return this.ToOkOrBadRequest(result);

        return result.Value is null ? this.ApiNotFound() : this.ApiOk(result.Value);
    }

    /// <summary>
    /// Mapeo delgado de contrato de API (<see cref="RetentionIntentRequest"/>) a command interno
    /// (<see cref="RetentionIntent"/>) — 1:1, sin cálculo ni resolución de contexto. <c>null</c> se
    /// preserva como <c>null</c> (comportamiento actual, sin retención).
    /// </summary>
    private static RetentionIntent? ToRetentionIntent(RetentionIntentRequest? request) =>
        request is null
            ? null
            : new RetentionIntent(
                request.AppliesRetention,
                request.EmissionPointId,
                request.IssueDate,
                request
                    .Lines?.Select(l => new IssueRetentionLineInput(
                        l.TaxType,
                        l.RetentionCode,
                        l.BaseAmount,
                        l.RetentionRate,
                        l.RetainedAmount,
                        l.Description,
                        l.RetentionCodeDescription
                    ))
                    .ToList()
            );
}
