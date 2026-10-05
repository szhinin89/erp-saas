using ERP.API.Extensions;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// RETENTIONS-ELECTRONIC-ENDPOINTS-03F — expone la generación on-demand del XML/RIDE de
/// Comprobante de Retención para QA/diagnóstico/vista previa. Controller delgado: recibe el
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
/// deliberadamente separado de los dos endpoints de arriba, que siguen siendo preview/on-demand
/// y nunca firman ni envían nada. Usa <see cref="ElectronicDocumentsPermissions.Retry"/> (no un
/// permiso nuevo): es el mismo permiso que ya protege la acción de registro/reintento manual
/// equivalente para Factura/Nota de Crédito en <c>ElectronicDocumentsController.Register</c>/
/// <c>Retry</c>, y ya es asignable desde el catálogo de permisos (ligado al <c>[NavItem]</c> del
/// Monitor de Documentos Electrónicos) — crear un permiso nuevo sin ese vínculo quedaría
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
    /// Genera el XML <c>comprobanteRetencion</c> on-demand (sin firmar, sin autorizar, sin
    /// persistir) y lo devuelve como archivo descargable. El nombre de archivo usa el
    /// <paramref name="id"/> (no el número de retención) — evita una consulta adicional solo para
    /// resolverlo, mismo criterio de "controller delgado" que el resto del endpoint.
    /// </summary>
    [HttpGet("{id:guid}/electronic/xml")]
    public async Task<IActionResult> GetElectronicXml(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateRetentionXmlQuery(id), ct);
        if (!result.IsSuccess)
            return this.ApiFailure(result);

        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Value!.Xml);
        return File(bytes, "application/xml; charset=utf-8", $"retencion-{id:N}.xml");
    }

    /// <summary>
    /// Genera el PDF RIDE del Comprobante de Retención on-demand (XML → PDF, sin cache, sin
    /// firmar, sin autorizar) y lo devuelve como archivo descargable.
    /// </summary>
    [HttpGet("{id:guid}/ride/pdf")]
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
    [HttpPost("{id:guid}/electronic/register")]
    [Authorize(Policy = $"perm:{ElectronicDocumentsPermissions.Retry}")]
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
    [HttpPost("annulments/{requestId:guid}/submission")]
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
    [HttpPost("annulments/{requestId:guid}/sri-verification")]
    public async Task<IActionResult> VerifyAnnulmentWithSri(Guid requestId, CancellationToken ct) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(new VerifyRetentionAnnulmentWithSriCommand(requestId), ct)
        );

    /// <summary>Desiste de la solicitud: antes de presentarla, o si el SRI confirma que el comprobante sigue AUTORIZADO.</summary>
    [HttpPost("annulments/{requestId:guid}/abandon")]
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
    [HttpPost("annulments/{requestId:guid}/finalization")]
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
