using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.CreatePurchaseReceptionDraft;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

public sealed class CreatePurchaseReceptionDraftHandlerTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId  = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private const string SampleXml =
        "<factura><infoTributaria><ruc>1790012345001</ruc><razonSocial>PROVEEDOR ACME S.A.</razonSocial>" +
        "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>" +
        "</infoTributaria><infoFactura><fechaEmision>08/07/2026</fechaEmision>" +
        "<pagos><pago><formaPago>01</formaPago><total>23.00</total></pago></pagos></infoFactura>" +
        "<detalles><detalle><codigoPrincipal>SKU-001</codigoPrincipal><descripcion>Producto de prueba</descripcion>" +
        "<cantidad>2.0000</cantidad><precioUnitario>10.000000</precioUnitario><descuento>0.00</descuento>" +
        "<precioTotalSinImpuesto>20.00</precioTotalSinImpuesto>" +
        "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>" +
        "<baseImponible>20.00</baseImponible><valor>3.00</valor></impuesto></impuestos></detalle></detalles></factura>";

    private static PurchaseReceptionDocument SampleDocument(Guid? supplierId = null) => PurchaseReceptionDocument.Create(
        TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.Invoice,
        "1790012345001", "PROVEEDOR ACME S.A.", supplierId,
        "0107202601179135268800120150270001617400016174011", "015-027-000161740",
        new DateOnly(2026, 7, 1), new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
        15.96m, 2.4m, 18.35m, UserId);

    private static (CreatePurchaseReceptionDraftHandler handler, Mock<IPurchaseReceptionDocumentRepository> repo)
        BuildHandler()
    {
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var handler = new CreatePurchaseReceptionDraftHandler(repo.Object, new PurchaseXmlDraftParser(), tenant.Object);
        return (handler, repo);
    }

    [Fact]
    public async Task Handle_builds_a_draft_from_the_stored_xml_of_a_verified_document()
    {
        var document = SampleDocument(SupplierId);
        document.AttachSriAuthorization("1234567890", DateTime.UtcNow, SampleXml, DateTime.UtcNow, [], UserId);
        var (handler, repo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var result = await handler.Handle(new CreatePurchaseReceptionDraftCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value!;
        dto.SupplierId.Should().Be(SupplierId);
        dto.SupplierRuc.Should().Be("1790012345001");
        dto.DocTypeCode.Should().Be("01");
        dto.InvoiceNumber.Should().Be("001-001-000000123");
        dto.AccessKey.Should().Be(document.AccessKey);
        dto.AuthorizationNumber.Should().Be("1234567890");
        dto.Lines.Should().ContainSingle();
        var line = dto.Lines[0];
        line.ItemId.Should().BeNull();
        line.WarehouseId.Should().BeNull();
        line.Description.Should().Be("Producto de prueba");
        line.VatCode.Should().Be("2");
    }

    [Fact]
    public async Task Handle_leaves_supplier_null_when_the_reception_document_never_matched_one()
    {
        var document = SampleDocument(supplierId: null);
        document.AttachSriAuthorization("1234567890", DateTime.UtcNow, SampleXml, DateTime.UtcNow, [], UserId);
        var (handler, repo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var result = await handler.Handle(new CreatePurchaseReceptionDraftCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.SupplierId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_document()
    {
        var (handler, repo) = BuildHandler();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var result = await handler.Handle(new CreatePurchaseReceptionDraftCommand(missingId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Handle_rejects_a_document_that_has_not_downloaded_its_xml_yet()
    {
        var document = SampleDocument();
        var (handler, repo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var result = await handler.Handle(new CreatePurchaseReceptionDraftCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }
}
