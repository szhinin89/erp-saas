using ERP.Application.Common;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Deployment;

public sealed class InstanceQuotaPersistence : IInstanceQuotaPersistence
{
    private readonly InstanceQuotaFileStore _store;

    public InstanceQuotaPersistence(InstanceQuotaFileStore store) => _store = store;

    public void Save(InstanceQuotaFileModel model) => _store.Write(model);
}
