using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Expone la descarga del XML fiscal y la generación on-demand del RIDE de
/// Comprobante de Retención. Controller delgado: recibe el
/// request, delega íntegramente a MediatR (<see cref="GenerateRetentionXmlQuery"/>/
/// <see cref="GenerateRetentionRidePdfQuery"/>, RETENTIONS-ELECTRONIC-WIRING-03E) y solo decide el
/// content-type de la respuesta — nunca consulta <c>RetentionDocument</c> ni ningún repositorio
/// directamente.
///
/// Autorización (ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A, ADR-036 D-9): sin permiso transversal de
/// Retenciones. El permiso se deriva del documento ORIGEN de la retención — Compra →
/// <c>purchases.view</c>, Gasto → <c>expenses.documents.view</c> — y como el origen es un dato, lo
/// resuelve server-side el handler (<c>IRetentionSourceAccess</c>), no un <c>[Authorize]</c> estático:
/// aquí solo se exige autenticación. Sin acceso al origen la respuesta es 404 (fail-closed).
///
/// No firma XML, no envía al SRI, no persiste el XML como autorizado, no cachea el PDF —
/// cada llamada genera XML y PDF de nuevo a partir del estado actual de la retención.
///
/// <see cref="Register"/> (RETENTIONS-SRI-MANUAL-REGISTER-04E; desde 01A solo acción de
/// RECUPERACIÓN — la transmisión es automática al confirmar Compra/Gasto): disparo explícito del
/// registro electrónico real (firma + SOAP + autorización, vía
/// <see cref="ERP.Application.Modules.ElectronicDocuments.Services.IElectronicDocumentIssuer"/>) —
/// deliberadamente separado de las lecturas XML/RIDE, que nunca firman ni envían nada.
/// Usa <see cref="ElectronicDocumentsPermissions.Retry"/> (no un
/// permiso nuevo): es el mismo permiso que ya protege la acción de registro/reintento manual
/// equivalente para Factura/Nota de Crédito en <c>ElectronicDocumentsController.Register</c>/
/// <c>Retry</c>, y ya es asignable desde el catálogo de permisos (ligado al <c>[NavItem]</c> del
/// Monitor de comprobantes) — crear un permiso nuevo sin ese vínculo quedaría
/// inasignable.
/// </summary>
[ApiController]
[Route("api/v1/retentions")]
[Authorize]
public sealed class RetentionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RetentionsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Descarga el XML autorizado almacenado si existe autorización; en otro estado ofrece
    /// una vista previa generada. Una variante explícita nunca se sustituye por otra.
    /// El nombre de archivo usa el
    /// <paramref name="id"/> (no el número de retención) — evita una consulta adicional solo para
    /// resolverlo, mismo criterio de "controller delgado" que el resto del endpoint.
    /// </summary>
    /// <remarks>
    /// Sin <c>variant</c>: si el documento electrónico está <c>Authorized</c> devuelve el XML
    /// autorizado almacenado; en cualquier otro estado devuelve una vista previa generada en el momento
    /// (no firma, no envía al SRI, no persiste nada). Con <c>variant</c> (<c>Draft</c>, <c>Signed</c> o
    /// <c>Authorized</c>) devuelve exactamente esa variante almacenada y nunca la sustituye por otra.
    /// La respuesta exitosa es el archivo XML (<c>application/xml</c>), no un envelope JSON; los errores
    /// sí usan el envelope. El usuario necesita acceso de lectura al documento origen de la retención;
    /// sin él la retención se trata como inexistente.
    /// </remarks>
    /// <param name="id">Id de la retención.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <param name="variant">Variante del XML a descargar; omitida, se elige según el estado del documento.</param>
    /// <response code="200">Archivo XML (<c>application/xml</c>) de la retención.</response>
    /// <response code="404">La retención no existe o no es visible para el usuario, o la variante pedida todavía no tiene XML almacenado, o el archivo ya no está disponible en el almacenamiento.</response>
    /// <response code="422">Id vacío o variante fuera del enumerado, o no se pudo componer el XML de la vista previa.</response>
    [HttpGet("{id:guid}/electronic/xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "application/xml")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetElectronicXml(Guid id, CancellationToken ct, [FromQuery] ElectronicDocumentXmlVariant? variant = null)
    {
        var result = await _mediator.Send(new GenerateRetentionXmlQuery(id, variant), ct);
        if (!result.IsSuccess)
            return this.ApiFailure(result);

        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Value!);
        return File(bytes, "application/xml; charset=utf-8", $"retencion-{id:N}.xml");
    }

    /// <summary>
    /// Genera el PDF RIDE del Comprobante de Retención on-demand (XML → PDF, sin cache, sin
    /// firmar, sin autorizar) y lo devuelve como archivo descargable.
    /// </summary>
    /// <remarks>
    /// Si el documento electrónico está <c>Authorized</c>, el PDF se genera desde el XML autorizado
    /// almacenado y con la fecha de autorización oficial; en otro estado se genera desde una vista previa
    /// del XML. No firma, no envía al SRI y no guarda el PDF: cada llamada lo genera de nuevo. La
    /// respuesta exitosa es el archivo PDF (<c>application/pdf</c>), no un envelope JSON; los errores sí
    /// usan el envelope. Sin acceso de lectura al documento origen la retención se trata como inexistente.
    /// </remarks>
    /// <param name="id">Id de la retención.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Archivo PDF (<c>application/pdf</c>) del RIDE.</response>
    /// <response code="400">No se pudo resolver el branding del RIDE o falló la generación del PDF.</response>
    /// <response code="404">La retención no existe o no es visible para el usuario, o no existe la empresa del RIDE.</response>
    /// <response code="422">Id vacío, no se pudo interpretar o componer el XML de la retención.</response>
    [HttpGet("{id:guid}/ride/pdf")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetRidePdf(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateRetentionRidePdfQuery(id), ct);
        if (!result.IsSuccess)
            return this.ApiFailure(result);

        return File(result.Value!, "application/pdf", $"retencion-{id:N}.pdf");
    }

    /// <summary>
    /// Dispara el registro electrónico real (firma XAdES-BES + envío a Recepción SRI + consulta
    /// de Autorización) de una retención ya <c>Issued</c>, vía el pipeline genérico
    /// <c>IElectronicDocumentIssuer.RegisterAsync</c>. Acción de recuperación controlada (el camino
    /// normal es automático): además de este permiso exige el permiso de acción del origen
    /// (<c>purchases.update</c> / <c>expenses.documents.confirm</c>), resuelto en el handler. Sin
    /// body — solo usa <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// Reutiliza el mismo flujo de transmisión que la transmisión automática al confirmar la Compra o el
    /// Gasto. Un documento electrónico en <c>Draft</c> o <c>Failed</c> se reanuda; si ya está en un estado
    /// posterior responde 409. Solo una retención en estado <c>Issued</c> es procesable; el estado y el
    /// ciclo de vida del origen los decide el emisor (si el origen ya no permite procesamiento, o la
    /// emisión electrónica está pausada para la empresa, responde 422). Además del permiso del endpoint se
    /// exige el permiso de acción del documento origen (resuelto en el handler): si falta, responde 403 con
    /// envelope.
    /// </remarks>
    /// <param name="id">Id de la retención.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Estado del documento electrónico tras el intento de registro.</response>
    /// <response code="400">Fallo del pipeline sin clasificar como validación (incluye «No se pudo iniciar la transmisión electrónica de la retención»).</response>
    /// <response code="403">El usuario no tiene permiso para operar el documento origen de la retención.</response>
    /// <response code="404">La retención no existe o no es visible para el usuario.</response>
    /// <response code="409">Ya existe un documento electrónico en un estado posterior a <c>Draft</c>/<c>Failed</c> para esta retención, o un registro concurrente lo creó antes.</response>
    /// <response code="422">El origen ya no permite procesamiento electrónico, la emisión electrónica está pausada, o el pipeline rechazó la retención por validación.</response>
    [HttpPost("{id:guid}/electronic/register")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Retry}")]
    [ProducesResponseType(typeof(ApiResponse<ElectronicDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(Guid id, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new RegisterRetentionElectronicDocumentCommand(id), ct),
            "OK"
        );

    // ── ZH-RETENTION-SRI-ANNULMENT-01 — anulación ante el SRI de una retención AUTORIZADA ──────────
    // La solicitud se inicia desde la anulación de la Compra/Gasto (RequestSriAnnulment = true). Estos
    // pasos se autorizan por el documento ORIGEN en el handler (IRetentionSourceAccess): permiso de
    // anular el origen. Aquí solo se exige autenticación. La SOLICITUD es asistida (no existe WS para
    // solicitarla); la VERIFICACIÓN es automática vía ConsultaComprobante (01B, Ficha Técnica v2.34 §8).
    // No hay endpoint para declarar ANULADO.

    /// <summary>Registra que la solicitud se presentó en SRI en Línea (no significa ANULADO).</summary>
    /// <remarks>
    /// Pasa la solicitud de <c>PendingSubmission</c> a <c>PendingSriResolution</c> y, enseguida, consulta
    /// automáticamente el estado fiscal en el SRI. Es idempotente: si la solicitud ya estaba presentada,
    /// no repite el registro y solo reintenta la verificación. La presentación queda registrada aunque la
    /// verificación no se pueda aplicar; entonces responde 200 con un <c>code</c> informativo en el
    /// envelope (por ejemplo <c>RETENTION_ANNULMENT_SRI_VERIFICATION_FAILED</c>, o
    /// <c>ELECTRONIC_DOCUMENT_EMISSION_DISABLED</c> si la emisión electrónica está pausada) y un job la
    /// reintenta. Según el resultado de la consulta, el <c>code</c> puede ser
    /// <c>RETENTION_ANNULMENT_SRI_STILL_AUTHORIZED</c>, <c>RETENTION_ANNULMENT_SRI_PENDING</c>,
    /// <c>RETENTION_ANNULMENT_SRI_NOT_AUTHORIZED</c>, <c>RETENTION_ANNULMENT_FINALIZED</c> o
    /// <c>RETENTION_ANNULMENT_FINALIZATION_PENDING</c>. El estado ANULADO lo informa el SRI, nunca el
    /// usuario. Requiere permiso de anular el documento origen; la solicitud debe pertenecer a la empresa
    /// y, si hay sucursal activa, a esa sucursal.
    /// </remarks>
    /// <param name="requestId">Id de la solicitud de anulación.</param>
    /// <param name="request">Fecha de presentación (no anterior a la emisión de la retención), referencia y notas opcionales.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Solicitud registrada como presentada; el <c>code</c> de la respuesta informa el resultado de la verificación.</response>
    /// <response code="403">El usuario no tiene permiso para anular el documento origen de la retención.</response>
    /// <response code="404">La solicitud no existe o no es visible para el usuario.</response>
    /// <response code="422">Fecha obligatoria o anterior a la emisión de la retención, referencia o notas demasiado largas, o la solicitud ya no está pendiente de presentación.</response>
    [HttpPost("annulments/{requestId:guid}/submission")]
    [ProducesResponseType(typeof(ApiResponse<RetentionAnnulmentRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitAnnulment(
        Guid requestId,
        [FromBody] SubmitRetentionAnnulmentRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new SubmitRetentionAnnulmentCommand(
                    requestId,
                    request.SubmittedOn,
                    request.Reference,
                    request.Notes
                ),
                ct
            )
        );

    /// <summary>
    /// Consulta el estado fiscal en el WS ConsultaComprobante del SRI y lo aplica (ZH-RETENTION-SRI-ANNULMENT-01B).
    /// No recibe ningún estado: ANULADO lo informa el SRI, nunca el usuario.
    /// </summary>
    /// <remarks>
    /// Solo una solicitud en <c>PendingSriResolution</c> se consulta; si el SRI informa ANULADO, la
    /// solicitud se acepta y se finaliza la anulación del documento origen. Si la solicitud ya estaba
    /// aceptada devuelve su estado con <c>RETENTION_ANNULMENT_FINALIZED</c> (o reintenta la finalización
    /// pendiente); si aún no se presentó o ya está cerrada responde 422. Con la emisión electrónica
    /// pausada para la empresa no se consulta al SRI y responde 422. El resultado de la consulta se
    /// informa en el <c>code</c> del envelope de una respuesta 200 (<c>RETENTION_ANNULMENT_SRI_STILL_AUTHORIZED</c>,
    /// <c>RETENTION_ANNULMENT_SRI_PENDING</c>, <c>RETENTION_ANNULMENT_SRI_NOT_AUTHORIZED</c>,
    /// <c>RETENTION_ANNULMENT_SRI_VERIFICATION_FAILED</c>, <c>RETENTION_ANNULMENT_FINALIZED</c> o
    /// <c>RETENTION_ANNULMENT_FINALIZATION_PENDING</c>). Requiere permiso de anular el documento origen.
    /// </remarks>
    /// <param name="requestId">Id de la solicitud de anulación.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Consulta realizada; el <c>code</c> de la respuesta informa el estado fiscal reportado por el SRI.</response>
    /// <response code="403">El usuario no tiene permiso para anular el documento origen de la retención.</response>
    /// <response code="404">La solicitud no existe o no es visible para el usuario.</response>
    /// <response code="422">La solicitud no está presentada o ya está cerrada, la emisión electrónica está pausada, o no se pudo confirmar el ANULADO en el documento electrónico.</response>
    [HttpPost("annulments/{requestId:guid}/sri-verification")]
    [ProducesResponseType(typeof(ApiResponse<RetentionAnnulmentRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> VerifyAnnulmentWithSri(Guid requestId, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new VerifyRetentionAnnulmentWithSriCommand(requestId), ct)
        );

    /// <summary>Desiste de la solicitud: antes de presentarla, o si el SRI confirma que el comprobante sigue AUTORIZADO.</summary>
    /// <remarks>
    /// Es idempotente: una solicitud ya desistida se devuelve sin cambios. Solo admite desistimiento una
    /// solicitud no presentada, o presentada cuando la última verificación del SRI confirma que el
    /// comprobante sigue AUTORIZADO; en otro estado responde 422. En una única transacción revierte el
    /// estado de anulación del documento electrónico y libera la retención de la cuenta por pagar del
    /// origen; si algún paso falla, no queda ningún cambio. Requiere permiso de anular el documento origen.
    /// </remarks>
    /// <param name="requestId">Id de la solicitud de anulación.</param>
    /// <param name="request">Motivo del desistimiento (obligatorio).</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Solicitud desistida (o ya lo estaba).</response>
    /// <response code="403">El usuario no tiene permiso para anular el documento origen de la retención.</response>
    /// <response code="404">La solicitud no existe o no es visible para el usuario.</response>
    /// <response code="422">Motivo vacío o demasiado largo, la solicitud ya no admite desistimiento, o no se pudo revertir la anulación en el documento electrónico.</response>
    [HttpPost("annulments/{requestId:guid}/abandon")]
    [ProducesResponseType(typeof(ApiResponse<RetentionAnnulmentRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AbandonAnnulment(
        Guid requestId,
        [FromBody] AbandonRetentionAnnulmentRequest request,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new AbandonRetentionAnnulmentCommand(requestId, request.Reason),
                ct
            )
        );

    /// <summary>Reintenta la anulación del documento origen tras un ANULADO cuya finalización falló.</summary>
    /// <remarks>
    /// Solo una solicitud aceptada por el SRI (ANULADO) finaliza la anulación del documento origen; en
    /// otro estado responde 422. Es idempotente: si ya está finalizada devuelve su estado con
    /// <c>RETENTION_ANNULMENT_FINALIZED</c>. Si el flujo de anulación del origen vuelve a fallar, el fallo
    /// se registra en la solicitud y responde 200 con <c>RETENTION_ANNULMENT_FINALIZATION_PENDING</c>
    /// para un nuevo reintento. Requiere permiso de anular el documento origen.
    /// </remarks>
    /// <param name="requestId">Id de la solicitud de anulación.</param>
    /// <param name="ct">Token de cancelación de la solicitud.</param>
    /// <response code="200">Finalización aplicada o ya aplicada (<c>RETENTION_ANNULMENT_FINALIZED</c>), o fallo registrado para reintento (<c>RETENTION_ANNULMENT_FINALIZATION_PENDING</c>).</response>
    /// <response code="403">El usuario no tiene permiso para anular el documento origen de la retención.</response>
    /// <response code="404">La solicitud no existe o no es visible para el usuario.</response>
    /// <response code="422">La solicitud todavía no tiene confirmación de ANULADO del SRI.</response>
    [HttpPost("annulments/{requestId:guid}/finalization")]
    [ProducesResponseType(typeof(ApiResponse<RetentionAnnulmentRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RetryAnnulmentFinalization(
        Guid requestId,
        CancellationToken ct
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new RetryRetentionAnnulmentFinalizationCommand(requestId), ct)
        );
}

public sealed record SubmitRetentionAnnulmentRequest(
    DateOnly SubmittedOn,
    string? Reference,
    string? Notes
);

public sealed record AbandonRetentionAnnulmentRequest(string Reason);
