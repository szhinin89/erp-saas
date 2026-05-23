using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IOperationalContext"/> para requests HTTP y jobs Hangfire.
/// Compone ICurrentSubscriber + ICurrentCompany + ICurrentUser sin lógica adicional.
/// </summary>
public sealed class OperationalContextService : IOperationalContext
{
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany    _company;
    private readonly ICurrentUser       _user;

    public OperationalContextService(
        ICurrentSubscriber subscriber,
        ICurrentCompany    company,
        ICurrentUser       user)
    {
        _subscriber = subscriber;
        _company    = company;
        _user       = user;
    }

    public Guid   SubscriberId    => _subscriber.SubscriberId;
    public Guid   CompanyId       => _company.CompanyId;
    public Guid   UserId          => _user.UserId;
    public string Role            => _user.Role ?? string.Empty;

    public bool HasSubscriber   => _subscriber.SubscriberId != Guid.Empty;
    public bool HasCompany      => _company.HasCompanyContext;
    public bool IsAuthenticated => _user.IsAuthenticated;

    public bool IsPlatformAdmin =>
        _subscriber.SubscriberId == Guid.Empty &&
        string.Equals(_user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
}
