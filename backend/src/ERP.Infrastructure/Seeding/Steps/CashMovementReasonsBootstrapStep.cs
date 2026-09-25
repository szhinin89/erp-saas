using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — motivos por defecto de movimiento manual de caja (Tipo A —
/// infraestructura del sistema, ver política de Bootstrap en CLAUDE.md). Cada empresa nueva parte
/// con un puñado de motivos editables/desactivables — nunca un enum estático en frontend. Solo
/// necesita TenantId/CompanyId (no depende de Caja ni de ningún otro step). Idempotente.
/// </summary>
public sealed partial class CashMovementReasonsBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.CashMovementReasons;

    private static readonly (string Code, string Name, CashMovementType Type, int Sort)[] DefaultReasons =
    [
        ("INGRESO_CAJA_CHICA", "Ingreso de caja chica", CashMovementType.ManualIncome, 1),
        ("OTRO_INGRESO", "Otro ingreso", CashMovementType.ManualIncome, 2),
        ("COMPRA_INSUMOS_MENORES", "Compra de insumos menores", CashMovementType.ManualExpense, 1),
        ("OTRO_EGRESO", "Otro egreso", CashMovementType.ManualExpense, 2),
        ("DEPOSITO_BANCARIO", "Depósito bancario", CashMovementType.Withdrawal, 1),
        ("RETIRO_EFECTIVO", "Retiro de efectivo", CashMovementType.Withdrawal, 2),
    ];

    private readonly ErpDbContext _db;
    private readonly ILogger<CashMovementReasonsBootstrapStep> _logger;

    public CashMovementReasonsBootstrapStep(
        ErpDbContext db,
        ILogger<CashMovementReasonsBootstrapStep> logger
    )
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    )
    {
        var (tenantId, companyId, actorId) = context;

        // Bootstrap sin tenant HTTP ambiente: bypass vía PlatformQueryAccessor, con TenantId +
        // CompanyId reaplicados explícitamente.
        var existingCodes = await _db
            .CashMovementReasons.AsPlatformQuery()
            .Where(r => r.TenantId == tenantId && r.CompanyId == companyId)
            .Select(r => r.Code)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (code, name, type, sort) in DefaultReasons)
        {
            if (existingSet.Contains(code))
                continue;

            var reason = CashMovementReason.Create(tenantId, companyId, code, name, type, sort, actorId);
            _db.CashMovementReasons.Add(reason);
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            LogSeeded(added, companyId);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Count} default cash movement reason(s) seeded for company {CompanyId}."
    )]
    private partial void LogSeeded(int count, Guid companyId);
}
