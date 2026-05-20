using ERP.Application.Common;
using ERP.Application.Sales.UseCases.ReintentarEnvio;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.API.Hangfire;

/// <summary>
/// Job recurrente que reintenta automáticamente el envío al SRI de facturas en
/// estado ErrorEnvio o Rechazado. Se ejecuta cada 5 minutos vía Hangfire.
/// </summary>
public sealed class SriRetryJob : ISriRetryJob
{
    private readonly ErpDbContext          _context;
    private readonly IPlatformQueryAccessor _platform;
    private readonly IServiceScopeFactory  _scopeFactory;
    private readonly ILogger<SriRetryJob>  _logger;

    public SriRetryJob(
        ErpDbContext         context,
        IPlatformQueryAccessor platform,
        IServiceScopeFactory scopeFactory,
        ILogger<SriRetryJob> logger)
    {
        _context      = context;
        _platform     = platform;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var pendientes = await _platform
            .Unfiltered(_context.SalesBills, PlatformQueryReason.BackgroundJob)
            .Where(b => b.Status == "ErrorEnvio" || b.Status == "Rechazado")
            .Select(b => new { b.Id, b.SubscriberId })
            .ToListAsync(ct);

        if (pendientes.Count == 0)
        {
            _logger.LogDebug("SriRetryJob: sin facturas pendientes de reintento.");
            return;
        }

        _logger.LogInformation(
            "SriRetryJob: {Count} factura(s) pendiente(s) de reintento.",
            pendientes.Count);

        // Agrupar por tenant para procesar en lote y minimizar cambios de contexto
        var porSubscriber = pendientes.GroupBy(b => b.SubscriberId);

        foreach (var grupo in porSubscriber)
        {
            var subscriberId = grupo.Key;

            // Establecer el contexto de tenant para que CurrentSubscriberService lo use
            // como fallback (AsyncLocal fluye a través de await)
            JobSubscriberContext.Current = subscriberId;
            try
            {
                foreach (var bill in grupo)
                {
                    try
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        var result = await mediator.Send(
                            new RetrySubmissionCommand(bill.Id), ct);

                        if (result.IsSuccess)
                            _logger.LogInformation(
                                "SriRetryJob: factura {BillId} (tenant {SubscriberId}) reintentada con éxito.",
                                bill.Id, subscriberId);
                        else
                            _logger.LogWarning(
                                "SriRetryJob: factura {BillId} (tenant {SubscriberId}) falló: {Error}",
                                bill.Id, subscriberId, result.Error);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "SriRetryJob: error inesperado al reintentar factura {BillId} (tenant {SubscriberId}).",
                            bill.Id, subscriberId);
                    }
                }
            }
            finally
            {
                JobSubscriberContext.Current = Guid.Empty;
            }
        }
    }
}
