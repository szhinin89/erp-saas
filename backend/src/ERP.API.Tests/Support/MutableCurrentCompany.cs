using ERP.Application.Common;

namespace ERP.API.Tests.Support;

internal sealed class MutableCurrentCompany : ICurrentCompany
{
    public Guid CompanyId { get; set; }

    public bool IsAuthenticated => CompanyId != Guid.Empty;

    public bool HasCompanyContext => CompanyId != Guid.Empty;
}
