using ERP.Application.Common;
using ERP.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests;

internal static class TestErpDbContextFactory
{
    internal static ErpDbContext Create(
        DbContextOptions<ErpDbContext> options,
        ICurrentTenant tenant,
        IPublisher publisher,
        ICurrentCompany? company = null
    )
    {
        company ??= new TestCurrentCompany();
        return new ErpDbContext(options, tenant, publisher, company);
    }

    private sealed class TestCurrentCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }
}
