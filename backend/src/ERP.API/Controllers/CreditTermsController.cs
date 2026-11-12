using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases.CreditTerms;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Catálogo de condiciones de crédito (plazos y cuotas). Las consultas y escrituras se resuelven por tenant;
/// al crear se registra la empresa del contexto. Las condiciones se habilitan/deshabilitan; no se eliminan.
/// </summary>
[AppFeature(
    "Condiciones de Crédito",
    $"perm:{FinancePermissions.View}",
    "📋",
    "/finance/credit-terms",
    null,
    55
)]
[ApiController]
[Route("api/v1/finance/credit-terms")]
[Authorize]
[Produces("application/json")]
public sealed class CreditTermsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CreditTermsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista las condiciones de crédito del tenant, con filtros opcionales.</summary>
    /// <param name="isActive">Si se indica, devuelve solo activas (<c>true</c>) o inactivas (<c>false</c>).</param>
    /// <param name="search">Texto de búsqueda aplicado por el repositorio a código y nombre.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Listado completo (sin paginación) con sus cuotas.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CreditTermDto>>), StatusCodes.Status200OK)]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    public async Task<IActionResult> GetCreditTerms(
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetCreditTermsQuery(isActive, search), ct),
            "OK"
        );

    /// <summary>Obtiene una condición de crédito por Id, con sus cuotas ordenadas por número.</summary>
    /// <param name="id">Identificador de la condición.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Condición encontrada.</response>
    /// <response code="404">La condición no existe.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CreditTermDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [Authorize(Policy = $"perm:{FinancePermissions.View}")]
    public async Task<IActionResult> GetCreditTermById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetCreditTermByIdQuery(id), ct));

    /// <summary>Crea una condición de crédito con su plan de cuotas.</summary>
    /// <remarks>
    /// El código debe ser único en el tenant. Sin <c>installments</c> se genera una única cuota del
    /// 100 % a <c>totalDays</c> días; con cuotas, los porcentajes deben sumar exactamente 100 % (máximo
    /// 2 decimales cada uno). El modo <c>FinancialStrict</c> exige al menos una cuota. La condición
    /// nace activa en la empresa del contexto.
    /// </remarks>
    /// <param name="command">Código, nombre, modo, plazo total y cuotas opcionales.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Condición creada.</response>
    /// <response code="409">Ya existe una condición con ese código.</response>
    /// <response code="422">Datos inválidos: campos obligatorios, longitudes, cuotas o suma distinta de 100 %.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreditTermDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = $"perm:{FinancePermissions.Create}")]
    public async Task<IActionResult> CreateCreditTerm(
        [FromBody] CreateCreditTermCommand command,
        CancellationToken ct
    ) => this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Actualiza nombre, modo, plazo y plan de cuotas de una condición (el código no cambia).</summary>
    /// <remarks>
    /// Reemplaza por completo las cuotas con las enviadas; aplican las mismas reglas de suma 100 % y
    /// modo <c>FinancialStrict</c> que en la creación.
    /// </remarks>
    /// <param name="id">Identificador de la condición; debe coincidir con <c>command.id</c>.</param>
    /// <param name="command">Nuevos datos de la condición.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Condición actualizada.</response>
    /// <response code="400">El Id de la ruta no coincide con el del cuerpo.</response>
    /// <response code="404">La condición no existe.</response>
    /// <response code="422">Datos inválidos: campos obligatorios, cuotas o suma distinta de 100 %.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CreditTermDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    public async Task<IActionResult> UpdateCreditTerm(
        Guid id,
        [FromBody] UpdateCreditTermCommand command,
        CancellationToken ct
    )
    {
        if (id != command.Id)
            return this.ApiBadRequest("El ID no coincide.");
        return this.ToOkOrBadRequest(await _mediator.Send(command, ct));
    }

    /// <summary>Habilita una condición de crédito para que pueda usarse.</summary>
    /// <param name="id">Identificador de la condición.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Condición habilitada; el dato devuelto es <c>true</c>.</response>
    /// <response code="404">La condición no existe.</response>
    [HttpPatch("{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    public async Task<IActionResult> EnableCreditTerm(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new EnableCreditTermCommand(id), ct));

    /// <summary>Deshabilita una condición de crédito (baja lógica; no se elimina).</summary>
    /// <param name="id">Identificador de la condición.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Condición deshabilitada; el dato devuelto es <c>true</c>.</response>
    /// <response code="404">La condición no existe.</response>
    [HttpPatch("{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [Authorize(Policy = $"perm:{FinancePermissions.Update}")]
    public async Task<IActionResult> DisableCreditTerm(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(await _mediator.Send(new DisableCreditTermCommand(id), ct));
}
