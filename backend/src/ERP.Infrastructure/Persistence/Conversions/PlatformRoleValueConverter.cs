using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ERP.Infrastructure.Persistence.Conversions;

/// <summary>Persiste <see cref="PlatformRole"/> como string; acepta valor wire legacy en lectura.</summary>
public sealed class PlatformRoleValueConverter : ValueConverter<PlatformRole?, string?>
{
    public PlatformRoleValueConverter()
        : base(
            role => role == null ? null : role.Value.ToString(),
            value => FromStore(value))
    {
    }

    private static PlatformRole? FromStore(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (PlatformAuthConstants.IsLegacyPlatformOperatorWireRole(value))
            return PlatformRole.PlatformOperator;

        return Enum.Parse<PlatformRole>(value, ignoreCase: true);
    }
}
