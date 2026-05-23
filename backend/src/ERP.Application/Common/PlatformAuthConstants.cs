namespace ERP.Application.Common;

/// <summary>
/// Contrato auth platform (única fuente de verdad en Application/API).
/// El literal legacy del rol JWT vive solo en <see cref="LegacyPlatformOperatorWireRole"/>.
/// </summary>
public static class PlatformAuthConstants
{
    /// <summary>Rol JWT canónico del operador platform global.</summary>
    public const string JwtPlatformOperatorRole = "PlatformOperator";

    /// <summary>Valor wire legacy (BD/tokens antiguos). No usar en nombres de producto.</summary>
    public const string LegacyPlatformOperatorWireRole = "SuperAdmin";

    public static bool IsJwtPlatformOperatorRole(string? role) =>
        string.Equals(role, JwtPlatformOperatorRole, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, LegacyPlatformOperatorWireRole, StringComparison.OrdinalIgnoreCase);

    public static bool IsLegacyPlatformOperatorWireRole(string? role) =>
        string.Equals(role, LegacyPlatformOperatorWireRole, StringComparison.OrdinalIgnoreCase);
}
