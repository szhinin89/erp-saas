using System.Numerics;
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
    public static byte[] Sign(string xmlUtf8, string p12FilePath, string p12Password)
    {
        // X509CertificateLoader recomendado en .NET 10
        var cert = X509CertificateLoader.LoadPkcs12FromFile(
            p12FilePath,
            p12Password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return Sign(xmlUtf8, cert);
    }

    /// <summary>Firma con el certificado ya cargado en memoria (vía <see cref="Application.Common.Interfaces.IFileStorage"/>) — no depende del filesystem local.</summary>
    public static byte[] Sign(string xmlUtf8, byte[] p12Bytes, string p12Password)
    {
        var cert = X509CertificateLoader.LoadPkcs12(
            p12Bytes,
            p12Password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return Sign(xmlUtf8, cert);
    }

    private static byte[] Sign(string xmlUtf8, X509Certificate2 cert)
    {
        // FIRMA-02 (auditoría SRI, re-auditoría independiente): firmar con un certificado fuera
        // de vigencia produce una firma estructuralmente válida pero que el SRI rechaza (código
        // 90 "Certificado inválido o caducado") — sin este guard, el fallo solo se descubría
        // después de un round-trip completo al SRI. Se valida antes de tocar la clave privada.
        var now = DateTime.Now;
        if (now < cert.NotBefore || now > cert.NotAfter)
            throw new InvalidOperationException(
                $"El certificado no está vigente (válido del {cert.NotBefore:dd/MM/yyyy} al {cert.NotAfter:dd/MM/yyyy}).");

        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado no contiene clave privada RSA.");

        // 2. Cargar XML
        var xmlDoc = new XmlDocument { PreserveWhitespace = false };
        xmlDoc.LoadXml(xmlUtf8);

        // 3. Construir elemento XAdES SignedProperties
        var signedPropsId     = "SignedPropertiesId";
        var sigId             = "Signature";
        var sigValueId        = "SignatureValue";
        var certId            = "Certificate";
        var docRefId          = "Reference-ID-comprobante";
        var qualifyingPropsId = "QualifyingPropertiesId";
        var objectId          = "XadesObjectId";

        var signedPropsXml = BuildXadesSignedProperties(cert, signedPropsId, sigId, docRefId);

        // 4. Configurar SignedXml — XadesSignedXml resuelve la referencia a SignedProperties
        // (Uri="#SignedPropertiesId") dentro del DataObject agregado más abajo; SignedXml.ComputeSignature()
        // calcula su digest automáticamente al resolverla — no se calcula ni se inyecta
        // manualmente aquí.
        var signedXml = new XadesSignedXml(xmlDoc)
        {
            SigningKey = rsa,
        };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        signedXml.SignedInfo!.SignatureMethod        = SignedXml.XmlDsigRSASHA1Url;

        // FIRMA-04 (rechazo real del SRI, 2026-07-11, segunda ronda): las 3 referencias de abajo,
        // el CertDigest y el KeyInfo se verificaron byte a byte contra una factura real AUTORIZADA
        // por el SRI (no contra el ejemplo del Anexo 14, que resultó no reflejar lo que el SRI
        // acepta en producción). Confirmado contra esa factura real:
        //   - DigestMethod es SHA-256 (xmlenc#sha256) en las 3 referencias y en CertDigest — NO
        //     SHA-1. El intento anterior de forzar SHA-1 uniforme (asumiendo que XAdES-BES exige
        //     SHA-1) fue incorrecto: SignatureMethod sigue siendo rsa-sha1, pero los digests de
        //     Reference/CertDigest son SHA-256 en la factura real. No asumir el algoritmo por el
        //     perfil teórico — esta es la única fuente de verdad.
        //   - Orden de las 3 referencias en SignedInfo: Documento, KeyInfo, SignedProperties.
        // Una diferencia de la factura real NO se replicó: sus referencias a KeyInfo y a
        // SignedProperties no declaran ningún <Transforms> (transform chain vacía). Se probó
        // omitirlos aquí y CheckSignature() dejó de validar — SignedXml de .NET, a diferencia del
        // firmante que emitió esa factura, no aplica una canonicalización implícita cuando la
        // cadena de transforms está vacía para un nodo resuelto por Id fuera del documento
        // principal (KeyInfo, SignedProperties). Se mantiene el transform de C14N explícito en
        // ambas referencias: el SRI valida el DigestValue contra los Transforms realmente
        // declarados, así que declarar el C14N explícitamente es al menos igual de correcto que
        // omitirlo — nunca menos.
        var refDoc = new Reference { Id = docRefId, Uri = "#comprobante" };
        refDoc.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        refDoc.DigestMethod = SignedXml.XmlDsigSHA256Url;
        signedXml.AddReference(refDoc);

        // KeyInfo con el certificado X509 + RSAKeyValue (la factura real autorizada incluye ambos
        // dentro de KeyInfo, no solo X509Data).
        var keyInfo   = new KeyInfo { Id = certId };
        var x509Data  = new KeyInfoX509Data(cert);
        keyInfo.AddClause(x509Data);
        keyInfo.AddClause(new RSAKeyValue(rsa));
        signedXml.KeyInfo = keyInfo;

        // Referencia al certificado en KeyInfo — sin ella, el certificado embebido en KeyInfo no
        // queda protegido por la firma (podría sustituirse sin invalidar CheckSignature()). Se
        // resuelve vía XadesSignedXml.GetIdElement (KeyInfo.Id == "Certificate"), igual que
        // SignedProperties se resuelve dentro del DataObject — KeyInfo todavía no es un nodo vivo
        // del documento en el momento de ComputeSignature().
        var refCert = new Reference { Uri = "#" + certId, DigestMethod = SignedXml.XmlDsigSHA256Url };
        refCert.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(refCert);

        // Referencia a SignedProperties (XAdES). Usa C14N EXCLUSIVO: SignedProperties se construye
        // desacoplado de xmlDoc y solo se incorpora a su posición final dentro de <Signature>
        // después de firmar. Con canonicalización inclusiva, el digest dependería de qué
        // namespaces están "en scope" por herencia de ancestros — que difieren entre el momento de
        // firmar (documento aislado) y el documento final embebido — haciendo que el digest nunca
        // coincida al verificar (confirmado por prueba). La exclusiva solo considera namespaces
        // realmente utilizados dentro del propio subárbol, insensible a dónde termine anidado el
        // nodo.
        var refProps = new Reference
        {
            Uri  = "#" + signedPropsId,
            Type = "http://uri.etsi.org/01903#SignedProperties",
        };
        refProps.AddTransform(new XmlDsigExcC14NTransform());
        refProps.DigestMethod = SignedXml.XmlDsigSHA256Url;
        signedXml.AddReference(refProps);

        // DataObject con el bloque XAdES QualifyingProperties. Data debe ser el elemento
        // <xades:QualifyingProperties> completo (no sus hijos sueltos): SignedProperties debe
        // permanecer anidado bajo él tanto al calcular el digest de la referencia como en el
        // documento final embebido — si el ancestro cambia entre ambos momentos (como ocurría
        // pasando solo .ChildNodes, que descarta el wrapper), el C14N de SignedProperties da un
        // resultado distinto en cada caso y el digest referenciado deja de coincidir al verificar.
        var qualifyingProps = BuildQualifyingProperties(cert, signedPropsId, sigId, qualifyingPropsId, signedPropsXml);
        var dataObj = new DataObject
        {
            Data = qualifyingProps.SelectNodes(".")!,
            Id   = objectId,
        };
        signedXml.AddObject(dataObj);

        // 5. Firmar
        signedXml.ComputeSignature();

        // 6. Obtener elemento <ds:Signature> y añadir IDs de los atributos
        var sigElement = signedXml.GetXml()
            ?? throw new InvalidOperationException("SignedXml.GetXml() retornó null.");
        SetAttrId(sigElement, sigId);
        SetChildAttrId(sigElement, "SignatureValue", sigValueId);
        // KeyInfo ya trae Id="Certificate" desde el objeto KeyInfo asignado arriba (necesario
        // para que la Referencia 3 lo resuelva durante ComputeSignature()) — no hace falta
        // añadirlo aquí de nuevo.

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
        string           sigId,
        string           docRefId)
    {
        var doc = new XmlDocument();
        var sp  = doc.CreateElement("xades", "SignedProperties", XadesNs);
        sp.SetAttribute("Id", signedPropsId);

        var ssp = doc.CreateElement("xades", "SignedSignatureProperties", XadesNs);
        sp.AppendChild(ssp);

        // SigningTime — ISO 8601 con offset explícito ("Z" = UTC), igual que el ejemplo oficial
        // del Anexo 14 de la Ficha Técnica del SRI ("2012-03-05T16:57:32-05:00"). Un timestamp
        // sin offset es ambiguo sobre si es hora local o UTC.
        var st = doc.CreateElement("xades", "SigningTime", XadesNs);
        st.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        ssp.AppendChild(st);

        // SigningCertificate
        var sc   = doc.CreateElement("xades", "SigningCertificate", XadesNs);
        var c    = doc.CreateElement("xades", "Cert", XadesNs);
        var cd   = doc.CreateElement("xades", "CertDigest", XadesNs);
        var dm   = doc.CreateElement("ds", "DigestMethod", DsNs);
        dm.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256");
        var dv   = doc.CreateElement("ds", "DigestValue", DsNs);
        dv.InnerText = Convert.ToBase64String(SHA256.HashData(cert.RawData));
        cd.AppendChild(dm);
        cd.AppendChild(dv);
        c.AppendChild(cd);

        var isSer = doc.CreateElement("xades", "IssuerSerial", XadesNs);
        var iss   = doc.CreateElement("ds", "X509IssuerName",   DsNs);
        iss.InnerText = cert.Issuer;
        var ser   = doc.CreateElement("ds", "X509SerialNumber",  DsNs);
        // FIRMA-05 (rechazo real del SRI 2026-07-11, código 39 "FIRMA INVALIDA" / "La información
        // sobre el certificado de firma no se ajusta a XAdES"): el esquema XAdES declara
        // ds:X509SerialNumber como xsd:integer (decimal) — cert.SerialNumber es la representación
        // HEXADECIMAL de .NET (p.ej. "5995DBFEAD52D214"), que no es un xsd:integer válido por
        // contener letras A-F. GetSerialNumberDecimal() convierte los mismos bytes al decimal
        // exigido por el esquema, sin cambiar el certificado ni la clave usados para firmar.
        ser.InnerText = GetSerialNumberDecimal(cert);
        isSer.AppendChild(iss);
        isSer.AppendChild(ser);
        c.AppendChild(isSer);

        sc.AppendChild(c);
        ssp.AppendChild(sc);

        // SignedDataObjectProperties/DataObjectFormat — describe el documento firmado referenciado
        // por docRefId. MimeType + Encoding, sin Description: así lo construye la factura real
        // autorizada por el SRI usada como fuente de verdad (FIRMA-04). Hermano de
        // SignedSignatureProperties dentro de SignedProperties, no dentro de él.
        var sdop = doc.CreateElement("xades", "SignedDataObjectProperties", XadesNs);
        var dof  = doc.CreateElement("xades", "DataObjectFormat", XadesNs);
        dof.SetAttribute("ObjectReference", "#" + docRefId);
        var mime = doc.CreateElement("xades", "MimeType", XadesNs);
        mime.InnerText = "text/xml";
        var enc  = doc.CreateElement("xades", "Encoding", XadesNs);
        enc.InnerText = "UTF-8";
        dof.AppendChild(mime);
        dof.AppendChild(enc);
        sdop.AppendChild(dof);
        sp.AppendChild(sdop);

        return sp;
    }

    private static XmlElement BuildQualifyingProperties(
        X509Certificate2 cert,
        string           signedPropsId,
        string           sigId,
        string           qualifyingPropsId,
        XmlElement       signedProps)
    {
        var doc = new XmlDocument();
        var qp  = doc.CreateElement("xades", "QualifyingProperties", XadesNs);
        qp.SetAttribute("Id", qualifyingPropsId);
        qp.SetAttribute("Target", "#" + sigId);
        qp.AppendChild(doc.ImportNode(signedProps, true));
        return qp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <see cref="X509Certificate2.GetSerialNumber"/> devuelve los bytes del número de serie en
    /// orden little-endian (convención histórica de .NET, inversa a la codificación DER real del
    /// certificado). Se invierten a big-endian y se interpretan como entero sin signo — mismo
    /// valor que un lector ASN.1 estándar (y el propio SRI) obtiene del certificado — antes de
    /// convertir a la representación decimal exigida por xsd:integer.
    /// </summary>
    private static string GetSerialNumberDecimal(X509Certificate2 cert)
    {
        var bigEndianBytes = cert.GetSerialNumber();
        Array.Reverse(bigEndianBytes);
        return new BigInteger(bigEndianBytes, isUnsigned: true, isBigEndian: true)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
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
