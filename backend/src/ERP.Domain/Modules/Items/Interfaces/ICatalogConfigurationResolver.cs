namespace ERP.Domain.Modules.Items.Interfaces;

/// <summary>
/// CONFIG-FOUNDATION-P1-04: único punto de resolución de OrgSettingKeys.Catalog.* — reemplaza la
/// lectura directa de IOrgSettingsRepository que antes vivía en CategoryDepthResolver
/// (CategoryNodeUseCases). Fallback SystemDefault: ausencia de fila o valor inválido/no-positivo
/// cae a 3 sin bloquear — la profundidad máxima de categorías es una restricción operativa, no
/// fiscal/crítica, consistente con FallbackStrategy.SystemDefault de su ConfigurationDefinition.
/// </summary>
public interface ICatalogConfigurationResolver
{
    Task<int> ResolveMaxCategoryDepthAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );
}
