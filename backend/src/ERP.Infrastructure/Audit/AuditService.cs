using ERP.Application.Audit;
using ERP.Domain.Audit;

namespace ERP.Infrastructure.Audit;

/// <summary>
/// Implementación única de <see cref="IAuditService"/> — resuelve el
/// <see cref="IAuditWriter{TAudit}"/> genérico correspondiente al tipo concreto de cada
/// dominio en tiempo de ejecución.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly IServiceProvider _serviceProvider;

    public AuditService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task RecordAsync<TAudit>(TAudit record, CancellationToken ct = default) where TAudit : AuditRecordBase
    {
        var writer = (IAuditWriter<TAudit>)_serviceProvider.GetService(typeof(IAuditWriter<TAudit>))!;
        return writer.RecordAsync(record, ct);
    }
}
