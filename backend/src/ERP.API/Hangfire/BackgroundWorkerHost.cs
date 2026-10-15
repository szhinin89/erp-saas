using ERP.API.Hosting;
using Microsoft.Extensions.Options;

namespace ERP.API.Hangfire;

/// <summary>
/// The same executable can host only workers, without creating an HTTP server. Composition comes
/// exclusively from <see cref="ErpHostRegistration"/> plus the Background Processing catalog.
/// </summary>
public static class BackgroundWorkerHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddErpConfigurationSources(builder.Environment, args);
        builder.Services.AddErpHostCore(builder.Configuration, builder.Environment);
        builder.Services.AddErpBackgroundProcessing(builder.Configuration);
        var host = builder.Build();
        if (host.Services.GetRequiredService<IOptions<HangfireOptions>>().Value.Server.Enabled != true)
            throw new InvalidOperationException("Worker host requires Hangfire:Server:Enabled=true.");
        return host;
    }
}
