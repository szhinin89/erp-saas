using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Services;
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
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A — cubre <see cref="RetentionElectronicDocumentDataProvider"/>.
/// Mismo patrón de fixture que <c>SalesInvoiceElectronicDocumentDataProviderTests</c>: sin mocks de
/// EF Core, solo Moq sobre las interfaces de repositorio, para probar exclusivamente la traducción
/// <c>RetentionDocument</c> → <c>RetentionElectronicDocumentData</c>. No genera XML, no genera
/// RIDE, no toca autorización SRI, no toca posting ni secuencia — el provider ni siquiera declara
/// esas dependencias.
/// </summary>
public sealed class RetentionElectronicDocumentDataProviderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid ExpenseDocumentId = Guid.NewGuid();

    private sealed class Mocks
    {
        public Mock<IRetentionDocumentRepository> RetentionRepo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<IEstablishmentRepository> EstablishmentRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<ISriSettingsRepository> SriSettingsRepo { get; } = new();
        public Mock<IBusinessPartnerRepository> BusinessPartnerRepo { get; } = new();
        public Mock<ISriDocTypeCatalogResolver> DocTypeResolver { get; } = new();

        public Mocks()
        {
            DocTypeResolver
                .Setup(r =>
                    r.IsActiveElectronicDocTypeAsync(
                        SriDocumentTypeCodes.Withholding,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(true);
        }

        public RetentionElectronicDocumentDataProvider BuildProvider() =>
            new(
                RetentionRepo.Object,
                EmissionPointRepo.Object,
                EstablishmentRepo.Object,
                CompanyRepo.Object,
                SriSettingsRepo.Object,
                BusinessPartnerRepo.Object,
                DocTypeResolver.Object
            );

        public void SeedHappyPath(RetentionDocument retention)
        {
            var establishment = Establishment.Create(
                TenantId,
                branchId: BranchId,
                CompanyId,
                code: "001",
                name: "Matriz",
                address: "Av. Principal 123",
                phone: null,
                isMain: true,
                createdBy: UserId
            );
            var emissionPoint = EmissionPoint.Create(
                TenantId,
                CompanyId,
                establishment.Id,
                code: "001",
                name: "PE-001",
                emissionType: EmissionType.Electronic,
                isDefault: true,
                createdBy: UserId
            );
            typeof(EmissionPoint)
                .GetProperty(nameof(EmissionPoint.Establishment))!
                .SetValue(emissionPoint, establishment);

            EmissionPointRepo
                .Setup(r => r.GetByIdAsync(EmissionPointId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emissionPoint);
            EstablishmentRepo
                .Setup(r => r.GetMainByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(establishment);

            var company = Company.CreateManaged(
                TenantId,
                "1790012345001",
                "Empresa Retenedora S.A.",
                createdBy: UserId
            );
            company.SpecialTaxpayerNo = "5368";
            CompanyRepo
                .Setup(r => r.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(company);

            SriSettingsRepo
                .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    SriSettings.Create(
                        TenantId,
                        CompanyId,
                        environment: 1,
                        emissionType: 1,
                        wsdlUrl: "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl",
                        createdBy: UserId
                    )
                );

            var supplier = BusinessPartner.Create(
                TenantId,
                "04",
                "1791352688001",
                2,
                "Proveedor Demo S.A.",
                UserId
            );
            BusinessPartnerRepo
                .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(supplier);

            RetentionRepo
                .Setup(r => r.GetByIdAsync(TenantId, retention.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(retention);
        }
    }

    private static RetentionDocument BuildIssuedRetention(
        decimal vatBase = 100m,
        decimal vatRate = 70m,
        decimal vatRetained = 70m,
        decimal incomeBase = 200m,
        decimal incomeRate = 1m,
        decimal incomeRetained = 2m
    )
    {
        var retention = RetentionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            RetentionSourceDocumentType.ExpenseDocument,
            ExpenseDocumentId,
            SupplierId,
            EmissionPointId,
            UserId,
            new RetentionDocument.SourceDocumentSnapshot(
                SriTypeCode: "01",
                DocumentNumber: "001-001-000000456",
                IssueDate: new DateOnly(2026, 8, 20),
                AuthorizationNumber: "1234567890",
                TaxSupportCode: "02",
                Subtotal: 300m,
                Total: 345m
            )
        );

        retention.AddLine(
            RetentionDocumentLine.Create(
                retention.Id,
                TenantId,
                RetentionTaxType.Vat,
                "725",
                "Retención IVA 70% bienes",
                vatBase,
                vatRate,
                vatRetained
            )
        );
        retention.AddLine(
            RetentionDocumentLine.Create(
                retention.Id,
                TenantId,
                RetentionTaxType.Income,
                "303",
                "Honorarios profesionales",
                incomeBase,
                incomeRate,
                incomeRetained
            )
        );

        retention.Issue("001-001-000000850", new DateOnly(2026, 9, 4), UserId);
        return retention;
    }

    /// <summary>
    /// PURCHASES-RETENTIONS-UI-MIGRATION-05C — mismo builder que <see cref="BuildIssuedRetention"/>
    /// pero con <see cref="RetentionSourceDocumentType.PurchaseInvoice"/>, para probar que el
    /// provider (y por lo tanto los endpoints de XML/RIDE/registro electrónico de
    /// <c>RetentionsController</c>, que solo llaman a <c>GetDataAsync</c> por Id) no distinguen
    /// origen — confirmado por diseño en PURCHASES-WITHHOLDING-RETENTIONS-AUDIT-05A.
    /// </summary>
    private static RetentionDocument BuildIssuedRetentionForPurchase()
    {
        var purchaseInvoiceId = Guid.NewGuid();
        var retention = RetentionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            RetentionSourceDocumentType.PurchaseInvoice,
            purchaseInvoiceId,
            SupplierId,
            EmissionPointId,
            UserId,
            new RetentionDocument.SourceDocumentSnapshot(
                SriTypeCode: "01",
                DocumentNumber: "001-001-000000123",
                IssueDate: new DateOnly(2026, 8, 27),
                AuthorizationNumber: null,
                TaxSupportCode: null,
                Subtotal: 100m,
                Total: 115m
            )
        );
        retention.AddLine(
            RetentionDocumentLine.Create(
                retention.Id, TenantId, RetentionTaxType.Vat, "725", "Retención IVA 30% bienes",
                100m, 30m, 30m
            )
        );
        retention.Issue("001-001-000000005", new DateOnly(2026, 9, 3), UserId);
        return retention;
    }

    // ── PURCHASES-RETENTIONS-UI-MIGRATION-05C: origen PurchaseInvoice ─────────────────────────

    [Fact]
    public async Task GetDataAsync_construye_el_modelo_igual_para_una_retencion_originada_en_PurchaseInvoice()
    {
        var retention = BuildIssuedRetentionForPurchase();
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Metadata.SourceDocumentType.Should().Be(RetentionSourceDocumentType.PurchaseInvoice);
        result.Value.Metadata.SourceDocumentId.Should().Be(retention.SourceDocumentId);
        result.Value.NumeroCompleto.Should().Be("001-001-000000005");
        result.Value.Emission.DocTypeCode.Should().Be("07");
    }

    // ── 1/2/3/4: construcción del modelo, número completo, codDoc, período fiscal ────────────

    [Fact]
    public async Task GetDataAsync_construye_el_modelo_desde_una_retencion_emitida()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.NumeroCompleto.Should().Be("001-001-000000850");
        result.Value.Emission.Sequential.Should().Be("000000850");
        result.Value.Emission.Establishment.Should().Be("001");
        result.Value.Emission.EmissionPoint.Should().Be("001");
        // codDoc = "07" desde la constante centralizada, nunca un literal — verificado contra la
        // misma constante que usa el provider (SriDocumentTypeCodes.Withholding).
        result.Value.Emission.DocTypeCode.Should().Be(SriDocumentTypeCodes.Withholding);
        result.Value.Emission.DocTypeCode.Should().Be("07");
        result.Value.RetentionInfo.FiscalPeriod.Should().Be("09/2026");
        // claveAcceso: preparatorio, siempre null en esta fase — la calcula el futuro XmlBuilder.
        result.Value.AccessKey.Should().BeNull();
    }

    // ── 5: snapshot del documento sustento ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDataAsync_copia_el_snapshot_del_documento_sustento_sin_recalcular()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var sourceDoc = result.Value!.SourceDocument;
        sourceDoc.TaxSupportCode.Should().Be("02");
        sourceDoc.DocTypeCode.Should().Be("01");
        sourceDoc.Number.Should().Be("001-001-000000456");
        sourceDoc.AuthorizationNumber.Should().Be("1234567890");
        sourceDoc.IssueDate.Should().Be(new DateOnly(2026, 8, 20));
        sourceDoc.Subtotal.Should().Be(300m);
        sourceDoc.Total.Should().Be(345m);
    }

    [Fact]
    public async Task GetDataAsync_permite_TaxSupportCode_null_en_el_documento_sustento_sin_bloquear()
    {
        var retention = RetentionDocument.Create(
            TenantId, CompanyId, BranchId, RetentionSourceDocumentType.ExpenseDocument,
            ExpenseDocumentId, SupplierId, EmissionPointId, UserId,
            new RetentionDocument.SourceDocumentSnapshot("01", "001-001-000000789", new DateOnly(2026, 8, 20), null, null, 100m, 115m)
        );
        retention.AddLine(
            RetentionDocumentLine.Create(retention.Id, TenantId, RetentionTaxType.Vat, "725", "Retención IVA 70%", 100m, 70m, 70m)
        );
        retention.Issue("001-001-000000851", new DateOnly(2026, 9, 4), UserId);

        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error, "un codSustento ausente es un gap de datos conocido, nunca un motivo de rechazo");
        result.Value!.SourceDocument.TaxSupportCode.Should().BeNull();
    }

    // ── 6: líneas de impuesto retenido (IVA + Renta) ────────────────────────────────────────

    [Fact]
    public async Task GetDataAsync_construye_las_lineas_IVA_y_Renta_con_codigo_SRI_de_impuesto()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().HaveCount(2);

        var vatLine = result.Value.Lines.Should().ContainSingle(l => l.TaxType == RetentionTaxType.Vat).Subject;
        vatLine.SriTaxTypeCode.Should().Be("2");
        vatLine.RetentionCode.Should().Be("725");
        vatLine.RetentionCodeDescription.Should().Be("Retención IVA 70% bienes");
        vatLine.BaseAmount.Should().Be(100m);
        vatLine.RetentionRate.Should().Be(70m);
        vatLine.RetainedAmount.Should().Be(70m);

        var incomeLine = result.Value.Lines.Should().ContainSingle(l => l.TaxType == RetentionTaxType.Income).Subject;
        incomeLine.SriTaxTypeCode.Should().Be("1");
        incomeLine.RetentionCode.Should().Be("303");
        incomeLine.RetentionCodeDescription.Should().Be("Honorarios profesionales");
        incomeLine.BaseAmount.Should().Be(200m);
        incomeLine.RetentionRate.Should().Be(1m);
        incomeLine.RetainedAmount.Should().Be(2m);
    }

    // ── 7: totales derivados desde las líneas ───────────────────────────────────────────────

    [Fact]
    public async Task GetDataAsync_los_totales_coinciden_con_la_suma_real_de_las_lineas()
    {
        var retention = BuildIssuedRetention(
            vatBase: 100m, vatRate: 70m, vatRetained: 70m,
            incomeBase: 200m, incomeRate: 1m, incomeRetained: 2m
        );
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        var totals = result.Value!.Totals;
        // Los totales del modelo deben ser exactamente los que RetentionDocument ya recalculó
        // desde sus líneas (RecalculateTotals) — nunca una segunda suma independiente.
        totals.TotalRetainedVat.Should().Be(retention.TotalRetainedVat);
        totals.TotalRetainedIncome.Should().Be(retention.TotalRetainedIncome);
        totals.TotalRetained.Should().Be(retention.TotalRetained);
        // Verificación cruzada directa contra las líneas del modelo ya construido.
        totals.TotalRetainedVat.Should().Be(
            result.Value.Lines.Where(l => l.TaxType == RetentionTaxType.Vat).Sum(l => l.RetainedAmount)
        );
        totals.TotalRetainedIncome.Should().Be(
            result.Value.Lines.Where(l => l.TaxType == RetentionTaxType.Income).Sum(l => l.RetainedAmount)
        );
        totals.TotalRetained.Should().Be(totals.TotalRetainedVat + totals.TotalRetainedIncome);
        totals.TotalRetained.Should().Be(72m);
    }

    // ── 8: no depende de cambios posteriores / no toca el documento origen ─────────────────

    [Fact]
    public void El_provider_no_declara_ninguna_dependencia_de_ExpenseDocument_ni_de_escritura()
    {
        // Regresión estructural: el provider construye el snapshot del documento sustento
        // ÚNICAMENTE desde RetentionDocument.SourceDocument* (ya congelado) — nunca vuelve a
        // consultar IExpenseDocumentRepository ni ningún repositorio de escritura. Mismo criterio
        // que el test de "Issuer nunca viene de SystemProviderSettings" en
        // SalesInvoiceElectronicDocumentDataProviderTests: si alguien agrega esa dependencia más
        // adelante, esta prueba lo detecta primero.
        var ctorParams = typeof(RetentionElectronicDocumentDataProvider)
            .GetConstructors()
            .Single()
            .GetParameters();

        ctorParams.Should().HaveCount(7);
        ctorParams.Select(p => p.ParameterType.Name)
            .Should()
            .NotContain(name => name.Contains("ExpenseDocument", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetDataAsync_no_llama_metodos_de_escritura_en_ningun_repositorio()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        // Regresión (punto 10 del ticket): no cambia posting, no cambia emisión, no cambia
        // secuencia — el provider es de solo lectura por construcción, verificado explícitamente.
        m.RetentionRepo.Verify(
            r => r.AddAsync(It.IsAny<RetentionDocument>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.EmissionPointRepo.Verify(
            r => r.AddAsync(It.IsAny<EmissionPoint>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.EmissionPointRepo.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // ── 9: falla de forma clara si falta un dato obligatorio ───────────────────────────────

    [Fact]
    public async Task GetDataAsync_falla_si_la_retencion_no_existe()
    {
        var m = new Mocks();

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetDataAsync_falla_si_la_retencion_todavia_esta_en_Draft()
    {
        var retention = RetentionDocument.Create(
            TenantId, CompanyId, BranchId, RetentionSourceDocumentType.ExpenseDocument,
            ExpenseDocumentId, SupplierId, EmissionPointId, UserId
        );
        retention.AddLine(
            RetentionDocumentLine.Create(retention.Id, TenantId, RetentionTaxType.Vat, "725", "Retención IVA 70%", 100m, 70m, 70m)
        );
        // Deliberadamente NO se llama Issue() — sigue en Draft, sin RetentionNumber/IssueDate.

        var m = new Mocks();
        m.RetentionRepo
            .Setup(r => r.GetByIdAsync(TenantId, retention.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitida");
    }

    [Fact]
    public async Task GetDataAsync_falla_si_el_punto_de_emision_ya_no_existe()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);
        m.EmissionPointRepo
            .Setup(r => r.GetByIdAsync(EmissionPointId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmissionPoint?)null);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("punto de emisión");
    }

    [Fact]
    public async Task GetDataAsync_falla_si_el_sujeto_retenido_ya_no_existe()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);
        m.BusinessPartnerRepo
            .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartner?)null);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sujeto retenido");
    }

    [Fact]
    public async Task GetDataAsync_falla_si_el_catalogo_SRI_no_tiene_activo_el_tipo_07()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);
        m.DocTypeResolver
            .Setup(r =>
                r.IsActiveElectronicDocTypeAsync(SriDocumentTypeCodes.Withholding, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("07");
    }

    // ── Sujeto retenido / emisor — fuente correcta de cada bloque ──────────────────────────

    [Fact]
    public async Task GetDataAsync_SubjectWithheld_proviene_de_BusinessPartner_no_del_documento_de_gasto()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.SubjectWithheld.IdentificationType.Should().Be("04");
        result.Value.SubjectWithheld.IdentificationNumber.Should().Be("1791352688001");
        result.Value.SubjectWithheld.LegalName.Should().Be("Proveedor Demo S.A.");
    }

    [Fact]
    public async Task GetDataAsync_Issuer_e_infoCompRetencion_provienen_de_Company_no_de_datos_inventados()
    {
        var retention = BuildIssuedRetention();
        var m = new Mocks();
        m.SeedHappyPath(retention);

        var result = await m.BuildProvider()
            .GetDataAsync(new ElectronicDocumentSourceReference(TenantId, CompanyId, retention.Id));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Issuer.TaxId.Should().Be("1790012345001");
        result.Value.Issuer.LegalName.Should().Be("Empresa Retenedora S.A.");
        result.Value.Issuer.MatrixAddress.Should().Be("Av. Principal 123");
        result.Value.RetentionInfo.SpecialTaxpayerNumber.Should().Be("5368");
        result.Value.AdditionalInfo.Should().BeEmpty();
    }
}
