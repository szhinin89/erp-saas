using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Employees.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Administración de los catálogos laborales por empresa (EMP-BR-15/16): <c>job-positions</c>,
/// <c>professions</c> y <c>specialties</c>. Activar o inactivar nunca borra. Sin menú (EMP-E7).
/// </summary>
[ApiController]
[Route("api/v1/employees/{catalog:regex(^(job-positions|professions|specialties)$)}")]
[Authorize]
[Produces("application/json")]
public sealed class EmployeeCatalogsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeeCatalogsController(IMediator mediator) => _mediator = mediator;

    private static EmployeeCatalogType Parse(string catalog) =>
        catalog switch
        {
            "job-positions" => EmployeeCatalogType.JobPosition,
            "professions" => EmployeeCatalogType.Profession,
            _ => EmployeeCatalogType.Specialty,
        };

    /// <summary>Lista paginada del catálogo de la empresa operativa.</summary>
    /// <param name="catalog"><c>job-positions</c>, <c>professions</c> o <c>specialties</c>.</param>
    /// <param name="isActive">Filtra por estado.</param>
    /// <param name="search">Texto sobre el nombre.</param>
    /// <param name="skip">Registros a omitir.</param>
    /// <param name="take">Registros a devolver (1–200).</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EmployeeCatalogEntryDto>>), StatusCodes.Status200OK)]
    [HttpGet]
    [Authorize(Policy = $"perm:{EmployeesPermissions.CatalogsView}")]
    public async Task<IActionResult> List(
        string catalog,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new ListEmployeeCatalogQuery(Parse(catalog), isActive, search, skip, take), ct)
        );

    /// <summary>Obtiene un registro del catálogo.</summary>
    /// <response code="404">No existe o pertenece a otra empresa o tenant.</response>
    /// <param name="catalog"><c>job-positions</c>, <c>professions</c> o <c>specialties</c>.</param>
    /// <param name="id">Identificador del registro del catálogo.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeCatalogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.CatalogsView}")]
    public async Task<IActionResult> GetById(string catalog, Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetEmployeeCatalogEntryQuery(Parse(catalog), id), ct));

    /// <summary>Crea un registro; el nombre se normaliza y debe ser único en la empresa.</summary>
    /// <response code="201">Registro creado.</response>
    /// <response code="409">Ya existe un registro con ese nombre normalizado.</response>
    /// <param name="catalog"><c>job-positions</c>, <c>professions</c> o <c>specialties</c>.</param>
    /// <param name="request">Nombre del registro.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeCatalogEntryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [HttpPost]
    [Authorize(Policy = $"perm:{EmployeesPermissions.CatalogsManage}")]
    public async Task<IActionResult> Create(
        string catalog,
        [FromBody] EmployeeCatalogEntryRequest request,
        CancellationToken ct
    ) =>
        this.ToCreatedOrBadRequest(
            await _mediator.Send(new CreateEmployeeCatalogEntryCommand(Parse(catalog), request.Name), ct)
        );

    /// <summary>Renombra un registro (las fichas que lo usan conservan la referencia).</summary>
    /// <response code="404">No existe o es de otro ámbito.</response>
    /// <response code="409">Ya existe otro registro con ese nombre normalizado.</response>
    /// <param name="catalog"><c>job-positions</c>, <c>professions</c> o <c>specialties</c>.</param>
    /// <param name="id">Identificador del registro del catálogo.</param>
    /// <param name="request">Versión leída (<c>rowVersion</c>) y nuevo nombre.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeCatalogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.CatalogsManage}")]
    public async Task<IActionResult> Rename(
        string catalog,
        Guid id,
        [FromBody] UpdateEmployeeCatalogEntryRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new RenameEmployeeCatalogEntryCommand(Parse(catalog), id, request.RowVersion, request.Name),
                ct
            )
        );

    /// <summary>Activa un registro inactivo.</summary>
    /// <response code="404">No existe o es de otro ámbito.</response>
    /// <response code="409">La versión (<c>rowVersion</c>) está obsoleta.</response>
    /// <response code="422">Ya está activo.</response>
    /// <param name="catalog"><c>job-positions</c>, <c>professions</c> o <c>specialties</c>.</param>
    /// <param name="id">Identificador del registro del catálogo.</param>
    /// <param name="request">Versión leída (<c>rowVersion</c>) del registro.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeCatalogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.CatalogsManage}")]
    public async Task<IActionResult> Activate(
        string catalog,
        Guid id,
        [FromBody] EmployeeCatalogVersionRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SetEmployeeCatalogEntryActiveCommand(Parse(catalog), id, request.RowVersion, true),
                ct
            )
        );

    /// <summary>Inactiva un registro (baja lógica; las fichas que ya lo usan no cambian).</summary>
    /// <response code="404">No existe o es de otro ámbito.</response>
    /// <response code="409">La versión (<c>rowVersion</c>) está obsoleta.</response>
    /// <response code="422">Ya está inactivo.</response>
    /// <param name="catalog"><c>job-positions</c>, <c>professions</c> o <c>specialties</c>.</param>
    /// <param name="id">Identificador del registro del catálogo.</param>
    /// <param name="request">Versión leída (<c>rowVersion</c>) del registro.</param>
    /// <param name="ct">Token de cancelación.</param>
    [ProducesResponseType(typeof(ApiResponse<EmployeeCatalogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = $"perm:{EmployeesPermissions.CatalogsManage}")]
    public async Task<IActionResult> Deactivate(
        string catalog,
        Guid id,
        [FromBody] EmployeeCatalogVersionRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SetEmployeeCatalogEntryActiveCommand(Parse(catalog), id, request.RowVersion, false),
                ct
            )
        );
}
