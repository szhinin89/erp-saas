using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Application.Common;

public interface IIdentificationUsageValidator
{
    Task<bool> IsAllowedAsync(
        string idTypeCode,
        IdentificationUsageType usageType,
        CancellationToken ct = default
    );
}
