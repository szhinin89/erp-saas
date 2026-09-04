using ERP.Application.Common;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Templates;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// RETENTIONS-ELECTRONIC-WIRING-03E — punto de entrada único y explícito para generar el PDF RIDE
/// de Comprobante de Retención a partir de un XML ya generado (típicamente por
/// <c>IRetentionElectronicDocumentXmlService</c>, aunque este servicio no lo conoce ni lo
/// requiere — recibe el XML como string, mismo criterio de independencia que
/// <see cref="Parsers.IRetentionRideXmlParser"/>). Orquesta en tres pasos: parsear (03C) →
/// componer plantilla (03C) → renderizar (03D, <see cref="IRideRenderer"/>, ya soporta
/// <c>RetentionRideDocumentLayout</c> desde esa fase).
///
/// Deliberadamente NO pasa por <see cref="RidePipeline"/> (fijo a <c>IRideXmlParserResolver</c>/
/// <c>IRideTemplateResolver</c>, ambos resueltos por <c>RideModel</c> — la forma comercial de
/// Factura/Nota de Crédito) ni por su cache/storage (claves por <c>ElectronicDocumentId</c> de un
/// documento ya autorizado — Retención todavía no tiene ese estado). Es, deliberadamente, un
/// pipeline paralelo pequeño: sin cache, sin persistencia — cada llamada renderiza de nuevo. Cachear/
/// persistir el PDF de Retención queda para la fase de autorización, cuando exista un
/// <c>ElectronicDocumentId</c> real al que anclar el fingerprint.
/// </summary>
public interface IRetentionRidePdfService
{
    Task<Result<byte[]>> GeneratePdfAsync(
        string retentionXml,
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? emissionPointId = null,
        CancellationToken ct = default
    );
}

public sealed class RetentionRidePdfService : IRetentionRidePdfService
{
    private readonly IRetentionRideXmlParser _parser;
    private readonly IRetentionRideTemplate _template;
    private readonly IRideRenderer _renderer;
    private readonly IRideBrandingProvider _brandingProvider;

    public RetentionRidePdfService(
        IRetentionRideXmlParser parser,
        IRetentionRideTemplate template,
        IRideRenderer renderer,
        IRideBrandingProvider brandingProvider
    )
    {
        _parser = parser;
        _template = template;
        _renderer = renderer;
        _brandingProvider = brandingProvider;
    }

    public async Task<Result<byte[]>> GeneratePdfAsync(
        string retentionXml,
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? emissionPointId = null,
        CancellationToken ct = default
    )
    {
        var parseResult = _parser.Parse(retentionXml);
        if (!parseResult.IsSuccess)
            return Result<byte[]>.ValidationFailure(
                parseResult.Error ?? "No se pudo interpretar el XML de la retención."
            );

        var brandingResult = await _brandingProvider.GetAsync(
            tenantId,
            companyId,
            branchId,
            emissionPointId,
            ct
        );
        if (!brandingResult.IsSuccess)
            return Result<byte[]>.Failure(
                brandingResult.Error ?? "No se pudo resolver el branding del RIDE."
            );

        try
        {
            var layout = _template.Compose(parseResult.Value!, brandingResult.Value!);
            var pdfBytes = await _renderer.RenderAsync(layout, ct);
            return Result<byte[]>.Success(pdfBytes);
        }
        catch (Exception ex)
        {
            // Mismo boundary que RidePipeline (ADR-025 §5): ni IRideTemplate ni IRideRenderer
            // devuelven Result<T> — este límite evita que una falla real del renderer escape como
            // excepción no manejada.
            return Result<byte[]>.Failure(
                $"No se pudo generar el PDF del comprobante de retención: {ex.GetType().Name}: {ex.Message}"
            );
        }
    }
}
