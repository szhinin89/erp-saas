using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ERP.Application.Common;

namespace ERP.Infrastructure.Services;

public class CurrentSubscriberService : ICurrentSubscriber
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentSubscriberService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid SubscriberId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst("subscriber_id")?.Value;

            if (Guid.TryParse(claim, out var id))
                return id;

            // Fallback para jobs de Hangfire (sin HttpContext)
            return JobSubscriberContext.Current;
        }
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false
           || JobSubscriberContext.Current != Guid.Empty;
}
