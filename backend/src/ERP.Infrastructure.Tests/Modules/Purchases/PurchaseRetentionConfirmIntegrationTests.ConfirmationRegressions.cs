using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    [Fact]
    public async Task Late_confirmation_rejection_preserves_saved_manual_schedules()
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        List<PurchasePaymentSchedule> original;
        await using (var db = CreateContext())
        {
            var invoice = await db.PurchaseInvoices.Include(i => i.PaymentSchedules)
                .SingleAsync(i => i.Id == invoiceId);
            invoice.ReplaceDraftPaymentSchedule(
                [(1, IssueDate.AddDays(10), 40m, "Primera manual"),
                 (2, IssueDate.AddDays(25), 75m, "Segunda manual")]);
            db.Set<PurchasePaymentSchedule>().AddRange(invoice.PaymentSchedules);
            await db.SaveChangesAsync();
            original = await db.Set<PurchasePaymentSchedule>().AsNoTracking()
                .Where(s => s.PurchaseInvoiceId == invoiceId).ToListAsync();
        }

        // The missing emission point is checked after schedule deletion and stock staging.
        var result = await ConfirmAsync(invoiceId, VatIntent(emissionPointId: Guid.NewGuid()));
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emisión");
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));

        await using var reopened = CreateContext();
        var saved = await reopened.PurchaseInvoices.AsNoTracking()
            .Include(i => i.Lines).ThenInclude(l => l.Taxes)
            .Include(i => i.PaymentSchedules).SingleAsync(i => i.Id == invoiceId);
        saved.PaymentSchedules.Should().BeEquivalentTo(original);
        saved.Lines.Should().OnlyContain(l => !l.IsFrozen);
        saved.ConfirmedGrandTotal.Should().BeNull();
    }

    [Fact]
    public async Task Xml_service_without_global_warehouse_confirms_without_inventory()
    {
        Guid invoiceId;
        Guid receptionId;
        await using (var db = CreateContext())
        {
            var physicalItem = await db.Set<Item>().SingleAsync(i => i.Id == _itemId);
            var service = Item.Create(_tenantId, "SERV-XML", "Servicio XML", "Servicio XML",
                physicalItem.ItemTypeId, "UNIT", ItemTaxConfig.Create("4", "4"),
                ItemSaleConfig.Create(), ItemStockConfig.Create(false), _userId,
                companyId: _companyId, nature: ItemNature.Service);
            db.Set<Item>().Add(service);
            var invoice = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId,
                _supplierId, "Proveedor Retenido", "1791352688001", "01", "001-001-000000901",
                IssueDate, _userId, _paymentTermId, "Contado", 1, 0,
                accessKey: new string('1', 49));
            var source = PurchaseReceptionDocument.Create(_tenantId, _companyId, _branchId,
                PurchaseReceptionSourceDocType.Invoice, invoice.SupplierTaxId, invoice.SupplierName,
                _supplierId, invoice.AccessKey!, invoice.InvoiceNumber, IssueDate, null, 100m, 15m, 115m, _userId);
            var sourceLine = PurchaseReceptionLine.Create(source.Id, _tenantId, "Servicio XML",
                1m, 100m, "4", "2", 15m, 15m, 0m, 0m, 100m, 115m,
                itemId: service.Id, matchStatus: ItemMatchStatus.ManuallyMatched);
            const string xml = """
                <factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>Proveedor Retenido</razonSocial>
                <codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000901</secuencial>
                </infoTributaria><infoFactura><fechaEmision>17/09/2026</fechaEmision>
                <totalSinImpuestos>100.00</totalSinImpuestos><importeTotal>115.00</importeTotal></infoFactura>
                <detalles><detalle><descripcion>Servicio XML</descripcion><cantidad>1</cantidad>
                <precioUnitario>100</precioUnitario><descuento>0</descuento><precioTotalSinImpuesto>100</precioTotalSinImpuesto>
                <impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15</tarifa>
                <baseImponible>100</baseImponible><valor>15</valor></impuesto></impuestos></detalle></detalles></factura>
                """;
            source.AttachSriAuthorization(source.AccessKey, DateTime.UtcNow, xml, DateTime.UtcNow,
                [sourceLine], _userId, "01", null,
                new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null));
            var line = PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Servicio XML", 1m, 100m,
                "4", "UNIT", itemId: service.Id, purchaseReceptionLineId: sourceLine.Id);
            line.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
            invoice.ReplaceLines([line], _userId);
            source.MarkProcessed(invoice.Id, _userId);
            db.PurchaseReceptionDocuments.Add(source);
            db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
            receptionId = source.Id;
        }

        await using (var db = CreateWiredContext())
        {
            var guard = new PurchaseXmlConfirmationGuard(new PurchaseReceptionDocumentRepository(
                db, new FixedCurrentCompany(_companyId)), new PurchaseXmlDraftParser());
            var result = await ConfirmHandler(db, confirmationGuard: guard)
                .Handle(new ConfirmPurchaseCommand(invoiceId), CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
        }

        var snapshot = await ReadAsync(invoiceId);
        snapshot.Status.Should().Be(PurchaseStatus.Confirmed);
        snapshot.StockMovements.Should().Be(0);
        snapshot.Payables.Should().ContainSingle().Which.Installments.Sum(i => i.Amount).Should().Be(115m);
        await using var verify = CreateContext();
        var saved = await verify.PurchaseInvoices.AsNoTracking().Include(i => i.Lines)
            .SingleAsync(i => i.Id == invoiceId);
        saved.GlobalWarehouseId.Should().BeNull();
        saved.Lines.Should().ContainSingle().Which.WarehouseId.Should().BeNull();
        saved.Lines.Single().IsFrozen.Should().BeTrue();
        (await verify.PurchaseReceptionDocuments.AsNoTracking().SingleAsync(r => r.Id == receptionId))
            .XmlContent.Should().Contain("<descripcion>Servicio XML</descripcion>");
    }
}
