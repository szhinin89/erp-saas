using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Tests;

internal static class TestErpDbContextFactory
{
    internal static ErpDbContext Create(
        DbContextOptions<ErpDbContext> options,
        ICurrentSubscriber tenant,
        IPublisher publisher)
    {
        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsOptions()));
        return new ErpDbContext(options, tenant, publisher, platform);
    }
}
