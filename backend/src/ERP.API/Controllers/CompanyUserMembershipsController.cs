using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserMembershipsAdmin;
using ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Fase I-A — wiring administrativo de <c>CompanyUserMembership</c>: expone los UseCases de Fase D
/// (<see cref="UpsertCompanyUserMembershipAdminCommand"/>/<see cref="RevokeCompanyUserMembershipAdminCommand"/>,
/// ya probados) que hasta ahora no tenían ningún Controller/ruta — quedaban sin uso en producción.
/// TenantId/CompanyId nunca viajan en el request: cada Admin command los resuelve del contexto
/// autenticado (ver los handlers), nunca del body — mismo principio anti-IDOR documentado en
/// docs/IDENTITY.md a raíz de la eliminación del antiguo UserSessionController self-service.
/// Reutiliza access.company_user_memberships.view (mismo criterio que AccessProfilesController,
/// que ya usa ProfilesView tanto para lectura como para mutación de profiles, y que
/// CompanyUserPreferencesController, que reutiliza MembershipsView para su PUT). No se introduce
/// un permiso .manage nuevo en esta fase — ver docs/STATUS.md para la decisión documentada.
/// CompanyUserBranch y CompanyUserPreferences siguen siendo fuente única de verdad de sus propios
/// datos: este controller no las toca directamente, solo reenvía al UseCase de Fase D que ya las
/// orquesta vía MediatR.
/// </summary>
[ApiController]
[Route("api/v1/admin/iam/memberships")]
[Authorize(Policy = $"perm:{AccessPermissions.MembershipsView}")]
[Produces("application/json")]
public sealed class CompanyUserMembershipsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyUserMembershipsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista las membresías de usuarios de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Incluye el nombre del perfil de cada usuario, ordenado por nombre completo. Con
    /// onlyActive=false (valor por defecto) incluye también las membresías revocadas. Las cuentas
    /// globales no son visibles desde la empresa.
    /// </remarks>
    /// <param name="onlyActive">
    /// true devuelve solo las membresías activas; false (por defecto) incluye las revocadas.
    /// </param>
    /// <response code="200">Membresías de la empresa (posiblemente vacías).</response>
    /// <response code="403">No hay una empresa activa en la sesión.</response>
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<CompanyUserMembershipAdminDto>>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] bool onlyActive = false,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetCompanyUserMembershipsAdminQuery(onlyActive),
            cancellationToken
        );
        return this.ToOkOrBadRequest(
            result,
            successFallbackFactory: () => Array.Empty<CompanyUserMembershipAdminDto>()
        );
    }

    /// <summary>
    /// Indica si un nombre de usuario existe y si ya tiene membresía en la empresa activa.
    /// </summary>
    /// <remarks>
    /// Primer paso de «Agregar usuario»: decide entre reutilizar la cuenta (POST de membresía) o
    /// crear una nueva (POST admin/iam/users). Solo revela cuentas visibles desde la empresa (con
    /// membresía en ella o vinculables dentro del tenant); una cuenta global o de otro tenant
    /// responde igual que un username inexistente (exists=false). Solo lectura.
    /// </remarks>
    /// <param name="username">
    /// Nombre de usuario a buscar (obligatorio, hasta 50 caracteres).
    /// </param>
    /// <response code="200">Resultado de la búsqueda.</response>
    /// <response code="403">No hay una empresa activa en la sesión.</response>
    /// <response code="422">El username está vacío o excede 50 caracteres.</response>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(ApiResponse<UsernameLookupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Lookup(
        [FromQuery] string username,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new LookupUserByUsernameAdminQuery(username),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Crea o actualiza la membresía de un usuario existente en la empresa activa.
    /// </summary>
    /// <remarks>
    /// El usuario se identifica por username y debe ser visible desde la empresa. role y profileId
    /// definen sus permisos: un usuario que no es administrador requiere un perfil activo de la
    /// empresa. Opcionalmente fija sucursales autorizadas, sucursal por defecto y modo de inicio de
    /// sesión (AskBranch o DirectToDefault). Tenant y empresa salen de la sesión, nunca del cuerpo.
    /// Responde 200 (no 201) aunque cree la membresía.
    ///
    /// Reglas de autoridad: solo un administrador de la empresa puede asignar, modificar o retirar
    /// el rol Admin; nadie modifica su propia membresía sin ser administrador; el perfil asignado
    /// no puede conceder permisos fuera del alcance de quien asigna. La empresa debe conservar al
    /// menos un administrador activo.
    /// </remarks>
    /// <param name="request">
    /// Username, rol y, opcionalmente, perfil, sucursales autorizadas, sucursal por defecto y modo
    /// de inicio de sesión.
    /// </param>
    /// <response code="200">Membresía creada o actualizada (data vacío).</response>
    /// <response code="403">
    /// Sin autoridad para asignar el rol o el perfil, intento de modificar su propia membresía o
    /// contexto de quien asigna no resoluble.
    /// </response>
    /// <response code="404">
    /// El usuario no es visible, o no existen la empresa activa, el perfil o alguna sucursal
    /// indicada.
    /// </response>
    /// <response code="422">
    /// Datos inválidos (username o rol vacíos o demasiado largos), perfil requerido o inactivo,
    /// preferencias inválidas, o se rompería el mínimo de un administrador activo.
    /// </response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertCompanyUserMembershipRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpsertCompanyUserMembershipAdminCommand(
            request.Username,
            request.Role,
            request.ProfileId,
            request.AuthorizedBranchIds,
            request.DefaultBranchId,
            request.LoginMode
        );

        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result, successFallbackFactory: () => new { });
    }

    /// <summary>
    /// Revoca la membresía de un usuario en la empresa activa.
    /// </summary>
    /// <remarks>
    /// Desactiva la membresía (no la elimina): el usuario pierde el acceso a la empresa. Es
    /// idempotente: revocar una membresía ya revocada responde 200 sin cambios. Solo un
    /// administrador de la empresa puede retirar el rol Admin, y la empresa debe conservar al menos
    /// un administrador activo. Invalida la caché de permisos y la navegación del usuario en la
    /// empresa.
    /// </remarks>
    /// <param name="request">Username del usuario a revocar.</param>
    /// <response code="200">Membresía revocada (data vacío).</response>
    /// <response code="403">
    /// Solo un administrador de la empresa puede retirar el rol Admin.
    /// </response>
    /// <response code="404">
    /// No existe la empresa activa, o el usuario no existe, es una cuenta global o no tiene
    /// membresía en la empresa.
    /// </response>
    /// <response code="422">
    /// Username vacío o excede 50 caracteres, o se rompería el mínimo de un administrador activo.
    /// </response>
    [HttpPost("revoke")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Revoke(
        [FromBody] RevokeCompanyUserMembershipRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new RevokeCompanyUserMembershipAdminCommand(request.Username);
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result, successFallbackFactory: () => new { });
    }
}

/// <summary>Body de POST — CompanyId/TenantId nunca viajan aquí, se resuelven del contexto autenticado.</summary>
public sealed record UpsertCompanyUserMembershipRequest(
    string Username,
    string Role,
    Guid? ProfileId = null,
    IReadOnlyList<Guid>? AuthorizedBranchIds = null,
    Guid? DefaultBranchId = null,
    string? LoginMode = null
);

/// <summary>Body de POST revoke — CompanyId/TenantId nunca viajan aquí.</summary>
public sealed record RevokeCompanyUserMembershipRequest(string Username);
