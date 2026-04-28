using Microsoft.AspNetCore.Mvc;
using Modules.Accounting.Application.DTOs;
using Modules.Accounting.Application.UseCases;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AccountsController(
    GetAllAccountsUseCase getAllUseCase,
    CreateAccountUseCase createUseCase) : ControllerBase
{
    private Guid GetTenantId()
    {
        var header = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(header) || !Guid.TryParse(header, out var tenantId))
            throw new UnauthorizedAccessException("X-Tenant-Id header requerido");
        return tenantId;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var result = await getAllUseCase.ExecuteAsync(tenantId, ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var result = await createUseCase.ExecuteAsync(tenantId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }
}
