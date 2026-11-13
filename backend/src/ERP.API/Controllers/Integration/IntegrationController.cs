using System.Security.Claims;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Integration;
using ERP.Application.Modules.Integration.UseCases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers.Integration;

/// <summary>
/// API de integración para sistemas externos (aprovisionamiento de tenants y empresas). Ruta propia
/// <c>/api/integration/v1</c>, fuera del patrón <c>/api/v1</c>, protegida por la política
/// <c>IntegrationApi</c> (JWT con el claim <c>integration_api=true</c>). Todas las respuestas usan el
/// envelope estándar <c>ApiResponse&lt;T&gt;</c>; el detalle de error viaja en <c>data.errors</c>.
/// </summary>
[ApiController]
[Route("api/integration/v1")]
[Authorize(Policy = "IntegrationApi")]
[Produces("application/json")]
public sealed class IntegrationController : ControllerBase
{
    private readonly IMediator _mediator;

    public IntegrationController(IMediator mediator) => _mediator = mediator;

    /// <summary>Crea un tenant (cliente) activo a partir de nombre y slug.</summary>
    /// <remarks>
    /// El slug se normaliza (trim y minúsculas) para comprobar unicidad global; el tenant nace activo.
    /// El actor de auditoría es el usuario del token (<c>NameIdentifier</c>, <c>sub</c> o
    /// <c>user_id</c>); si el token no lo trae se registra <c>Guid.Empty</c>. La operación no es
    /// idempotente: repetir el mismo slug devuelve 409.
    /// </remarks>
    /// <param name="request">Nombre y slug obligatorios; idioma preferido opcional (por defecto <c>es</c>).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="201">Tenant creado.</response>
    /// <response code="409">Ya existe un tenant con ese slug.</response>
    /// <response code="422">Nombre o slug vacíos.</response>
    [HttpPost("tenants")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationTenantStatusDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateTenant(
        [FromBody] IntegrationTenantCreateRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new CreateIntegrationTenantCommand(request, ResolveActorId()),
            cancellationToken
        );
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Consulta el estado de un tenant: identidad, indicador de activo e idioma.</summary>
    /// <param name="id">Identificador del tenant.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Estado del tenant.</response>
    /// <response code="404">El tenant no existe.</response>
    [HttpGet("tenants/{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationTenantStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetIntegrationTenantStatusQuery(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Activa un tenant (<c>isActive=true</c>) y devuelve su estado resultante.</summary>
    /// <remarks>
    /// Idempotente: activar un tenant ya activo no falla y devuelve el mismo estado (el handler solo
    /// fija el indicador y la auditoría).
    /// </remarks>
    /// <param name="id">Identificador del tenant.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Tenant activado.</response>
    /// <response code="404">El tenant no existe.</response>
    [HttpPut("tenants/{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationTenantStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateTenant(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ActivateIntegrationTenantCommand(id, ResolveActorId()),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Suspende un tenant (<c>isActive=false</c>) y devuelve su estado resultante.</summary>
    /// <remarks>
    /// Idempotente: suspender un tenant ya suspendido no falla. Solo cambia el indicador del tenant;
    /// no modifica el estado de sus empresas.
    /// </remarks>
    /// <param name="id">Identificador del tenant.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Tenant suspendido.</response>
    /// <response code="404">El tenant no existe.</response>
    [HttpPut("tenants/{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationTenantStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendTenant(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SuspendIntegrationTenantCommand(id, ResolveActorId()),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Aprovisiona una empresa dentro de un tenant existente, lista para operar.</summary>
    /// <remarks>
    /// Alta atómica (todo o nada): crea la empresa, la membresía del usuario creador con el rol indicado
    /// (<c>creatorRole</c>, por defecto Admin), el bootstrap de datos iniciales de la empresa y el
    /// acceso del creador a la sucursal principal. El RUC (<c>taxId</c>) debe ser único en todo el
    /// sistema y ser un RUC ecuatoriano válido de 13 dígitos o un identificador provisional <c>TMP-EC-</c> con el
    /// formato del sistema; no se rellena, trunca ni recorta (un valor inválido responde 422). Valores
    /// por defecto: <c>countryCode</c> ECU, <c>timezone</c> America/Guayaquil, <c>currencyCode</c> USD.
    /// El campo <c>brandingJson</c> se conserva en el contrato v1 pero no está soportado: debe omitirse o
    /// enviarse vacío (un valor no vacío responde 422) y nunca se persiste. La operación no es idempotente.
    /// </remarks>
    /// <param name="request">Tenant, RUC, razón social, dirección principal y usuario creador obligatorios.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="201">Empresa creada.</response>
    /// <response code="409">Ya existe una empresa con ese RUC (<c>COMPANY_RUC_ALREADY_EXISTS</c>).</response>
    /// <response code="422">RUC vacío, de longitud distinta de 13, con caracteres no numéricos o con dígito verificador incorrecto o con espacios iniciales o finales (salvo provisional <c>TMP-EC-</c> válido), o <c>brandingJson</c> no vacío.</response>
    [HttpPost("companies")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationCompanyStatusDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCompany(
        [FromBody] IntegrationCompanyCreateRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new CreateIntegrationCompanyCommand(request),
            cancellationToken
        );
        return this.ToCreatedOrBadRequest(result);
    }

    /// <summary>Consulta el estado de una empresa: indicador de activa, onboarding y estado operativo.</summary>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Estado de la empresa.</response>
    /// <response code="404">La empresa no existe.</response>
    [HttpGet("companies/{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationCompanyStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCompanyStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetIntegrationCompanyStatusQuery(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Reactiva las operaciones de una empresa: estado operativo <c>Operational</c>.</summary>
    /// <remarks>
    /// Idempotente: activar una empresa ya operativa no falla. Solo cambia el estado operativo:
    /// <c>isActive</c> (habilitación administrativa) no se modifica, de modo que una empresa
    /// deshabilitada sigue deshabilitada y sin acceso operativo.
    /// </remarks>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Empresa activada.</response>
    /// <response code="404">La empresa no existe.</response>
    [HttpPut("companies/{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationCompanyStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateCompany(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ActivateIntegrationCompanyCommand(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>Suspende las operaciones de una empresa (estado operativo <c>Suspended</c>).</summary>
    /// <remarks>
    /// Idempotente. Solo cambia el estado operativo: <c>isActive</c> no se modifica, de modo que una
    /// empresa suspendida puede seguir reportando <c>isActive=true</c>; el bloqueo efectivo lo
    /// determina <c>operationalStatus</c>.
    /// </remarks>
    /// <param name="id">Identificador de la empresa.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Empresa suspendida.</response>
    /// <response code="404">La empresa no existe.</response>
    [HttpPut("companies/{id:guid}/suspend")]
    [ProducesResponseType(typeof(ApiResponse<IntegrationCompanyStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendCompany(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SuspendIntegrationCompanyCommand(id),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    private Guid ResolveActorId()
    {
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("user_id");
        return Guid.TryParse(raw, out var actorId) ? actorId : Guid.Empty;
    }
}
