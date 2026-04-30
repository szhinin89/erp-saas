using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Application.Modules.Branches.UseCases.CreateBranch;
using ERP.Application.Modules.Branches.UseCases.DisableBranch;
using ERP.Application.Modules.Branches.UseCases.EnableBranch;
using ERP.Application.Modules.Branches.UseCases.GetBranchById;
using ERP.Application.Modules.Branches.UseCases.GetBranches;
using ERP.Application.Modules.Branches.UseCases.UpdateBranch;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class BranchesController : ControllerBase
{
    private readonly GetBranchesHandler _getList;
    private readonly GetBranchByIdHandler _getById;
    private readonly CreateBranchHandler _create;
    private readonly UpdateBranchHandler _update;
    private readonly DisableBranchHandler _disable;
    private readonly EnableBranchHandler _enable;

    public BranchesController(
        GetBranchesHandler getList,
        GetBranchByIdHandler getById,
        CreateBranchHandler create,
        UpdateBranchHandler update,
        DisableBranchHandler disable,
        EnableBranchHandler enable)
    {
        _getList = getList;
        _getById = getById;
        _create = create;
        _update = update;
        _disable = disable;
        _enable = enable;
    }

    [HttpGet]
    [Authorize(Policy = "perm:saas.branches.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BranchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _getList.HandleAsync(activeFilter, search, ct);
        return Ok(new ApiResponse<IReadOnlyList<BranchDto>>(
            result.IsSuccess,
            result.IsSuccess ? "OK" : result.Error ?? "Error",
            result.Value ?? Array.Empty<BranchDto>()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:saas.branches.view")]
    [ProducesResponseType(typeof(ApiResponse<BranchDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _getById.HandleAsync(id, ct);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object?>(false, result.Error ?? "No encontrado", null));

        return Ok(new ApiResponse<BranchDetailDto?>(true, "OK", result.Value));
    }

    [HttpPost]
    [Authorize(Policy = "perm:saas.branches.create")]
    [ProducesResponseType(typeof(ApiResponse<BranchDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateBranchCommand command, CancellationToken ct = default)
    {
        var result = await _create.HandleAsync(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<BranchDto?>(true, "Creado", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:saas.branches.update")]
    [ProducesResponseType(typeof(ApiResponse<BranchDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return BadRequest(new ApiResponse<object?>(false, "El id de ruta no coincide con el cuerpo.", null));

        var result = await _update.HandleAsync(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<BranchDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:saas.branches.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _disable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<BranchDto?>(true, "Deshabilitado", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:saas.branches.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _enable.HandleAsync(id, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<BranchDto?>(true, "Habilitado", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }
}
