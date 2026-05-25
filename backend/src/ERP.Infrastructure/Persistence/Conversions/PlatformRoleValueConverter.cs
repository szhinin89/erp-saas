using ERP.Domain.Access.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ERP.Infrastructure.Persistence.Conversions;

/// <summary>Persiste <see cref="PlatformRole"/> como string.</summary>
public sealed class PlatformRoleValueConverter : ValueConverter<PlatformRole?, string?>
{
    public PlatformRoleValueConverter()
        : base(
            role => role == null ? null : role.Value.ToString(),
            value => string.IsNullOrWhiteSpace(value)
                ? null
                : Enum.Parse<PlatformRole>(value, ignoreCase: true))
    {
    }
}
