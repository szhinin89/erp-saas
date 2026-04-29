using Microsoft.AspNetCore.Mvc;
using ERP.Application.Tenants.UseCases.CreateTenant;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly CreateTenantHandler _createHandler;

    public TenantsController(CreateTenantHandler createHandler)
    {
        _createHandler = createHandler;
    }

    [HttpPost]
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
