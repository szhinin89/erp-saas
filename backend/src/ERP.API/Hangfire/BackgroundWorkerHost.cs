using ERP.Application;
using ERP.Application.Common.Config;
using ERP.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;

namespace ERP.API.Hangfire;

/// <summary>The same executable can host only workers, without creating an HTTP server.</summary>
public static class BackgroundWorkerHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        if (!builder.Environment.IsEnvironment("Testing"))
            builder.Configuration.AddUserSecrets(typeof(BackgroundWorkerHost).Assembly, optional: true);
        builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(
            builder.Configuration["FileStorage:BasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "files"),
            "dataprotection-keys")));
        builder.Services.Configure<SriPollingOptions>(builder.Configuration.GetSection(SriPollingOptions.Section));
        builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
        builder.Services.Configure<KardexOptions>(builder.Configuration.GetSection(KardexOptions.Section));
        builder.Services.AddSingleton(builder.Configuration.GetSection(KardexOptions.Section).Get<KardexOptions>() ?? new());
        builder.Services.AddSingleton(builder.Configuration.GetSection(SessionExpirationOptions.Section).Get<SessionExpirationOptions>() ?? new());
        builder.Services.AddErpBackgroundProcessing(builder.Configuration);
        var host = builder.Build();
        if (host.Services.GetRequiredService<IOptions<HangfireOptions>>().Value.Server.Enabled != true)
            throw new InvalidOperationException("Worker host requires Hangfire:Server:Enabled=true.");
        QuestPDF.Settings.License = LicenseType.Community;
        return host;
    }
}
