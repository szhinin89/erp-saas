using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using ERP.Infrastructure.Services.Sri;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Services.Sri;

/// <summary>
/// Fase 9 — verifica que <see cref="XadesBesSigner"/> produce una firma criptográficamente
/// válida (<see cref="SignedXml.CheckSignature()"/>), no solo una estructura que "parece"
/// correcta. Ninguna prueba anterior a esta fase ejercitaba el firmador real: esta suite
/// descubrió que <c>SignedXml.ComputeSignature()</c> lanzaba <c>CryptographicException:
/// Malformed reference element</c> el 100% de las veces (la referencia a SignedProperties no
/// se podía resolver dentro del DataObject) — defecto real, corregido con
/// <see cref="XadesSignedXml"/> (ver su documentación). La verificación aquí usa la misma
/// subclase, igual que tendría que hacerlo cualquier verificador real de este formato XAdES.
/// </summary>
public sealed class XadesBesSignerTests
{
    private const string SampleXml =
        "<factura id=\"comprobante\" version=\"1.1.0\">"
        + "<infoTributaria><ruc>1790012345001</ruc><claveAcceso>0000000000000000000000000000000000000000000000</claveAcceso></infoTributaria>"
        + "</factura>";

    [Fact]
    public void Sign_produces_a_signature_that_passes_SignedXml_CheckSignature()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            var signatureNode = doc.SelectSingleNode("//ds:Signature", nsMgr) as XmlElement;
            signatureNode
                .Should()
                .NotBeNull("el XML firmado debe contener un elemento ds:Signature embebido");

            var signedXml = new XadesSignedXml(doc);
            signedXml.LoadXml(signatureNode!);

            var isValid = signedXml.CheckSignature();

            isValid
                .Should()
                .BeTrue(
                    "SignedXml.CheckSignature() debe validar tanto los digests de las referencias "
                        + "como la firma criptográfica contra la clave pública del certificado embebido"
                );
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    /// <summary>
    /// FIRMA-02 (auditoría SRI, re-auditoría independiente): firmar con un certificado vencido
    /// producía una firma criptográficamente válida pero rechazable por el SRI (código 90) —
    /// sin detección local antes del envío.
    /// </summary>
    [Fact]
    public void Sign_with_expired_certificate_throws_before_signing()
    {
        var p12Path = TestP12CertificateFactory.CreateExpiredTempP12File();
        try
        {
            var act = () =>
                XadesBesSigner.Sign(SampleXml, p12Path, TestP12CertificateFactory.Password);

            act.Should().Throw<InvalidOperationException>().WithMessage("*no está vigente*");
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    [Fact]
    public void Sign_with_not_yet_valid_certificate_throws_before_signing()
    {
        var p12Path = TestP12CertificateFactory.CreateNotYetValidTempP12File();
        try
        {
            var act = () =>
                XadesBesSigner.Sign(SampleXml, p12Path, TestP12CertificateFactory.Password);

            act.Should().Throw<InvalidOperationException>().WithMessage("*no está vigente*");
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    [Fact]
    public void Sign_embeds_KeyInfo_with_the_signing_certificate()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            doc.SelectSingleNode("//ds:KeyInfo/ds:X509Data/ds:X509Certificate", nsMgr)
                .Should()
                .NotBeNull(
                    "la firma debe incluir el certificado X.509 en KeyInfo (KeyInfoX509Data)"
                );
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    [Fact]
    public void Sign_embeds_XAdES_QualifyingProperties_with_SignedProperties()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var qualifyingProps =
                doc.SelectSingleNode("//xades:QualifyingProperties", nsMgr) as XmlElement;
            qualifyingProps
                .Should()
                .NotBeNull("XAdES-BES exige QualifyingProperties dentro del DataObject");

            var signedProps = doc.SelectSingleNode("//xades:SignedProperties", nsMgr) as XmlElement;
            signedProps.Should().NotBeNull();
            signedProps!.GetAttribute("Id").Should().Be("SignedPropertiesId");
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    [Fact]
    public void Sign_covers_KeyInfo_with_its_own_Reference_matching_the_official_SRI_example()
    {
        // FIRMA-01 (auditoría SRI, Fase 2): el Anexo 14 de la Ficha Técnica del SRI incluye
        // explícitamente <ds:Reference URI="#Certificate..."> cubriendo el certificado en KeyInfo,
        // además de las referencias al documento y a SignedProperties — sin esta referencia, el
        // certificado en KeyInfo no está protegido por la firma. Esta prueba fija la estructura de
        // 3 referencias exigida por el ejemplo oficial.
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            var keyInfo = doc.SelectSingleNode("//ds:Signature/ds:KeyInfo", nsMgr) as XmlElement;
            keyInfo.Should().NotBeNull();
            keyInfo!
                .GetAttribute("Id")
                .Should()
                .NotBeNullOrEmpty(
                    "KeyInfo debe tener un Id propio para poder ser referenciado desde SignedInfo"
                );

            var references = doc.SelectNodes("//ds:Signature/ds:SignedInfo/ds:Reference", nsMgr)!;
            references
                .Count.Should()
                .Be(3, "el Anexo 14 exige 3 referencias: documento, SignedProperties y KeyInfo");

            var certReferenceUri = "#" + keyInfo.GetAttribute("Id");
            var hasCertReference = references
                .OfType<XmlElement>()
                .Any(r => r.GetAttribute("URI") == certReferenceUri);
            hasCertReference
                .Should()
                .BeTrue(
                    "debe existir una <ds:Reference> cuyo URI apunte al Id de KeyInfo, cubriéndolo con la firma"
                );

            var signedXml = new XadesSignedXml(doc);
            signedXml.LoadXml((doc.SelectSingleNode("//ds:Signature", nsMgr) as XmlElement)!);
            signedXml
                .CheckSignature()
                .Should()
                .BeTrue(
                    "la referencia a KeyInfo debe validar su digest correctamente, no solo estar presente"
                );
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    /// <summary>
    /// FIRMA-05 (rechazo real del SRI 2026-07-11, código 39 "FIRMA INVALIDA" / "La información
    /// sobre el certificado de firma no se ajusta a XAdES"): xades:IssuerSerial/ds:X509SerialNumber
    /// tiene tipo xsd:integer en el esquema XAdES — un decimal. cert.SerialNumber es la
    /// representación hexadecimal de .NET (puede contener A-F), que el SRI rechazó por no ser un
    /// xsd:integer válido. Esta prueba fija que el valor persistido sea puramente decimal y
    /// coincida con el número de serie real del certificado (bytes de GetSerialNumber(),
    /// interpretados como entero sin signo big-endian).
    /// </summary>
    [Fact]
    public void Sign_writes_X509SerialNumber_as_decimal_not_hexadecimal()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                p12Path,
                TestP12CertificateFactory.Password,
                X509KeyStorageFlags.Exportable
            );
            var serialBytes = cert.GetSerialNumber();
            Array.Reverse(serialBytes);
            var expectedDecimal = new BigInteger(
                serialBytes,
                isUnsigned: true,
                isBigEndian: true
            ).ToString(CultureInfo.InvariantCulture);

            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            var serialNode = doc.SelectSingleNode("//ds:X509SerialNumber", nsMgr);
            serialNode.Should().NotBeNull();
            serialNode!
                .InnerText.Should()
                .MatchRegex(
                    "^[0-9]+$",
                    "ds:X509SerialNumber es xsd:integer — el SRI lo rechaza si contiene dígitos hexadecimales A-F"
                );
            serialNode
                .InnerText.Should()
                .Be(
                    expectedDecimal,
                    "debe representar el mismo número de serie del certificado, solo que en base 10"
                );
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    [Fact]
    public void Sign_includes_SigningTime_with_explicit_UTC_offset()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var signingTime = doc.SelectSingleNode("//xades:SigningTime", nsMgr);
            signingTime.Should().NotBeNull();
            signingTime!
                .InnerText.Should()
                .EndWith(
                    "Z",
                    "el ejemplo oficial del Anexo 14 usa un offset explícito (ej. -05:00); UTC sin offset es ambiguo"
                );
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    [Fact]
    public void Sign_includes_SignedDataObjectProperties_with_text_xml_mime_type()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");

            var mimeType = doc.SelectSingleNode(
                "//xades:SignedDataObjectProperties/xades:DataObjectFormat/xades:MimeType",
                nsMgr
            );
            mimeType
                .Should()
                .NotBeNull(
                    "el Anexo 14 incluye SignedDataObjectProperties/DataObjectFormat/MimeType"
                );
            mimeType!.InnerText.Should().Be("text/xml");
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    /// <summary>
    /// FIRMA-03 (rechazo real del SRI, 2026-07-11): "La firma es inválida" / "El nodo
    /// [comprobante] no se encuentra firmado" — el SRI rechazó facturas firmadas con
    /// <c>Reference URI=""</c> porque su validador resuelve la referencia al comprobante por Id
    /// explícitamente, no por equivalencia de node-set. Una factura real autorizada usa
    /// <c>URI="#comprobante"</c>, igual que el Anexo 14 de la Ficha Técnica del SRI.
    /// </summary>
    [Fact]
    public void Sign_references_the_document_by_its_comprobante_id_not_by_the_empty_same_document_uri()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            var references = doc.SelectNodes("//ds:Signature/ds:SignedInfo/ds:Reference", nsMgr)!;
            var uris = references.OfType<XmlElement>().Select(r => r.GetAttribute("URI")).ToList();
            uris.Should()
                .Contain(
                    "#comprobante",
                    "el validador del SRI exige que la referencia al documento apunte explícitamente "
                        + "al Id del elemento raíz (id=\"comprobante\"), no Uri=\"\""
                );
            uris.Should()
                .NotContain(
                    "",
                    "Uri=\"\" es válido para XMLDSig genérico pero el SRI lo rechaza con "
                        + "\"El nodo [comprobante] no se encuentra firmado\""
                );

            var signedXml = new XadesSignedXml(doc);
            signedXml.LoadXml((doc.SelectSingleNode("//ds:Signature", nsMgr) as XmlElement)!);
            signedXml
                .CheckSignature()
                .Should()
                .BeTrue("la referencia al comprobante por Id debe validar su digest correctamente");
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    /// <summary>
    /// FIRMA-04 (rechazo real del SRI, 2026-07-11, segunda ronda): comparado byte a byte contra
    /// una factura real AUTORIZADA por el SRI (no el ejemplo del Anexo 14), las 3 referencias de
    /// SignedInfo usan DigestMethod SHA-256 — no SHA-1. Un intento previo de forzar SHA-1 uniforme
    /// (razonando desde el perfil teórico del Anexo 14 en vez de la factura real) fue incorrecto.
    /// SignatureMethod sigue siendo rsa-sha1; solo los digests de Reference/CertDigest son SHA-256.
    /// </summary>
    [Fact]
    public void Sign_uses_SHA256_digest_for_every_SignedInfo_reference_matching_a_real_authorized_invoice()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(
                SampleXml,
                p12Path,
                TestP12CertificateFactory.Password
            );

            var doc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                doc.Load(ms);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

            var digestMethods = doc.SelectNodes(
                "//ds:Signature/ds:SignedInfo/ds:Reference/ds:DigestMethod",
                nsMgr
            )!;
            digestMethods.Count.Should().Be(3);

            foreach (XmlElement dm in digestMethods)
            {
                dm.GetAttribute("Algorithm")
                    .Should()
                    .Be(
                        "http://www.w3.org/2001/04/xmlenc#sha256",
                        "una factura real autorizada por el SRI usa SHA-256 uniformemente en las 3 "
                            + "referencias — no SHA-1, a pesar de que SignatureMethod sea rsa-sha1"
                    );
            }
        }
        finally
        {
            File.Delete(p12Path);
        }
    }

    [Fact]
    public void Sign_throws_a_clean_exception_for_a_wrong_password_never_silently_produces_a_bad_signature()
    {
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var act = () => XadesBesSigner.Sign(SampleXml, p12Path, "wrong-password");

            act.Should().Throw<CryptographicException>();
        }
        finally
        {
            File.Delete(p12Path);
        }
    }
}
