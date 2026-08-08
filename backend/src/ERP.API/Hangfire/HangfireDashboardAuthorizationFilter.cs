using System.Net;
using System.Net.Sockets;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;

namespace ERP.API.Hangfire;

/// <summary>
/// Autorización del dashboard Hangfire: localhost, prefijos de IP de allowlist,
/// o usuario autenticado si <c>Hangfire:Dashboard:AllowAnyAuthenticatedUser</c> = true.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        var env = http.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var section = config.GetSection("Hangfire:Dashboard");

        if (!section.GetValue("Enabled", true))
            return false;

        // Default seguro: false en Production si la config no lo declara explícitamente.
        // appsettings.Production.json ya lo fija en false; esto es defensa en profundidad.
        var allowLocalhostDefault = !env.IsProduction();
        if (section.GetValue("AllowLocalhost", allowLocalhostDefault) && IsLocalOrLoopback(http))
            return true;

        foreach (var prefix in section.GetSection("IpAllowlistPrefixes").Get<string[]>() ?? [])
        {
            if (string.IsNullOrWhiteSpace(prefix))
                continue;
            if (RemoteIpStartsWith(http, prefix))
                return true;
        }

        return section.GetValue("AllowAnyAuthenticatedUser", false)
            && http.User.Identity?.IsAuthenticated == true;
    }

    private static bool IsLocalOrLoopback(HttpContext http)
    {
        var remote = http.Connection.RemoteIpAddress;
        if (remote is null)
            return false;
        if (IPAddress.IsLoopback(remote))
            return true;
        if (remote.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (remote.IsIPv4MappedToIPv6)
                return IPAddress.IsLoopback(remote.MapToIPv4());
        }

        return false;
    }

    private static bool RemoteIpStartsWith(HttpContext http, string prefix)
    {
        var remote = http.Connection.RemoteIpAddress;
        if (remote is null)
            return false;

        var s = remote.ToString();
        if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        if (remote.IsIPv4MappedToIPv6)
        {
            var v4 = remote.MapToIPv4().ToString();
            return v4.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
