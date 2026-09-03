using ERP.API.Auth;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.OperateCompany;
using ERP.Application.Auth.UseCases.ReturnToGlobal;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// AdminGlobalCore — puente auditable entre el token global (tenant_id == Guid.Empty) y un token
/// operativo scoped a una empresa concreta. El admin global nunca llama endpoints operativos con
/// su token global directo: primero pasa por <see cref="OperateCompany"/> para obtener un token
/// operativo real, y puede volver con <see cref="Return"/>.
/// </summary>
[ApiController]
[Route("api/v1/auth/global")]
[Produces("application/json")]
public sealed class GlobalAuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public GlobalAuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Requiere token global (tenant_id == Guid.Empty + rol Admin) — ver policy "PlatformAdmin".</summary>
    [HttpPost("operate-company")]
    [Authorize(Policy = "PlatformAdmin")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OperateCompany(
        [FromBody] OperateCompanyRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new OperateCompanyCommand(request.CompanyId),
            cancellationToken
        );
        return CompleteAuthResponse(result);
    }

    /// <summary>
    /// El token que llama aquí es OPERATIVO (tenant_id real), no global — "PlatformAdmin" lo
    /// rechazaría. Se usa la policy "Session" por defecto (cualquier sesión autenticada con
    /// tenant real) y la autorización real la hace <c>ReturnToGlobalHandler</c> verificando los
    /// claims <c>operator_mode</c>/<c>global_admin_user_id</c> — ver <see cref="ReturnToGlobalCommand"/>.
    /// </summary>
    [HttpPost("return")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Return(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReturnToGlobalCommand(), cancellationToken);
        return CompleteAuthResponse(result);
    }

    private IActionResult CompleteAuthResponse(ERP.Application.Common.Result<AuthResponseDto> result)
    {
        if (result.IsSuccess)
        {
            if (
                result.Value?.RefreshToken is not null
                && result.Value.RefreshTokenExpiry is not null
            )
                AuthRefreshCookieHelper.SetRefreshCookie(
                    HttpContext,
                    result.Value.RefreshToken,
                    result.Value.RefreshTokenExpiry.Value
                );

            return this.ApiOk(result.Value);
        }

        return this.ToOkOrBadRequest(result);
    }
}
