using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Asignaciones laborales de un empleado a sucursales (EMP-BR-11). No concede acceso ni permisos y no toca
/// <c>CompanyUserBranch</c> (INV-11). Sin menú (EMP-E7); cada acción exige su permiso <c>perm:</c>.
/// </summary>
[ApiController]
[Route("api/v1/employees/{employeeId:guid}/branch-assignments")]
[Authorize]
[Produces("application/json")]
public sealed class EmployeeBranchAssignmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeBranchAssignmentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Historial de asignaciones del empleado (abiertas y cerradas).</summary>
    /// <param name="employeeId">Identificador de la ficha de empleado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Asignaciones con sucursal (id, nombre y código), fechas y <c>rowVersion</c>.</response>
    /// <response code="404">La ficha no existe o pertenece a otra empresa o tenant.</response>
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmployeeBranchAssignmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpGet]
    [Authorize(Policy = $"perm:{EmployeesPermissions.View}")]
    public async Task<IActionResult> List(Guid employeeId, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new ListBranchAssignmentsQuery(employeeId), ct));

    /// <summary>Asigna al empleado a una sucursal de la empresa operativa.</summary>
    /// <remarks>
    /// Las fechas son inclusivas y deben quedar dentro de un período laboral; una asignación abierta exige un
    /// período abierto. No puede solaparse con otra del mismo empleado y sucursal.
    /// </remarks>
    /// <param name="employeeId">Identificador de la ficha de empleado.</param>
    /// <param name="request">Sucursal, fecha de inicio y fecha de fin opcional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Asignación creada.</response>
    /// <response code="400">Ficha en baja, fechas fuera de un período laboral o solapamiento.</response>
    /// <response code="404">La ficha no existe o es de otro ámbito.</response>
    /// <response code="422">Sucursal inexistente, inactiva o de otro ámbito, o fechas inválidas.</response>
    [ProducesResponseType(typeof(ApiResponse<EmployeeBranchAssignmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost]
    [Authorize(Policy = $"perm:{EmployeesPermissions.AssignmentsManage}")]
    public async Task<IActionResult> Create(
        Guid employeeId,
        [FromBody] CreateBranchAssignmentRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(
                new CreateBranchAssignmentCommand(employeeId, request.BranchId, request.StartDate, request.EndDate),
                ct
            )
        );

    /// <summary>Cierra una asignación abierta con una fecha fin inclusiva.</summary>
    /// <param name="employeeId">Identificador de la ficha de empleado.</param>
    /// <param name="assignmentId">Identificador de la asignación.</param>
    /// <param name="request">Versión leída (<c>rowVersion</c>) de la asignación y fecha de fin.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Asignación cerrada.</response>
    /// <response code="400">La fecha de cierre queda fuera del período laboral.</response>
    /// <response code="404">La ficha o la asignación no existen o son de otro ámbito.</response>
    /// <response code="409">La versión (<c>rowVersion</c>) está obsoleta.</response>
    /// <response code="422">La asignación ya está cerrada o la fecha no es válida.</response>
    [ProducesResponseType(typeof(ApiResponse<EmployeeBranchAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost("{assignmentId:guid}/close")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.AssignmentsManage}")]
    public async Task<IActionResult> Close(
        Guid employeeId,
        Guid assignmentId,
        [FromBody] CloseBranchAssignmentRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new CloseBranchAssignmentCommand(employeeId, assignmentId, request.RowVersion, request.EndDate),
                ct
            )
        );
}
