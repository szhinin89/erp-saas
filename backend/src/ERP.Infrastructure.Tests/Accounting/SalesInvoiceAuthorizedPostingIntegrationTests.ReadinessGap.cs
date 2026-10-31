using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Company.PhysicalEmission;
using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// READINESS-PREVENTIVE-1A/1B — autorización de una factura con configuración fiscal incompleta
/// (PostgreSQL real). Handler, estrategias de emisión, gate fiscal preventivo
/// (ElectronicEmissionRequirementsValidator), ElectronicDocumentIssuer,
/// SalesInvoiceElectronicDocumentDataProvider e InvoiceXmlBuilder son los reales; solo la frontera
/// SRI (XSD, firma, almacenamiento, recepción, autorización) y la inspección del .p12 son dobles.
/// 1A fijó que la factura se comiteaba y fallaba después en el XML; 1B invierte esa expectativa.
/// </summary>
public sealed partial class SalesInvoiceAuthorizedPostingIntegrationTests
{
    private static readonly DateOnly ReadinessGapDate = new(2026, 7, 25);

    [Fact]
    public async Task Factura_electronica_con_direccion_vacia_se_rechaza_sin_secuencial_ni_efectos()
    {
        // SriSettings completa (certificado válido): la dirección es el único requisito faltante.
        var seed = await SeedReadinessGapAsync(EmissionType.Electronic, withSriSettings: true);
        var boundary = new ReadinessGapSriBoundary();

        await using var gateDb = CreateContext();
        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary,
            EmissionRequirementsTestWiring.Real(gateDb));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing);
        // Matriz = establecimiento del punto de emisión: un solo requisito por dato.
        result.Error.Should().Contain("[electronicEmission.matrixAddress]")
            .And.NotContain("[electronicEmission.establishmentAddress]")
            .And.NotContain("[electronicEmission.sriSettings]")
            .And.NotContain("[electronicEmission.certificate");

        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == seed.InvoiceId))
            .Status.Should().Be(SalesInvoiceStatus.Draft);
        (await verify.DocumentSequences.AsNoTracking()
                .AnyAsync(s => s.EmissionPointId == seed.EmissionPointId && s.DocTypeCode == "01" && s.HasBeenUsed))
            .Should().BeFalse("el gate corre antes de CaptureNextAsync");
        (await verify.StockMovements.AsNoTracking()
                .AnyAsync(m => m.SourceDocId == seed.InvoiceId && m.MovementType == StockMovementType.SaleExit))
            .Should().BeFalse();
        (await verify.CashMovements.AsNoTracking().AnyAsync(m => m.ReferenceId == seed.InvoiceId))
            .Should().BeFalse();
        (await verify.SalesReceivables.AsNoTracking().AnyAsync(r => r.InvoiceId == seed.InvoiceId))
            .Should().BeFalse();
        (await verify.JournalEntries.AsNoTracking().AnyAsync(e => e.SourceEventId == seed.InvoiceId))
            .Should().BeFalse();
        (await verify.ElectronicDocuments.AsNoTracking().AnyAsync(d => d.SourceEntityId == seed.InvoiceId))
            .Should().BeFalse();
        boundary.SupplierResolveCount.Should().Be(0);
        boundary.AssertSriBoundaryNeverReached();
    }

    [Fact]
    public async Task Factura_fisica_sin_configuracion_SRI_se_autoriza_sin_documento_electronico()
    {
        var seed = await SeedReadinessGapAsync(EmissionType.Physical, withSriSettings: false);
        var boundary = new ReadinessGapSriBoundary();
        // La estrategia física nunca consulta el gate electrónico ni toca ElectronicDocuments. La
        // política física se da por cumplida: hoy ningún régimen la habilita (ver la prueba siguiente).
        var gate = new Mock<IElectronicEmissionRequirementsValidator>(MockBehavior.Strict);

        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary, gate.Object,
            EmissionRequirementsTestWiring.PhysicalSatisfied());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Authorized");
        result.Value.InvoiceNumber.Should().Be("001-001-000000001");
        result.Value.ElectronicIssueError.Should().BeNull();

        await using var verify = CreateContext();
        (await verify.SriSettings.AnyAsync(s => s.CompanyId == _companyId)).Should().BeFalse();
        await AssertCommittedCommercialEffectsAsync(verify, seed);
        (await verify.ElectronicDocuments.AnyAsync(d => d.SourceEntityId == seed.InvoiceId))
            .Should().BeFalse();
        boundary.SupplierResolveCount.Should().Be(0);
        boundary.AssertSriBoundaryNeverReached();
    }

    [Fact]
    public async Task Regimen_general_con_autorizacion_vigente_no_habilita_la_factura_fisica_ordinaria()
    {
        var seed = await SeedReadinessGapAsync(EmissionType.Physical, withSriSettings: false);
        await SeedPhysicalPolicyAsync(seed.EmissionPointId, authorizationExpiresOn: ReadinessGapDate.AddMonths(6));
        var boundary = new ReadinessGapSriBoundary();

        await using var physicalDb = CreateContext();
        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary,
            new Mock<IElectronicEmissionRequirementsValidator>(MockBehavior.Strict).Object,
            EmissionRequirementsTestWiring.RealPhysical(physicalDb, ReadinessGapDate));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.PhysicalEmission.RequirementsMissing);
        result.Error.Should().Contain("[physicalEmission.docTypeNotPermitted]")
            .And.NotContain("[physicalEmission.authorization");
        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == seed.InvoiceId))
            .Status.Should().Be(SalesInvoiceStatus.Draft);
        (await verify.DocumentSequences.AsNoTracking()
                .AnyAsync(s => s.EmissionPointId == seed.EmissionPointId && s.DocTypeCode == "01"))
            .Should().BeFalse("el rechazo ocurre antes de CaptureNextAsync");
        boundary.AssertSriBoundaryNeverReached();
    }

    [Fact]
    public async Task Rango_agotado_entre_la_validacion_y_la_captura_se_rechaza_bajo_el_lock_sin_efectos()
    {
        // Otro consumidor tomó el último número autorizado (1) después de que esta factura pasara la
        // validación previa: la captura acotada lo detecta bajo el lock de la secuencia.
        var seed = await SeedReadinessGapAsync(EmissionType.Physical, withSriSettings: false);
        await using (var seqDb = CreateContext())
            await new DocumentSequenceRepository(seqDb).CaptureNextAsync(_tenantId, _companyId, seed.EmissionPointId, "01");
        var policy = new Mock<IPhysicalEmissionRequirementsValidator>();
        policy.Setup(p => p.ValidateAsync(It.IsAny<PhysicalEmissionRequirementsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElectronicEmissionRequirementsResult([]) { Modality = EmissionType.Physical });
        policy.Setup(p => p.GetAuthorizedSequentialRangesAsync(It.IsAny<PhysicalEmissionRequirementsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ERP.Domain.Modules.Company.Interfaces.AuthorizedSequentialRange(1, 1)]);
        var boundary = new ReadinessGapSriBoundary();

        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary,
            new Mock<IElectronicEmissionRequirementsValidator>(MockBehavior.Strict).Object, policy.Object);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.PhysicalEmission.RequirementsMissing);
        result.Error.Should().Contain("[physicalEmission.sequentialOutOfRange]");
        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == seed.InvoiceId))
            .Status.Should().Be(SalesInvoiceStatus.Draft);
        (await verify.DocumentSequences.AsNoTracking()
                .SingleAsync(s => s.EmissionPointId == seed.EmissionPointId && s.DocTypeCode == "01"))
            .CurrentSeq.Should().Be(2, "el número fuera de rango no se consume");
        (await verify.StockMovements.AsNoTracking()
                .AnyAsync(m => m.SourceDocId == seed.InvoiceId && m.MovementType == StockMovementType.SaleExit))
            .Should().BeFalse();
        (await verify.JournalEntries.AsNoTracking().AnyAsync(e => e.SourceEventId == seed.InvoiceId))
            .Should().BeFalse();
        boundary.AssertSriBoundaryNeverReached();
    }

    [Theory]
    [InlineData(false, "[physicalEmission.authorization]")]
    [InlineData(true, "[physicalEmission.authorizationExpired]")]
    public async Task Factura_fisica_sin_autorizacion_vigente_se_rechaza_sin_secuencial_ni_efectos(
        bool withExpiredAuthorization, string expectedRequirement)
    {
        var seed = await SeedReadinessGapAsync(EmissionType.Physical, withSriSettings: false);
        await SeedPhysicalPolicyAsync(seed.EmissionPointId,
            authorizationExpiresOn: withExpiredAuthorization ? ReadinessGapDate.AddDays(-1) : null);
        var boundary = new ReadinessGapSriBoundary();

        await using var physicalDb = CreateContext();
        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary,
            new Mock<IElectronicEmissionRequirementsValidator>(MockBehavior.Strict).Object,
            EmissionRequirementsTestWiring.RealPhysical(physicalDb, ReadinessGapDate));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.PhysicalEmission.RequirementsMissing);
        result.Error.Should().Contain(expectedRequirement);

        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == seed.InvoiceId))
            .Status.Should().Be(SalesInvoiceStatus.Draft);
        (await verify.DocumentSequences.AsNoTracking()
                .AnyAsync(s => s.EmissionPointId == seed.EmissionPointId && s.DocTypeCode == "01" && s.HasBeenUsed))
            .Should().BeFalse("la política física corre antes de CaptureNextAsync");
        (await verify.StockMovements.AsNoTracking()
                .AnyAsync(m => m.SourceDocId == seed.InvoiceId && m.MovementType == StockMovementType.SaleExit))
            .Should().BeFalse();
        (await verify.CashMovements.AsNoTracking().AnyAsync(m => m.ReferenceId == seed.InvoiceId))
            .Should().BeFalse();
        (await verify.SalesReceivables.AsNoTracking().AnyAsync(r => r.InvoiceId == seed.InvoiceId))
            .Should().BeFalse();
        (await verify.JournalEntries.AsNoTracking().AnyAsync(e => e.SourceEventId == seed.InvoiceId))
            .Should().BeFalse();
        boundary.AssertSriBoundaryNeverReached();
    }

    [Fact]
    public async Task Politica_fisica_real_aplica_el_catalogo_regimen_tipo_el_rango_y_el_scope_de_empresa()
    {
        var seed = await SeedReadinessGapAsync(EmissionType.Physical, withSriSettings: false);
        await SeedPhysicalPolicyAsync(seed.EmissionPointId, authorizationExpiresOn: ReadinessGapDate.AddMonths(6),
            lastSequential: 2);
        // Contexto nuevo por evaluación: cada consulta ve el estado comiteado, nunca una entidad cacheada.
        async Task<IEnumerable<string>> CodesFor(Guid tenantId, Guid companyId, Guid pointId, string docType = "01")
        {
            await using var fresh = CreateContext();
            var result = await EmissionRequirementsTestWiring.RealPhysical(fresh, ReadinessGapDate)
                .ValidateAsync(new(tenantId, companyId, pointId, docType));
            return result.Issues.Select(i => i.Code).ToList();
        }

        // Régimen General + autorización 1..2 vigente: la factura física ordinaria sigue bloqueada
        // (fila General → 01 inactiva en el catálogo GLOBAL); la autorización en sí es válida.
        (await CodesFor(_tenantId, _companyId, seed.EmissionPointId))
            .Should().Equal(PhysicalEmissionRequirementCodes.DocTypeNotPermitted);

        // Catálogo GLOBAL sembrado: RIMPE Negocio Popular y Emprendedor no tienen factura física;
        // la nota de venta 02 de Negocio Popular está permitida pero no implementada (inactiva).
        await SetTaxRegimeAsync("03");
        (await CodesFor(_tenantId, _companyId, seed.EmissionPointId))
            .Should().Equal(PhysicalEmissionRequirementCodes.DocTypeNotPermitted);
        (await CodesFor(_tenantId, _companyId, seed.EmissionPointId, "02"))
            .Should().Contain(PhysicalEmissionRequirementCodes.DocType)
            .And.NotContain(PhysicalEmissionRequirementCodes.DocTypeNotPermitted);
        await SetTaxRegimeAsync("02");
        (await CodesFor(_tenantId, _companyId, seed.EmissionPointId))
            .Should().Equal(PhysicalEmissionRequirementCodes.DocTypeNotPermitted);
        await SetTaxRegimeAsync("01");

        // Rango agotado tras capturas reales de CaptureNextAsync (1 y 2): el siguiente es 3.
        await using (var seqDb = CreateContext())
        {
            var sequences = new DocumentSequenceRepository(seqDb);
            await sequences.CaptureNextAsync(_tenantId, _companyId, seed.EmissionPointId, "01");
            await sequences.CaptureNextAsync(_tenantId, _companyId, seed.EmissionPointId, "01");
        }
        (await CodesFor(_tenantId, _companyId, seed.EmissionPointId))
            .Should().Equal(PhysicalEmissionRequirementCodes.DocTypeNotPermitted,
                PhysicalEmissionRequirementCodes.SequentialOutOfRange);

        // Otra empresa del mismo tenant con su propio punto: la autorización de A no la habilita y
        // el punto de A no existe en su scope.
        var companyB = Company.CreateManaged(_tenantId, "1790012345004", "Empresa B S.A.", createdBy: _createdBy);
        companyB.TaxRegimeCode = "01";
        await using (var dbA = CreateContext())
        {
            dbA.Companies.Add(companyB);
            await dbA.SaveChangesAsync();
        }
        Guid pointB;
        await using (var dbB = CreateCompanyContext(companyB.Id))
        {
            var mainB = Establishment.Create(_tenantId, null, companyB.Id, "001", "Matriz B",
                "Av. B 100", null, true, _createdBy);
            dbB.Establishments.Add(mainB);
            await dbB.SaveChangesAsync();
            var epB = EmissionPoint.Create(_tenantId, companyB.Id, mainB.Id, "001", "PE-B",
                EmissionType.Physical, true, _createdBy);
            dbB.EmissionPoints.Add(epB);
            await dbB.SaveChangesAsync();
            pointB = epB.Id;
        }
        await using (var dbB = CreateCompanyContext(companyB.Id))
        {
            var policyB = EmissionRequirementsTestWiring.RealPhysical(dbB, ReadinessGapDate);
            (await policyB.ValidateAsync(new(_tenantId, companyB.Id, pointB, "01")))
                .Issues.Select(i => i.Code).Should().Equal(PhysicalEmissionRequirementCodes.DocTypeNotPermitted,
                    PhysicalEmissionRequirementCodes.Authorization);
            (await policyB.ValidateAsync(new(_tenantId, companyB.Id, seed.EmissionPointId, "01")))
                .Issues.Select(i => i.Code).Should().Equal(PhysicalEmissionRequirementCodes.EmissionPoint);
        }
    }

    /// <summary>
    /// Régimen General y, si se indica caducidad, una autorización de impresión de la factura 01
    /// del punto (desde un año antes de su caducidad, rango 1..<paramref name="lastSequential"/>).
    /// </summary>
    private async Task SeedPhysicalPolicyAsync(Guid emissionPointId, DateOnly? authorizationExpiresOn,
        int lastSequential = 1000)
    {
        await SetTaxRegimeAsync("01");
        if (authorizationExpiresOn is not { } expiresOn)
            return;
        await using var db = CreateContext();
        db.PhysicalDocumentAuthorizations.Add(PhysicalDocumentAuthorization.Create(_tenantId, _companyId,
            emissionPointId, "01", "1122334455", expiresOn.AddYears(-1), expiresOn, 1, lastSequential,
            _createdBy));
        await db.SaveChangesAsync();
    }

    private async Task SetTaxRegimeAsync(string regimeCode)
    {
        await using var db = CreateContext();
        (await db.Companies.SingleAsync(c => c.Id == _companyId)).TaxRegimeCode = regimeCode;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Factura_electronica_con_configuracion_completa_se_autoriza_y_registra_el_documento()
    {
        var seed = await SeedReadinessGapAsync(EmissionType.Electronic, withSriSettings: true,
            address: "Av. Amazonas N24-03");
        var boundary = new ReadinessGapSriBoundary();

        await using var gateDb = CreateContext();
        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary,
            EmissionRequirementsTestWiring.Real(gateDb));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be("Authorized");
        result.Value.InvoiceNumber.Should().Be("001-001-000000001");

        await using var verify = CreateContext();
        await AssertCommittedCommercialEffectsAsync(verify, seed);
        // El pipeline post-commit construyó el XML real (dirección presente) y se detuvo en la
        // validación XSD: la frontera SRI es un doble sin validador registrado.
        var edoc = await verify.ElectronicDocuments.AsNoTracking()
            .SingleAsync(d => d.SourceEntityId == seed.InvoiceId);
        boundary.SupplierResolveCount.Should().Be(1);
        boundary.InvoiceStatusSeenAtXmlStage.Should().Be(SalesInvoiceStatus.Authorized);
        edoc.LastError.Should().NotContain("dirección");
        boundary.Signing.Invocations.Should().BeEmpty();
        boundary.Reception.Invocations.Should().BeEmpty();
        boundary.Authorization.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Factura_electronica_RIMPE_Negocio_Popular_se_rechaza_por_R20_sin_secuencial_ni_efectos()
    {
        // Configuración fiscal completa: el régimen (catálogo sri_tax_regime "03") es lo único que bloquea.
        var seed = await SeedReadinessGapAsync(EmissionType.Electronic, withSriSettings: true,
            address: "Av. Amazonas N24-03");
        await SetTaxRegimeAsync("03");
        var boundary = new ReadinessGapSriBoundary();

        await using var gateDb = CreateContext();
        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary,
            EmissionRequirementsTestWiring.Real(gateDb));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing);
        result.Error.Should().Contain($"[{ElectronicEmissionRequirementCodes.RimpeNegocioPopularLegend}]")
            .And.Contain("Anexo 22");

        await using var verify = CreateContext();
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == seed.InvoiceId))
            .Status.Should().Be(SalesInvoiceStatus.Draft);
        (await verify.DocumentSequences.AsNoTracking()
                .AnyAsync(s => s.EmissionPointId == seed.EmissionPointId && s.DocTypeCode == "01" && s.HasBeenUsed))
            .Should().BeFalse("el gate corre antes de CaptureNextAsync");
        (await verify.StockMovements.AsNoTracking().AnyAsync(m => m.SourceDocId == seed.InvoiceId))
            .Should().BeFalse();
        (await verify.SalesReceivables.AsNoTracking().AnyAsync(r => r.InvoiceId == seed.InvoiceId))
            .Should().BeFalse();
        (await verify.ElectronicDocuments.AsNoTracking().AnyAsync(d => d.SourceEntityId == seed.InvoiceId))
            .Should().BeFalse();
        boundary.AssertSriBoundaryNeverReached();
    }

    [Theory]
    [InlineData("02")] // RIMPE Emprendedor
    [InlineData("01")] // Régimen General
    public async Task Factura_electronica_RIMPE_Emprendedor_o_regimen_general_no_se_bloquea_por_R20(string regimeCode)
    {
        var seed = await SeedReadinessGapAsync(EmissionType.Electronic, withSriSettings: true,
            address: "Av. Amazonas N24-03");
        await SetTaxRegimeAsync(regimeCode);
        var boundary = new ReadinessGapSriBoundary();

        await using var gateDb = CreateContext();
        var result = await AuthorizeWithRealEmissionAsync(seed.InvoiceId, boundary,
            EmissionRequirementsTestWiring.Real(gateDb));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.InvoiceNumber.Should().Be("001-001-000000001");
    }

    [Fact]
    public async Task Gate_fiscal_evalua_solo_la_configuracion_de_la_empresa_y_tenant_del_contexto()
    {
        // Empresa A (fixture): dirección vacía y sin SriSettings. Empresa B (mismo tenant):
        // configuración fiscal completa. Nada de B puede satisfacer los requisitos de A.
        var seedA = await SeedReadinessGapAsync(EmissionType.Electronic, withSriSettings: false);
        var companyB = Company.CreateManaged(_tenantId, "1790012345003", "Empresa B S.A.", createdBy: _createdBy);
        await using (var db = CreateContext())
        {
            db.Companies.Add(companyB);
            await db.SaveChangesAsync();
        }
        Guid emissionPointB;
        await using (var dbB = CreateCompanyContext(companyB.Id))
        {
            var mainB = Establishment.Create(_tenantId, null, companyB.Id, "001", "Matriz B",
                "Av. B 100", null, true, _createdBy);
            dbB.Establishments.Add(mainB);
            await dbB.SaveChangesAsync();
            var epB = EmissionPoint.Create(_tenantId, companyB.Id, mainB.Id, "001", "PE-B",
                EmissionType.Electronic, true, _createdBy);
            dbB.EmissionPoints.Add(epB);
            dbB.SriSettings.Add(EmissionRequirementsTestWiring.CompleteSriSettings(_tenantId, companyB.Id, _createdBy));
            await dbB.SaveChangesAsync();
            emissionPointB = epB.Id;
        }

        await using var dbA = CreateContext();
        var gate = EmissionRequirementsTestWiring.Real(dbA);
        var own = await gate.ValidateAsync(new(_tenantId, _companyId, seedA.EmissionPointId, "01"));
        var foreignPoint = await gate.ValidateAsync(new(_tenantId, _companyId, emissionPointB, "01"));
        var foreignTenant = await gate.ValidateAsync(new(Guid.NewGuid(), _companyId, seedA.EmissionPointId, "01"));

        own.CanIssue.Should().BeFalse();
        own.Issues.Select(i => i.Code).Should().Contain(
            [ElectronicEmissionRequirementCodes.MatrixAddress, ElectronicEmissionRequirementCodes.SriSettings]);
        foreignPoint.Issues.Select(i => i.Code).Should().Contain(ElectronicEmissionRequirementCodes.EmissionPoint);
        foreignTenant.Issues.Should().ContainSingle()
            .Which.Code.Should().Be(ElectronicEmissionRequirementCodes.Company);
    }

    private ErpDbContext CreateCompanyContext(Guid companyId) =>
        new(new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(companyId));

    private sealed record ReadinessGapSeed(Guid InvoiceId, Guid EmissionPointId, Guid Item, Guid Warehouse);

    private async Task<ReadinessGapSeed> SeedReadinessGapAsync(
        EmissionType emissionType, bool withSriSettings, string address = "")
    {
        var item = await SeedInventoryProductAsync();
        var warehouse = await SeedA7WarehouseAsync("RG-1A");
        var term = PaymentTerm.Create(_tenantId, "RG-CASH", "Contado", 1, 0, _createdBy);
        var cash = PaymentMethod.Create(_tenantId, "RG-CASH", "Efectivo", false, false, 0, _createdBy,
            sriPaymentMethodCode: "01", affectsPhysicalCash: true);
        Guid invoiceId; Guid emissionPointId;
        await using (var seed = CreateContext())
        {
            // Estado real tras el bootstrap: establecimiento 001 sin dirección fiscal (address = "").
            var establishment = await seed.Establishments.SingleAsync(e => e.CompanyId == _companyId);
            establishment.Update(establishment.Name, address, null, _createdBy);
            emissionPointId = (await seed.EmissionPoints.SingleAsync(e => e.CompanyId == _companyId)).Id;
            if (withSriSettings)
                // Configuración SRI completa; la inspección del .p12 es un doble (certificado válido).
                seed.SriSettings.Add(EmissionRequirementsTestWiring.CompleteSriSettings(
                    _tenantId, _companyId, _createdBy));

            seed.PaymentTerms.Add(term);
            seed.PaymentMethods.Add(cash);
            Account AccountFor(string code, string name, AccountType type) =>
                Account.Create(_tenantId, _companyId, AccountCode.Create(code), name, null,
                    type, type == AccountType.Income ? AccountNature.Credit : AccountNature.Debit,
                    true, _createdBy);
            var cashAccount = AccountFor("1.1.81", "RG Cash", AccountType.Asset);
            var inventoryAccount = AccountFor("1.1.83", "RG Inventory", AccountType.Asset);
            var revenueAccount = AccountFor("4.1.81", "RG Sales", AccountType.Income);
            var cogsAccount = AccountFor("5.1.81", "RG COGS", AccountType.Expense);
            seed.Accounts.AddRange(cashAccount, inventoryAccount, revenueAccount, cogsAccount);
            var session = await seed.CashSessions.SingleAsync(s => s.Id == _cashSessionId);
            (await seed.CashRegisters.SingleAsync(r => r.Id == session.CashRegisterId))
                .SetAccountingAccount(cashAccount.Id, _createdBy);
            var issued = PostingRule.Create(_tenantId, _companyId, "Sales", "InvoiceIssued", _createdBy);
            issued.AddLine(cashAccount.Id, AccountNature.Debit, PostingAmountKind.CashApplied);
            issued.AddLine(revenueAccount.Id, AccountNature.Credit, PostingAmountKind.Subtotal);
            var cogs = PostingRule.Create(_tenantId, _companyId, "Sales", "CostOfGoodsSold", _createdBy);
            cogs.AddLine(cogsAccount.Id, AccountNature.Debit, PostingAmountKind.HistoricalCost);
            cogs.AddLine(inventoryAccount.Id, AccountNature.Credit, PostingAmountKind.HistoricalCost);
            seed.PostingRules.AddRange(issued, cogs);
            seed.AccountingPeriods.Add(AccountingPeriod.Create(_tenantId, _companyId,
                ReadinessGapDate.Year, ReadinessGapDate.Month,
                new(ReadinessGapDate.Year, ReadinessGapDate.Month, 1),
                new(ReadinessGapDate.Year, ReadinessGapDate.Month, 31), _createdBy));

            var invoice = SalesInvoice.CreateDraft(_tenantId, _companyId, _branchId, _customerId,
                CustomerSnapshot.Create("Cliente Test", "1710034065", "05"), SalesInvoice.PendingNumberPlaceholder,
                ReadinessGapDate, _createdBy, PaymentTermSnapshot.Create(term.Id, term.Name, 1, 0),
                _cashSessionId, emissionType: emissionType, emissionPointId: emissionPointId,
                sriPaymentMethodCode: "01");
            invoice.ReplaceLines(new[] { SalesInvoiceDetail.Create(invoice.Id, _tenantId,
                "RG Product", 1m, 20m, "10", "UNIT", itemId: item, snapshotSku: "SKU-RG",
                warehouseId: warehouse) }, _createdBy);
            invoice.ReplacePayments(new[] {
                SalesInvoicePayment.Create(invoice.Id, _tenantId, cash.Id, "01", "Efectivo", 20m)
            }, _createdBy);
            seed.SalesInvoices.Add(invoice);
            await seed.SaveChangesAsync();
            invoiceId = invoice.Id;

            var stock = RetryStockRepository(seed);
            await stock.AppendMovementAsync(_tenantId, _companyId, item, warehouse,
                StockMovementType.PurchaseEntry, 5m, "UNIT", ReadinessGapDate, "RG Initial", null, null,
                _createdBy, unitCost: 2m);
            await stock.SaveChangesWithSequenceRetryAsync();
        }
        return new(invoiceId, emissionPointId, item, warehouse);
    }

    private async Task<Result<ERP.Application.Modules.Sales.DTOs.SalesInvoiceDto>> AuthorizeWithRealEmissionAsync(
        Guid invoiceId, ReadinessGapSriBoundary boundary, IElectronicEmissionRequirementsValidator gate,
        IPhysicalEmissionRequirementsValidator? physicalGate = null)
    {
        var (db, provider) = BuildA7Context(new A8NoOpInterceptor(), "RG-1A");
        using var disposeProvider = provider;
        await using var disposeDb = db;
        var company = new FixedCurrentCompany(_companyId);
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.TodayAsync(_companyId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadinessGapDate);
        var tax = new Mock<ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(0m, "VAT 0"));
        var docTypes = new Mock<ISriDocTypeCatalogResolver>();
        docTypes.Setup(r => r.IsActiveElectronicDocTypeAsync("01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var composer = new Mock<IElectronicDocumentAdditionalInfoComposer>();
        composer.Setup(c => c.ComposeAsync(It.IsAny<AdditionalInfoCompositionContext>(),
                It.IsAny<IReadOnlyList<ElectronicDocumentAdditionalField>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdditionalInfoCompositionContext _, IReadOnlyList<ElectronicDocumentAdditionalField> f,
                CancellationToken _) => Result<IReadOnlyList<ElectronicDocumentAdditionalField>>.Success(f));

        // El pipeline electrónico corre después del commit de Ventas: usa su propio contexto (sin
        // publisher — el contenedor A7 no registra la auditoría de ElectronicDocuments) y por lo
        // tanto solo ve estado comiteado.
        await using var edocDb = CreateContext();
        var dataProvider = new SalesInvoiceElectronicDocumentDataProvider(
            new SalesInvoiceRepository(edocDb, company), new EmissionPointRepository(edocDb),
            new EstablishmentRepository(edocDb), new CompanyRepository(edocDb),
            new SriSettingsRepository(edocDb), docTypes.Object, new PaymentMethodRepository(edocDb));
        boundary.Supplier = new CommitProbeSupplier(
            new CommercialElectronicDocumentXmlSupplier(ElectronicDocumentType.Invoice, dataProvider,
                new InvoiceXmlBuilder(Mock.Of<ISriTaxCategoryCodeResolver>()), composer.Object),
            boundary, CreateContext, invoiceId);

        var issuer = new ElectronicDocumentIssuer(
            new ElectronicDocumentRepository(edocDb, clock.Object), boundary.Suppliers.Object,
            boundary.Validators.Object, boundary.Signing.Object, boundary.Storage.Object,
            boundary.Reception.Object, boundary.Authorization.Object,
            new PostgresDatabaseExceptionTranslator(),
            new ElectronicDocumentSourceLifecycleGuardResolver([]), new UnitOfWork(edocDb),
            NullLogger<ElectronicDocumentIssuer>.Instance);
        var strategies = new SalesInvoiceEmissionStrategyResolver(new ISalesInvoiceEmissionStrategy[]
        {
            new ElectronicSalesInvoiceEmissionStrategy(issuer, gate, boundary.StrategyLogger),
            new PhysicalSalesInvoiceEmissionStrategy(physicalGate ?? EmissionRequirementsTestWiring.PhysicalSatisfied(),
                NullLogger<PhysicalSalesInvoiceEmissionStrategy>.Instance),
        });

        var handler = new AuthorizeSalesInvoiceHandler(
            new SalesInvoiceRepository(db, company), new SalesReceivableRepository(db, company),
            provider.GetRequiredService<IStockRepository>(), new PaymentTermRepository(db), tax.Object,
            new DocumentSequenceRepository(db), new EmissionPointRepository(db), new EstablishmentRepository(db),
            new ElectronicDocumentRepository(db, clock.Object), strategies,
            clock.Object, new BusinessPartnerRepository(db), Mock.Of<ISalesFiscalPolicyResolver>(),
            new PaymentMethodRepository(db),
            new CompanyBankAccountRepository(db, company), new CashSessionRepository(db, company),
            new CashRegisterRepository(db, company), NullLogger<AuthorizeSalesInvoiceHandler>.Instance,
            new FixedCurrentTenant(_tenantId), company, Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId),
            Mock.Of<ICurrentUser>(u => u.UserId == _createdBy), new A7Preferences(),
            StandardPrecisionPolicyProvider.Instance, new ItemRepository(db), new WarehouseRepository(db, company));
        return await handler.Handle(new(invoiceId), CancellationToken.None).ThenDispatchAsync(db);
    }

    private async Task AssertCommittedCommercialEffectsAsync(ErpDbContext verify, ReadinessGapSeed seed)
    {
        (await verify.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == seed.InvoiceId))
            .Status.Should().Be(SalesInvoiceStatus.Authorized);
        (await verify.DocumentSequences.AsNoTracking()
                .SingleAsync(s => s.EmissionPointId == seed.EmissionPointId && s.DocTypeCode == "01"))
            .CurrentSeq.Should().Be(2, "el secuencial 000000001 quedó consumido");
        (await verify.StockMovements.AsNoTracking().Where(m => m.SourceDocId == seed.InvoiceId).ToListAsync())
            .Should().ContainSingle(m => m.MovementType == StockMovementType.SaleExit && m.Quantity == -1m);
        (await verify.CashMovements.AsNoTracking().Where(m => m.ReferenceId == seed.InvoiceId).ToListAsync())
            .Should().ContainSingle().Which.Amount.Should().Be(20m);
        (await verify.JournalEntries.AsNoTracking().Where(e => e.SourceEventId == seed.InvoiceId)
                .Select(e => e.SourceEventType).ToListAsync())
            .Should().Contain("InvoiceIssued");
    }

    /// <summary>Frontera SRI: XSD, firma, almacenamiento, recepción y autorización. Nunca debe alcanzarse.</summary>
    private sealed class ReadinessGapSriBoundary
    {
        public Mock<IElectronicDocumentXmlSupplierResolver> Suppliers { get; } = new();
        public Mock<IElectronicDocumentSchemaValidatorResolver> Validators { get; } = new();
        public Mock<IElectronicDocumentSigningService> Signing { get; } = new();
        public Mock<IElectronicDocumentXmlStorageService> Storage { get; } = new();
        public Mock<IElectronicDocumentReceptionService> Reception { get; } = new();
        public Mock<IElectronicDocumentAuthorizationService> Authorization { get; } = new();
        public CapturingLogger<ElectronicSalesInvoiceEmissionStrategy> StrategyLogger { get; } = new();
        public IElectronicDocumentXmlSupplier? Supplier { get; set; }
        public int SupplierResolveCount { get; private set; }
        public SalesInvoiceStatus? InvoiceStatusSeenAtXmlStage { get; set; }
        public int? SequenceSeenAtXmlStage { get; set; }

        public ReadinessGapSriBoundary() =>
            Suppliers.Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
                .Returns(() => { SupplierResolveCount++; return Supplier; });

        public void AssertSriBoundaryNeverReached()
        {
            Validators.Invocations.Should().BeEmpty();
            Signing.Invocations.Should().BeEmpty();
            Storage.Invocations.Should().BeEmpty();
            Reception.Invocations.Should().BeEmpty();
            Authorization.Invocations.Should().BeEmpty();
        }
    }

    /// <summary>Conserva las excepciones que la estrategia electrónica traga y solo registra en log.</summary>
    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<Exception> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
                Exceptions.Add(exception);
        }
    }

    /// <summary>Lee el estado comiteado (contexto limpio) justo antes de generar el XML real.</summary>
    private sealed class CommitProbeSupplier(
        IElectronicDocumentXmlSupplier inner,
        ReadinessGapSriBoundary boundary,
        Func<MediatR.IPublisher?, ErpDbContext> contextFactory,
        Guid invoiceId) : IElectronicDocumentXmlSupplier
    {
        public ElectronicDocumentType DocumentType => inner.DocumentType;

        public async Task<Result<ElectronicDocumentXml>> BuildXmlAsync(
            ElectronicDocumentSourceReference reference, CancellationToken cancellationToken = default)
        {
            await using (var fresh = contextFactory(null))
            {
                var invoice = await fresh.SalesInvoices.AsNoTracking()
                    .SingleAsync(i => i.Id == invoiceId, cancellationToken);
                boundary.InvoiceStatusSeenAtXmlStage = invoice.Status;
                boundary.SequenceSeenAtXmlStage = (await fresh.DocumentSequences.AsNoTracking()
                    .SingleAsync(s => s.EmissionPointId == invoice.EmissionPointId, cancellationToken)).CurrentSeq;
            }
            return await inner.BuildXmlAsync(reference, cancellationToken);
        }
    }
}
