namespace ERP.Application.Common.Interfaces;

/// <summary>Persistencia de cuotas de instancia (<c>App_Data/instance-quota.json</c>).</summary>
public interface IInstanceQuotaPersistence
{
    void Save(InstanceQuotaFileModel model);
}
