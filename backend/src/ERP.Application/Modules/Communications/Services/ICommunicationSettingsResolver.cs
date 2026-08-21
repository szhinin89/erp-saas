namespace ERP.Application.Modules.Communications.Services;

public interface ICommunicationSettingsResolver
{
    Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default);
}
