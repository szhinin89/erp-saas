using ERP.Application.Modules.Company.PhysicalEmission;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Support;

/// <summary>PHYSICAL-FISCAL-F1..F5 — datos y dobles de la política fiscal física para E2E.</summary>
public static class PhysicalEmissionTestData
{
    public const string GeneralRegime = "01";

    /// <summary>
    /// Régimen General y una autorización de impresión vigente para la factura 01 del punto, con
    /// la política REAL: demuestra que una autorización registrada no habilita la factura física
    /// ordinaria (catálogo GLOBAL: Régimen General bloqueado).
    /// </summary>
    public static async Task AuthorizeInvoicesAsync(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        Guid userId
    )
    {
        var company = await db.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == companyId);
        company.TaxRegimeCode = GeneralRegime;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.PhysicalDocumentAuthorizations.Add(
            PhysicalDocumentAuthorization.Create(
                tenantId,
                companyId,
                emissionPointId,
                "01",
                "1234567890",
                today.AddMonths(-1),
                today.AddMonths(11),
                1,
                999_999,
                userId
            )
        );
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Política física satisfecha y sin límite de rango, SOLO para E2E de caja/ventas cuyo objeto
    /// no es la política (usan un punto físico para no ejercitar el pipeline SRI). La política real
    /// la cubren CompanyOperationalReadinessBranchHttpTests y las pruebas PostgreSQL de
    /// ERP.Infrastructure.Tests.
    /// </summary>
    public static IPhysicalEmissionRequirementsValidator Satisfied() => new SatisfiedPolicy();

    private sealed class SatisfiedPolicy : IPhysicalEmissionRequirementsValidator
    {
        public Task<ElectronicEmissionRequirementsResult> ValidateAsync(
            PhysicalEmissionRequirementsRequest request,
            CancellationToken ct = default
        ) =>
            Task.FromResult(
                new ElectronicEmissionRequirementsResult([]) { Modality = EmissionType.Physical }
            );

        public Task<IReadOnlyList<AuthorizedSequentialRange>> GetAuthorizedSequentialRangesAsync(
            PhysicalEmissionRequirementsRequest request,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<AuthorizedSequentialRange>>(
                [new(1, DocumentSequence.MaxSequentialValue)]
            );
    }
}
