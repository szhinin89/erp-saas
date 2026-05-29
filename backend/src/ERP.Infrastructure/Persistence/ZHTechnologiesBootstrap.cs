using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Subscribers.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Crea el suscriptor y la empresa ZH Technologies en el arranque.
/// Idempotente: no modifica si ya existe el slug "zh-technologies".
/// </summary>
public static class ZHTechnologiesBootstrap
{
    private const string ZhSlug = "zh-technologies";

    public static async Task EnsureAsync(ErpDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        var existing = await db.Subscribers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Slug == ZhSlug, ct);

        if (existing is not null)
            return;

        var actorId = Guid.Empty;

        var subscriber = Subscriber.Create(
            name:         config["ZHTechnologies:Name"] ?? "ZH Technologies",
            slug:         ZhSlug,
            createdBy:    actorId,
            displayOrder: -1,
            priority:     -1,
            planCode:     "platform",
            preferredLanguage: "es");

        db.Subscribers.Add(subscriber);

        var taxId  = config["ZHTechnologies:TaxId"]      ?? "9999999999001";
        var legal  = config["ZHTechnologies:LegalName"]  ?? "ZH Technologies S.A.S.";
        var addr   = config["ZHTechnologies:MainAddress"] ?? "Dirección ZH Technologies";
        var tz     = config["ZHTechnologies:Timezone"]   ?? "America/Guayaquil";

        var company = Company.CreateManaged(
            subscriberId:       subscriber.Id,
            ruc:                taxId,
            legalName:          legal,
            mainAddress:        addr,
            tradeName:          "ZH Technologies",
            email:              config["ZHTechnologies:Email"],
            phone:              null,
            countryCode:        "ECU",
            timezone:           tz,
            currencyCode:       "USD",
            isProvisionalTaxId: ProvisionalTaxIdGenerator.IsProvisional(taxId),
            taxIdStatus:        ProvisionalTaxIdGenerator.IsProvisional(taxId)
                                    ? TaxIdStatus.Pending
                                    : TaxIdStatus.Verified);

        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
    }
}
