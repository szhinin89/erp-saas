namespace ERP.Application.Common;

/// <summary>
/// Cuotas editables por SuperAdmin; se persiste en <c>App_Data/instance-quota.json</c> (opcional).
/// Valores <see langword="null"/> en propiedades omitidas en el JSON = no sobrescriben la configuración por defecto (<c>appsettings</c> / variables de entorno).
/// </summary>
public sealed class InstanceQuotaFileModel
{
    public bool? DedicatedSingleClientInstance { get; set; }
    public int? MaxActiveTenants { get; set; }
    public int? MaxIdentityUsers { get; set; }
    public int? MaxUsersPerTenant { get; set; }
}
