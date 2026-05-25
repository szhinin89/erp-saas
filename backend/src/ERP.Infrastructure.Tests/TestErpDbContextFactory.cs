using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common;
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
        IPublisher publisher,
        ICurrentCompany? company = null)
    {
        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SubscriberEntitlementsOptions()));
        company ??= new TestCurrentCompany();
        return new ErpDbContext(options, tenant, publisher, platform, company);
    }

    private sealed class TestCurrentCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
        public bool IsAuthenticated => false;
        public bool HasCompanyContext => false;
    }
}
