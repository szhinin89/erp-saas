using ERP.Application.Common.Services;
using ERP.Application.Modules.Company.PhysicalEmission;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Services;
using Moq;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>
/// READINESS-PREVENTIVE-1B — cableado del gate fiscal preventivo para pruebas de integración.
/// <see cref="Satisfied"/>: pruebas cuyo objeto no es la configuración fiscal del emisor.
/// <see cref="Real"/>: validador real sobre PostgreSQL (Company, Establishment, EmissionPoint,
/// SriSettings); solo la inspección del .p12 (almacenamiento de archivos) y el catálogo global de
/// tipos de comprobante son dobles. Ninguno de los dos contacta al SRI.
/// </summary>
public static class EmissionRequirementsTestWiring
{
    public static IElectronicEmissionRequirementsValidator Satisfied()
    {
        var validator = new Mock<IElectronicEmissionRequirementsValidator>();
        validator
            .Setup(v =>
                v.ValidateAsync(
                    It.IsAny<ElectronicEmissionRequirementsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(ElectronicEmissionRequirementsResult.NotApplicable);
        return validator.Object;
    }

    /// <summary>
    /// Resolver de estrategias de Ventas para pruebas ajenas a la emisión: cualquier tipo resuelve
    /// una estrategia sin requisitos ni efectos posteriores.
    /// </summary>
    public static ISalesInvoiceEmissionStrategyResolver PassThroughStrategies()
    {
        var strategy = new Mock<ISalesInvoiceEmissionStrategy>();
        strategy
            .Setup(s =>
                s.CheckRequirementsAsync(
                    It.IsAny<SalesInvoiceEmissionPreflight>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(ElectronicEmissionRequirementsResult.NotApplicable);
        strategy
            .Setup(s =>
                s.ExecuteAsync(It.IsAny<SalesInvoiceEmissionContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((string?)null);
        var resolver = new Mock<ISalesInvoiceEmissionStrategyResolver>();
        resolver
            .Setup(r => r.Resolve(It.IsAny<ERP.Domain.Modules.Company.Enums.EmissionType>()))
            .Returns(strategy.Object);
        return resolver.Object;
    }

    /// <summary>PHYSICAL-FISCAL-F1..F5 — política física satisfecha (pruebas ajenas a ella).</summary>
    public static IPhysicalEmissionRequirementsValidator PhysicalSatisfied()
    {
        var validator = new Mock<IPhysicalEmissionRequirementsValidator>();
        validator
            .Setup(v =>
                v.ValidateAsync(
                    It.IsAny<PhysicalEmissionRequirementsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ElectronicEmissionRequirementsResult([])
                {
                    Modality = ERP.Domain.Modules.Company.Enums.EmissionType.Physical,
                }
            );
        return validator.Object;
    }

    /// <summary>
    /// PHYSICAL-FISCAL-F1..F5 — política fiscal física real sobre PostgreSQL: Company, EmissionPoint,
    /// catálogo GLOBAL sembrado por la migración (tipos y régimen × tipo físico), autorizaciones y
    /// secuencia documental. Solo el día de empresa es un doble.
    /// </summary>
    public static IPhysicalEmissionRequirementsValidator RealPhysical(ErpDbContext db, DateOnly today)
    {
        var clock = new Mock<ICompanyClock>();
        clock
            .Setup(c => c.TodayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(today);
        return new PhysicalEmissionRequirementsValidator(
            new CompanyRepository(db),
            new EmissionPointRepository(db),
            new SriDocTypeCatalogResolver(db),
            new PhysicalDocumentAuthorizationRepository(db),
            new DocumentSequenceRepository(db),
            clock.Object
        );
    }

    public static IElectronicEmissionRequirementsValidator Real(
        ErpDbContext db,
        SriCertificateStatus? certificate = null
    )
    {
        var certificates = new Mock<ISriCertificateStatusResolver>();
        certificates
            .Setup(c => c.ResolveAsync(It.IsAny<SriSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificate ?? ValidCertificate());
        var docTypes = new Mock<ISriDocTypeCatalogResolver>();
        docTypes
            .Setup(d =>
                d.IsActiveElectronicDocTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        return new ElectronicEmissionRequirementsValidator(
            new CompanyRepository(db),
            new EstablishmentRepository(db),
            new EmissionPointRepository(db),
            new SriSettingsRepository(db),
            certificates.Object,
            docTypes.Object
        );
    }

    public static SriCertificateStatus ValidCertificate() =>
        new(true, true, true, DateTime.UtcNow.AddYears(1), "CN=TEST", "CN=TEST CA", null);

    /// <summary>SriSettings completa (ambiente de pruebas, WSDL con formato válido, .p12 adjunto).</summary>
    public static SriSettings CompleteSriSettings(Guid tenantId, Guid companyId, Guid userId)
    {
        var settings = SriSettings.Create(
            tenantId,
            companyId,
            environment: 1,
            emissionType: 1,
            wsdlUrl: "https://sri.invalid/never-called",
            userId
        );
        settings.AttachCertificate("certs/test.p12", "test.p12", 1024, DateTime.UtcNow, userId);
        return settings;
    }
}
