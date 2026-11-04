using System.Net;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
using ERP.Infrastructure.Services.Sri;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Retentions;

// Reproduces the reported SOAP shape and timestamp, with synthetic identities and signature
// placeholders. Tests preservation of evidence, not cryptographic validity; no network or DB.
public sealed class RetentionAuthorizedEvidenceTests
{
    private static readonly string Key = new('1', 49);
    private static string SignedXml => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <comprobanteRetencion id="comprobante" version="1.0.0">
          <infoTributaria><ambiente>1</ambiente><tipoEmision>1</tipoEmision>
            <razonSocial>Emisor Sintetico</razonSocial><ruc>1790012345001</ruc>
            <claveAcceso>{{Key}}</claveAcceso><codDoc>07</codDoc><estab>001</estab>
            <ptoEmi>001</ptoEmi><secuencial>000000001</secuencial><dirMatriz>Direccion ficticia</dirMatriz>
          </infoTributaria>
          <infoCompRetencion><fechaEmision>04/11/2026</fechaEmision><dirEstablecimiento>Direccion ficticia</dirEstablecimiento>
            <obligadoContabilidad>SI</obligadoContabilidad><tipoIdentificacionSujetoRetenido>04</tipoIdentificacionSujetoRetenido>
            <razonSocialSujetoRetenido>Proveedor Sintetico</razonSocialSujetoRetenido>
            <identificacionSujetoRetenido>1790098765001</identificacionSujetoRetenido><periodoFiscal>11/2026</periodoFiscal>
          </infoCompRetencion>
          <impuestos><impuesto><codigo>1</codigo><codigoRetencion>303</codigoRetencion>
            <baseImponible>100.00</baseImponible><porcentajeRetener>8.00</porcentajeRetener><valorRetenido>8.00</valorRetenido>
          </impuesto></impuestos>
          <ds:Signature xmlns:ds="http://www.w3.org/2000/09/xmldsig#" Id="sanitized-signature">
            <ds:SignatureValue>U0FOSVRJWkVE</ds:SignatureValue>
            <ds:Object><xades:QualifyingProperties xmlns:xades="http://uri.etsi.org/01903/v1.3.2#" Target="#sanitized-signature" /></ds:Object>
          </ds:Signature>
        </comprobanteRetencion>
        """;

    private sealed class SoapHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><ns:autorizacionComprobanteResponse xmlns:ns="http://ec.gob.sri.ws.autorizacion">
                        <RespuestaAutorizacionComprobante><claveAccesoConsultada>{{Key}}</claveAccesoConsultada>
                          <numeroComprobantes>1</numeroComprobantes><autorizaciones><autorizacion>
                            <estado>AUTORIZADO</estado><numeroAutorizacion>{{Key}}</numeroAutorizacion>
                            <fechaAutorizacion>2026-11-04T13:56:35-05:00</fechaAutorizacion><ambiente>PRUEBAS</ambiente>
                            <comprobante><![CDATA[{{SignedXml}}]]></comprobante><mensajes />
                          </autorizacion></autorizaciones>
                        </RespuestaAutorizacionComprobante>
                      </ns:autorizacionComprobanteResponse></soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml"),
            });
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid RetentionId { get; } = Guid.NewGuid();
        public Mock<IRetentionElectronicDocumentXmlService> Generator { get; } = new(MockBehavior.Strict);
        public Mock<IFileStorage> Storage { get; } = new();
        public Mock<IRetentionSourceAccess> Access { get; } = new();
        public Mock<IRideRenderer> Renderer { get; } = new();
        public RetentionRideDocumentLayout? Layout { get; private set; }
        public ElectronicDocument Document { get; }
        private ICurrentTenant Tenant { get; }
        private ICurrentCompany Company { get; }
        private IElectronicDocumentInternalReader Reader { get; }
        private IRetentionRidePdfService Pdf { get; }

        public Fixture(SriAutorizacionResult authorization, bool missingPath = false, bool missingFile = false)
        {
            Tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
            Company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId && c.HasCompanyContext);
            var source = RetentionDocument.Create(TenantId, CompanyId, Guid.NewGuid(),
                RetentionSourceDocumentType.PurchaseInvoice, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            Access.Setup(a => a.FindViewableAsync(RetentionId, It.IsAny<CancellationToken>())).ReturnsAsync(source);
            Document = ElectronicDocument.Create(TenantId, CompanyId, ElectronicDocumentType.Retention,
                "Retentions", RetentionId, Guid.NewGuid());
            Document.MarkXmlGenerated("draft.xml", "1.0.0", "2.34", Guid.NewGuid());
            Document.MarkSigned("signed.xml", AccessKey.Create(Key), Guid.NewGuid());
            Document.MarkAuthorized(AuthorizationNumber.Create(authorization.AuthorizationNumber),
                authorization.AuthorizationDate, missingPath ? null : "authorized.xml", Guid.NewGuid());
            var repository = new Mock<IElectronicDocumentRepository>();
            repository.Setup(r => r.GetBySourceAsync(TenantId, "Retentions", RetentionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Document);
            Storage.Setup(s => s.GetAsync("authorized.xml", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => missingFile ? null : new MemoryStream(Encoding.UTF8.GetBytes(authorization.DocumentXml)));
            Storage.Setup(s => s.GetAsync("draft.xml", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("<generated />")));
            Storage.Setup(s => s.GetAsync("signed.xml", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("<signed />")));
            Reader = new ElectronicDocumentInternalReader(repository.Object, Storage.Object, Tenant, Company);
            var branding = new Mock<IRideBrandingProvider>();
            branding.Setup(b => b.GetAsync(TenantId, CompanyId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
            var companies = new Mock<ICompanyRepository>();
            companies.Setup(c => c.GetByIdForTenantAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ERP.Domain.Modules.Company.Entities.Company { Timezone = "America/Guayaquil" });
            Renderer.Setup(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()))
                .Callback<IRideDocumentLayout, CancellationToken>((layout, _) => Layout = (RetentionRideDocumentLayout)layout)
                .ReturnsAsync([1, 2, 3]);
            Pdf = new RetentionRidePdfService(new RetentionRideXmlParser(), new RetentionRideTemplate(),
                Renderer.Object, branding.Object, companies.Object);
        }

        public Task<Result<string>> Xml(ElectronicDocumentXmlVariant? variant = null) =>
            new GenerateRetentionXmlHandler(Generator.Object, Tenant, Company, Access.Object, Reader)
                .Handle(new GenerateRetentionXmlQuery(RetentionId, variant), CancellationToken.None);

        public Task<Result<byte[]>> Ride() =>
            new GenerateRetentionRidePdfHandler(Generator.Object, Pdf, Tenant, Company, Access.Object, Reader)
                .Handle(new GenerateRetentionRidePdfQuery(RetentionId), CancellationToken.None);
    }

    private static async Task<SriAutorizacionResult> ReadSanitizedSoap()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new SoapHandler()));
        var response = await new SriSoapClient(factory.Object, NullLogger<SriSoapClient>.Instance)
            .CheckAuthorizationAsync(Key, "https://sri.invalid/offline?wsdl");
        response.Authorized.Should().BeTrue();
        response.Messages.Should().BeEmpty();
        response.AuthorizationNumber.Should().Be(Key);
        response.AuthorizationDate.Should().Be(new DateTime(2026, 11, 4, 18, 56, 35, DateTimeKind.Utc));
        return response;
    }

    [Fact]
    public async Task Authorized_download_preserves_the_exact_signed_comprobante_from_soap()
    {
        var fx = new Fixture(await ReadSanitizedSoap());
        var result = await fx.Xml();
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(SignedXml).And.Contain("ds:Signature").And.Contain("xades:QualifyingProperties");
        fx.Generator.VerifyNoOtherCalls();
        fx.Storage.Verify(s => s.GetAsync("draft.xml", It.IsAny<CancellationToken>()), Times.Never);
        fx.Storage.Verify(s => s.GetAsync("signed.xml", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ride_uses_the_official_stored_date_in_company_timezone_without_changing_xml()
    {
        var fx = new Fixture(await ReadSanitizedSoap());
        var result = await fx.Ride();
        result.IsSuccess.Should().BeTrue(result.Error);
        fx.Layout.Should().NotBeNull();
        fx.Layout!.AuthorizationDateDisplay.Should().Be("04/11/2026 13:56:35");
        fx.Layout.Header.AuthorizationNumber.Should().Be(Key);
        fx.Layout.TotalRetained.Should().Be(8m);
        fx.Document.AuthorizationDate.Should().Be(new DateTime(2026, 11, 4, 18, 56, 35, DateTimeKind.Utc));
        (await fx.Xml()).Value.Should().Be(SignedXml);
        fx.Generator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Missing_authorized_evidence_fails_without_regeneration(bool missingPath, bool missingFile)
    {
        var fx = new Fixture(await ReadSanitizedSoap(), missingPath, missingFile);
        (await fx.Xml(ElectronicDocumentXmlVariant.Authorized)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await fx.Xml()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await fx.Ride()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.Generator.VerifyNoOtherCalls();
        fx.Renderer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ElectronicDocumentXmlVariant.Draft, "<generated />")]
    [InlineData(ElectronicDocumentXmlVariant.Signed, "<signed />")]
    public async Task Explicit_variants_remain_distinct(ElectronicDocumentXmlVariant variant, string expected)
    {
        var fx = new Fixture(await ReadSanitizedSoap());
        (await fx.Xml(variant)).Value.Should().Be(expected);
        fx.Storage.Verify(s => s.GetAsync("authorized.xml", It.IsAny<CancellationToken>()), Times.Never);
        fx.Generator.VerifyNoOtherCalls();
    }
}
