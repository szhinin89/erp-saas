namespace ERP.API.Hangfire;

/// <summary>
/// Hangfire recurring job que ejecuta la limpieza pasiva de UserSession (Fase 9).
/// Solo orquesta ExpireUserSessionsCommand vía MediatR — sin acceso a DbContext ni reglas de
/// negocio propias, mismo patrón que IProcessOutboxJob/IElectronicDocumentRetryJob.
/// </summary>
public interface IExpireUserSessionsJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
