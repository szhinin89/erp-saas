namespace ERP.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando hay un error en la comunicación con el SRI Ecuador
/// (generación de XML, firma digital, envío WSDL o respuesta inválida).
/// El handler de emisión debe capturarla y marcar la salesBill como "ErrorEnvio"
/// para permitir el reintento posterior sin pérdida de datos.
/// </summary>
public sealed class SriCommunicationException : Exception
{
    public SriCommunicationException(string message)
        : base(message) { }

    public SriCommunicationException(string message, Exception innerException)
        : base(message, innerException) { }
}
