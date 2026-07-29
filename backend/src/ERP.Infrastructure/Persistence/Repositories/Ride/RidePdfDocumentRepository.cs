using ERP.Application.Common.Persistence;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Ride;

public sealed class RidePdfDocumentRepository : IRidePdfDocumentRepository
{
    /// <summary>Debe coincidir exactamente con <c>uq_ride_pdf_document_fingerprint</c> (migración Fase 4).</summary>
    private const string FingerprintConstraintName = "uq_ride_pdf_document_fingerprint";

    private readonly ErpDbContext _db;
    private readonly IDatabaseExceptionTranslator _dbEx;

    public RidePdfDocumentRepository(ErpDbContext db, IDatabaseExceptionTranslator dbEx)
    {
        _db = db;
        _dbEx = dbEx;
    }

    public Task<RidePdfDocument?> GetByFingerprintAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        RideContentHash sourceXmlHash,
        string templateVersion,
        string brandingVersion,
        string rendererVersion,
        string rideSpecificationVersion,
        CancellationToken ct = default
    ) =>
        _db.RidePdfDocuments.FirstOrDefaultAsync(
            x =>
                x.TenantId == tenantId
                && x.ElectronicDocumentId == electronicDocumentId
                && x.SourceXmlHash == sourceXmlHash
                && x.TemplateVersion == templateVersion
                && x.BrandingVersion == brandingVersion
                && x.RendererVersion == rendererVersion
                && x.RideSpecificationVersion == rideSpecificationVersion,
            ct
        );

    public Task AddAsync(RidePdfDocument document, CancellationToken ct = default) =>
        _db.RidePdfDocuments.AddAsync(document, ct).AsTask();

    /// <summary>
    /// H4 (ADR-025 §14, Fase 8): dos generaciones concurrentes de la MISMA huella exacta violan
    /// <c>uq_ride_pdf_document_fingerprint</c> — el perdedor de la carrera nunca tiene nada
    /// distinto que persistir, porque la huella (hash del XML + 4 versiones) ya determina de
    /// forma unívoca tanto la ruta de storage (<c>IRidePdfStorageNamingStrategy</c>, determinística)
    /// como el contenido exacto del PDF (render puro). Se descarta el intento local — nunca se
    /// propaga la excepción al consumidor ni se reporta como conflicto de negocio (a diferencia
    /// de <c>ElectronicDocumentIssuer.RegisterAsync</c>, que sí es una colisión de negocio real:
    /// esto es una carrera de infraestructura sobre un artefacto puramente derivado).
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
            when (_dbEx.TryGetUniqueViolation(ex, out var info)
                && info.ConstraintName == FingerprintConstraintName
            )
        {
            foreach (var entry in _db.ChangeTracker.Entries<RidePdfDocument>().ToList())
                entry.State = EntityState.Detached;
        }
    }
}
