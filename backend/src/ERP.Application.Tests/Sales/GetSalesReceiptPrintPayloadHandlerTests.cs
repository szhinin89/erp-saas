using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases.GetSalesReceiptPrintPayload;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

public sealed class GetSalesReceiptPrintPayloadHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();
    private static readonly Guid CashSessionEmissionPointId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid PaymentMethodId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISalesInvoiceRepository> SalesInvoices { get; } = new();
        public Mock<IElectronicDocumentRepository> ElectronicDocuments { get; } = new();
        public Mock<ICompanyRepository> Companies { get; } = new();
        public Mock<IBranchRepository> Branches { get; } = new();
        public Mock<ICashSessionRepository> CashSessions { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPoints { get; } = new();
        public Mock<ICompanyBrandingResolver> Branding { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();
        public Mock<ICurrentBranch> CurrentBranch { get; } = new();

        public Fixture(Guid? branchContextId = null)
        {
            CurrentTenant.Setup(t => t.TenantId).Returns(TenantId);
            CurrentTenant.Setup(t => t.Slug).Returns("tenant-test");
            CurrentBranch.Setup(b => b.BranchId).Returns(branchContextId ?? BranchId);
            CurrentBranch.Setup(b => b.HasBranchContext).Returns(true);
            CurrentBranch.Setup(b => b.IsAuthenticated).Returns(true);

            Companies.Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCompany());
            Branches.Setup(r => r.GetByIdAsync(TenantId, BranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateBranch());
            CashSessions.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCashSession());
            EmissionPoints.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((EmissionPoint?)null);
            Branding.Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CompanyBrandingSettings(null, null, null, null, "Gracias por su compra"));
        }

        public GetSalesReceiptPrintPayloadQueryHandler BuildHandler() =>
            new(
                SalesInvoices.Object,
                ElectronicDocuments.Object,
                Companies.Object,
                Branches.Object,
                CashSessions.Object,
                EmissionPoints.Object,
                Branding.Object,
                CurrentTenant.Object,
                CurrentBranch.Object
            );

        public void VerifyReadOnlyRepositories()
        {
            SalesInvoices.Verify(r => r.AddAsync(It.IsAny<SalesInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
            SalesInvoices.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            SalesInvoices.Verify(
                r =>
                    r.RemoveLinesByInvoiceAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<IEnumerable<SalesInvoiceDetail>>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
            SalesInvoices.Verify(r => r.RemovePaymentsByInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            ElectronicDocuments.Verify(r => r.AddAsync(It.IsAny<ElectronicDocument>(), It.IsAny<CancellationToken>()), Times.Never);
            ElectronicDocuments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            CashSessions.Verify(r => r.AddAsync(It.IsAny<CashSession>(), It.IsAny<CancellationToken>()), Times.Never);
            CashSessions.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task Factura_fisica_emitida_devuelve_payload_oficial_con_lineas_totales_y_pagos()
    {
        var invoice = CreateAuthorizedInvoice(EmissionType.Physical);
        var f = new Fixture();
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetSalesReceiptPrintPayloadQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var payload = result.Value!;
        payload.TenantId.Should().Be(TenantId);
        payload.CompanyId.Should().Be(CompanyId);
        payload.BranchId.Should().Be(BranchId);
        payload.CompanyName.Should().Be("ZH Technologies S.A.");
        payload.TradeName.Should().Be("ZH Tech");
        payload.Ruc.Should().Be("1790012345001");
        payload.BranchName.Should().Be("Matriz");
        payload.EstablishmentCode.Should().Be("001");
        payload.EmissionPointCode.Should().Be("002");
        payload.CashRegisterName.Should().Be("Caja Principal");
        payload.InvoiceId.Should().Be(invoice.Id);
        payload.InvoiceNumber.Should().Be("001-002-000000123");
        payload.CustomerName.Should().Be("Cliente POS");
        payload.CustomerIdentification.Should().Be("1710034065");
        payload.CustomerEmail.Should().Be("cliente@example.com");
        payload.DocumentType.Should().Be("01");
        payload.IsElectronic.Should().BeFalse();
        payload.ElectronicStatus.Should().BeNull();
        payload.AccessKey.Should().BeNull();
        payload.AuthorizationNumber.Should().BeNull();
        payload.Lines.Should().ContainSingle();
        payload.Lines[0].ProductName.Should().Be("Producto Test");
        payload.Lines[0].Sku.Should().Be("SKU-001");
        payload.Lines[0].Quantity.Should().Be(2m);
        payload.Lines[0].UnitPrice.Should().Be(10m);
        payload.Lines[0].Discount.Should().Be(2m);
        payload.Lines[0].Subtotal.Should().Be(18m);
        payload.Lines[0].VatRate.Should().Be(15m);
        payload.Lines[0].VatAmount.Should().Be(2.70m);
        payload.Lines[0].Total.Should().Be(20.70m);
        payload.Lines[0].UomCode.Should().Be("UNIT");
        payload.Lines[0].ConversionFactor.Should().Be(1m);
        payload.Totals.SubtotalWithoutTaxes.Should().Be(20m);
        payload.Totals.DiscountTotal.Should().Be(2m);
        payload.Totals.VatTotal.Should().Be(2.70m);
        payload.Totals.Total.Should().Be(20.70m);
        payload.Payments.Should().ContainSingle();
        payload.Payments[0].Method.Should().Be("Efectivo");
        payload.Payments[0].Amount.Should().Be(20.70m);
        payload.Payments[0].Reference.Should().Be("REC-001");
        payload.CashReceived.Should().BeNull("el monto entregado por el cliente no está persistido en Sales/Cash");
        payload.CashChange.Should().BeNull("el vuelto no está persistido en Sales/Cash");
        payload.FooterMessage.Should().Be("Gracias por su compra");
        f.VerifyReadOnlyRepositories();
    }

    // SALES-PRESENTATIONS-04: la tirilla debe mostrar la presentación vendida (UomCode/
    // ConversionFactor) — nunca la cantidad/unidad base (QuantityInBaseUom/BaseUomCode), que solo
    // es de stock/kardex.
    [Fact]
    public async Task Venta_por_presentacion_expone_UomCode_y_ConversionFactor_de_la_presentacion_vendida()
    {
        var invoice = CreateDraftInvoice(EmissionType.Physical);
        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Caja x12",
            quantity: 1m,
            unitPrice: 18m,
            vatCode: "2",
            uomCode: "CAJA",
            snapshotSku: "15865",
            snapshotItemName: "Atún",
            conversionFactor: 12m,
            baseUomCode: "UNIT"
        );
        line.ApplyTaxes("2", 15m, "IVA 15%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);
        var payment = SalesInvoicePayment.Create(
            invoice.Id,
            TenantId,
            PaymentMethodId,
            "01",
            "Efectivo",
            line.TaxInclusiveTotal
        );
        invoice.ReplacePayments(new[] { payment }, UserId);
        invoice.Authorize(UserId);

        var f = new Fixture();
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetSalesReceiptPrintPayloadQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var payloadLine = result.Value!.Lines.Should().ContainSingle().Subject;
        payloadLine.Quantity.Should().Be(1m); // cantidad VISIBLE (1 caja), no 12 (unidad base)
        payloadLine.UomCode.Should().Be("CAJA");
        payloadLine.ConversionFactor.Should().Be(12m);
    }

    [Fact]
    public async Task Factura_electronica_autorizada_incluye_estado_clave_y_autorizacion_SRI()
    {
        var invoice = CreateAuthorizedInvoice(EmissionType.Electronic);
        var electronicDocument = CreateAuthorizedElectronicDocument(invoice.Id);
        var f = new Fixture();
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        f.ElectronicDocuments.Setup(r => r.GetBySourceAsync(TenantId, "Sales", invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(electronicDocument);

        var result = await f.BuildHandler()
            .Handle(new GetSalesReceiptPrintPayloadQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.IsElectronic.Should().BeTrue();
        result.Value.ElectronicStatus.Should().Be(ElectronicDocumentState.Authorized.ToString());
        result.Value.AccessKey.Should().Be(TestAccessKey);
        result.Value.AuthorizationNumber.Should().Be(TestAccessKey);
        result.Value.AuthorizationDate.Should().Be(new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Factura_inexistente_devuelve_not_found()
    {
        var f = new Fixture();
        var invoiceId = Guid.NewGuid();
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesInvoice?)null);

        var result = await f.BuildHandler()
            .Handle(new GetSalesReceiptPrintPayloadQuery(invoiceId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        result.Error.Should().Be("Factura no encontrada.");
    }

    [Fact]
    public async Task Factura_de_otra_sucursal_no_entrega_payload()
    {
        var invoice = CreateAuthorizedInvoice(EmissionType.Physical);
        var f = new Fixture(branchContextId: Guid.NewGuid());
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetSalesReceiptPrintPayloadQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Companies.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Borrador_no_es_reimprimible()
    {
        var invoice = CreateDraftInvoice(EmissionType.Physical);
        var f = new Fixture();
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetSalesReceiptPrintPayloadQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    private const string TestAccessKey = "1234567890123456789012345678901234567890123456789";

    private static SalesInvoice CreateAuthorizedInvoice(EmissionType emissionType)
    {
        var invoice = CreateDraftInvoice(emissionType);
        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto Test",
            quantity: 2m,
            unitPrice: 10m,
            vatCode: "2",
            uomCode: "UNIT",
            snapshotSku: "SKU-001",
            snapshotItemName: "Producto Test",
            discountPct: 10m
        );
        line.ApplyTaxes("2", 15m, "IVA 15%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            invoice.Id,
            TenantId,
            PaymentMethodId,
            "01",
            "Efectivo",
            line.TaxInclusiveTotal,
            "REC-001"
        );
        invoice.ReplacePayments(new[] { payment }, UserId);
        invoice.Authorize(UserId);

        return invoice;
    }

    private static SalesInvoice CreateDraftInvoice(EmissionType emissionType)
    {
        var customer = CustomerSnapshot.Create(
            "Cliente POS",
            "1710034065",
            "05",
            "cliente@example.com",
            "Av. Principal"
        );
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);

        return SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            "001-002-000000123",
            new DateOnly(2026, 8, 20),
            UserId,
            paymentTerm,
            CreateCashSession().Id,
            docTypeCode: "01",
            emissionPointId: CashSessionEmissionPointId,
            emissionType: emissionType,
            sriPaymentMethodCode: "01"
        );
    }

    private static ElectronicDocument CreateAuthorizedElectronicDocument(Guid invoiceId)
    {
        var document = ElectronicDocument.Create(
            TenantId,
            CompanyId,
            ElectronicDocumentType.Invoice,
            "Sales",
            invoiceId,
            UserId
        );
        document.SetEnvironment("1");
        document.MarkXmlGenerated("electronic/sales/draft.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned(
            "electronic/sales/signed.xml",
            AccessKey.Create(TestAccessKey),
            UserId
        );
        document.MarkSent(UserId);
        document.MarkAuthorized(
            AuthorizationNumber.Create(TestAccessKey),
            new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc),
            "electronic/sales/authorized.xml",
            UserId
        );

        return document;
    }

    private static Company CreateCompany()
    {
        var company = Company.CreateManaged(
            TenantId,
            "1790012345001",
            "ZH Technologies S.A.",
            "ZH Tech",
            "facturacion@example.com",
            createdBy: UserId
        );
        company.UpdateDocumentsSettings("Pie legal de respaldo", UserId);
        return company;
    }

    private static Branch CreateBranch() =>
        Branch.Create(
            TenantId,
            "Matriz",
            "Av. Principal",
            "MAT",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: UserId,
            companyId: CompanyId
        );

    private static CashSession CreateCashSession() =>
        CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            CashRegisterId,
            "CAJA-01",
            "Caja Principal",
            CashSessionEmissionPointId,
            "002",
            100m,
            UserId
        );
}
