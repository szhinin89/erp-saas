namespace ERP.Application.Modules.ElectronicInvoicing.Enums;

/// <summary>
/// Estado operativo oficial de la facturación electrónica de una empresa — contrato único y
/// estable consumido por <c>GET /api/v1/electronic-invoicing/status</c> y por cualquier UI
/// transversal (p. ej. <c>ZHElectronicEnvironmentBanner</c>). Application es la única capa que
/// calcula este valor (<see cref="UseCases.GetElectronicInvoicingStatus.GetElectronicInvoicingStatusQueryHandler"/>)
/// — el frontend nunca lo infiere combinando booleanos, solo lo renderiza.
///
/// Extensibilidad: agregar un estado nuevo consiste únicamente en (1) sumar un valor aquí y
/// (2) calcularlo en el handler — ninguna pantalla consumidora (Ventas, Notas de Crédito,
/// Retenciones, Guías, etc.) requiere cambios.
/// </summary>
public enum ElectronicInvoicingStatus
{
    /// <summary>Configurado, certificado válido y vigente, ambiente Producción — listo para emitir con validez tributaria.</summary>
    Ready = 0,

    /// <summary>Configurado, certificado válido y vigente, ambiente Pruebas — emite sin validez tributaria.</summary>
    Testing = 1,

    /// <summary>Configuración SRI existe pero está incompleta (sin certificado instalado o certificado no válido por causa distinta a vencimiento).</summary>
    Incomplete = 2,

    /// <summary>No existe ninguna configuración SRI registrada para la empresa.</summary>
    NotConfigured = 3,

    /// <summary>El certificado digital instalado ya venció.</summary>
    CertificateExpired = 4,

    /// <summary>El certificado digital vencerá dentro del umbral de aviso (ver <c>CertificateExpiringThresholdDays</c>).</summary>
    CertificateExpiring = 5,

    /// <summary>Configuración y certificado correctos, pero el webservice del SRI no respondió.</summary>
    SriUnavailable = 6,

    /// <summary>Reservado para cuando exista un flujo de habilitar/deshabilitar emisión electrónica por empresa — ningún camino actual del handler lo produce todavía.</summary>
    Disabled = 7,

    /// <summary>No fue posible determinar el estado (excepción inesperada al resolverlo) — fallback seguro, nunca se propaga como error HTTP.</summary>
    Error = 8,
}
