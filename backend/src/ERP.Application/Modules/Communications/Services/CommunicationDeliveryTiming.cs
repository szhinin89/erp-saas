namespace ERP.Application.Modules.Communications.Services;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — relación de tiempos de una entrega (ADR-039 D9):
/// <c>timeout SMTP (≤ 120 s) + margen de trabajo &lt; lease (5 min)</c>.
/// <para>
/// Un claim cubre UNA comunicación: cargar la fila y sus adjuntos, resolver la configuración, enviar
/// (acotado por el timeout) y finalizar. El timeout configurable se limita a
/// <see cref="MaxSmtpTimeout"/>, de modo que ningún valor de configuración puede igualar o superar el
/// lease: un envío vivo nunca pierde su claim por vencimiento, y un worker muerto libera la fila como
/// máximo <see cref="Lease"/> después de reclamarla.
/// </para>
/// </summary>
public static class CommunicationDeliveryTiming
{
    public static readonly TimeSpan DefaultSmtpTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MinSmtpTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxSmtpTimeout = TimeSpan.FromSeconds(120);

    /// <summary>Holgura para carga de fila/adjuntos, resolución de configuración y finalización.</summary>
    public static readonly TimeSpan WorkMargin = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    /// <summary>Timeout efectivo: <paramref name="configuredSeconds"/> acotado a [5, 120] s; 30 s si no hay valor.</summary>
    public static TimeSpan ResolveSmtpTimeout(int? configuredSeconds)
    {
        if (configuredSeconds is not > 0)
            return DefaultSmtpTimeout;

        var value = TimeSpan.FromSeconds(configuredSeconds.Value);
        if (value < MinSmtpTimeout)
            return MinSmtpTimeout;
        return value > MaxSmtpTimeout ? MaxSmtpTimeout : value;
    }
}
