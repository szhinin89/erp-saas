using ERP.Application.Modules.ElectronicDocuments.Services;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>
/// SRI-TOGGLE-02 — interruptor habilitado desde siempre para los tests que arman el issuer a mano y no
/// prueban el interruptor: conserva su comportamiento previo. El interruptor real se prueba en
/// <c>ElectronicEmissionSwitchPostgreSqlTests</c>.
/// </summary>
public sealed class EnabledElectronicEmissionSwitch : IElectronicEmissionSwitch
{
    public static readonly EnabledElectronicEmissionSwitch Instance = new();

    public Task<ElectronicEmissionState> GetAsync(
        Guid tenantId,
        Guid companyId,
        bool lockForShare,
        CancellationToken ct = default
    ) => Task.FromResult(new ElectronicEmissionState(true, DateTime.MinValue));

    public Task<DateTime?> GetSourceActivityUtcAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        ERP.Domain.Modules.ElectronicDocuments.Enums.ElectronicDocumentType documentType,
        Guid sourceEntityId,
        CancellationToken ct = default
    ) => Task.FromResult<DateTime?>(DateTime.UtcNow);

    public Task LockForChangeAsync(Guid tenantId, Guid companyId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IAsyncDisposable> EnterSriCallAsync(Guid tenantId, Guid companyId, CancellationToken ct = default) =>
        Task.FromResult<IAsyncDisposable>(NoScope.Instance);

    private sealed class NoScope : IAsyncDisposable
    {
        public static readonly NoScope Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
