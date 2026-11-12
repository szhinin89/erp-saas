using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Accounting;

[ApiController]
[Route("api/v1/accounting/manual-journal-entries")]
[Authorize]
public sealed class ManualJournalController(IMediator mediator) : ControllerBase
{
    /// <summary>Crea un asiento contable manual en estado borrador.</summary>
    /// <remarks>
    /// El borrador no tiene número de asiento ni afecta saldos hasta publicarse (<c>post</c>). Valida que las cuentas
    /// existan en la empresa activa y que la fecha no caiga en un período cerrado. Es idempotente por
    /// <c>idempotencyKey</c>: repetirla devuelve el borrador ya creado (estado <c>Draft</c>, versión 1, sin número).
    /// </remarks>
    /// <param name="command">Fecha contable, concepto, referencia opcional, líneas y clave de idempotencia.</param>
    /// <response code="201">Borrador creado (o el ya existente para la misma clave de idempotencia).</response>
    /// <response code="400">El período de la fecha contable no está abierto (<c>PERIOD_NOT_OPEN</c>).</response>
    /// <response code="422">Campos inválidos, cuenta inexistente en la empresa o regla de dominio del asiento incumplida.</response>
    [HttpPost]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualCreate)]
    [ProducesResponseType(typeof(ApiResponse<ManualJournalResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateManualJournalCommand command, CancellationToken ct) =>
        this.ToCreatedOrBadRequest(await mediator.Send(command, ct));

    /// <summary>Reemplaza la cabecera y las líneas de un asiento manual en borrador.</summary>
    /// <remarks>
    /// Control de concurrencia optimista: <c>version</c> debe coincidir con la versión actual del asiento. Solo se
    /// edita un asiento en <c>Draft</c>; el asiento de cierre anual se calcula desde Períodos y no se edita (se
    /// descarta y se vuelve a generar). La fecha anterior y la nueva deben estar en períodos escribibles.
    /// </remarks>
    /// <param name="id">Id del asiento manual; debe coincidir con <c>command.id</c>.</param>
    /// <param name="command">Id, versión esperada, fecha contable, concepto, referencia y líneas.</param>
    /// <response code="200">Borrador actualizado.</response>
    /// <response code="400">El id de la ruta no coincide con el del cuerpo, el período no está abierto o es un asiento de cierre anual.</response>
    /// <response code="404">El asiento manual no existe en la empresa activa.</response>
    /// <response code="409">La versión enviada no es la actual: el asiento cambió, recargar antes de continuar.</response>
    /// <response code="422">El asiento ya no está en borrador, cuenta inexistente o regla de dominio incumplida.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualEdit)]
    [ProducesResponseType(typeof(ApiResponse<ManualJournalResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Edit(Guid id, EditManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));

    /// <summary>Publica (contabiliza) un asiento manual en borrador asignándole número.</summary>
    /// <remarks>
    /// Transacción propia: valida que la fecha no sea futura, que Contabilidad esté habilitada en esa fecha, que exista
    /// un período abierto y que las cuentas sean contabilizables; reserva el número de asiento y pasa a <c>Posted</c>.
    /// Es idempotente por (asiento, <c>idempotencyKey</c>): cada intento queda registrado y repetir la misma clave con
    /// la misma versión devuelve el resultado original, también si fue un fallo; la misma clave con otra versión
    /// responde 409. Para un asiento de cierre anual se revalidan las precondiciones del cierre bajo el bloqueo del ejercicio.
    /// </remarks>
    /// <param name="id">Id del asiento manual; debe coincidir con <c>command.id</c>.</param>
    /// <param name="command">Id, versión esperada y clave de idempotencia.</param>
    /// <response code="200">Asiento publicado (o resultado de un intento previo con la misma clave).</response>
    /// <response code="400">Id de ruta distinto del cuerpo, fecha futura (<c>MANUAL_FUTURE_DATE</c>), Contabilidad no habilitada (<c>ACCOUNTING_NOT_ENABLED</c>), período inexistente o no abierto (<c>PERIOD_NOT_OPEN</c>), cierre anual desactualizado (<c>YEAR_END_CLOSING_OUTDATED</c>), fallo interno de publicación (<c>MANUAL_POST_INTERNAL</c>) o repetición de un intento fallido.</response>
    /// <response code="404">El asiento manual no existe en la empresa activa.</response>
    /// <response code="409">La versión no es la actual, o la clave de idempotencia pertenece a otra versión.</response>
    /// <response code="422">El asiento no está en borrador o incumple una regla de dominio para contabilizarse.</response>
    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualPost)]
    [ProducesResponseType(typeof(ApiResponse<ManualJournalResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Post(Guid id, PostManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));

    /// <summary>Descarta un asiento manual en borrador.</summary>
    /// <remarks>
    /// Baja lógica: el asiento pasa a <c>Discarded</c> con el motivo indicado y no se elimina. Requiere la versión
    /// actual y que el asiento siga en <c>Draft</c>; el período de su fecha debe estar abierto.
    /// </remarks>
    /// <param name="id">Id del asiento manual; debe coincidir con <c>command.id</c>.</param>
    /// <param name="command">Id, versión esperada y motivo obligatorio.</param>
    /// <response code="200">Borrador descartado.</response>
    /// <response code="400">Id de ruta distinto del cuerpo o período no abierto (<c>PERIOD_NOT_OPEN</c>).</response>
    /// <response code="404">El asiento manual no existe en la empresa activa.</response>
    /// <response code="409">La versión enviada no es la actual.</response>
    /// <response code="422">El asiento ya no está en borrador, motivo vacío o excedido.</response>
    [HttpPost("{id:guid}/discard")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualDiscard)]
    [ProducesResponseType(typeof(ApiResponse<ManualJournalResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Discard(Guid id, DiscardManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));

    /// <summary>Reversa un asiento manual contabilizado generando el asiento inverso.</summary>
    /// <remarks>
    /// Solo un manual en <c>Posted</c>. Crea un asiento de reverso numerado en el período abierto de
    /// <c>effectiveDate</c>; la respuesta describe el asiento de reverso, no el original. Es idempotente por asiento
    /// original: si ya existe su reverso se devuelve ese mismo. El asiento de cierre anual solo se reversa con su fecha
    /// original, mientras el último período del ejercicio esté abierto.
    /// </remarks>
    /// <param name="id">Id del asiento manual contabilizado; debe coincidir con <c>command.id</c>.</param>
    /// <param name="command">Id, fecha efectiva del reverso y motivo obligatorio.</param>
    /// <response code="200">Asiento de reverso (nuevo o ya existente).</response>
    /// <response code="400">Id de ruta distinto del cuerpo, período no abierto (<c>PERIOD_NOT_OPEN</c>) o fecha inválida para el cierre anual (<c>YEAR_END_CLOSING_REVERSAL_DATE</c>).</response>
    /// <response code="404">El asiento manual no existe en la empresa activa.</response>
    /// <response code="422">El asiento no está contabilizado, motivo vacío o excedido, o regla de dominio incumplida.</response>
    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = "perm:" + AccountingPermissions.ManualReverse)]
    [ProducesResponseType(typeof(ApiResponse<ManualJournalResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reverse(Guid id, ReverseManualJournalCommand command, CancellationToken ct) =>
        id != command.Id ? this.ApiBadRequest("El ID no coincide.")
            : this.ToOkOrBadRequest(await mediator.Send(command, ct));
}
