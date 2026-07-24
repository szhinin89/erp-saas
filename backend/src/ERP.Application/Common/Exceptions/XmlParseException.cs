namespace ERP.Application.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando el XML del comprobante SRI no puede parsearse
/// por estructura inválida o nodos críticos ausentes.
/// </summary>
public sealed class XmlParseException : Exception
{
    public XmlParseException(string message)
        : base(message) { }

    public XmlParseException(string message, Exception inner)
        : base(message, inner) { }
}
