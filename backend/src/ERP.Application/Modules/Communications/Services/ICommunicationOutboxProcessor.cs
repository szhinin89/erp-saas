namespace ERP.Application.Modules.Communications.Services;

public interface ICommunicationOutboxProcessor
{
    Task ProcessPendingAsync(CancellationToken ct = default);
}
