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

    /// <summary>
    /// Opera una empresa como administrador global.
    /// </summary>
    /// <remarks>
    /// Puente auditable entre el token global (tenant vacío) y un token operativo de una empresa concreta. Requiere
    /// la política <c>PlatformAdmin</c>. El administrador global no llama endpoints operativos con su token global:
    /// usa el token que devuelve esta operación y puede volver con <c>auth/global/return</c>. Emite cookie de
    /// refresh.
    /// </remarks>
    /// <param name="request">Id de la empresa destino.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Token operativo de la empresa destino.</response>
    /// <response code="400">Usuario no autenticado o no autorizado como administrador global, o la empresa o el tenant destino no existen o están inactivos.</response>
    [HttpPost("operate-company")]
    [Authorize(Policy = "PlatformAdmin")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
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
    /// Vuelve del modo operador a la sesión de administrador global.
    /// </summary>
    /// <remarks>
    /// Se llama con el token operativo emitido por <c>operate-company</c>. La autorización real la verifica el
    /// handler con los claims de modo operador; un token de sesión ordinaria es rechazado.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Token global restablecido.</response>
    /// <response code="400">La sesión no proviene de un modo operador válido.</response>
    [HttpPost("return")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Return(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReturnToGlobalCommand(), cancellationToken);
        return CompleteAuthResponse(result);
    }

    private IActionResult CompleteAuthResponse(
        ERP.Application.Common.Result<AuthResponseDto> result
    )
    {
        if (result.IsSuccess)
        {
            AuthRefreshCookieHelper.SetRefreshCookieIfIssued(HttpContext, result.Value);
            return this.ApiOk(result.Value);
        }

        return this.ToOkOrBadRequest(result);
    }
}
