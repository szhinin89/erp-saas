using ERP.Application.Common;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Storage;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// Orquestador genérico del pipeline (ADR-025 §6): XML autorizado → Parser → RideModel →
/// Template → Renderer → Storage → resultado. Depende exclusivamente de interfaces — nunca
/// conoce un parser, una plantilla, un renderer ni un tipo de comprobante concretos. La ausencia
/// de un parser o plantilla registrados es un resultado esperado (<see cref="RideOutcome.Failed"/>
/// con <c>ReasonCode</c> explícito), nunca una excepción.
///
/// La versión de plantilla, de branding y de renderer son constantes neutras en esta fase
/// (ADR-025, Fase 5 del plan): ni <see cref="IRideTemplate"/> ni
/// <see cref="IRideBrandingProvider"/> ni <see cref="IRideRenderer"/> exponen todavía su propia
/// versión — ese es un hueco real entre ADR-025 §14 y los contratos ya congelados en Fases 2-4,
/// documentado en la auditoría de esta fase. La lógica de comparación de huella en sí (delegada a
/// <see cref="IRideCacheStrategy"/>) ya es definitiva, no provisional.
/// </summary>
public sealed class RidePipeline
{
    /// <summary>Versión de la forma de <see cref="ERP.Domain.Modules.Ride.ValueObjects.RideModel"/> — se incrementa si ese VO cambia.</summary>
    public const string RideSpecificationVersion = "1.0.0";

    private const string NeutralTemplateVersion = "unversioned";
    private const string NeutralBrandingVersion = "unversioned";
    private const string NeutralRendererVersion = "unversioned";

    private readonly IRideSourceXmlProvider _sourceXmlProvider;
    private readonly IRideXmlParserResolver _parserResolver;
    private readonly IRideTemplateResolver _templateResolver;
    private readonly IRideCacheStrategy _cacheStrategy;
    private readonly IRideContentHasher _contentHasher;
    private readonly IRideBrandingProvider _brandingProvider;
    private readonly IRideRenderer _renderer;
    private readonly IRidePdfStorageService _storageService;
    private readonly IRidePdfDocumentRepository _repository;
    private readonly ICurrentUser _currentUser;

    public RidePipeline(
        IRideSourceXmlProvider sourceXmlProvider,
        IRideXmlParserResolver parserResolver,
        IRideTemplateResolver templateResolver,
        IRideCacheStrategy cacheStrategy,
        IRideContentHasher contentHasher,
        IRideBrandingProvider brandingProvider,
        IRideRenderer renderer,
        IRidePdfStorageService storageService,
        IRidePdfDocumentRepository repository,
        ICurrentUser currentUser
    )
    {
        _sourceXmlProvider = sourceXmlProvider;
        _parserResolver = parserResolver;
        _templateResolver = templateResolver;
        _cacheStrategy = cacheStrategy;
        _contentHasher = contentHasher;
        _brandingProvider = brandingProvider;
        _renderer = renderer;
        _storageService = storageService;
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<RideGenerationResultDto>> ExecuteAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        bool forceRegenerate,
        CancellationToken ct
    )
    {
        // 1. Obtener XML autorizado.
        var sourceResult = await _sourceXmlProvider.GetAuthorizedXmlAsync(
            tenantId,
            companyId,
            sourceModule,
            sourceEntityId,
            ct
        );
        if (!sourceResult.IsSuccess)
            return Result<RideGenerationResultDto>.Failure(
                sourceResult.Error ?? "No se pudo consultar el documento electrónico de origen."
            );

        var lookup = sourceResult.Value!;
        if (lookup.Status == RideSourceXmlStatus.PendingSource)
            return Success(
                RideOutcome.PendingSource,
                storagePath: null,
                metadata: null,
                "source_xml_pending"
            );
        if (lookup.Status == RideSourceXmlStatus.NotApplicable)
            return Success(
                RideOutcome.NotApplicable,
                storagePath: null,
                metadata: null,
                "source_not_applicable"
            );

        var authorizedXml = lookup.AuthorizedXml!;
        var electronicDocumentId = lookup.ElectronicDocumentId;
        var documentType = lookup.DocumentType;

        // 2. Resolver parser (Strategy — ADR-025 §9).
        var parser = _parserResolver.Resolve(documentType);
        if (parser is null)
            return Success(
                RideOutcome.Failed,
                storagePath: null,
                metadata: null,
                "parser_not_registered"
            );

        // 3. Resolver plantilla (Strategy — ADR-025 §9).
        var selector = new RideTemplateSelector(
            tenantId,
            companyId,
            BranchId: null,
            EmissionPointId: null,
            documentType
        );
        var template = _templateResolver.Resolve(selector);
        if (template is null)
            return Success(
                RideOutcome.Failed,
                storagePath: null,
                metadata: null,
                "template_not_registered"
            );

        // 4. Consultar estrategia de cache — huella completa (ADR-025 §14).
        var sourceXmlHash = _contentHasher.Compute(authorizedXml);
        var templateId = documentType.ToString();

        var cacheResult = await _cacheStrategy.TryGetCachedAsync(
            tenantId,
            electronicDocumentId,
            sourceXmlHash,
            templateId,
            NeutralTemplateVersion,
            NeutralBrandingVersion,
            NeutralRendererVersion,
            RideSpecificationVersion,
            ct
        );
        if (!cacheResult.IsSuccess)
            return Result<RideGenerationResultDto>.Failure(
                cacheResult.Error ?? "No se pudo consultar el cache de RIDE."
            );

        if (!forceRegenerate && cacheResult.Value is { } cachedMetadata)
        {
            var cachedDocument = await _repository.GetByFingerprintAsync(
                tenantId,
                electronicDocumentId,
                sourceXmlHash,
                NeutralTemplateVersion,
                NeutralBrandingVersion,
                NeutralRendererVersion,
                RideSpecificationVersion,
                ct
            );
            return Success(
                RideOutcome.Cached,
                cachedDocument?.StoragePath,
                cachedMetadata,
                reasonCode: null
            );
        }

        // 5. Renderizar (solo si corresponde: cache-miss o regeneración forzada).
        string storagePath;
        try
        {
            var parseResult = parser.Parse(authorizedXml);
            if (!parseResult.IsSuccess)
                return Success(
                    RideOutcome.Failed,
                    storagePath: null,
                    metadata: null,
                    "xml_parse_failed"
                );

            var brandingResult = await _brandingProvider.GetAsync(
                tenantId,
                companyId,
                branchId: null,
                emissionPointId: null,
                ct
            );
            if (!brandingResult.IsSuccess)
                return Success(
                    RideOutcome.Failed,
                    storagePath: null,
                    metadata: null,
                    "branding_resolution_failed"
                );

            var layout = template.Compose(parseResult.Value!, brandingResult.Value!);
            var pdfBytes = await _renderer.RenderAsync(layout, ct);

            var storeResult = await _storageService.StoreAsync(
                tenantId,
                documentType,
                electronicDocumentId,
                NeutralTemplateVersion,
                pdfBytes,
                ct
            );
            if (!storeResult.IsSuccess)
                return Result<RideGenerationResultDto>.Failure(
                    storeResult.Error ?? "No se pudo almacenar el PDF generado."
                );

            storagePath = storeResult.Value!;
        }
        catch (Exception ex)
        {
            // Ningún contrato de render/plantilla expone un canal de error tipado (IRideRenderer/
            // IRideTemplate no devuelven Result<T>) — este boundary evita que una falla real de
            // un futuro renderer/plantilla concretos escape como excepción no manejada del pipeline.
            return Success(
                RideOutcome.Failed,
                storagePath: null,
                metadata: null,
                $"render_pipeline_error:{ex.GetType().Name}"
            );
        }

        // 6. Persistir.
        var generatedAtUtc = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        var existing = await _repository.GetByFingerprintAsync(
            tenantId,
            electronicDocumentId,
            sourceXmlHash,
            NeutralTemplateVersion,
            NeutralBrandingVersion,
            NeutralRendererVersion,
            RideSpecificationVersion,
            ct
        );

        RidePdfDocument document;
        if (existing is null)
        {
            document = RidePdfDocument.Create(
                tenantId,
                companyId,
                electronicDocumentId,
                documentType,
                sourceXmlHash,
                templateId,
                NeutralTemplateVersion,
                NeutralBrandingVersion,
                NeutralRendererVersion,
                RideSpecificationVersion,
                userId
            );
            document.MarkGenerated(storagePath, generatedAtUtc, userId);
            await _repository.AddAsync(document, ct);
        }
        else if (existing.State == RidePdfState.Generated)
        {
            existing.MarkRegenerated(storagePath, generatedAtUtc, userId);
            document = existing;
        }
        else
        {
            existing.MarkGenerated(storagePath, generatedAtUtc, userId);
            document = existing;
        }

        await _repository.SaveChangesAsync(ct);

        var metadata = new RidePdfMetadataDto(
            templateId,
            NeutralTemplateVersion,
            NeutralBrandingVersion,
            NeutralRendererVersion,
            sourceXmlHash.Value,
            generatedAtUtc,
            WasCached: false
        );

        return Success(RideOutcome.Generated, storagePath, metadata, reasonCode: null);
    }

    private static Result<RideGenerationResultDto> Success(
        RideOutcome outcome,
        string? storagePath,
        RidePdfMetadataDto? metadata,
        string? reasonCode
    ) =>
        Result<RideGenerationResultDto>.Success(
            new RideGenerationResultDto(outcome, storagePath, metadata, reasonCode)
        );
}
