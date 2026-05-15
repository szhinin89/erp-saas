using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearProveedor;
using ERP.Application.Modules.Purchasing.UseCases.DeshabilitarProveedor;
using ERP.Application.Modules.Purchasing.UseCases.HabilitarProveedor;
using ERP.Application.Modules.Purchasing.UseCases.ObtenerProveedor;
using ERP.Application.Modules.Purchasing.UseCases.ListarProveedores;
using ERP.Application.Modules.Purchasing.UseCases.ActualizarProveedor;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

/// <summary>
/// GestiÃ³n del catÃ¡logo de proveedores del tenant autenticado.
/// Incluye validaciÃ³n de RUC ecuatoriano (algoritmo SRI mÃ³dulo 10/11).
/// </summary>
[Modulo("Proveedores", "perm:compras.proveedores.view", "ðŸ­", "/compras/proveedores", "perm:compras.facturas.view", 46)]
[ApiController]
[Route("api/Proveedores")]
[Authorize]
[Produces("application/json")]
public sealed class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SuppliersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista proveedores con filtros opcionales.</summary>
    /// <remarks>
    /// Query params:
    /// - <c>activeStatus</c>: active | inactive | all (default: active)
    /// - <c>search</c>: busca en razÃ³n social, RUC, correo y telÃ©fono
    /// - <c>tipoPersona</c>: Natural | Juridica
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = "perm:compras.proveedores.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SupplierDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search       = CatalogQueryParameters.ParseSearch(Request.Query);
        string? tipo     = Request.Query.TryGetValue("tipoPersona", out var tv) ? tv.ToString() : null;

        var result = await _mediator.Send(new GetSuppliersQuery(activeFilter, search, tipo), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<SupplierDto>());
    }

    /// <summary>Retorna un proveedor por su ID.</summary>
    /// <response code="200">Proveedor encontrado.</response>
    /// <response code="404">No existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:compras.proveedores.view")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSupplierByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Crea un nuevo proveedor.</summary>
    /// <remarks>
    /// El RUC se valida con el algoritmo del SRI (mÃ³dulo 10 para personas naturales,
    /// mÃ³dulo 11 para sociedades privadas y entidades pÃºblicas).
    /// </remarks>
    /// <response code="201">Proveedor creado.</response>
    /// <response code="400">RUC duplicado en el tenant.</response>
    /// <response code="422">RUC invÃ¡lido u otros datos incorrectos.</response>
    [HttpPost]
    [Authorize(Policy = "perm:compras.proveedores.create")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Actualiza los datos de un proveedor.</summary>
    /// <response code="200">Proveedor actualizado.</response>
    /// <response code="400">RUC duplicado u otro error de negocio.</response>
    /// <response code="422">Datos invÃ¡lidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:compras.proveedores.update")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateSupplierCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Deshabilita un proveedor (soft delete). No lo elimina.</summary>
    /// <response code="200">Proveedor deshabilitado.</response>
    /// <response code="400">Ya estÃ¡ deshabilitado o no existe.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:compras.proveedores.delete")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableSupplierCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    /// <summary>Reactiva un proveedor previamente deshabilitado.</summary>
    /// <response code="200">Proveedor habilitado.</response>
    /// <response code="400">Ya estÃ¡ activo o no existe.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:compras.proveedores.update")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableSupplierCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
