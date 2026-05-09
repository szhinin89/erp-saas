using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Proveedores.DTOs;
using ERP.Application.Modules.Proveedores.UseCases.CreateProveedor;
using ERP.Application.Modules.Proveedores.UseCases.DisableProveedor;
using ERP.Application.Modules.Proveedores.UseCases.EnableProveedor;
using ERP.Application.Modules.Proveedores.UseCases.GetProveedorById;
using ERP.Application.Modules.Proveedores.UseCases.GetProveedores;
using ERP.Application.Modules.Proveedores.UseCases.UpdateProveedor;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión del catálogo de proveedores del tenant autenticado.
/// Incluye validación de RUC ecuatoriano (algoritmo SRI módulo 10/11).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class ProveedoresController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProveedoresController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista proveedores con filtros opcionales.</summary>
    /// <remarks>
    /// Query params:
    /// - <c>activeStatus</c>: active | inactive | all (default: active)
    /// - <c>search</c>: busca en razón social, RUC, correo y teléfono
    /// - <c>tipoPersona</c>: Natural | Juridica
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = "perm:compras.proveedores.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProveedorDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search       = CatalogQueryParameters.ParseSearch(Request.Query);
        string? tipo     = Request.Query.TryGetValue("tipoPersona", out var tv) ? tv.ToString() : null;

        var result = await _mediator.Send(new GetProveedoresQuery(activeFilter, search, tipo), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<ProveedorDto>());
    }

    /// <summary>Retorna un proveedor por su ID.</summary>
    /// <response code="200">Proveedor encontrado.</response>
    /// <response code="404">No existe o no pertenece al tenant.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:compras.proveedores.view")]
    [ProducesResponseType(typeof(ApiResponse<ProveedorDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProveedorByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    /// <summary>Crea un nuevo proveedor.</summary>
    /// <remarks>
    /// El RUC se valida con el algoritmo del SRI (módulo 10 para personas naturales,
    /// módulo 11 para sociedades privadas y entidades públicas).
    /// </remarks>
    /// <response code="201">Proveedor creado.</response>
    /// <response code="400">RUC duplicado en el tenant.</response>
    /// <response code="422">RUC inválido u otros datos incorrectos.</response>
    [HttpPost]
    [Authorize(Policy = "perm:compras.proveedores.create")]
    [ProducesResponseType(typeof(ApiResponse<ProveedorDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProveedorCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    /// <summary>Actualiza los datos de un proveedor.</summary>
    /// <response code="200">Proveedor actualizado.</response>
    /// <response code="400">RUC duplicado u otro error de negocio.</response>
    /// <response code="422">Datos inválidos.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:compras.proveedores.update")]
    [ProducesResponseType(typeof(ApiResponse<ProveedorDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateProveedorCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");
        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Deshabilita un proveedor (soft delete). No lo elimina.</summary>
    /// <response code="200">Proveedor deshabilitado.</response>
    /// <response code="400">Ya está deshabilitado o no existe.</response>
    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:compras.proveedores.delete")]
    [ProducesResponseType(typeof(ApiResponse<ProveedorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableProveedorCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    /// <summary>Reactiva un proveedor previamente deshabilitado.</summary>
    /// <response code="200">Proveedor habilitado.</response>
    /// <response code="400">Ya está activo o no existe.</response>
    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:compras.proveedores.update")]
    [ProducesResponseType(typeof(ApiResponse<ProveedorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableProveedorCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}
