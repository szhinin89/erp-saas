using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Numeraciones (DocumentSequence) por cada tipo de documento SRI activo/electrónico, sobre el
/// punto de emisión principal de la empresa. Depende de <see cref="OrganizationBootstrapStep"/>
/// (debe ejecutarse después — <see cref="Order"/> mayor) y localiza ese punto de emisión
/// consultando <c>EmissionPoint.IsDefault</c>, nunca a través de una referencia directa al otro
/// step. Idempotente.
/// </summary>
public sealed partial class ElectronicDocumentsBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.ElectronicDocuments;

    private readonly ErpDbContext _db;
    private readonly ILogger<ElectronicDocumentsBootstrapStep> _logger;

    public ElectronicDocumentsBootstrapStep(ErpDbContext db, ILogger<ElectronicDocumentsBootstrapStep> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CompanyBootstrapContext context, CancellationToken cancellationToken = default)
    {
        var (tenantId, companyId, _) = context;

        var emissionPointId = await _db.EmissionPoints.IgnoreQueryFilters()
            .Where(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.IsDefault)
            .Select(ep => (Guid?)ep.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!emissionPointId.HasValue)
        {
            LogNoDefaultEmissionPoint(companyId);
            return;
        }

        var docTypeCodes = await _db.Set<ERP.Domain.Modules.SriCatalogs.Entities.SriDocType>()
            .Where(d => d.IsActive && d.IsElectronic)
            .Select(d => d.Code)
            .ToListAsync(cancellationToken);

        foreach (var docTypeCode in docTypeCodes)
        {
            var exists = await _db.DocumentSequences.IgnoreQueryFilters()
                .AnyAsync(ds => ds.EmissionPointId == emissionPointId.Value
                             && ds.DocTypeCode     == docTypeCode, cancellationToken);

            if (exists)
            {
                LogDocSequenceSkipped(docTypeCode, emissionPointId.Value);
                continue;
            }

            var sequence = DocumentSequence.Create(
                tenantId:        tenantId,
                companyId:       companyId,
                emissionPointId: emissionPointId.Value,
                docTypeCode:     docTypeCode);

            _db.DocumentSequences.Add(sequence);
            LogDocSequenceSeeded(docTypeCode, emissionPointId.Value);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No default EmissionPoint found for company {CompanyId}. Skipping document sequence seeding.")]
    private partial void LogNoDefaultEmissionPoint(Guid companyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DocumentSequence {DocType} already exists for emissionPoint {EmPointId}. Skipping.")]
    private partial void LogDocSequenceSkipped(string docType, Guid emPointId);

    [LoggerMessage(Level = LogLevel.Information, Message = "DocumentSequence {DocType} seeded for emissionPoint {EmPointId}.")]
    private partial void LogDocSequenceSeeded(string docType, Guid emPointId);
}
