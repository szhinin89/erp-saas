using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// READINESS-PREVENTIVE-1A/1B — Retención 07 emitida dentro de la confirmación de compra con
/// configuración fiscal incompleta (PostgreSQL real). ConfirmPurchaseHandler, RetentionIssuer y el
/// gate fiscal preventivo (ElectronicEmissionRequirementsValidator sobre Company/Establishment/
/// EmissionPoint/SriSettings reales) son reales; solo la inspección del .p12 y la frontera SRI son
/// dobles. 1A fijó que la compra y la retención se comiteaban y el XML fallaba después; 1B invierte
/// esa expectativa: el gate corre antes del secuencial "07" y la confirmación se revierte completa.
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    [Fact]
    public async Task Retencion07_con_direccion_vacia_rechaza_la_confirmacion_sin_efectos_parciales()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        await SetFiscalConfigurationAsync(address: "", withSriSettings: true);

        Result<ERP.Application.Modules.Purchases.DTOs.PurchaseInvoiceDto> result;
        await using (var gateDb = CreateContext())
        await using (var db = CreateWiredContext())
            result = await ConfirmHandler(db,
                    emissionRequirements: EmissionRequirementsTestWiring.Real(gateDb))
                .Handle(new ConfirmPurchaseCommand(invoiceId, null, VatIntent()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing);
        result.Error.Should().Contain("[electronicEmission.matrixAddress]");

        // Ninguna confirmación parcial: compra en Draft, sin CxP, Kardex, retención ni asientos.
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Draft);
        s.Retentions.Should().BeEmpty();
        s.Payables.Should().BeEmpty();
        s.StockMovements.Should().Be(0);
        s.PurchaseEntries.Should().Be(0);
        s.RetentionEntries.Should().Be(0);

        await using var verify = CreateContext();
        (await verify.DocumentSequences.AsNoTracking()
                .AnyAsync(q => q.EmissionPointId == _emissionPointId && q.DocTypeCode == "07" && q.HasBeenUsed))
            .Should().BeFalse("el gate corre antes de CaptureNextAsync");
        (await verify.ElectronicDocuments.AsNoTracking().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Retencion07_con_configuracion_fiscal_completa_confirma_compra_y_emite_la_retencion()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        await SetFiscalConfigurationAsync(address: "Av. Amazonas N24-03", withSriSettings: true);

        Result<ERP.Application.Modules.Purchases.DTOs.PurchaseInvoiceDto> result;
        await using (var gateDb = CreateContext())
        await using (var db = CreateWiredContext())
            result = await ConfirmHandler(db,
                    emissionRequirements: EmissionRequirementsTestWiring.Real(gateDb))
                .Handle(new ConfirmPurchaseCommand(invoiceId, null, VatIntent()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        var retention = s.Retentions.Should().ContainSingle().Which;
        retention.Status.Should().Be(RetentionStatus.Issued);
        retention.RetentionNumber.Should().Be("001-001-000000001");
        s.Payables.Should().ContainSingle().Which.RetainedAmount.Should().Be(4.5m);
        s.StockMovements.Should().Be(1);
    }

    [Fact]
    public async Task Retencion07_RIMPE_Negocio_Popular_rechaza_la_confirmacion_por_R20_sin_secuencial()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        await SetFiscalConfigurationAsync(address: "Av. Amazonas N24-03", withSriSettings: true);
        await using (var regime = CreateContext())
        {
            (await regime.Companies.SingleAsync(c => c.Id == _companyId)).TaxRegimeCode = "03";
            await regime.SaveChangesAsync();
        }

        Result<ERP.Application.Modules.Purchases.DTOs.PurchaseInvoiceDto> result;
        await using (var gateDb = CreateContext())
        await using (var db = CreateWiredContext())
            result = await ConfirmHandler(db,
                    emissionRequirements: EmissionRequirementsTestWiring.Real(gateDb))
                .Handle(new ConfirmPurchaseCommand(invoiceId, null, VatIntent()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionRequirementsMissing);
        result.Error.Should().Contain($"[{ElectronicEmissionRequirementCodes.RimpeNegocioPopularLegend}]");

        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Draft);
        s.Retentions.Should().BeEmpty();
        s.Payables.Should().BeEmpty();
        s.StockMovements.Should().Be(0);
        await using var verify = CreateContext();
        (await verify.DocumentSequences.AsNoTracking()
                .AnyAsync(q => q.EmissionPointId == _emissionPointId && q.DocTypeCode == "07" && q.HasBeenUsed))
            .Should().BeFalse("el gate corre antes de CaptureNextAsync");
        (await verify.ElectronicDocuments.AsNoTracking().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Compra_sin_retencion_no_exige_configuracion_fiscal_electronica()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        await SetFiscalConfigurationAsync(address: "", withSriSettings: false);
        // Sin intención de retención no hay emisión electrónica: el gate nunca se consulta.
        var gate = new Mock<IElectronicEmissionRequirementsValidator>(MockBehavior.Strict);

        Result<ERP.Application.Modules.Purchases.DTOs.PurchaseInvoiceDto> result;
        await using (var db = CreateWiredContext())
            result = await ConfirmHandler(db, emissionRequirements: gate.Object)
                .Handle(new ConfirmPurchaseCommand(invoiceId, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        s.Retentions.Should().BeEmpty();
        s.Payables.Should().ContainSingle();
    }

    private async Task SetFiscalConfigurationAsync(string address, bool withSriSettings)
    {
        await using var seed = CreateContext();
        var establishment = await seed.Establishments.SingleAsync(e => e.CompanyId == _companyId);
        establishment.Update(establishment.Name, address, null, _userId);
        if (withSriSettings)
            seed.SriSettings.Add(EmissionRequirementsTestWiring.CompleteSriSettings(_tenantId, _companyId, _userId));
        await seed.SaveChangesAsync();
    }
}
