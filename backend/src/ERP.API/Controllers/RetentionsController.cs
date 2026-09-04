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
/// Sin permiso propio de Retentions todavía (ver comentario de <see cref="ExpensesController.GetRetention"/>):
/// reutiliza <see cref="ExpensePermissions.DocumentsView"/>, el mismo permiso de solo lectura que
/// ya protege el resto de las consultas de retención expuestas en <c>ExpensesController</c>.
/// Agregar un permiso nuevo sin un <c>[NavItem]</c> que lo referencie quedaría inasignable desde
/// el catálogo de permisos (<c>GetPermissionCatalogHandler</c> construye el catálogo desde
/// <c>KernelRegistry.Navigation</c>) — y esta fase explícitamente no agrega ítems de menú.
///
/// No firma XML, no envía al SRI, no persiste el XML como autorizado, no cachea el PDF —
/// cada llamada genera XML y PDF de nuevo a partir del estado actual de la retención.
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
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    public async Task<IActionResult> GetElectronicXml(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateRetentionXmlQuery(id), ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(
                result.Error ?? "No se pudo generar el XML de la retención."
            );

        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Value!.Xml);
        return File(bytes, "application/xml; charset=utf-8", $"retencion-{id:N}.xml");
    }

    /// <summary>
    /// Genera el PDF RIDE del Comprobante de Retención on-demand (XML → PDF, sin cache, sin
    /// firmar, sin autorizar) y lo devuelve como archivo descargable.
    /// </summary>
    [HttpGet("{id:guid}/ride/pdf")]
    [Authorize(Policy = $"perm:{ExpensePermissions.DocumentsView}")]
    public async Task<IActionResult> GetRidePdf(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateRetentionRidePdfQuery(id), ct);
        if (!result.IsSuccess)
            return this.ApiBadRequest(
                result.Error ?? "No se pudo generar el PDF de la retención."
            );

        return File(result.Value!, "application/pdf", $"retencion-{id:N}.pdf");
    }
}
