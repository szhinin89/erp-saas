namespace ERP.Application.Subscriptions;

/// <summary>
/// Resuelve módulos habilitados para JWT, permisos de sesión y DTOs de tenant vía modelo relacional.
/// </summary>
public interface ISessionModulesResolver
{
    Task<IReadOnlyList<string>> GetEnabledModuleKeysAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
