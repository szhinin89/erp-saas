using System.Security.Cryptography.Xml;
using System.Xml;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Extiende <see cref="SignedXml"/> únicamente para resolver referencias a elementos que viven
/// dentro de un <see cref="DataObject"/> agregado vía <see cref="SignedXml.AddObject"/> (el caso
/// de <c>xades:SignedProperties</c>) — algo que <see cref="SignedXml.GetIdElement"/> no soporta
/// de forma nativa: solo busca por Id dentro del <see cref="XmlDocument"/> pasado al
/// constructor, nunca dentro de <see cref="SignedXml.Signature"/>.ObjectList.
///
/// Sin esta resolución, <see cref="SignedXml.ComputeSignature"/> lanza
/// <c>CryptographicException: Malformed reference element</c> al intentar calcular el digest
/// de la referencia <c>Uri="#SignedPropertiesId"</c> — confirmado por prueba en
/// <c>XadesBesSignerTests</c> (Fase 9): toda firma fallaba, el 100% de las veces, antes de esta
/// clase. No es un rediseño del firmador: es la única pieza que faltaba para que la resolución
/// de esa referencia funcione tal como <see cref="XadesBesSigner"/> ya la construye.
/// </summary>
public sealed class XadesSignedXml : SignedXml
{
    public XadesSignedXml(XmlDocument document) : base(document) { }

    public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
    {
        var fromDocument = base.GetIdElement(document, idValue);
        if (fromDocument is not null) return fromDocument;

        // FIRMA-01 (auditoría SRI, Fase 2): resuelve la referencia a KeyInfo (Uri="#Certificate",
        // ver XadesBesSigner) durante ComputeSignature() — en ese momento KeyInfo todavía no es
        // un nodo vivo dentro de xmlDoc (recién se materializa al serializar la firma completa),
        // por lo que la resolución por defecto de SignedXml no lo encuentra. Mismo patrón que ya
        // se usa abajo para SignedProperties dentro de un DataObject: KeyInfo.GetXml() construye
        // el elemento <ds:KeyInfo Id="..."> bajo demanda a partir del objeto KeyInfo en memoria,
        // con el mismo contenido (certificado X509) que terminará embebido en el documento final.
        if (Signature.KeyInfo is { } keyInfo && keyInfo.Id == idValue)
            return keyInfo.GetXml();

        foreach (DataObject dataObject in Signature.ObjectList)
        {
            if (dataObject.Data is null) continue;

            foreach (XmlNode node in dataObject.Data)
            {
                if (node is not XmlElement element) continue;

                if (element.GetAttribute("Id") == idValue) return element;

                if (element.SelectSingleNode($"//*[@Id='{idValue}']") is XmlElement match)
                    return match;
            }
        }

        return null;
    }
}
