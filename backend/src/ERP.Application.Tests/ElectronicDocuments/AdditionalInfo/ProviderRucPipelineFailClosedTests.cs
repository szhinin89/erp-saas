using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D7) — por el pipeline real de
/// <see cref="ElectronicDocumentIssuer"/>: con el RUC Proveedor exigible y la configuración global
/// incompleta, el documento queda <c>Failed</c> con <c>SRI_SYSTEM_PROVIDER_RUC_NOT_CONFIGURED</c>, sin
/// XSD, sin firma, sin almacenamiento y sin llamada al SRI. Al corregir la configuración, el reintento
/// genera el XML con el campo. Solo la firma, el almacenamiento y el SRI son dobles.
/// </summary>
public sealed class ProviderRucPipelineFailClosedTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SourceEntityId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly IssueDate = new(2026, 11, 3);

    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) => taxCode == "VAT" ? "2" : null;
    }

    private sealed class Fixture
    {
        public Mock<IElectronicDocumentRepository> Repository { get; } = new();
        public Mock<IElectronicDocumentSchemaValidator> Validator { get; } = new();
        public Mock<IElectronicDocumentSigningService> Signing { get; } = new();
        public Mock<IElectronicDocumentXmlStorageService> Storage { get; } = new();
        public Mock<IElectronicDocumentReceptionService> Reception { get; } = new();
        public Mock<IElectronicDocumentAuthorizationService> Authorization { get; } = new();
        public Mock<ISystemProviderSettingsRepository> Settings { get; }
        public ElectronicDocument? Document { get; private set; }
        public string? SignedInputXml { get; private set; }

        public Fixture(Mock<ISystemProviderSettingsRepository> settings)
        {
            Settings = settings;

            Repository
                .Setup(r => r.GetBySourceAsync(TenantId, "Sales", SourceEntityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Document);
            Repository
                .Setup(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()))
                .Callback<ElectronicDocument, CancellationToken>((d, _) => Document = d)
                .Returns(Task.CompletedTask);
            Repository
                .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Document);
            Repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            Validator.Setup(v => v.DocumentType).Returns(ElectronicDocumentType.Invoice);
            Validator
                .Setup(v => v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ElectronicDocumentSchemaValidationResult(true, [], [], "1.1.0", ElectronicDocumentType.Invoice));

            Signing
                .Setup(s => s.SignAsync(TenantId, CompanyId, It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, ElectronicDocumentXml, CancellationToken>((_, _, xml, _) => SignedInputXml = xml.Xml)
                .ReturnsAsync((Guid _, Guid _, ElectronicDocumentXml xml, CancellationToken _) =>
                    Result<SignedElectronicDocumentXml>.Success(
                        new SignedElectronicDocumentXml(xml.Xml, "UTF-8", xml.Version, xml.DocumentType, xml.AccessKey, DateTime.UtcNow)
                    ));

            Storage
                .Setup(s => s.StoreAsync(TenantId, ElectronicDocumentType.Invoice, It.IsAny<Guid>(),
                    It.IsAny<ElectronicDocumentXml>(), It.IsAny<SignedElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ElectronicDocumentStoredXmlPaths>.Success(
                    new ElectronicDocumentStoredXmlPaths("draft/path.xml", "signed/path.xml")));

            Reception
                .Setup(r => r.SendAsync(CompanyId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SriReceptionResult>.Failure("SRI no disponible en el test."));
        }

        public ElectronicDocumentIssuer BuildIssuer()
        {
            var provider = new Mock<IElectronicDocumentDataProvider>();
            provider
                .Setup(p => p.GetDataAsync(It.IsAny<ElectronicDocumentSourceReference>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ElectronicDocumentData>.Success(InvoiceData()));

            var supplier = new CommercialElectronicDocumentXmlSupplier(
                ElectronicDocumentType.Invoice,
                provider.Object,
                new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver()),
                AdditionalInfoTestDoubles.ComposerWithProviderRuc(Settings)
            );
            var supplierResolver = new Mock<IElectronicDocumentXmlSupplierResolver>();
            supplierResolver.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(supplier);

            var validatorResolver = new Mock<IElectronicDocumentSchemaValidatorResolver>();
            validatorResolver.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(Validator.Object);

            var dbEx = new Mock<IDatabaseExceptionTranslator>();
            DatabaseUniqueViolationInfo? none = null;
            dbEx.Setup(d => d.TryGetUniqueViolation(It.IsAny<Exception>(), out none)).Returns(false);

            return new ElectronicDocumentIssuer(
                Repository.Object,
                supplierResolver.Object,
                validatorResolver.Object,
                Signing.Object,
                Storage.Object,
                Reception.Object,
                Authorization.Object,
                new Mock<IFileStorage>().Object,
                dbEx.Object,
                RetentionElectronicTestDoubles.NoGuards(),
                Mock.Of<IUnitOfWork>(),
                NullLogger<ElectronicDocumentIssuer>.Instance
            );
        }
    }

    private static ElectronicDocumentData InvoiceData() =>
        new(
            Emission: new ElectronicDocumentEmissionContext("1", "1", "01", "001", "Av. Amazonas", "001", "000000123", IssueDate),
            Issuer: new ElectronicDocumentIssuerData("1790012345001", "ACME CIA LTDA", null, "Av. Amazonas", null, true),
            Counterparty: new ElectronicDocumentCounterpartyData("05", "1710034065", "Juan Perez", null, null),
            Details:
            [
                new ElectronicDocumentDetailLine("SKU-001", "Producto", 1m, 10m, 0m, 10m,
                    [new ElectronicDocumentDetailTax("VAT", "2", 10m, 15m, 1.5m)]),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 10m, 1.5m)],
            Totals: new ElectronicDocumentTotals(10m, 0m, 1.5m, 11.5m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 11.5m, null, null)],
            AdditionalInfo: []
        );

    private static RegisterElectronicDocumentRequest Request() =>
        new(TenantId, CompanyId, ElectronicDocumentType.Invoice, "Sales", SourceEntityId, UserId);

    public static TheoryData<string, bool, DateOnly?, string?> ConfiguracionesExigiblesIncompletas() =>
        new()
        {
            { "habilitado sin fecha (B)", true, null, AdditionalInfoTestDoubles.ProviderRuc },
            { "exigible y deshabilitado (E)", false, IssueDate, AdditionalInfoTestDoubles.ProviderRuc },
            { "exigible con RUC inválido (F)", true, IssueDate, "123" },
        };

    [Theory]
    [MemberData(nameof(ConfiguracionesExigiblesIncompletas))]
    public async Task Configuracion_incompleta_deja_el_documento_Failed_sin_firma_ni_llamada_al_SRI(
        string escenario,
        bool enabled,
        DateOnly? effectiveDate,
        string? ruc
    )
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(enabled, effectiveDate);
        if (ruc != AdditionalInfoTestDoubles.ProviderRuc)
            AdditionalInfoTestDoubles.ForceRuc((await settings.Object.GetAsync())!, ruc);
        var f = new Fixture(settings);

        var result = await f.BuildIssuer().RegisterAsync(Request());

        result.IsSuccess.Should().BeFalse(escenario);
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SystemProviderRucNotConfigured);
        f.Document!.CurrentState.Should().Be(ElectronicDocumentState.Failed);
        f.Document.LastError.Should().NotBeNullOrWhiteSpace();
        f.Document.AccessKey.Should().BeNull("no se generó ni firmó XML");
        f.Validator.Verify(v => v.ValidateAsync(It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Signing.Verify(s => s.SignAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ElectronicDocumentXml>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Storage.Verify(s => s.StoreAsync(It.IsAny<Guid>(), It.IsAny<ElectronicDocumentType>(), It.IsAny<Guid>(),
            It.IsAny<ElectronicDocumentXml>(), It.IsAny<SignedElectronicDocumentXml>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Reception.Verify(r => r.SendAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Corregir_la_configuracion_y_reintentar_genera_el_XML_con_RUC_Proveedor()
    {
        var settings = AdditionalInfoTestDoubles.ProviderSettings(false, IssueDate);
        var f = new Fixture(settings);
        var issuer = f.BuildIssuer();

        await issuer.RegisterAsync(Request());
        f.Document!.CurrentState.Should().Be(ElectronicDocumentState.Failed);

        var current = (await settings.Object.GetAsync())!;
        current.Configure(AdditionalInfoTestDoubles.ProviderRuc, "ZH Technologies S.A.", "J62021002", IssueDate, enabled: true, UserId);

        var retry = await issuer.RetryAsync(TenantId, f.Document.Id, UserId);

        retry.IsSuccess.Should().BeTrue(retry.Error);
        f.Document.CurrentState.Should().Be(ElectronicDocumentState.Signed, "la recepción del test no responde: queda firmado");
        f.SignedInputXml.Should().Contain(
            $"<campoAdicional nombre=\"{SriAdditionalInfoFieldNames.SystemProviderRuc}\">{AdditionalInfoTestDoubles.ProviderRuc}</campoAdicional>"
        );
    }
}
