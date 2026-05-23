namespace ERP.Application.Common;

/// <summary>
/// Contrato auth platform (única fuente de verdad en Application/API).
/// </summary>
public static class PlatformAuthConstants
{
    /// <summary>Rol JWT canónico del operador platform global.</summary>
    public const string JwtPlatformOperatorRole = "PlatformOperator";

    /// <summary>Alias legacy aceptado en tokens y menús antiguos.</summary>
    public const string LegacyJwtPlatformOperatorRole = "SuperAdmin";

    public static bool IsJwtPlatformOperatorRole(string? role) =>
        string.Equals(role, JwtPlatformOperatorRole, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, LegacyJwtPlatformOperatorRole, StringComparison.OrdinalIgnoreCase);
}
