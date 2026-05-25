using ERP.Application.Common;
using ERP.Application.Common.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class PlatformQueryAccessor : IPlatformQueryAccessor
{
    private readonly ILogger<PlatformQueryAccessor> _logger;
    private readonly SubscriberEntitlementsOptions _options;

    public PlatformQueryAccessor(
        ILogger<PlatformQueryAccessor> logger,
        IOptions<SubscriberEntitlementsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public IQueryable<TEntity> Unfiltered<TEntity>(IQueryable<TEntity> query, PlatformQueryReason reason)
        where TEntity : class
    {
        if (_options.LogPlatformQueries)
        {
            _logger.LogDebug(
                "Platform query without tenant filter: {Reason} on {Entity}",
                reason,
                typeof(TEntity).Name);
        }

        return query.IgnoreQueryFilters();
    }
}
