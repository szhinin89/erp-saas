using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

public sealed class HttpSessionContext : ISessionContext
{
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public HttpSessionContext(
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _subscriber = subscriber;
        _company = company;
        _user = user;
    }

    public Guid SubscriberId => _subscriber.SubscriberId;

    public Guid CompanyId => _company.CompanyId;

    public bool HasSubscriberContext => _subscriber.SubscriberId != Guid.Empty;

    public bool HasCompanyContext => _company.HasCompanyContext;

    public bool IsPlatformAdmin =>
        _subscriber.SubscriberId == Guid.Empty
        && string.Equals(_user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
}
