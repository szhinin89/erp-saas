using ERP.Domain.MasterData.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// BANK-CATALOG-01: seed mínimo Ecuador del catálogo maestro de Bancos. Fuente única de la lista,
/// compartida por <see cref="Steps.BankCatalogBootstrapStep"/> (empresas nuevas) y
/// <see cref="BankCatalogBackfillService"/> (tenants ya existentes) — mismo criterio que
/// <see cref="MasterDataClassificationSeeder"/>. Idempotente: nunca duplica ni pisa un banco que
/// el tenant ya tenga (creado por el seed o manualmente) — solo agrega los códigos faltantes.
/// </summary>
public sealed partial class BankCatalogSeeder
{
    private static readonly (string Code, string Name, string? ShortName)[] DefaultBanks =
    [
        ("PICHINCHA", "Banco Pichincha", null),
        ("GUAYAQUIL", "Banco Guayaquil", null),
        ("PACIFICO", "Banco del Pacífico", null),
        ("AUSTRO", "Banco del Austro", null),
        ("PRODUBANCO", "Produbanco", null),
        ("INTERNACIONAL", "Banco Internacional", null),
        ("BOLIVARIANO", "Banco Bolivariano", null),
        ("JEP", "Cooperativa JEP", null),
        ("JARDIN_AZUAYO", "Cooperativa Jardín Azuayo", null),
    ];

    private readonly ErpDbContext _db;
    private readonly ILogger<BankCatalogSeeder> _logger;

    public BankCatalogSeeder(ErpDbContext db, ILogger<BankCatalogSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> SeedAsync(Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        var existingCodes = await _db
            .Banks.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.CountryCode == Bank.DefaultCountryCode)
            .Select(b => b.Code)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var (code, name, shortName) in DefaultBanks)
        {
            if (existingSet.Contains(code))
                continue;

            var bank = Bank.CreateSystemSeeded(tenantId, code, name, shortName, actorId);
            _db.Banks.Add(bank);
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            LogBanksSeeded(added, tenantId);
        }

        return added;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Count} default bank(s) seeded for tenant {TenantId}."
    )]
    private partial void LogBanksSeeded(int count, Guid tenantId);
}
