using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Firma un XML con XAdES-BES usando un certificado P12/PKCS#12.
/// Implementación basada en System.Security.Cryptography.Xml (built-in .NET).
/// Sin dependencias externas — reutilizable desde cualquier contexto.
/// </summary>
public sealed class XadesBesSigner
{
    private const string DsNs    = "http://www.w3.org/2000/09/xmldsig#";
    private const string XadesNs = "http://uri.etsi.org/01903/v1.3.2#";

    /// <summary>
    /// Firma el XML y devuelve los bytes UTF-8 del XML firmado.
    /// El elemento <ds:Signature> se incrusta dentro del elemento raíz.
    /// </summary>
    public byte[] Sign(string xmlUtf8, string p12FilePath, string p12Password)
    {
        // 1. Cargar certificado P12 — X509CertificateLoader recomendado en .NET 10
        var cert = X509CertificateLoader.LoadPkcs12FromFile(
            p12FilePath,
            p12Password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado no contiene clave privada RSA.");

        // 2. Cargar XML
        var xmlDoc = new XmlDocument { PreserveWhitespace = false };
        xmlDoc.LoadXml(xmlUtf8);

        // 3. Construir elemento XAdES SignedProperties y su digest
        var signedPropsId = "SignedPropertiesId";
        var sigId         = "Signature";
        var sigValueId    = "SignatureValue";
        var certId        = "Certificate";

        var signedPropsXml = BuildXadesSignedProperties(cert, signedPropsId, sigId);

        // Digest del elemento SignedProperties (C14N → SHA1)
        var signedPropsBytes = C14NBytes(signedPropsXml.OuterXml);
        var signedPropsDigest = Convert.ToBase64String(SHA1.HashData(signedPropsBytes));

        // 4. Configurar SignedXml
        var signedXml = new SignedXml(xmlDoc)
        {
            SigningKey = rsa,
        };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signedXml.SignedInfo!.SignatureMethod        = SignedXml.XmlDsigRSASHA1Url;

        // Referencia 1: el documento completo (#comprobante → transform enveloped + C14N)
        var refDoc = new Reference { Uri = "" };
        refDoc.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        refDoc.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(refDoc);

        // Referencia 2: SignedProperties (XAdES)
        var refProps = new Reference
        {
            Uri  = "#" + signedPropsId,
            Type = "http://uri.etsi.org/01903#SignedProperties",
        };
        refProps.AddTransform(new XmlDsigC14NTransform());
        refProps.DigestMethod = SignedXml.XmlDsigSHA1Url;
        // Inyectamos el digest calculado manualmente porque SignedXml calculará el suyo,
        // pero necesitamos que referencie el nodo correcto del DataObject.
        signedXml.AddReference(refProps);

        // KeyInfo con el certificado X509
        var keyInfo   = new KeyInfo();
        var x509Data  = new KeyInfoX509Data(cert);
        keyInfo.AddClause(x509Data);
        signedXml.KeyInfo = keyInfo;

        // DataObject con el bloque XAdES QualifyingProperties
        var qualifyingProps = BuildQualifyingProperties(cert, signedPropsId, sigId, signedPropsXml);
        var dataObj = new DataObject
        {
            Data = qualifyingProps.ChildNodes,
            Id   = "QualifyingProperties",
        };
        signedXml.AddObject(dataObj);

        // 5. Firmar
        signedXml.ComputeSignature();

        // 6. Obtener elemento <ds:Signature> y añadir IDs de los atributos
        var sigElement = signedXml.GetXml()
            ?? throw new InvalidOperationException("SignedXml.GetXml() retornó null.");
        SetAttrId(sigElement, sigId);
        SetChildAttrId(sigElement, "SignatureValue", sigValueId);
        SetChildAttrId(sigElement, "KeyInfo", certId);

        // 7. Insertar <ds:Signature> dentro del elemento raíz
        xmlDoc.DocumentElement!.AppendChild(xmlDoc.ImportNode(sigElement, true));

        // 8. Serializar a UTF-8 sin BOM
        using var ms = new MemoryStream();
        using var w  = new XmlTextWriter(ms, new UTF8Encoding(false));
        w.Formatting = Formatting.None;
        xmlDoc.Save(w);
        w.Flush();
        return ms.ToArray();
    }

    // ── Construcción XAdES ────────────────────────────────────────────────────

    private static XmlElement BuildXadesSignedProperties(
        X509Certificate2 cert,
        string           signedPropsId,
        string           sigId)
    {
        var doc = new XmlDocument();
        var sp  = doc.CreateElement("xades", "SignedProperties", XadesNs);
        sp.SetAttribute("Id", signedPropsId);

        var ssp = doc.CreateElement("xades", "SignedSignatureProperties", XadesNs);
        sp.AppendChild(ssp);

        // SigningTime
        var st = doc.CreateElement("xades", "SigningTime", XadesNs);
        st.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        ssp.AppendChild(st);

        // SigningCertificate
        var sc   = doc.CreateElement("xades", "SigningCertificate", XadesNs);
        var c    = doc.CreateElement("xades", "Cert", XadesNs);
        var cd   = doc.CreateElement("xades", "CertDigest", XadesNs);
        var dm   = doc.CreateElement("ds", "DigestMethod", DsNs);
        dm.SetAttribute("Algorithm", "http://www.w3.org/2000/09/xmldsig#sha1");
        var dv   = doc.CreateElement("ds", "DigestValue", DsNs);
        dv.InnerText = Convert.ToBase64String(SHA1.HashData(cert.RawData));
        cd.AppendChild(dm);
        cd.AppendChild(dv);
        c.AppendChild(cd);

        var isSer = doc.CreateElement("xades", "IssuerSerial", XadesNs);
        var iss   = doc.CreateElement("ds", "X509IssuerName",   DsNs);
        iss.InnerText = cert.Issuer;
        var ser   = doc.CreateElement("ds", "X509SerialNumber",  DsNs);
        ser.InnerText = cert.SerialNumber;
        isSer.AppendChild(iss);
        isSer.AppendChild(ser);
        c.AppendChild(isSer);

        sc.AppendChild(c);
        ssp.AppendChild(sc);

        return sp;
    }

    private static XmlElement BuildQualifyingProperties(
        X509Certificate2 cert,
        string           signedPropsId,
        string           sigId,
        XmlElement       signedProps)
    {
        var doc = new XmlDocument();
        var qp  = doc.CreateElement("xades", "QualifyingProperties", XadesNs);
        qp.SetAttribute("Target", "#" + sigId);
        qp.AppendChild(doc.ImportNode(signedProps, true));
        return qp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static byte[] C14NBytes(string xml)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xml);
        var c14n = new XmlDsigC14NTransform();
        c14n.LoadInput(doc);
        using var ms = (MemoryStream)c14n.GetOutput(typeof(Stream));
        return ms.ToArray();
    }

    private static void SetAttrId(XmlElement el, string id)
    {
        if (!el.HasAttribute("Id"))
            el.SetAttribute("Id", id);
    }

    private static void SetChildAttrId(XmlElement parent, string localName, string id)
    {
        var child = parent.GetElementsByTagName(localName).OfType<XmlElement>().FirstOrDefault();
        if (child is not null && !child.HasAttribute("Id"))
            child.SetAttribute("Id", id);
    }
}
