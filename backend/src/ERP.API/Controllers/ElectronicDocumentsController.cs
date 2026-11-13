using ERP.API.Attributes;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.UseCases.CreateElectronicDocument;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDiagnosticBySource;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsDashboard;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentsList;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentTimeline;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentXml;
using ERP.Application.Modules.ElectronicDocuments.UseCases.ReconcileElectronicDocument;
using ERP.Application.Modules.ElectronicDocuments.UseCases.RetransmitCorrectedElectronicDocument;
using ERP.Application.Modules.ElectronicDocuments.UseCases.RetryElectronicDocument;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Monitor de comprobantes — consola de consulta del ciclo electrónico. No emite,
/// no firma ni cancela documentos. Acciones de escritura: reintentar manualmente un documento
/// varado (Signed/Received/DeadLetter/Failed), conciliar un resultado incierto, corregir y
/// retransmitir un comprobante rechazado (R12 Fase B), o registrar el documento faltante de un origen ya
/// autorizado comercialmente que nunca llegó a generar uno (backfill) — el resto de transiciones
/// las ejecuta <c>ElectronicDocumentIssuer</c>, invocado desde Ventas o desde el job automático
/// de reintentos.
/// </summary>
[AppFeature(
    "Monitor de comprobantes",
    $"perm:{ElectronicDocumentsPermissions.View}",
    "📡",
    "/sri/electronic-documents/monitor",
    "perm:settings.group",
    26
)]
[ApiController]
[Route("api/v1/electronic-documents")]
[Authorize]
[Produces("application/json")]
public sealed class ElectronicDocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ElectronicDocumentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista los comprobantes electrónicos con filtros y paginación (Monitor).
    /// </summary>
    /// <remarks>
    /// Todos los filtros son opcionales y se combinan. Las fechas son días de negocio de la empresa
    /// (<c>YYYY-MM-DD</c>).
    /// </remarks>
    /// <param name="dateFrom">Día de negocio inicial (<c>YYYY-MM-DD</c>).</param>
    /// <param name="dateTo">Día de negocio final (<c>YYYY-MM-DD</c>).</param>
    /// <param name="state">Filtra por estado del comprobante electrónico.</param>
    /// <param name="documentType">Filtra por tipo de comprobante.</param>
    /// <param name="environment">Filtra por ambiente del SRI.</param>
    /// <param name="search">Texto libre de búsqueda.</param>
    /// <param name="pageNumber">Número de página, base 1.</param>
    /// <param name="pageSize">Cantidad de comprobantes por página.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Página de comprobantes electrónicos.</response>
    [HttpGet]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentsListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] string? state = null,
        [FromQuery] string? documentType = null,
        [FromQuery] string? environment = null,
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new GetElectronicDocumentsListQuery(
                    dateFrom,
                    dateTo,
                    state,
                    documentType,
                    environment,
                    search,
                    pageNumber,
                    pageSize
                ),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Indicadores del Monitor de comprobantes electrónicos.
    /// </summary>
    /// <remarks>
    /// Consulta de solo lectura.
    /// </remarks>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Indicadores del Monitor.</response>
    [HttpGet("dashboard")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.View}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentsDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new GetElectronicDocumentsDashboardQuery(), ct),
            "OK"
        );

    /// <summary>
    /// Obtiene el detalle de un comprobante electrónico.
    /// </summary>
    /// <param name="id">Id de el documento electrónico.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Detalle del comprobante.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetElectronicDocumentDetailQuery(id), ct));

    /// <summary>
    /// Diagnóstico del comprobante electrónico de un documento de origen.
    /// </summary>
    /// <remarks>
    /// Agnóstico de módulo: lo usa cualquier pantalla que conoce su módulo y su documento de origen pero no el id
    /// interno del comprobante electrónico.
    /// </remarks>
    /// <param name="sourceModule">Módulo de origen del documento (por ejemplo <c>Sales</c>).</param>
    /// <param name="sourceEntityId">Id del documento de origen en su módulo.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Diagnóstico del comprobante.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("by-source")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDiagnosticDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDiagnosticBySource(
        [FromQuery] string sourceModule,
        [FromQuery] Guid sourceEntityId,
        CancellationToken ct
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(
                new GetElectronicDocumentDiagnosticBySourceQuery(sourceModule, sourceEntityId),
                ct
            )
        );

    /// <summary>
    /// Línea de tiempo de un comprobante electrónico.
    /// </summary>
    /// <param name="id">Id de el documento electrónico.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Línea de tiempo del comprobante.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("{id:guid}/timeline")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ElectronicDocumentTimelineEventDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline(Guid id, CancellationToken ct) =>
        this.ToOkOrNotFound(await _mediator.Send(new GetElectronicDocumentTimelineQuery(id), ct));

    /// <summary>
    /// Devuelve el XML almacenado de un comprobante electrónico.
    /// </summary>
    /// <remarks>
    /// Devuelve el XML ya almacenado de la variante pedida; nunca genera uno nuevo. La respuesta es un envelope
    /// JSON que contiene el XML, no un archivo.
    /// </remarks>
    /// <param name="sourceModule">Módulo de origen del documento (por ejemplo <c>Sales</c>).</param>
    /// <param name="sourceEntityId">Id del documento de origen en su módulo.</param>
    /// <param name="variant">Variante del XML almacenado (por ejemplo borrador o firmado).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">XML almacenado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpGet("xml")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Detail}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetXml(
        [FromQuery] string sourceModule,
        [FromQuery] Guid sourceEntityId,
        [FromQuery] ElectronicDocumentXmlVariant variant,
        CancellationToken ct
    ) =>
        this.ToOkOrNotFound(
            await _mediator.Send(
                new GetElectronicDocumentXmlQuery(sourceModule, sourceEntityId, variant),
                ct
            )
        );

    /// <summary>
    /// Reintenta manualmente un comprobante electrónico.
    /// </summary>
    /// <remarks>
    /// Reintenta un documento varado en <c>Signed</c>, <c>Received</c> o <c>Failed</c>, o lo reactiva desde
    /// <c>DeadLetter</c> y reintenta. Si la emisión electrónica está deshabilitada para la empresa, la operación se
    /// rechaza con 422 (<c>ELECTRONIC_DOCUMENT_EMISSION_DISABLED</c>) sin escribir sobre el comprobante.
    /// </remarks>
    /// <param name="id">Id de el documento electrónico.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado del reintento.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">El comprobante cambió de estado o lo está procesando otra solicitud concurrente.</response>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Retry}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new RetryElectronicDocumentCommand(id), ct),
            "OK"
        );

    /// <summary>
    /// Registra una conciliación manual y reconsulta al SRI.
    /// </summary>
    /// <remarks>
    /// Caso #32: registra la evidencia de conciliación (referencia y observación) y reconsulta el estado de
    /// autorización en el SRI. Nunca reenvía el comprobante ni lo autoriza por declaración. Si la emisión
    /// electrónica está deshabilitada para la empresa, la operación se rechaza con 422
    /// (<c>ELECTRONIC_DOCUMENT_EMISSION_DISABLED</c>) sin escribir sobre el comprobante.
    /// </remarks>
    /// <param name="id">Id de el documento electrónico.</param>
    /// <param name="body">Referencia de la evidencia y observación.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la conciliación.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPost("{id:guid}/reconciliation")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Reconcile}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reconcile(
        Guid id,
        [FromBody] ReconcileElectronicDocumentRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new ReconcileElectronicDocumentCommand(id, body.Reference, body.Observation),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Corrige y retransmite con la misma clave un comprobante rechazado.
    /// </summary>
    /// <remarks>
    /// R12 Fase B: solo si el backend declara elegible el comprobante y el XML regenerado demuestra una corrección
    /// real. Una sola transmisión por solicitud, identificada por <c>idempotencyKey</c>. Nunca modifica la
    /// operación comercial. Si la emisión electrónica está deshabilitada para la empresa, la operación se rechaza
    /// con 422 (<c>ELECTRONIC_DOCUMENT_EMISSION_DISABLED</c>) sin escribir sobre el comprobante.
    /// </remarks>
    /// <param name="id">Id de el documento electrónico.</param>
    /// <param name="body">Motivo de la corrección y clave de idempotencia.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Resultado de la retransmisión.</response>
    /// <response code="400">La operación no se puede completar por una regla de negocio incumplida; el envelope detalla el motivo.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    /// <response code="409">Otra transmisión concurrente del mismo comprobante ya reclamó el envío.</response>
    [HttpPost("{id:guid}/corrected-retransmission")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.RetransmitCorrected}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetransmitCorrected(
        Guid id,
        [FromBody] RetransmitCorrectedElectronicDocumentRequest body,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new RetransmitCorrectedElectronicDocumentCommand(id, body.Reason, body.IdempotencyKey),
                ct
            ),
            "OK"
        );

    /// <summary>
    /// Registra el comprobante electrónico de un documento de origen ya autorizado comercialmente.
    /// </summary>
    /// <remarks>
    /// Backfill para un documento de origen autorizado comercialmente que nunca generó comprobante electrónico.
    /// Idempotente: si ya existe uno más allá de <c>Draft</c> o <c>Failed</c> responde 409. Si la emisión
    /// electrónica está deshabilitada para la empresa, la operación se rechaza con 422
    /// (<c>ELECTRONIC_DOCUMENT_EMISSION_DISABLED</c>) sin escribir sobre el comprobante.
    /// </remarks>
    /// <param name="command">Módulo y documento de origen a registrar.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Comprobante electrónico registrado.</response>
    /// <response code="409">Ya existe un comprobante electrónico del documento de origen más allá de Draft/Failed, o lo crea otra solicitud concurrente.</response>
    [HttpPost("register")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Retry}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] CreateElectronicDocumentCommand command,
        CancellationToken ct
    ) => this.ToOkOrBadRequest(await _mediator.Send(command, ct), "OK");
}

public sealed record ReconcileElectronicDocumentRequest(string Reference, string Observation);

public sealed record RetransmitCorrectedElectronicDocumentRequest(string Reason, Guid IdempotencyKey);
