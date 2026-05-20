using ERP.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class PlatformQueryAccessor : IPlatformQueryAccessor
{
    private readonly ILogger<PlatformQueryAccessor> _logger;

    public PlatformQueryAccessor(ILogger<PlatformQueryAccessor> logger) => _logger = logger;

    public IQueryable<TEntity> Unfiltered<TEntity>(IQueryable<TEntity> query, PlatformQueryReason reason)
        where TEntity : class
    {
        _logger.LogDebug(
            "Platform query without tenant filter: {Reason} on {Entity}",
            reason,
            typeof(TEntity).Name);

        return query.IgnoreQueryFilters();
    }
}
