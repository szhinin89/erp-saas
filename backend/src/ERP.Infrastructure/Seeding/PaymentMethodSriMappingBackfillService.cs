using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// SALES-PAYMENT-METHOD-SRI-MAPPING-EFECTIVO-WRONG-CODE-01: backfill idempotente de
/// <see cref="PaymentMethod.SriPaymentMethodCode"/> para tenants que ya existían antes de
/// SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01 — <c>Steps.SalesBootstrapStep</c> solo siembra formas
/// de cobro para empresas NUEVAS y nunca toca filas ya existentes (mismo patrón de brecha que
/// <see cref="MasterDataClassificationBackfillService"/>). Sin este backfill, esos tenants quedan
/// con SriPaymentMethodCode NULL en sus 5 formas de cobro Tipo A y la UI cae al default de empresa
/// (invoice.default_payment_method_code) — visible como "Efectivo → SRI 20" cuando el default de
/// la empresa es 20.
///
/// Reglas de seguridad:
/// - Nunca pisa un valor ya configurado (<see cref="PaymentMethod.BackfillSriPaymentMethodCode"/>
///   es un no-op si el campo ya tiene valor) — respeta cualquier mapeo manual existente.
/// - Solo aplica el código sugerido de <see cref="DefaultPaymentMethodSeedData"/> si ese código
///   existe y está activo en el catálogo real <c>global.sri_payment_method</c> — nunca escribe un
///   código a ciegas.
/// - Multi-tenant seguro: cada fila conserva su propio TenantId (no se reasigna nada entre
///   tenants); se itera sobre TODOS los PaymentMethod vía IgnoreQueryFilters() porque esto corre
///   fuera de cualquier contexto de tenant autenticado (operación de despliegue), igual que
///   MasterDataClassificationBackfillService.
///
/// Invocación: <c>dotnet run -- backfill-payment-method-sri-mapping</c> (ver Program.cs). No
/// expone endpoint HTTP — operación de despliegue de una sola vez, re-ejecutable sin riesgo.
/// </summary>
public sealed class PaymentMethodSriMappingBackfillService
{
    /// <summary>Actor de sistema para operaciones sin usuario autenticado (mismo criterio que MasterDataClassificationBackfillService).</summary>
    private static readonly Guid SystemActor = Guid.Empty;

    private readonly ErpDbContext _db;
    private readonly ISriCatalogLookupRepository _catalogRepo;
    private readonly ILogger<PaymentMethodSriMappingBackfillService> _logger;

    public PaymentMethodSriMappingBackfillService(
        ErpDbContext db,
        ISriCatalogLookupRepository catalogRepo,
        ILogger<PaymentMethodSriMappingBackfillService> logger
    )
    {
        _db = db;
        _catalogRepo = catalogRepo;
        _logger = logger;
    }

    public async Task<PaymentMethodSriMappingBackfillResult> RunAsync(CancellationToken ct = default)
    {
        var candidates = await _db
            .PaymentMethods.IgnoreQueryFilters()
            .Where(pm => pm.SriPaymentMethodCode == null)
            .ToListAsync(ct);

        var activePaymentMethodCodes = (
            await _catalogRepo.GetActivePaymentMethodsAsync(ct)
        )
            .Select(c => c.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        var skippedNoMapping = 0;
        var skippedInactiveCatalog = 0;

        foreach (var pm in candidates)
        {
            var suggestedCode = DefaultPaymentMethodSeedData.SriCodeFor(pm.Code);
            if (suggestedCode is null)
            {
                skippedNoMapping++;
                continue;
            }

            if (!activePaymentMethodCodes.Contains(suggestedCode))
            {
                skippedInactiveCatalog++;
                _logger.LogWarning(
                    "Backfill PaymentMethod.SriPaymentMethodCode: código sugerido {SuggestedCode} para {Code} (tenant {TenantId}) no existe o está inactivo en global.sri_payment_method — se omite.",
                    suggestedCode,
                    pm.Code,
                    pm.TenantId
                );
                continue;
            }

            if (pm.BackfillSriPaymentMethodCode(suggestedCode, SystemActor))
                updated++;
        }

        if (updated > 0)
            await _db.SaveChangesAsync(ct);

        var result = new PaymentMethodSriMappingBackfillResult(
            candidates.Count,
            updated,
            skippedNoMapping,
            skippedInactiveCatalog
        );

        _logger.LogInformation(
            "Backfill PaymentMethod.SriPaymentMethodCode: {Candidates} fila(s) sin mapeo, {Updated} actualizada(s), {SkippedNoMapping} sin código sugerido (p. ej. Crédito), {SkippedInactiveCatalog} con código sugerido inactivo en catálogo.",
            result.CandidatesFound,
            result.RowsUpdated,
            result.SkippedNoMapping,
            result.SkippedInactiveCatalog
        );

        return result;
    }
}

public sealed record PaymentMethodSriMappingBackfillResult(
    int CandidatesFound,
    int RowsUpdated,
    int SkippedNoMapping,
    int SkippedInactiveCatalog
);
