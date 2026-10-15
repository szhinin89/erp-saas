using ERP.Application.Access.UseCases.ExpireUserSessions;
using ERP.Infrastructure.BackgroundProcessing;
using MediatR;
namespace ERP.API.Hangfire;
public sealed class ExpireUserSessionsJob(IMediator mediator) : IRecurringJob
{
    public async Task ExecuteAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ExpireUserSessionsCommand(batchSize), cancellationToken);
        if (!result.IsSuccess) throw new InvalidOperationException(result.Error);
        JobMetrics.RecordProcessed(result.Value);
    }
}
