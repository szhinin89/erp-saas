using ERP.Domain.Common;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversiones entre WithholdingDirection (dominio) y valores de BD.
/// Consumers: EF value converters en SalesWithholdingConfiguration, PurchaseWithholdingConfiguration.
/// </summary>
internal static class WithholdingDirectionConversions
{
    internal static string ToDb(WithholdingDirection direction) =>
        direction == WithholdingDirection.Issued ? "ISSUED" : "RECEIVED";

    internal static WithholdingDirection FromDb(string value) =>
        value.Trim().ToUpperInvariant() == "ISSUED"
            ? WithholdingDirection.Issued
            : WithholdingDirection.Received;
}
