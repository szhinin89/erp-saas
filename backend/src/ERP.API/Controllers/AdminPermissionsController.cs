using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 — catálogo de permisos asignables, derivado del Kernel
/// Registry. Controller propio (responsabilidad única) separado de
/// <see cref="AccessProfilesController"/>, que ya tiene su propio prefijo de ruta
/// (<c>admin/iam</c>) y su propia responsabilidad (CRUD de perfiles + guardado de permisos).
/// </summary>
[ApiController]
[Route("api/v1/admin/permissions")]
[Produces("application/json")]
public sealed class AdminPermissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminPermissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Reutiliza el mismo permiso que ya exigen <c>GET/PUT .../profiles/{id}/permissions</c> —
    /// no se inventa un permiso nuevo para esta pantalla.
    /// </summary>
    [HttpGet("catalog")]
    [Authorize(Policy = "Session")]
    [Authorize(Policy = $"perm:{AccessPermissions.ProfilesView}")]
    [ProducesResponseType(typeof(ApiResponse<PermissionCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPermissionCatalogQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
