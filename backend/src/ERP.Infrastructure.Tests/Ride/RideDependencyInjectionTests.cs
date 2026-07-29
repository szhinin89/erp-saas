using ERP.Application.Common;
using ERP.Application.Modules.Ride.Barcodes;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Qr;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Storage;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Infrastructure.Codes;
using ERP.Infrastructure.Codes.Barcodes;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Ride;
using ERP.Infrastructure.Ride.Barcodes;
using ERP.Infrastructure.Ride.Branding;
using ERP.Infrastructure.Ride.ElectronicDocumentsAdapter;
using ERP.Infrastructure.Ride.Qr;
using ERP.Infrastructure.Ride.Rendering;
using ERP.Infrastructure.Ride.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Construye un <see cref="ServiceProvider"/> real con exactamente el registro que
/// <c>DependencyInjection.AddInfrastructure</c> aplica para Ride (más lo mínimo que esos
/// registros requieren: MediatR, <see cref="ErpDbContext"/>, identidad fija) y confirma que los
/// contratos requeridos se resuelven sin excepción — sin arrancar Hangfire, Redis, JWT ni el
/// resto de subsistemas del ERP. Actualizado en Fase 7: <c>IRideRenderer</c> ahora resuelve a
/// <see cref="QuestPdfRideRenderer"/> (reemplaza a <c>NullRideRenderer</c>, eliminado en esa
/// fase); <c>IRideCacheStrategy</c>/<c>IRideContentHasher</c>/<c>IRideDocumentService</c> ya
/// resolvían a sus implementaciones reales de Application desde la Fase 5 — este archivo había
/// quedado desactualizado (referenciaba clases ya eliminadas) sin que ningún build lo detectara
/// porque no se reconstruyó ERP.Infrastructure.Tests en esas fases; corregido aquí.
/// </summary>
public sealed class RideDependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ERP.Application.DependencyInjection).Assembly));
        services.AddDbContext<ErpDbContext>(options => options.UseNpgsql("Host=localhost;Database=ride-di-test-only"));
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(Guid.NewGuid));
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(Guid.NewGuid));
        services.AddScoped<ICurrentUser>(_ => new FixedCurrentUser(Guid.NewGuid));
        services.AddScoped<IElectronicDocumentRepository, ElectronicDocumentRepository>();
        services.AddScoped<ERP.Application.Common.Interfaces.IFileStorage, ERP.Infrastructure.Services.LocalFileStorage>();

        services.AddSingleton<ERP.Application.Common.Persistence.IDatabaseExceptionTranslator,
            PostgresDatabaseExceptionTranslator>();
        services.AddScoped<ERP.Domain.Configuration.Interfaces.IOrgSettingsRepository>(_ =>
        {
            var mock = new Mock<ERP.Domain.Configuration.Interfaces.IOrgSettingsRepository>();
            mock.Setup(r => r.GetAllForScopeAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(),
                    ERP.Domain.Configuration.Enums.OrgScope.Company, It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ERP.Domain.Configuration.Entities.OrgSetting>());
            return mock.Object;
        });

        // ── Exactamente el bloque "Ride BC" registrado en ERP.Infrastructure/DependencyInjection.cs ──
        services.AddScoped<IRidePdfDocumentRepository, RidePdfDocumentRepository>();
        services.AddScoped<IRideXmlParserResolver, RideXmlParserResolver>();
        services.AddScoped<IRideXmlParser, InvoiceRideXmlParser>();
        services.AddScoped<IRideTemplateResolver, RideTemplateResolver>();
        services.AddScoped<IRideTemplate, DefaultInvoiceRideTemplate>();
        services.AddScoped<IRideSourceXmlProvider, ElectronicDocumentRideSourceXmlProvider>();
        services.AddScoped<IRideRenderer, QuestPdfRideRenderer>();
        services.AddScoped<IRideBrandingProvider, OrgSettingsRideBrandingProvider>();
        services.AddSingleton<ERP.Application.Codes.IQrCodeGenerator, QrCodeGenerator>();
        services.AddScoped<IRideQrCodeGenerator, RideQrCodeGenerator>();
        services.AddSingleton<ERP.Application.Codes.Barcodes.IBarcodeGenerator, Code128BarcodeGenerator>();
        services.AddScoped<IRideBarcodeGenerator, RideBarcodeGenerator>();
        services.AddScoped<IRidePdfStorageNamingStrategy, RidePdfStorageNamingStrategy>();
        services.AddScoped<IRidePdfStorageService, RidePdfStorageService>();
        services.AddScoped<IRideCacheStrategy, ERP.Application.Modules.Ride.Services.RideCacheStrategy>();
        services.AddScoped<IRideContentHasher, ERP.Application.Modules.Ride.Services.RideContentHasher>();
        services.AddScoped<ERP.Application.Modules.Ride.Services.RidePipeline>();
        services.AddScoped<IRideDocumentService, ERP.Application.Modules.Ride.Services.RideDocumentService>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void All_six_required_contracts_resolve_without_error()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var act = () =>
        {
            sp.GetRequiredService<IRideRenderer>();
            sp.GetRequiredService<IRideBrandingProvider>();
            sp.GetRequiredService<IRideQrCodeGenerator>();
            sp.GetRequiredService<IRideBarcodeGenerator>();
            sp.GetRequiredService<IRidePdfStorageService>();
            sp.GetRequiredService<IRideDocumentService>();
            sp.GetRequiredService<IRideSourceXmlProvider>();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Resolvers_and_repository_also_resolve_without_error()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var act = () =>
        {
            sp.GetRequiredService<IRideXmlParserResolver>();
            sp.GetRequiredService<IRideTemplateResolver>();
            sp.GetRequiredService<IRidePdfDocumentRepository>();
            sp.GetRequiredService<IRidePdfStorageNamingStrategy>();
        };

        act.Should().NotThrow();
    }

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class FixedCurrentUser(Func<Guid> userId) : ICurrentUser
    {
        public Guid UserId => userId();
        public bool IsAuthenticated => true;
        public string? Username => "test.user";
        public string? Email => "test.user@example.com";
        public string? FullName => "Test User";
        public string? Role => "Admin";
    }
}
