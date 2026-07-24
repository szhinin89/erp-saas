using ERP.Application.Access.UseCases.ExpireUserSessions;
using Hangfire;
using MediatR;

namespace ERP.API.Hangfire;

/// <summary>
/// Ejecuta ExpireUserSessionsCommand y registra la cantidad de sesiones cerradas. No consulta
/// ErpDbContext directamente, no aplica reglas de negocio propias, no toca RefreshToken —
/// toda esa responsabilidad vive en Application/Domain (ver ExpireUserSessionsHandler y
/// UserSession.MarkExpired).
/// </summary>
public sealed partial class ExpireUserSessionsJob : IExpireUserSessionsJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpireUserSessionsJob> _logger;

    public ExpireUserSessionsJob(IServiceScopeFactory scopeFactory, ILogger<ExpireUserSessionsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Mismo guard que ElectronicDocumentRetryJob: si una corrida se solapa con la siguiente
    // (job lento, instancia caída a mitad de corrida), evita procesar el mismo lote dos veces.
    [DisableConcurrentExecution(timeoutInSeconds: 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var result = await mediator.Send(new ExpireUserSessionsCommand(), cancellationToken);
            if (result.IsSuccess)
                LogExpired(result.Value);
            else
                LogCommandFailed(result.Error);
        }
        catch (Exception ex)
        {
            LogThrew(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ExpireUserSessionsJob: {Count} sesiones expiradas")]
    private partial void LogExpired(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ExpireUserSessionsJob: el comando no resolvió: {Reason}")]
    private partial void LogCommandFailed(string? reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "ExpireUserSessionsJob: excepción no controlada")]
    private partial void LogThrew(Exception ex);
}
