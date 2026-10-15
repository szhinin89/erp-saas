namespace ERP.Application.Modules.Communications.Services;

public interface ICommunicationOutboxProcessor
{
    Task ProcessPendingAsync(int batchSize, CancellationToken ct = default);
}
