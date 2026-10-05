using System.Xml.Linq;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Configurations.SriCatalogs;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Services.ElectronicDocuments;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

/// <summary>Una sola base PostgreSQL migrada (todas las migraciones, igual que una instalación nueva) para la clase.</summary>
public sealed class SriRetentionCatalogDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sri_retention_catalog_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options,
            new NoTenant(),
            new NoOpPublisher(),
            new NoCompany()
        );

    private sealed class NoTenant : ICurrentTenant
    {
        public Guid TenantId => Guid.Empty;
        public string? Slug => null;
    }

    private sealed class NoCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
        public bool IsAuthenticated => false;
        public bool HasCompanyContext => false;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 — resolver real (<see cref="RetentionCodeResolver"/>) sobre la base migrada,
/// sin contexto de tenant/empresa (catálogo global), y XML de retención de punta a punta:
/// provider real → resolver real → <see cref="RetentionXmlBuilder"/> real → XSD oficial 1.0.0. Ningún XML
/// se arma con valores manuales.
/// </summary>
public sealed class SriRetentionCatalogResolutionIntegrationTests
    : IClassFixture<SriRetentionCatalogDatabaseFixture>
{
    private static readonly DateOnly IssueDate = new(2026, 10, 2);
    private static readonly Guid Concept725 = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid ConceptIva0 = Guid.Parse("10000000-0000-0000-0000-000000000008");
    private static readonly Guid Concept728 = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid ConceptIsd = Guid.Parse("30000000-0000-0000-0000-000000000001");

    private readonly SriRetentionCatalogDatabaseFixture _fixture;

    public SriRetentionCatalogResolutionIntegrationTests(
        SriRetentionCatalogDatabaseFixture fixture
    ) => _fixture = fixture;

    // ── Instalación = migraciones: el estado real en BD respeta la habilitación ──────────────────────

    [Fact]
    public async Task La_migracion_deja_no_habilitados_la_retencion_en_cero_y_no_procede()
    {
        await using var db = _fixture.CreateContext();
        var flags = await db
            .SriRetentionCodes.AsNoTracking()
            .Where(c => c.TaxType == "IVA")
            .ToDictionaryAsync(c => c.Code, c => c.IsActive);

        flags["IVA-0"].Should().BeFalse();
        flags["IVA-NP"].Should().BeFalse();
        flags["IVA-50"].Should().BeTrue();
        flags["728"]
            .Should()
            .BeFalse("728 no tiene representación oficial: no se habilita para operaciones nuevas");
        var officialConceptIds = db
            .SriRetentionCodes.Where(c => c.TaxType != "TEST")
            .Select(c => c.Id);
        (
            await db.SriRetentionCodeVersions.CountAsync(v =>
                officialConceptIds.Contains(v.RetentionCodeId)
            )
        )
            .Should()
            .Be(32, "22 del slice IVA + 10 versiones de Renta del Catálogo ATS 06/08/2026");
        (await db.SriNormativeSources.CountAsync()).Should().Be(3);
    }

    // ── 7-9. Separación Selectable / ForDate / Historical ────────────────────────────────────────────

    [Fact]
    public async Task ResolveForDate_ignora_la_habilitacion_operativa()
    {
        await using var db = _fixture.CreateContext();
        var resolution = await new RetentionCodeResolver(db).ResolveForDateAsync(
            ConceptIva0,
            IssueDate,
            0m
        );

        resolution.IsResolved.Should().BeTrue(resolution.Detail);
        resolution.Representation!.XmlCode.Should().Be("7");
    }

    [Fact]
    public async Task GetSelectable_excluye_conceptos_no_habilitados()
    {
        await using var db = _fixture.CreateContext();
        var resolver = new RetentionCodeResolver(db);

        (await resolver.GetSelectableByIdAsync(ConceptIva0)).Should().BeNull();
        (await resolver.GetSelectableByCodeAsync("IVA-0", "IVA")).Should().BeNull();
        (await resolver.GetSelectableByIdAsync(Concept725))!.Code.Should().Be("725");
    }

    [Fact]
    public async Task GetByIdIncludingDisabled_recupera_el_concepto_historico()
    {
        await using var db = _fixture.CreateContext();
        var info = await new RetentionCodeResolver(db).GetByIdIncludingDisabledAsync(ConceptIva0);

        info.Should().NotBeNull();
        info!.Code.Should().Be("IVA-0");
        info.IsActive.Should().BeFalse();
        info.Id.Should().Be(ConceptIva0);
    }

    [Fact]
    public async Task Concepto_728_no_es_seleccionable_pero_sigue_recuperable_como_historico()
    {
        await using var db = _fixture.CreateContext();
        var resolver = new RetentionCodeResolver(db);

        (await resolver.GetSelectableByIdAsync(Concept728)).Should().BeNull();
        (await resolver.GetSelectableByCodeAsync("728", "IVA")).Should().BeNull();

        var historical = await resolver.GetByIdIncludingDisabledAsync(Concept728);
        historical.Should().NotBeNull();
        historical!.Code.Should().Be("728");
        historical.Percentage.Should().Be(15m);
        historical.IsActive.Should().BeFalse();
    }

    // ── ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01: Renta del Catálogo ATS 06/08/2026 ────────────────────

    private static readonly DateOnly LastLegacyDay = new(2026, 8, 5);
    private static readonly DateOnly AtsBlockStart = new(2026, 8, 6);
    private static readonly Guid Concept341 = Guid.Parse("20000000-0000-0000-0000-000000000010");

    [Fact]
    public async Task El_2026_08_05_resuelve_la_version_anterior_y_el_2026_08_06_la_tasa_oficial_ATS()
    {
        await using var db = _fixture.CreateContext();
        var resolver = new RetentionCodeResolver(db);

        var before = await resolver.ResolveForDateAsync(
            RetentionTaxType.Income,
            "304",
            LastLegacyDay,
            2m
        );
        before.IsResolved.Should().BeTrue(before.Detail);
        before
            .Representation!.Percentage.Should()
            .BeNull("la versión heredada no exige tasa (histórico preservado)");
        before
            .Representation.NormativeSourceId.Should()
            .Be(SriNormativeSourceConfiguration.AtsIncomeRetentionCatalogId);

        (await resolver.ResolveForDateAsync(RetentionTaxType.Income, "304", AtsBlockStart, 2m))
            .Error.Should()
            .Be(
                RetentionCodeResolutionError.RateMismatch,
                "desde 06/08/2026 la tasa oficial de 304 es 10 %"
            );

        var after = await resolver.ResolveForDateAsync(
            RetentionTaxType.Income,
            "304",
            AtsBlockStart,
            10m
        );
        after.IsResolved.Should().BeTrue(after.Detail);
        after.Representation!.XmlCode.Should().Be("304");
        after.Representation.Percentage.Should().Be(10m);
        after
            .Representation.NormativeSourceId.Should()
            .Be(SriNormativeSourceConfiguration.AtsIncomeTable310From20260806Id);
        after.Representation.NormativeDocument.Should().Be("CATALOGO_ATS");
    }

    [Theory]
    [InlineData("310", 1)]
    [InlineData("327", 12)]
    public async Task Tasa_condicional_falla_cerrado_desde_2026_08_06(string code, int anyRate)
    {
        await using var db = _fixture.CreateContext();
        var resolver = new RetentionCodeResolver(db);

        (await resolver.ResolveForDateAsync(RetentionTaxType.Income, code, AtsBlockStart, anyRate))
            .Error.Should()
            .Be(RetentionCodeResolutionError.ConditionalRateUndetermined);
        (await resolver.ResolveForDateAsync(RetentionTaxType.Income, code, LastLegacyDay, anyRate))
            .IsResolved.Should()
            .BeTrue("los documentos anteriores conservan su resolución histórica");
        (await resolver.GetSelectableByCodeAsync(code, "RENTA")).Should().BeNull();
    }

    [Fact]
    public async Task Codigo_retirado_341_no_es_seleccionable_no_resuelve_desde_2026_08_06_y_sigue_legible()
    {
        await using var db = _fixture.CreateContext();
        var resolver = new RetentionCodeResolver(db);

        (await resolver.GetSelectableByIdAsync(Concept341)).Should().BeNull();
        (await resolver.ResolveForDateAsync(Concept341, AtsBlockStart, 2m))
            .Error.Should()
            .Be(RetentionCodeResolutionError.NoValidVersion);
        (await resolver.ResolveForDateAsync(Concept341, LastLegacyDay, 2m))
            .IsResolved.Should()
            .BeTrue();

        var historical = await resolver.GetByIdIncludingDisabledAsync(Concept341);
        historical!.Code.Should().Be("341");
        historical.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Las_tasas_operativas_seleccionables_coinciden_con_el_ATS_vigente()
    {
        await using var db = _fixture.CreateContext();
        var resolver = new RetentionCodeResolver(db);

        (await resolver.GetSelectableByCodeAsync("304", "RENTA"))!.Percentage.Should().Be(10m);
        (await resolver.GetSelectableByCodeAsync("307", "RENTA"))!.Percentage.Should().Be(3m);
        (await resolver.GetSelectableByCodeAsync("312", "RENTA"))!.Percentage.Should().Be(2m);
        (await resolver.GetSelectableByCodeAsync("343", "RENTA"))!.Percentage.Should().Be(1m);
    }

    // ── 10-12 + 728. Fail-closed tipado ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sin_version_vigente_falla_cerrado()
    {
        await using var db = _fixture.CreateContext();
        var resolution = await new RetentionCodeResolver(db).ResolveForDateAsync(
            ConceptIsd,
            IssueDate,
            5m
        );

        resolution.IsResolved.Should().BeFalse();
        resolution.Error.Should().Be(RetentionCodeResolutionError.NoValidVersion);
    }

    [Fact]
    public async Task Version_cerrada_no_resuelve_despues_de_su_ValidUntil()
    {
        var conceptId = await SeedTestConceptAsync(
            "TST-CLOSED",
            (new DateOnly(2020, 1, 1), new DateOnly(2025, 12, 31), "99")
        );
        await using var db = _fixture.CreateContext();
        var resolver = new RetentionCodeResolver(db);

        (await resolver.ResolveForDateAsync(conceptId, new DateOnly(2025, 6, 1), 1m))
            .Representation!.XmlCode.Should()
            .Be("99");
        (await resolver.ResolveForDateAsync(conceptId, IssueDate, 1m))
            .Error.Should()
            .Be(RetentionCodeResolutionError.NoValidVersion);
    }

    [Fact]
    public async Task Mas_de_una_version_vigente_falla_cerrado()
    {
        var conceptId = await SeedTestConceptAsync(
            "TST-AMBIG",
            (new DateOnly(2020, 1, 1), null, "98"),
            (new DateOnly(2024, 1, 1), null, "97")
        );
        await using var db = _fixture.CreateContext();
        var resolution = await new RetentionCodeResolver(db).ResolveForDateAsync(
            conceptId,
            IssueDate,
            1m
        );

        resolution.IsResolved.Should().BeFalse();
        resolution.Error.Should().Be(RetentionCodeResolutionError.AmbiguousVersions);
    }

    [Fact]
    public async Task Tasa_distinta_a_la_oficial_falla_cerrado()
    {
        await using var db = _fixture.CreateContext();
        var resolution = await new RetentionCodeResolver(db).ResolveForDateAsync(
            RetentionTaxType.Vat,
            "725",
            IssueDate,
            70m
        );

        resolution.IsResolved.Should().BeFalse();
        resolution.Error.Should().Be(RetentionCodeResolutionError.RateMismatch);
    }

    [Fact]
    public async Task Concepto_728_no_es_emitible()
    {
        await using var db = _fixture.CreateContext();
        var resolution = await new RetentionCodeResolver(db).ResolveForDateAsync(
            RetentionTaxType.Vat,
            "728",
            IssueDate,
            15m
        );

        resolution.IsResolved.Should().BeFalse();
        resolution.Error.Should().Be(RetentionCodeResolutionError.MissingXmlCode);
    }

    [Fact]
    public async Task Codigo_inexistente_falla_cerrado()
    {
        await using var db = _fixture.CreateContext();
        var resolution = await new RetentionCodeResolver(db).ResolveForDateAsync(
            RetentionTaxType.Vat,
            "999",
            IssueDate,
            30m
        );

        resolution.Error.Should().Be(RetentionCodeResolutionError.ConceptNotFound);
    }

    [Fact]
    public async Task La_representacion_resuelta_es_trazable_a_la_Ficha_234_Tabla_20()
    {
        await using var db = _fixture.CreateContext();
        var representation = (
            await new RetentionCodeResolver(db).ResolveForDateAsync(
                RetentionTaxType.Vat,
                "725",
                IssueDate,
                30m
            )
        ).Representation!;

        representation.RetentionCodeId.Should().Be(Concept725);
        representation
            .NormativeSourceId.Should()
            .Be(SriNormativeSourceConfiguration.FichaV234Table20Id);
        representation.NormativeDocument.Should().Be("FICHA_TECNICA_OFFLINE");
        representation.NormativeVersion.Should().Be("2.34");
        representation.NormativeSection.Should().Contain("Tabla 20");
    }

    [Theory]
    [InlineData("IVA-0", 0, "7")]
    [InlineData("IVA-NP", 0, "8")]
    public async Task Retencion_en_cero_y_no_procede_resuelven_su_codigo_oficial(
        string code,
        int rate,
        string xmlCode
    )
    {
        // Solo a nivel de resolver: RetentionDocumentLine exige tasa > 0, así que estas representaciones
        // aún no pueden llegar a un XML (ver pendientes del ticket).
        await using var db = _fixture.CreateContext();
        var resolution = await new RetentionCodeResolver(db).ResolveForDateAsync(
            RetentionTaxType.Vat,
            code,
            IssueDate,
            rate
        );

        resolution.Representation!.XmlCode.Should().Be(xmlCode);
    }

    // ── 14. XML de punta a punta: provider + resolver + builder + XSD reales ─────────────────────────

    [Theory]
    [InlineData("721", 10, "9")]
    [InlineData("723", 20, "10")]
    [InlineData("725", 30, "1")]
    [InlineData("IVA-50", 50, "11")]
    [InlineData("726", 70, "2")]
    [InlineData("727", 100, "3")]
    public async Task El_XML_de_retencion_emite_el_codigoRetencion_oficial_de_la_Tabla_20(
        string businessCode,
        int vatRate,
        string expectedXmlCode
    )
    {
        var (xml, schemaErrors) = await GenerateRetentionXmlAsync(businessCode, vatRate);

        schemaErrors.Should().BeEmpty("el XML debe validar contra ComprobanteRetencion_V1.0.0.xsd");
        var impuestos = XDocument.Parse(xml).Descendants("impuesto").ToList();
        impuestos
            .Single(i => i.Element("codigo")!.Value == SriRetentionTaxTypeCodes.Vat)
            .Element("codigoRetencion")!
            .Value.Should()
            .Be(expectedXmlCode);
        impuestos
            .Single(i => i.Element("codigo")!.Value == SriRetentionTaxTypeCodes.Income)
            .Element("codigoRetencion")!
            .Value.Should()
            .Be("303", "Renta conserva el comportamiento previo");
        xml.Should().NotContain($"<codigoRetencion>{businessCode}</codigoRetencion>");
    }

    [Fact]
    public async Task Una_retencion_con_728_no_genera_XML_y_devuelve_error_fiscal_estructurado()
    {
        var provider = BuildProvider(out var reference, vatCode: "728", vatRate: 15m);

        var result = await provider.GetDataAsync(reference);

        result.IsSuccess.Should().BeFalse();
        result
            .Code.Should()
            .Be(ApiResponseCodes.ElectronicDocuments.FiscalCatalogConfigurationError);
        result.Error.Should().Contain("728");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────────

    private async Task<(string Xml, IReadOnlyList<string> SchemaErrors)> GenerateRetentionXmlAsync(
        string vatCode,
        decimal vatRate
    )
    {
        var provider = BuildProvider(out var reference, vatCode, vatRate);
        var data = await provider.GetDataAsync(reference);
        data.IsSuccess.Should().BeTrue(data.Error);

        var built = new RetentionXmlBuilder().Build(data.Value!);
        built.IsSuccess.Should().BeTrue(built.Error);

        var validator = new RetentionXmlSchemaValidator(
            new EmbeddedXmlSchemaProvider(NullLogger<EmbeddedXmlSchemaProvider>.Instance)
        );
        var validation = await validator.ValidateAsync(built.Value!);
        return (built.Value!.Xml, validation.Errors);
    }

    private RetentionElectronicDocumentDataProvider BuildProvider(
        out ElectronicDocumentSourceReference reference,
        string vatCode,
        decimal vatRate
    )
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();

        var retention = RetentionDocument.Create(
            tenantId,
            companyId,
            Guid.NewGuid(),
            RetentionSourceDocumentType.PurchaseInvoice,
            Guid.NewGuid(),
            supplierId,
            emissionPointId,
            userId,
            new RetentionDocument.SourceDocumentSnapshot(
                SriTypeCode: "01",
                DocumentNumber: "001-001-000000123",
                IssueDate: new DateOnly(2026, 9, 28),
                AuthorizationNumber: null,
                TaxSupportCode: "01",
                Subtotal: 1000m,
                Total: 1150m
            )
        );
        retention.AddLine(
            RetentionDocumentLine.Create(
                retention.Id,
                tenantId,
                RetentionTaxType.Vat,
                vatCode,
                $"Retención IVA {vatRate}%",
                150m,
                vatRate,
                decimal.Round(150m * vatRate / 100m, 2)
            )
        );
        retention.AddLine(
            RetentionDocumentLine.Create(
                retention.Id,
                tenantId,
                RetentionTaxType.Income,
                "303",
                "Honorarios profesionales",
                1000m,
                10m,
                100m
            )
        );
        retention.Issue("001-001-000000001", IssueDate, userId);

        var establishment = Establishment.Create(
            tenantId,
            branchId: Guid.NewGuid(),
            companyId,
            code: "001",
            name: "Matriz",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: userId
        );
        var emissionPoint = EmissionPoint.Create(
            tenantId,
            companyId,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: userId
        );
        typeof(EmissionPoint)
            .GetProperty(nameof(EmissionPoint.Establishment))!
            .SetValue(emissionPoint, establishment);
        var company = Company.CreateManaged(
            tenantId,
            "1790012345001",
            "Empresa Retenedora S.A.",
            createdBy: userId
        );
        var supplier = BusinessPartner.Create(
            tenantId,
            "04",
            "1791352688001",
            2,
            "Proveedor Demo S.A.",
            userId
        );

        var retentionRepo = new Mock<IRetentionDocumentRepository>();
        retentionRepo
            .Setup(r => r.GetByIdAsync(tenantId, retention.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(retention);
        var emissionPointRepo = new Mock<IEmissionPointRepository>();
        emissionPointRepo
            .Setup(r => r.GetByIdAsync(emissionPointId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emissionPoint);
        var establishmentRepo = new Mock<IEstablishmentRepository>();
        establishmentRepo
            .Setup(r => r.GetMainByCompanyAsync(tenantId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(establishment);
        var companyRepo = new Mock<ICompanyRepository>();
        companyRepo
            .Setup(r => r.GetByIdForTenantAsync(companyId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        var sriSettingsRepo = new Mock<ISriSettingsRepository>();
        sriSettingsRepo
            .Setup(r => r.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                SriSettings.Create(
                    tenantId,
                    companyId,
                    environment: 1,
                    emissionType: 1,
                    wsdlUrl: "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl",
                    createdBy: userId
                )
            );
        var partnerRepo = new Mock<IBusinessPartnerRepository>();
        partnerRepo
            .Setup(r => r.GetByIdAsync(supplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);
        var docTypes = new Mock<ISriDocTypeCatalogResolver>();
        docTypes
            .Setup(r =>
                r.IsActiveElectronicDocTypeAsync(
                    SriDocumentTypeCodes.Withholding,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        reference = new ElectronicDocumentSourceReference(tenantId, companyId, retention.Id);
        return new RetentionElectronicDocumentDataProvider(
            retentionRepo.Object,
            emissionPointRepo.Object,
            establishmentRepo.Object,
            companyRepo.Object,
            sriSettingsRepo.Object,
            partnerRepo.Object,
            docTypes.Object,
            new RetentionCodeResolver(_fixture.CreateContext())
        );
    }

    /// <summary>Concepto de prueba aislado (código único por test) con las versiones dadas, en la base del fixture.</summary>
    private async Task<Guid> SeedTestConceptAsync(
        string code,
        params (DateOnly? From, DateOnly? Until, string Xml)[] versions
    )
    {
        await using var db = _fixture.CreateContext();
        var concept = new SriRetentionCode
        {
            Id = Guid.NewGuid(),
            TaxType = "TEST",
            Code = code,
            Name = "Concepto de prueba",
            Percentage = 1m,
        };
        db.SriRetentionCodes.Add(concept);
        foreach (var (from, until, xml) in versions)
            db.SriRetentionCodeVersions.Add(
                new SriRetentionCodeVersion
                {
                    Id = Guid.NewGuid(),
                    RetentionCodeId = concept.Id,
                    ValidFrom = from,
                    ValidUntil = until,
                    Percentage = 1m,
                    XmlCode = xml,
                    NormativeSourceId = SriNormativeSourceConfiguration.FichaV234Table20Id,
                }
            );
        await db.SaveChangesAsync();
        return concept.Id;
    }
}
