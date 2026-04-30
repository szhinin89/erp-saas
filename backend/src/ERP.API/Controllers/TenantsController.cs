using Microsoft.AspNetCore.Mvc;
using ERP.Application.Tenants.UseCases.CreateTenant;
using ERP.Application.Tenants.DTOs;

namespace ERP.API.Controllers;

/// <summary>
/// Gestión de tenants (empresas).
/// En producción este endpoint debería estar restringido a administradores del sistema.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TenantsController : ControllerBase
{
    private readonly CreateTenantHandler _createHandler;

    public TenantsController(CreateTenantHandler createHandler)
    {
        _createHandler = createHandler;
    }

    /// <summary>Crea un nuevo tenant (empresa) en el sistema.</summary>
    /// <remarks>El slug debe ser único y se usa como identificador amigable del tenant.</remarks>
    /// <response code="201">Tenant creado correctamente.</response>
    /// <response code="400">El slug ya está en uso.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantCommand command,
        CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Create), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { error = result.Error });
    }
}
