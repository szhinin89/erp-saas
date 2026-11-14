using ERP.API.Contracts;
using ERP.API.Contracts.MasterData;
using ERP.API.Extensions;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.GetSupplierExpenseSubcategories;
using ERP.Application.MasterData.UseCases.SetSupplierExpenseSubcategories;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01 — subcategorías de gasto permitidas para un PROVEEDOR en la
/// empresa activa. Un proveedor sin asignaciones activas puede usar cualquier subcategoría; con
/// asignaciones activas, Gastos solo ofrece y acepta las disponibles.
///
/// SCOPE: company-scoped (Company A puede configurar distinto de Company B para el mismo proveedor).
/// Permisos: reutiliza los de configuración de proveedor por empresa (BusinessPartnersView /
/// BusinessPartnersConfigureCompany) — no se crea un permiso nuevo.
/// </summary>
[ApiController]
[Route("api/v1/master/business-partners/{bpId:guid}/expense-subcategories")]
[Authorize(Policy = "Session")]
[Produces("application/json")]
[Tags("MasterData — Business Partners")]
public sealed class SupplierExpenseSubcategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupplierExpenseSubcategoriesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Consulta el modo y las subcategorías de gasto de un proveedor.
    /// </summary>
    /// <remarks>
    /// Devuelve el modo (<c>Unconfigured</c>, <c>Restricted</c> o <c>RestrictedEmpty</c>), las asignaciones activas e
    /// inactivas con sus nombres y las subcategorías disponibles, calculados por el servidor en la empresa activa. No
    /// incluye cuentas contables.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Modo y subcategorías del proveedor.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersView}")]
    [ProducesResponseType(
        typeof(ApiResponse<SupplierExpenseSubcategoriesDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpenseSubcategories(
        [FromRoute] Guid bpId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetSupplierExpenseSubcategoriesQuery(bpId),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Fija el conjunto de subcategorías de gasto de un proveedor.
    /// </summary>
    /// <remarks>
    /// Reemplazo atómico del conjunto deseado: crea las nuevas, reactiva las retiradas reutilizando su registro y retira
    /// las ausentes (baja lógica, sin eliminar registros). Un conjunto vacío retira todas las asignaciones activas y deja
    /// al proveedor sin configuración. Es idempotente.
    /// </remarks>
    /// <param name="bpId">Id del tercero (Business Partner).</param>
    /// <param name="body">Identificadores de las subcategorías deseadas (puede ser vacío).</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Conjunto resultante del proveedor.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Conflicto con el estado actual del recurso o con un valor que debe ser único.</response>
    /// <response code="422">La solicitud no supera la validación de entrada.</response>
    [HttpPut]
    [Authorize(Policy = $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}")]
    [ProducesResponseType(
        typeof(ApiResponse<SupplierExpenseSubcategoriesDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetExpenseSubcategories(
        [FromRoute] Guid bpId,
        [FromBody] SetSupplierExpenseSubcategoriesRequest body,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new SetSupplierExpenseSubcategoriesCommand(bpId, body.ExpenseSubcategoryIds),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }
}
