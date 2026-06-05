using ERP.Application.Sales.UseCases.ListPendingSriRetry;
using ERP.Application.Sales.UseCases.RetrySubmission;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.API.Hangfire;

/// <summary>
/// Job recurrente que reintenta automáticamente el envío al SRI de facturas en
/// estado ErrorEnvio o Rechazado. Se ejecuta cada 5 minutos vía Hangfire.
/// </summary>
public sealed class SriRetryJob : ISriRetryJob
{
    private readonly IMediator            _mediator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SriRetryJob> _logger;

    public SriRetryJob(
        IMediator mediator,
        IServiceScopeFactory scopeFactory,
        ILogger<SriRetryJob> logger)
    {
        _mediator      = mediator;
        _scopeFactory  = scopeFactory;
        _logger        = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var pendingResult = await _mediator.Send(new ListPendingSriRetryQuery(), ct);
        if (!pendingResult.IsSuccess)
        {
            _logger.LogWarning("SriRetryJob: no se pudo listar pendientes: {Error}", pendingResult.Error);
            return;
        }

        var pendientes = pendingResult.Value ?? Array.Empty<SalesBillRetryCandidate>();
        if (pendientes.Count == 0)
        {
            _logger.LogDebug("SriRetryJob: sin facturas pendientes de reintento.");
            return;
        }

        _logger.LogInformation(
            "SriRetryJob: {Count} salesBill(s) pendiente(s) de reintento.",
            pendientes.Count);

        var porSubscriber = pendientes.GroupBy(b => b.SubscriberId);

        foreach (var grupo in porSubscriber)
        {
            var subscriberId = grupo.Key;

            using var jobCtx = JobExecutionContext.Begin(subscriberId);
            try
            {
                foreach (var bill in grupo)
                {
                    try
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        var result = await mediator.Send(
                            new RetrySubmissionCommand(bill.BillId), ct);

                        if (result.IsSuccess)
                            _logger.LogInformation(
                                "SriRetryJob: salesBill {BillId} (tenant {SubscriberId}) reintentada con éxito.",
                                bill.BillId, subscriberId);
                        else
                            _logger.LogWarning(
                                "SriRetryJob: salesBill {BillId} (tenant {SubscriberId}) falló: {Error}",
                                bill.BillId, subscriberId, result.Error);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "SriRetryJob: error inesperado al reintentar salesBill {BillId} (tenant {SubscriberId}).",
                            bill.BillId, subscriberId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SriRetryJob: error procesando subscriber {SubscriberId}", subscriberId);
            }
        }
    }
}
