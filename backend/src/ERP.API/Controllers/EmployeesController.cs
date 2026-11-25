using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Ficha de empleado (EMP-E4). Sin <c>[AppFeature]</c>: el menú y el módulo de Kernel llegan con
/// EMP-E7. La ausencia de menú no es un control de seguridad: cada acción exige su permiso
/// <c>perm:</c> y el contexto de empresa operativa.
/// </summary>
[ApiController]
[Route("api/v1/employees")]
[Authorize]
[Produces("application/json")]
public sealed class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lista paginada de empleados de la empresa operativa (skip/take).</summary>
    /// <param name="isActive">Solo activos (<c>true</c>) o en baja (<c>false</c>).</param>
    /// <param name="search">Texto sobre nombre o identificación del tercero.</param>
    /// <param name="jobPositionId">Filtra por cargo.</param>
    /// <param name="professionId">Filtra por profesión.</param>
    /// <param name="specialtyId">Filtra por especialidad activa.</param>
    /// <param name="skip">Registros a omitir.</param>
    /// <param name="take">Registros a devolver (1–200).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Página de empleados.</response>
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EmployeeListItemDto>>), StatusCodes.Status200OK)]
    [HttpGet]
    [Authorize(Policy = $"perm:{EmployeesPermissions.View}")]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? jobPositionId = null,
        [FromQuery] Guid? professionId = null,
        [FromQuery] Guid? specialtyId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ListEmployeesQuery(isActive, search, jobPositionId, professionId, specialtyId, skip, take),
                ct
            )
        );

    /// <summary>Ficha de un empleado de la empresa operativa.</summary>
    /// <response code="200">Ficha con proyección del tercero, períodos y especialidades.</response>
    /// <response code="404">La ficha no existe o pertenece a otra empresa o tenant.</response>
    /// <param name="id">Identificador de la ficha de empleado.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.View}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetEmployeeByIdQuery(id), ct));

    /// <summary>Crea la ficha de un tercero existente y abre su primer período laboral.</summary>
    /// <param name="command">Tercero, fecha de ingreso y, opcionalmente, cargo, profesión y especialidades.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Ficha creada.</response>
    /// <response code="404">El tercero no existe en el tenant.</response>
    /// <response code="409">El tercero ya tiene ficha en la empresa.</response>
    /// <response code="422">Datos inválidos o referencia de catálogo inexistente o fuera de ámbito.</response>
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost]
    [Authorize(Policy = $"perm:{EmployeesPermissions.Create}")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeCommand command, CancellationToken ct) =>
        this.ToCreatedOrBadRequest(await _mediator.Send(command, ct));

    /// <summary>Edita cargo y profesión (omitido = sin cambio; null = elimina la referencia).</summary>
    /// <response code="200">Ficha actualizada.</response>
    /// <response code="404">La ficha no existe o es de otro ámbito.</response>
    /// <response code="409">La versión (<c>rowVersion</c>) está obsoleta.</response>
    /// <param name="id">Identificador de la ficha de empleado.</param>
    /// <param name="request">Versión leída (<c>rowVersion</c>) y los campos a cambiar.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.Update}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdateEmployeeCommand(id, request.RowVersion, request.JobPositionId, request.ProfessionId),
                ct
            )
        );

    /// <summary>Fija el conjunto de especialidades activas del empleado.</summary>
    /// <response code="200">Ficha actualizada.</response>
    /// <response code="404">La ficha no existe o es de otro ámbito.</response>
    /// <response code="409">La versión (<c>rowVersion</c>) está obsoleta.</response>
    /// <param name="id">Identificador de la ficha de empleado.</param>
    /// <param name="request">Versión leída (<c>rowVersion</c>) y el conjunto de especialidades activas.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [HttpPut("{id:guid}/specialties")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.Update}")]
    public async Task<IActionResult> SetSpecialties(
        Guid id,
        [FromBody] SetEmployeeSpecialtiesRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SetEmployeeSpecialtiesCommand(id, request.RowVersion, request.SpecialtyIds ?? []),
                ct
            )
        );
}
