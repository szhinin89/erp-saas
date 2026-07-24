namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>
/// Verifica que el webservice SRI configurado responda — solo diagnóstico, nunca envía ni
/// consulta comprobantes. Envuelve el mismo cliente SOAP (<c>SriSoapClient</c>, Infrastructure)
/// que ya usan la recepción y autorización reales, sin duplicar su lógica de conexión.
/// </summary>
public interface ISriConnectivityChecker
{
    Task<bool> PingAsync(string wsdlUrl, CancellationToken ct = default);
}
